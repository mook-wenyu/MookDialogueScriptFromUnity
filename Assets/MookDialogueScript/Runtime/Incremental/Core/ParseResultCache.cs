using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MookDialogueScript.Incremental.Contracts;

namespace MookDialogueScript.Incremental.Core
{
    /// <summary>
    /// 解析结果缓存实现
    /// 基于内存的高性能解析结果缓存，支持LRU淘汰策略和统计信息收集
    /// </summary>
    public sealed class ParseResultCache : IParseResultCache
    {
        #region 字段
        private readonly IncrementalCacheOptions _options;
        private readonly ConcurrentDictionary<string, CacheEntry> _cache;
        private readonly ConcurrentDictionary<string, long> _accessOrder;
        private readonly ReaderWriterLockSlim _rwLock;
        private long _currentAccessCounter;
        private long _hitCount;
        private long _missCount;
        private volatile bool _disposed;

        // 统计相关字段
        private long _addOperations;
        private long _updateOperations;
        private long _removeOperations;
        private long _clearOperations;
        private long _cleanupOperations;
        #endregion

        #region 属性
        /// <summary>
        /// 缓存大小（项数）
        /// </summary>
        public int Count => _cache.Count;

        /// <summary>
        /// 缓存使用的内存大小（字节）
        /// </summary>
        public long MemoryUsage
        {
            get
            {
                long totalSize = 0;
                foreach (var entry in _cache.Values)
                {
                    totalSize += entry.EstimateSize();
                }
                return totalSize;
            }
        }
        #endregion

        #region 构造函数
        /// <summary>
        /// 初始化解析结果缓存
        /// </summary>
        /// <param name="options">缓存配置选项</param>
        public ParseResultCache(IncrementalCacheOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _cache = new ConcurrentDictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
            _accessOrder = new ConcurrentDictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            _rwLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
            _currentAccessCounter = 0;
        }
        #endregion

        #region IParseResultCache 实现
        /// <summary>
        /// 尝试从缓存获取解析结果
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="fileMetadata">文件元数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>解析结果，如果缓存未命中则返回null</returns>
        public async Task<ParseResult> GetAsync(string filePath, FileMetadata fileMetadata, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(filePath) || fileMetadata == null)
            {
                Interlocked.Increment(ref _missCount);
                return null;
            }

            return await Task.Run(() =>
            {
                var cacheKey = GenerateCacheKey(filePath);

                if (_cache.TryGetValue(cacheKey, out var entry))
                {
                    // 检查缓存项是否仍然有效
                    if (entry.IsValid(fileMetadata, _options.CacheExpiration))
                    {
                        // 更新访问顺序
                        UpdateAccessOrder(cacheKey);

                        // 更新统计信息
                        Interlocked.Increment(ref _hitCount);

                        // 返回更新了访问统计的解析结果
                        return entry.ParseResult.WithUpdatedAccess();
                    }
                    else
                    {
                        // 缓存项已过期，移除它
                        _ = Task.Run(() => RemoveAsync(filePath, cancellationToken), cancellationToken);
                    }
                }

                Interlocked.Increment(ref _missCount);
                return null;
            }, cancellationToken);
        }

        /// <summary>
        /// 将解析结果存储到缓存
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="parseResult">解析结果</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>存储任务</returns>
        public async Task SetAsync(string filePath, ParseResult parseResult, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(filePath) || parseResult == null)
                return;

