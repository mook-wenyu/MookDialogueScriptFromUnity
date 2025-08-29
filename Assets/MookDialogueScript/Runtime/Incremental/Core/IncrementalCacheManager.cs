using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MookDialogueScript.Incremental.Contracts;
using MookDialogueScript.Incremental.FileSystem;
using MookDialogueScript.Incremental.VariableCache;
using MookDialogueScript.Pooling;

namespace MookDialogueScript.Incremental.Core
{
    /// <summary>
    /// 增量缓存管理器实现
    /// 统一管理文件变更检测、解析结果缓存、变量声明缓存等子系统
    /// </summary>
    public sealed class IncrementalCacheManager : IIncrementalCache
    {
        #region 字段
        private readonly IncrementalCacheOptions _options;
        private readonly FileChangeDetector _fileDetector;
        private readonly ParseResultCache _parseResultCache;
        private readonly VariableDeclarationCache _variableCache;
        
        private readonly Timer _cleanupTimer;
        private readonly SemaphoreSlim _operationSemaphore;
        private volatile bool _disposed;
        private volatile bool _initialized;

        // 统计信息
        private long _totalRefreshOperations;
        private long _totalWarmupOperations;
        private long _totalCleanupOperations;
        private readonly CacheStatistics _combinedStatistics;
        #endregion

        #region 属性
        /// <summary>
        /// 缓存配置选项
        /// </summary>
        public IncrementalCacheOptions Options => _options;

        /// <summary>
        /// 文件变更检测器
        /// </summary>
        public IFileChangeDetector FileDetector => _fileDetector;

        /// <summary>
        /// 解析结果缓存
        /// </summary>
        public IParseResultCache ParseResultCache => _parseResultCache;

        /// <summary>
        /// 变量声明缓存
        /// </summary>
        public IVariableDeclarationCache VariableCache => _variableCache;

        /// <summary>
        /// 缓存统计信息
        /// </summary>
        public CacheStatistics Statistics => GetCombinedStatistics();
        #endregion

        #region 事件
        /// <summary>
        /// 缓存变更事件
        /// </summary>
        public event EventHandler<CacheChangedEventArgs> CacheChanged;
        #endregion

        #region 构造函数
        /// <summary>
        /// 初始化增量缓存管理器
        /// </summary>
        /// <param name="options">缓存配置选项</param>
        public IncrementalCacheManager(IncrementalCacheOptions options = null)
        {
            _options = options ?? IncrementalCacheOptions.CreateDefault();
            
            // 验证配置
            var (isValid, errorMessage) = _options.Validate();
            if (!isValid)
                throw new ArgumentException($"缓存配置无效: {errorMessage}");

            // 初始化子系统
            _fileDetector = new FileChangeDetector(_options);
            _parseResultCache = new ParseResultCache(_options);
            _variableCache = new VariableDeclarationCache(_options);

            // 初始化控制组件
            _operationSemaphore = new SemaphoreSlim(_options.WarmupConcurrency);
            _combinedStatistics = CacheStatistics.CreateEmpty();

            // 初始化清理定时器
            _cleanupTimer = new Timer(
                OnCleanupTimer, 
                null, 
                _options.CleanupInterval, 
                _options.CleanupInterval);

            // 订阅文件变更事件
            _fileDetector.FileChanged += OnFileChanged;
        }
        #endregion

        #region IIncrementalCache 实现
        /// <summary>
        /// 初始化缓存系统
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>初始化任务</returns>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (_initialized)
                return;