            await Task.Run(() =>
            {
                var cacheKey = GenerateCacheKey(filePath);
                var entry = new CacheEntry(parseResult, DateTime.UtcNow);
                var isUpdate = _cache.ContainsKey(cacheKey);

                // 添加或更新缓存项
                _cache.AddOrUpdate(cacheKey, entry, (key, oldEntry) => entry);

                // 更新访问顺序
                UpdateAccessOrder(cacheKey);

                // 更新统计信息
                if (isUpdate)
                {
                    Interlocked.Increment(ref _updateOperations);
                }
                else
                {
                    Interlocked.Increment(ref _addOperations);
                }

                // 检查是否需要清理过期项或执行LRU淘汰
                _ = Task.Run(() => EnforceCapacityLimits(cancellationToken), cancellationToken);

            }, cancellationToken);
        }

        /// <summary>
        /// 从缓存中移除指定文件的解析结果
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否成功移除</returns>
        public async Task<bool> RemoveAsync(string filePath, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(filePath))
                return false;

            return await Task.Run(() =>
            {
                var cacheKey = GenerateCacheKey(filePath);
                var removed = _cache.TryRemove(cacheKey, out _);

                if (removed)
                {
                    _accessOrder.TryRemove(cacheKey, out _);
                    Interlocked.Increment(ref _removeOperations);
                }

                return removed;
            }, cancellationToken);
        }

        /// <summary>
        /// 批量获取解析结果
        /// </summary>
        /// <param name="requests">批量请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>解析结果字典</returns>
        public async Task<Dictionary<string, ParseResult>> BatchGetAsync(
            IEnumerable<(string filePath, FileMetadata metadata)> requests,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var results = new Dictionary<string, ParseResult>();
            var requestList = requests?.ToList() ?? new List<(string, FileMetadata)>();

            if (requestList.Count == 0)
                return results;

            // 限制并发数量
            var semaphore = new SemaphoreSlim(Math.Min(_options.WarmupConcurrency, requestList.Count));
            var tasks = requestList.Select(async request =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var result = await GetAsync(request.filePath, request.metadata, cancellationToken);
                    return new { FilePath = request.filePath, Result = result };
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var batchResults = await Task.WhenAll(tasks);

            foreach (var item in batchResults)
            {
                results[item.FilePath] = item.Result;
            }

            return results;
        }

        /// <summary>
        /// 批量存储解析结果
        /// </summary>
        /// <param name="results">解析结果字典</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>存储任务</returns>
        public async Task BatchSetAsync(Dictionary<string, ParseResult> results, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (results == null || results.Count == 0)
                return;

            // 限制并发数量
            var semaphore = new SemaphoreSlim(Math.Min(_options.WarmupConcurrency, results.Count));
            var tasks = results.Select(async kvp =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    await SetAsync(kvp.Key, kvp.Value, cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// 检查缓存中是否存在指定文件的有效解析结果
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="fileMetadata">文件元数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否存在有效缓存</returns>
        public async Task<bool> ContainsValidAsync(string filePath, FileMetadata fileMetadata, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(filePath) || fileMetadata == null)
                return false;

            return await Task.Run(() =>
            {
                var cacheKey = GenerateCacheKey(filePath);
                return _cache.TryGetValue(cacheKey, out var entry) && 
                       entry.IsValid(fileMetadata, _options.CacheExpiration);
            }, cancellationToken);
        }

        /// <summary>
        /// 获取缓存中的所有文件路径
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>文件路径集合</returns>
        public async Task<IEnumerable<string>> GetAllKeysAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            return await Task.Run(() =>
            {
                return _cache.Keys.Select(key => RestoreFilePathFromKey(key)).ToList();
            }, cancellationToken);
        }

        /// <summary>
        /// 清空所有解析结果缓存
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>清空任务</returns>
        public async Task ClearAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            await Task.Run(() =>
            {
                _cache.Clear();
                _accessOrder.Clear();
                Interlocked.Increment(ref _clearOperations);
            }, cancellationToken);
        }

        /// <summary>
        /// 清理过期的解析结果缓存
        /// </summary>
        /// <param name="maxAge">最大缓存时间</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>清理的项数</returns>
        public async Task<int> CleanupExpiredAsync(TimeSpan maxAge, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            return await Task.Run(() =>
            {
                var expiredKeys = new List<string>();
                var cutoffTime = DateTime.UtcNow - maxAge;

                // 查找过期项
                foreach (var kvp in _cache)
                {
                    if (kvp.Value.CreatedTime < cutoffTime)
                    {
                        expiredKeys.Add(kvp.Key);
                    }
                }

                // 移除过期项
                foreach (var key in expiredKeys)
                {
                    _cache.TryRemove(key, out _);
                    _accessOrder.TryRemove(key, out _);
                }

                if (expiredKeys.Count > 0)
                {
                    Interlocked.Increment(ref _cleanupOperations);
                }

                return expiredKeys.Count;
            }, cancellationToken);
        }

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>缓存统计信息</returns>
        public async Task<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            return await Task.Run(() =>
            {
                return CacheStatistics.CreateEmpty()
                    .AddHits(_hitCount)
                    .AddMisses(_missCount)
                    .UpdateItemCounts(Count, 0, 0)
                    .UpdateMemoryUsage(MemoryUsage, 0, 0)
                    .RecordOperation(CacheOperationType.Add, _addOperations)
                    .RecordOperation(CacheOperationType.Update, _updateOperations)
                    .RecordOperation(CacheOperationType.Remove, _removeOperations)
                    .RecordOperation(CacheOperationType.Clear, _clearOperations)
                    .RecordOperation(CacheOperationType.Cleanup, _cleanupOperations);
            }, cancellationToken);
        }

        /// <summary>
        /// 预热缓存（预加载解析结果）
        /// </summary>
        /// <param name="filePaths">要预热的文件路径集合</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>预热任务</returns>
        public async Task WarmupAsync(IEnumerable<string> filePaths, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var filePathList = filePaths?.ToList() ?? new List<string>();
            if (filePathList.Count == 0)
                return;

            // 预热操作通常由外部系统（如IncrementalCacheManager）负责
            // 这里只是标记预热操作的统计信息
            await Task.Run(() =>
            {
                // 预热操作的具体实现依赖于外部调用者
                // 这里只更新统计信息
                foreach (var filePath in filePathList)
                {
                    var cacheKey = GenerateCacheKey(filePath);
                    if (_cache.ContainsKey(cacheKey))
                    {
                        UpdateAccessOrder(cacheKey);
                    }
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 验证缓存内容的完整性
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>验证结果</returns>
        public async Task<bool> ValidateIntegrityAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            return await Task.Run(() =>
            {
                try
                {
                    // 验证缓存项的完整性
                    foreach (var kvp in _cache)
                    {
                        var entry = kvp.Value;
                        if (entry.ParseResult == null)
                            return false;

                        // 验证解析结果的基本属性
                        if (string.IsNullOrEmpty(entry.ParseResult.FilePath))
                            return false;

                        if (entry.ParseResult.FileMetadata == null)
                            return false;
                    }

                    // 验证访问顺序数据的一致性
                    foreach (var key in _accessOrder.Keys)
                    {
                        if (!_cache.ContainsKey(key))
                            return false;
                    }

                    return true;
                }
                catch
                {
                    return false;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 计算缓存命中率
        /// </summary>
        /// <returns>命中率（0.0-1.0）</returns>
        public double GetHitRatio()
        {
            var totalAccesses = _hitCount + _missCount;
            return totalAccesses > 0 ? (double)_hitCount / totalAccesses : 0.0;
        }
        #endregion

        #region 私有方法
        /// <summary>
        /// 生成缓存键
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>缓存键</returns>
        private string GenerateCacheKey(string filePath)
        {
            return _options.KeyStrategy switch
            {
                CacheKeyStrategy.FilePath => filePath,
                CacheKeyStrategy.FilePathHash => filePath.GetHashCode().ToString(),
                CacheKeyStrategy.ContentHash => filePath, // 内容哈希需要文件内容，这里简化处理
                CacheKeyStrategy.CompositeHash => $"{filePath}_{filePath.GetHashCode()}",
                _ => filePath
            };
        }

        /// <summary>
        /// 从缓存键恢复文件路径
        /// </summary>
        /// <param name="cacheKey">缓存键</param>
        /// <returns>文件路径</returns>
        private string RestoreFilePathFromKey(string cacheKey)
        {
            // 这是一个简化的实现，实际中可能需要维护键到路径的映射
            return _options.KeyStrategy switch
            {
                CacheKeyStrategy.FilePath => cacheKey,
                CacheKeyStrategy.CompositeHash => cacheKey.Contains('_') ? cacheKey.Substring(0, cacheKey.LastIndexOf('_')) : cacheKey,
                _ => cacheKey
            };
        }

        /// <summary>
        /// 更新访问顺序
        /// </summary>
        /// <param name="cacheKey">缓存键</param>
        private void UpdateAccessOrder(string cacheKey)
        {
            var accessTime = Interlocked.Increment(ref _currentAccessCounter);
            _accessOrder.AddOrUpdate(cacheKey, accessTime, (key, oldTime) => accessTime);
        }

        /// <summary>
        /// 强制执行容量限制
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        private async Task EnforceCapacityLimits(CancellationToken cancellationToken = default)
        {
            await Task.Run(() =>
            {
                // 检查内存使用量
                if (_options.MaxMemoryUsage > 0 && MemoryUsage > _options.MaxMemoryUsage)
                {
                    EvictLeastRecentlyUsed(0.1); // 淘汰10%的项
                }

                // 检查项数限制
                if (_options.MaxCacheSize > 0 && Count > _options.MaxCacheSize)
                {
                    var excessCount = Count - _options.MaxCacheSize;
                    EvictLeastRecentlyUsed(excessCount);
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 淘汰最少使用的项
        /// </summary>
        /// <param name="countOrRatio">要淘汰的项数或比例</param>
        private void EvictLeastRecentlyUsed(double countOrRatio)
        {
            var totalCount = Count;
            if (totalCount == 0)
                return;

            int itemsToEvict;
            if (countOrRatio >= 1.0)
            {
                itemsToEvict = (int)countOrRatio;
            }
            else
            {
                itemsToEvict = (int)(totalCount * countOrRatio);
            }

            if (itemsToEvict <= 0)
                return;

            // 获取最少使用的项
            var leastUsedItems = _accessOrder
                .OrderBy(kvp => kvp.Value)
                .Take(itemsToEvict)
                .Select(kvp => kvp.Key)
                .ToList();

            // 移除这些项
            foreach (var key in leastUsedItems)
            {
                _cache.TryRemove(key, out _);
                _accessOrder.TryRemove(key, out _);
            }
        }

        /// <summary>
        /// 检查是否已释放资源
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ParseResultCache));
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
            
            _cache.Clear();
            _accessOrder.Clear();
            _rwLock.Dispose();
        }
        #endregion

        #region 内部类型
        /// <summary>
        /// 缓存条目
        /// </summary>
        private sealed class CacheEntry
        {
            public ParseResult ParseResult { get; }
            public DateTime CreatedTime { get; }
            public DateTime LastAccessTime { get; private set; }

            public CacheEntry(ParseResult parseResult, DateTime createdTime)
            {
                ParseResult = parseResult;
                CreatedTime = createdTime;
                LastAccessTime = createdTime;
            }

            public bool IsValid(FileMetadata fileMetadata, TimeSpan maxAge)
            {
                // 检查是否过期
                if (DateTime.UtcNow - CreatedTime > maxAge)
                    return false;

                // 检查文件元数据是否匹配
                return ParseResult.MatchesFileMetadata(fileMetadata);
            }

            public void UpdateAccess()
            {
                LastAccessTime = DateTime.UtcNow;
            }

            public long EstimateSize()
            {
                return ParseResult.EstimateMemoryUsage() + 128; // 加上Entry本身的开销
            }
        }
        #endregion
    }
}