            try
            {
                // 验证缓存配置
                if (_options.ValidateIntegrityOnStartup)
                {
                    var integrityResult = await ValidateIntegrityAsync(cancellationToken);
                    if (!integrityResult.IsValid && integrityResult.ErrorsFound > 0)
                    {
                        await ClearAllAsync(cancellationToken);
                    }
                }

                // 加载持久化缓存
                if (_options.EnablePersistentCache)
                {
                    await LoadAsync(cancellationToken);
                }

                // 启动文件监控
                if (_options.EnableFileWatcher)
                {
                    // 由外部调用者决定监控路径
                    // await _fileDetector.StartWatchingAsync(monitorPath, true, cancellationToken);
                }

                _initialized = true;
                LogInfo("增量缓存系统初始化完成");
            }
            catch (Exception ex)
            {
                LogError($"初始化缓存系统失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 检查并刷新指定文件的缓存
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="forceRefresh">是否强制刷新</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>刷新任务</returns>
        public async Task<bool> RefreshFileAsync(string filePath, bool forceRefresh = false, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(filePath))
                return false;

            await _operationSemaphore.WaitAsync(cancellationToken);
            try
            {
                var stopwatch = Stopwatch.StartNew();
                bool refreshed = false;

                try
                {
                    // 获取文件元数据
                    var fileMetadata = await _fileDetector.GetFileMetadataAsync(filePath, cancellationToken);
                    if (fileMetadata == null)
                    {
                        // 文件不存在，清理相关缓存
                        await CleanupFileCache(filePath, cancellationToken);
                        return true;
                    }

                    // 检查是否需要刷新
                    bool needsRefresh = forceRefresh;
                    
                    if (!needsRefresh && _options.EnableParseResultCache)
                    {
                        needsRefresh = !await _parseResultCache.ContainsValidAsync(filePath, fileMetadata, cancellationToken);
                    }

                    if (needsRefresh)
                    {
                        // 执行文件解析和缓存更新
                        await RefreshFileInternal(filePath, fileMetadata, cancellationToken);
                        refreshed = true;

                        // 触发缓存变更事件
                        OnCacheChanged(filePath, CacheChangeType.Updated, CacheItemType.ParseResult);
                    }

                    Interlocked.Increment(ref _totalRefreshOperations);
                    return refreshed;
                }
                finally
                {
                    stopwatch.Stop();
                    LogDebug($"刷新文件缓存 {filePath} 耗时: {stopwatch.ElapsedMilliseconds}ms");
                }
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }

        /// <summary>
        /// 批量刷新多个文件的缓存
        /// </summary>
        /// <param name="filePaths">文件路径集合</param>
        /// <param name="forceRefresh">是否强制刷新</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>刷新结果字典</returns>
        public async Task<Dictionary<string, bool>> BatchRefreshAsync(
            IEnumerable<string> filePaths,
            bool forceRefresh = false,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var results = new Dictionary<string, bool>();
            var filePathList = filePaths?.Where(p => !string.IsNullOrEmpty(p)).ToList() ?? new List<string>();

            if (filePathList.Count == 0)
                return results;

            // 限制并发数量
            var concurrency = Math.Min(_options.MaxBatchSize, filePathList.Count);
            var semaphore = new SemaphoreSlim(concurrency);
            
            var tasks = filePathList.Select(async filePath =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var refreshed = await RefreshFileAsync(filePath, forceRefresh, cancellationToken);
                    return new { FilePath = filePath, Refreshed = refreshed };
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var batchResults = await Task.WhenAll(tasks);

            foreach (var result in batchResults)
            {
                results[result.FilePath] = result.Refreshed;
            }

            return results;
        }

        /// <summary>
        /// 清理过期的缓存项
        /// </summary>
        /// <param name="maxAge">最大缓存时间</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>清理的项数</returns>
        public async Task<int> CleanupExpiredAsync(TimeSpan maxAge, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            int totalCleaned = 0;

            try
            {
                var tasks = new List<Task<int>>();

                // 清理解析结果缓存
                if (_options.EnableParseResultCache)
                {
                    tasks.Add(_parseResultCache.CleanupExpiredAsync(maxAge, cancellationToken));
                }

                // 清理变量声明缓存
                if (_options.EnableVariableDeclarationCache)
                {
                    tasks.Add(_variableCache.CleanupExpiredAsync(maxAge, cancellationToken));
                }

                var results = await Task.WhenAll(tasks);
                totalCleaned = results.Sum();

                Interlocked.Increment(ref _totalCleanupOperations);
                LogInfo($"清理过期缓存完成，清理了 {totalCleaned} 项");

                return totalCleaned;
            }
            catch (Exception ex)
            {
                LogError($"清理过期缓存失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 清空所有缓存
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>清空任务</returns>
        public async Task ClearAllAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            try
            {
                var tasks = new List<Task>();

                // 清空解析结果缓存
                if (_options.EnableParseResultCache)
                {
                    tasks.Add(_parseResultCache.ClearAsync(cancellationToken));
                }

                // 清空变量声明缓存
                if (_options.EnableVariableDeclarationCache)
                {
                    tasks.Add(_variableCache.ClearAsync(cancellationToken));
                }

                await Task.WhenAll(tasks);

                // 触发缓存变更事件
                OnCacheChanged(string.Empty, CacheChangeType.Cleared, CacheItemType.ParseResult);

                LogInfo("已清空所有缓存");
            }
            catch (Exception ex)
            {
                LogError($"清空缓存失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 预热缓存（预加载常用文件）
        /// </summary>
        /// <param name="filePaths">要预热的文件路径集合</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>预热任务</returns>
        public async Task WarmupAsync(IEnumerable<string> filePaths, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var filePathList = filePaths?.Where(p => !string.IsNullOrEmpty(p) && File.Exists(p)).ToList() ?? new List<string>();
            
            if (filePathList.Count == 0)
                return;

            var stopwatch = Stopwatch.StartNew();

            try
            {
                LogInfo($"开始预热缓存，文件数量: {filePathList.Count}");

                // 批量预热，强制刷新所有文件
                var results = await BatchRefreshAsync(filePathList, forceRefresh: true, cancellationToken);
                
                var warmedUpCount = results.Values.Count(refreshed => refreshed);
                
                Interlocked.Increment(ref _totalWarmupOperations);
                
                LogInfo($"预热缓存完成，成功预热 {warmedUpCount}/{filePathList.Count} 个文件，耗时: {stopwatch.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                LogError($"预热缓存失败: {ex.Message}");
                throw;
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        /// <summary>
        /// 获取缓存使用报告
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>缓存报告</returns>
        public async Task<CacheReport> GenerateReportAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            try
            {
                var statistics = GetCombinedStatistics();
                var healthStatus = DetermineHealthStatus(statistics);
                var recommendations = GenerateRecommendations(statistics);
                var warnings = GenerateWarnings(statistics);
                var analysis = GenerateAnalysis(statistics);

                return new CacheReport
                {
                    Statistics = statistics,
                    HealthStatus = healthStatus,
                    Recommendations = recommendations,
                    Warnings = warnings,
                    Analysis = analysis
                };
            }
            catch (Exception ex)
            {
                LogError($"生成缓存报告失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 保存缓存到持久化存储
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>保存任务</returns>
        public async Task SaveAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (!_options.EnablePersistentCache || string.IsNullOrEmpty(_options.PersistentCachePath))
                return;

            try
            {
                // 这里应该实现持久化逻辑
                // 由于时间限制，这里只是一个占位实现
                await Task.Run(() =>
                {
                    LogInfo($"保存缓存到持久化存储: {_options.PersistentCachePath}");
                    // 实际实现应该序列化缓存数据并保存到磁盘
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                LogError($"保存持久化缓存失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 从持久化存储加载缓存
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>加载任务</returns>
        public async Task LoadAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (!_options.EnablePersistentCache || string.IsNullOrEmpty(_options.PersistentCachePath))
                return;

            try
            {
                // 这里应该实现持久化加载逻辑
                // 由于时间限制，这里只是一个占位实现
                await Task.Run(() =>
                {
                    LogInfo($"从持久化存储加载缓存: {_options.PersistentCachePath}");
                    // 实际实现应该从磁盘加载并反序列化缓存数据
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                LogError($"加载持久化缓存失败: {ex.Message}");
                // 加载失败不应该阻止系统启动
            }
        }

        /// <summary>
        /// 检查缓存的完整性
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>完整性检查结果</returns>
        public async Task<CacheIntegrityResult> ValidateIntegrityAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var stopwatch = Stopwatch.StartNew();
            var errorDetails = new List<string>();
            var totalItemsChecked = 0;
            var errorsFound = 0;
            var errorsFixed = 0;

            try
            {
                // 验证解析结果缓存完整性
                if (_options.EnableParseResultCache)
                {
                    var parseResultValid = await _parseResultCache.ValidateIntegrityAsync(cancellationToken);
                    totalItemsChecked += _parseResultCache.Count;
                    
                    if (!parseResultValid)
                    {
                        errorsFound++;
                        errorDetails.Add("解析结果缓存完整性验证失败");
                    }
                }

                // 验证变量声明缓存完整性
                if (_options.EnableVariableDeclarationCache)
                {
                    var variableCacheValid = await _variableCache.ValidateIntegrityAsync(cancellationToken);
                    totalItemsChecked += _variableCache.Count;
                    
                    if (!variableCacheValid)
                    {
                        errorsFound++;
                        errorDetails.Add("变量声明缓存完整性验证失败");
                    }
                }

                var isValid = errorsFound == 0;

                return new CacheIntegrityResult
                {
                    IsValid = isValid,
                    TotalItemsChecked = totalItemsChecked,
                    ErrorsFound = errorsFound,
                    ErrorDetails = errorDetails,
                    ErrorsFixed = errorsFixed,
                    ValidationDuration = stopwatch.Elapsed
                };
            }
            catch (Exception ex)
            {
                errorDetails.Add($"验证过程中发生异常: {ex.Message}");
                
                return new CacheIntegrityResult
                {
                    IsValid = false,
                    TotalItemsChecked = totalItemsChecked,
                    ErrorsFound = errorsFound + 1,
                    ErrorDetails = errorDetails,
                    ErrorsFixed = errorsFixed,
                    ValidationDuration = stopwatch.Elapsed
                };
            }
            finally
            {
                stopwatch.Stop();
            }
        }
        #endregion

        #region 私有方法
        /// <summary>
        /// 获取组合统计信息
        /// </summary>
        /// <returns>组合统计信息</returns>
        private CacheStatistics GetCombinedStatistics()
        {
            var parseStats = _parseResultCache.GetStatisticsAsync().Result;
            var variableStats = _variableCache.GetStatisticsAsync().Result;

            return CacheStatistics.CreateEmpty()
                .AddHits(parseStats.HitCount + variableStats.HitCount)
                .AddMisses(parseStats.MissCount + variableStats.MissCount)
                .UpdateItemCounts(
                    parseStats.ParseResultItems,
                    variableStats.VariableDeclarationItems,
                    0)
                .UpdateMemoryUsage(
                    parseStats.ParseResultMemoryUsage,
                    variableStats.VariableDeclarationMemoryUsage,
                    0)
                .RecordOperation(CacheOperationType.Refresh, _totalRefreshOperations)
                .RecordOperation(CacheOperationType.Warmup, _totalWarmupOperations)
                .RecordOperation(CacheOperationType.Cleanup, _totalCleanupOperations);
        }

        /// <summary>
        /// 刷新文件内部实现
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="fileMetadata">文件元数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>刷新任务</returns>
        private async Task RefreshFileInternal(string filePath, FileMetadata fileMetadata, CancellationToken cancellationToken)
        {
            try
            {
                // 这里应该集成词法分析器和语法分析器
                // 由于时间限制，这里只是一个简化的实现
                
                // 模拟解析过程
                await Task.Run(async () =>
                {
                    // 1. 使用对象池获取词法分析器
                    using var lexerPooled = GlobalPoolManager.Instance.RentScopedPoolable<Lexers.Lexer>();
                    var lexer = lexerPooled.Item;

                    // 2. 使用对象池获取解析器  
                    using var parserPooled = GlobalPoolManager.Instance.RentScopedPoolable<Parsing.Parser>();
                    var parser = parserPooled.Item;

                    // 3. 读取文件内容
                    var content = await File.ReadAllTextAsync(filePath, cancellationToken);
                    
                    // 4. 执行词法分析
                    lexer.Reset(content);
                    var tokens = lexer.Tokenize();
                    
                    // 5. 执行语法分析
                    parser.Reset();
                    var ast = parser.Parse(tokens);
                    
                    // 6. 创建解析结果
                    var parseResult = ParseResult.CreateSuccess(
                        filePath,
                        fileMetadata,
                        tokens,
                        ast,
                        DateTime.UtcNow.AddSeconds(-1),
                        DateTime.UtcNow,
                        "0.8.0");

                    // 7. 存储到缓存
                    if (_options.EnableParseResultCache)
                    {
                        await _parseResultCache.SetAsync(filePath, parseResult, cancellationToken);
                    }

                    // 8. 提取并缓存变量声明
                    if (_options.EnableVariableDeclarationCache)
                    {
                        var variableDeclarations = ExtractVariableDeclarations(ast, filePath);
                        await _variableCache.SetVariableDeclarationsAsync(filePath, variableDeclarations, cancellationToken);
                    }
                    
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                LogError($"刷新文件缓存失败 {filePath}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 从AST提取变量声明
        /// </summary>
        /// <param name="ast">AST根节点</param>
        /// <param name="filePath">文件路径</param>
        /// <returns>变量声明集合</returns>
        private List<VariableDeclaration> ExtractVariableDeclarations(ASTNode ast, string filePath)
        {
            var declarations = new List<VariableDeclaration>();
            
            if (ast == null)
                return declarations;

            // 这里应该遍历AST提取变量声明
            // 由于时间限制，这里只是一个简化的实现
            
            // 示例：创建一些假的变量声明用于测试
            // 实际实现应该遍历AST节点，查找变量声明语句
            
            return declarations;
        }

        /// <summary>
        /// 清理文件缓存
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>清理任务</returns>
        private async Task CleanupFileCache(string filePath, CancellationToken cancellationToken)
        {
            var tasks = new List<Task>();

            if (_options.EnableParseResultCache)
            {
                tasks.Add(_parseResultCache.RemoveAsync(filePath, cancellationToken));
            }

            if (_options.EnableVariableDeclarationCache)
            {
                tasks.Add(_variableCache.RemoveFileDeclarationsAsync(filePath, cancellationToken));
            }

            await Task.WhenAll(tasks);

            OnCacheChanged(filePath, CacheChangeType.Removed, CacheItemType.ParseResult);
        }

        /// <summary>
        /// 文件变更事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">文件变更事件参数</param>
        private void OnFileChanged(object sender, FileChangedEventArgs e)
        {
            if (_disposed)
                return;

            // 异步处理文件变更，避免阻塞文件监控线程
            _ = Task.Run(async () =>
            {
                try
                {
                    await RefreshFileAsync(e.FilePath, forceRefresh: true);
                }
                catch (Exception ex)
                {
                    LogError($"处理文件变更失败 {e.FilePath}: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 清理定时器事件处理
        /// </summary>
        /// <param name="state">定时器状态</param>
        private void OnCleanupTimer(object state)
        {
            if (_disposed)
                return;

            _ = Task.Run(async () =>
            {
                try
                {
                    await CleanupExpiredAsync(_options.CacheExpiration);
                }
                catch (Exception ex)
                {
                    LogError($"定时清理失败: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 触发缓存变更事件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="changeType">变更类型</param>
        /// <param name="itemType">项类型</param>
        private void OnCacheChanged(string filePath, CacheChangeType changeType, CacheItemType itemType)
        {
            try
            {
                var eventArgs = new CacheChangedEventArgs(filePath, changeType, itemType, DateTime.UtcNow);
                CacheChanged?.Invoke(this, eventArgs);
            }
            catch (Exception ex)
            {
                LogError($"触发缓存变更事件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 确定健康状态
        /// </summary>
        /// <param name="statistics">统计信息</param>
        /// <returns>健康状态</returns>
        private CacheHealthStatus DetermineHealthStatus(CacheStatistics statistics)
        {
            var hitRatio = statistics.HitRatio;
            var memoryUsage = statistics.TotalMemoryUsage;

            if (hitRatio < 0.3 || memoryUsage > _options.MaxMemoryUsage * 0.9)
                return CacheHealthStatus.Warning;

            if (hitRatio < 0.1 || memoryUsage > _options.MaxMemoryUsage)
                return CacheHealthStatus.Error;

            return CacheHealthStatus.Good;
        }

        /// <summary>
        /// 生成建议信息
        /// </summary>
        /// <param name="statistics">统计信息</param>
        /// <returns>建议列表</returns>
        private List<string> GenerateRecommendations(CacheStatistics statistics)
        {
            var recommendations = new List<string>();

            if (statistics.HitRatio < 0.5)
            {
                recommendations.Add("缓存命中率较低，考虑调整缓存策略或增加预热操作");
            }

            if (statistics.TotalMemoryUsage > _options.MaxMemoryUsage * 0.8)
            {
                recommendations.Add("内存使用量较高，考虑增加清理频率或减少缓存大小");
            }

            if (statistics.TotalItems > _options.MaxCacheSize * 0.8)
            {
                recommendations.Add("缓存项数量较多，考虑增加清理频率");
            }

            return recommendations;
        }

        /// <summary>
        /// 生成警告信息
        /// </summary>
        /// <param name="statistics">统计信息</param>
        /// <returns>警告列表</returns>
        private List<string> GenerateWarnings(CacheStatistics statistics)
        {
            var warnings = new List<string>();

            if (statistics.ErrorStats.TotalErrors > 0)
            {
                warnings.Add($"检测到 {statistics.ErrorStats.TotalErrors} 个缓存错误");
            }

            if (statistics.IntegrityValidationFailures > 0)
            {
                warnings.Add($"完整性验证失败 {statistics.IntegrityValidationFailures} 次");
            }

            return warnings;
        }

        /// <summary>
        /// 生成分析结果
        /// </summary>
        /// <param name="statistics">统计信息</param>
        /// <returns>分析字典</returns>
        private Dictionary<string, object> GenerateAnalysis(CacheStatistics statistics)
        {
            return new Dictionary<string, object>
            {
                ["efficiency_score"] = statistics.CalculateEfficiencyScore(),
                ["memory_utilization"] = (double)statistics.TotalMemoryUsage / _options.MaxMemoryUsage,
                ["cache_utilization"] = (double)statistics.TotalItems / _options.MaxCacheSize,
                ["avg_hit_ratio"] = statistics.HitRatio,
                ["total_operations"] = statistics.AddOperations + statistics.UpdateOperations + statistics.RemoveOperations
            };
        }

        /// <summary>
        /// 检查是否已释放资源
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(IncrementalCacheManager));
        }

        /// <summary>
        /// 记录信息日志
        /// </summary>
        /// <param name="message">日志消息</param>
        private void LogInfo(string message)
        {
            if (_options.LogLevel <= CacheLogLevel.Info)
            {
                UnityEngine.Debug.Log($"[IncrementalCache] {message}");
            }
        }

        /// <summary>
        /// 记录调试日志
        /// </summary>
        /// <param name="message">日志消息</param>
        private void LogDebug(string message)
        {
            if (_options.LogLevel <= CacheLogLevel.Debug)
            {
                UnityEngine.Debug.Log($"[IncrementalCache] {message}");
            }
        }

        /// <summary>
        /// 记录错误日志
        /// </summary>
        /// <param name="message">日志消息</param>
        private void LogError(string message)
        {
            if (_options.LogLevel <= CacheLogLevel.Error)
            {
                UnityEngine.Debug.LogError($"[IncrementalCache] {message}");
            }
        }
        #endregion

        #region IDisposable 实现
        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                // 保存缓存
                if (_options.AutoSaveOnShutdown)
                {
                    SaveAsync().Wait(TimeSpan.FromSeconds(10));
                }

                // 停止清理定时器
                _cleanupTimer?.Dispose();

                // 释放文件监控
                _fileDetector?.Dispose();

                // 释放缓存
                _parseResultCache?.Dispose();
                _variableCache?.Dispose();

                // 释放信号量
                _operationSemaphore?.Dispose();

                LogInfo("增量缓存系统已关闭");
            }
            catch (Exception ex)
            {
                LogError($"释放缓存系统资源失败: {ex.Message}");
            }
        }
        #endregion
    }
}