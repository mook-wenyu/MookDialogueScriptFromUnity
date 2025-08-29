using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MookDialogueScript.Incremental.Contracts;

namespace MookDialogueScript.Incremental.VariableCache
{
    /// <summary>
    /// 变量声明缓存实现
    /// 基于内存的高性能变量声明缓存，支持作用域查询、模糊搜索和使用统计
    /// </summary>
    public sealed class VariableDeclarationCache : IVariableDeclarationCache
    {
        #region 字段
        private readonly IncrementalCacheOptions _options;
        
        // 主缓存：文件路径 -> 变量声明列表
        private readonly ConcurrentDictionary<string, CacheEntry> _fileDeclarations;
        
        // 索引缓存：变量名 -> 声明信息列表（支持多个作用域）
        private readonly ConcurrentDictionary<string, List<VariableDeclaration>> _variableIndex;
        
        // 作用域索引：作用域 -> 变量名集合
        private readonly ConcurrentDictionary<string, HashSet<string>> _scopeIndex;
        
        private readonly ReaderWriterLockSlim _rwLock;
        private volatile bool _disposed;

        // 统计相关字段
        private long _hitCount;
        private long _missCount;
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
        public int Count => _variableIndex.Values.Sum(list => list.Count);

        /// <summary>
        /// 缓存使用的内存大小（字节）
        /// </summary>
        public long MemoryUsage
        {
            get
            {
                long totalSize = 0;
                
                // 估算文件声明缓存大小
                foreach (var entry in _fileDeclarations.Values)
                {
                    totalSize += entry.EstimateSize();
                }
                
                // 估算索引大小
                totalSize += _variableIndex.Count * 64; // 字典开销
                totalSize += _scopeIndex.Count * 64; // 字典开销
                
                return totalSize;
            }
        }
        #endregion

        #region 构造函数
        /// <summary>
        /// 初始化变量声明缓存
        /// </summary>
        /// <param name="options">缓存配置选项</param>
        public VariableDeclarationCache(IncrementalCacheOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _fileDeclarations = new ConcurrentDictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
            _variableIndex = new ConcurrentDictionary<string, List<VariableDeclaration>>(StringComparer.OrdinalIgnoreCase);
            _scopeIndex = new ConcurrentDictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            _rwLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        }
        #endregion

        #region IVariableDeclarationCache 实现
        /// <summary>
        /// 获取指定文件中的所有变量声明
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>变量声明集合</returns>
        public async Task<IEnumerable<VariableDeclaration>> GetVariableDeclarationsAsync(string filePath, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(filePath))
            {
                Interlocked.Increment(ref _missCount);
                return null;
            }

            return await Task.Run(() =>
            {
                var cacheKey = GenerateCacheKey(filePath);

                if (_fileDeclarations.TryGetValue(cacheKey, out var entry))
                {
                    if (!entry.IsExpired(_options.CacheExpiration))
                    {
                        entry.UpdateAccess();
                        Interlocked.Increment(ref _hitCount);
                        return entry.Declarations.AsEnumerable();
                    }
                    else
                    {
                        // 缓存项已过期，移除它
                        _ = Task.Run(() => RemoveFileDeclarationsAsync(filePath, cancellationToken), cancellationToken);
                    }
                }

                Interlocked.Increment(ref _missCount);
                return null;
            }, cancellationToken);
        }

        /// <summary>
        /// 获取指定变量的声明信息
        /// </summary>
        /// <param name="variableName">变量名</param>
        /// <param name="scope">作用域（文件路径或节点名）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>变量声明信息</returns>
        public async Task<VariableDeclaration> GetVariableDeclarationAsync(string variableName, string scope = null, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(variableName))
            {
                Interlocked.Increment(ref _missCount);
                return null;
            }

            return await Task.Run(() =>
            {
                _rwLock.EnterReadLock();
                try
                {
                    if (_variableIndex.TryGetValue(variableName, out var declarations))
                    {
                        VariableDeclaration result = null;

                        if (string.IsNullOrEmpty(scope))
                        {
                            // 没有指定作用域，返回第一个匹配的声明（通常是全局的）
                            result = declarations.FirstOrDefault(d => d.IsGlobal) ?? declarations.FirstOrDefault();
                        }
                        else
                        {
                            // 指定了作用域，查找匹配的声明
                            result = declarations.FirstOrDefault(d => 
                                d.Scope.Equals(scope, StringComparison.OrdinalIgnoreCase) ||
                                d.DeclarationFilePath.Equals(scope, StringComparison.OrdinalIgnoreCase));
                            
                            // 如果在指定作用域中找不到，尝试查找全局变量
                            result ??= declarations.FirstOrDefault(d => d.IsGlobal);
                        }

                        if (result != null)
                        {
                            Interlocked.Increment(ref _hitCount);
                            return result;
                        }
                    }

                    Interlocked.Increment(ref _missCount);
                    return null;
                }
                finally
                {
                    _rwLock.ExitReadLock();
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 存储变量声明信息
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="declarations">变量声明集合</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>存储任务</returns>
        public async Task SetVariableDeclarationsAsync(string filePath, IEnumerable<VariableDeclaration> declarations, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(filePath) || declarations == null)
                return;

            await Task.Run(() =>
            {
                var declarationList = declarations.ToList();
                var cacheKey = GenerateCacheKey(filePath);
                var entry = new CacheEntry(declarationList, DateTime.UtcNow);
                
                _rwLock.EnterWriteLock();
                try
                {
                    // 移除旧的声明（如果存在）
                    if (_fileDeclarations.TryGetValue(cacheKey, out var oldEntry))
                    {
                        RemoveFromIndices(oldEntry.Declarations);
                        Interlocked.Increment(ref _updateOperations);
                    }
                    else
                    {
                        Interlocked.Increment(ref _addOperations);
                    }

                    // 添加新的声明
                    _fileDeclarations[cacheKey] = entry;
                    AddToIndices(declarationList);
                }
                finally
                {
                    _rwLock.ExitWriteLock();
                }

                // 检查是否需要清理
                _ = Task.Run(() => EnforceCapacityLimits(cancellationToken), cancellationToken);

            }, cancellationToken);
        }

        /// <summary>
        /// 添加或更新单个变量声明
        /// </summary>
        /// <param name="declaration">变量声明</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>存储任务</returns>
        public async Task AddOrUpdateDeclarationAsync(VariableDeclaration declaration, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (declaration == null)
                return;

            await Task.Run(() =>
            {
                var filePath = declaration.DeclarationFilePath;
                var cacheKey = GenerateCacheKey(filePath);

                _rwLock.EnterWriteLock();
                try
                {
                    // 获取或创建文件的声明列表
                    if (!_fileDeclarations.TryGetValue(cacheKey, out var entry))
                    {
                        entry = new CacheEntry(new List<VariableDeclaration>(), DateTime.UtcNow);
                        _fileDeclarations[cacheKey] = entry;
                    }

                    // 查找现有声明
                    var existingIndex = entry.Declarations.FindIndex(d => 
                        d.Name.Equals(declaration.Name, StringComparison.OrdinalIgnoreCase) &&
                        d.Scope.Equals(declaration.Scope, StringComparison.OrdinalIgnoreCase));

                    if (existingIndex >= 0)
                    {
                        // 更新现有声明
                        var oldDeclaration = entry.Declarations[existingIndex];
                        entry.Declarations[existingIndex] = declaration;
                        
                        // 更新索引
                        RemoveFromIndices(new[] { oldDeclaration });
                        AddToIndices(new[] { declaration });
                        
                        Interlocked.Increment(ref _updateOperations);
                    }
                    else
                    {
                        // 添加新声明
                        entry.Declarations.Add(declaration);
                        AddToIndices(new[] { declaration });
                        
                        Interlocked.Increment(ref _addOperations);
                    }

                    entry.UpdateAccess();
                }
                finally
                {
                    _rwLock.ExitWriteLock();
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 移除指定文件的所有变量声明
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否成功移除</returns>
        public async Task<bool> RemoveFileDeclarationsAsync(string filePath, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(filePath))
                return false;

            return await Task.Run(() =>
            {
                var cacheKey = GenerateCacheKey(filePath);

                _rwLock.EnterWriteLock();
                try
                {
                    if (_fileDeclarations.TryRemove(cacheKey, out var entry))
                    {
                        RemoveFromIndices(entry.Declarations);
                        Interlocked.Increment(ref _removeOperations);
                        return true;
                    }

                    return false;
                }
                finally
                {
                    _rwLock.ExitWriteLock();
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 移除指定变量的声明
        /// </summary>
        /// <param name="variableName">变量名</param>
        /// <param name="scope">作用域</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否成功移除</returns>
        public async Task<bool> RemoveVariableDeclarationAsync(string variableName, string scope = null, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(variableName))
                return false;

            return await Task.Run(() =>
            {
                _rwLock.EnterWriteLock();
                try
                {
                    if (_variableIndex.TryGetValue(variableName, out var declarations))
                    {
                        var toRemove = string.IsNullOrEmpty(scope) 
                            ? declarations.ToList()
                            : declarations.Where(d => 
                                d.Scope.Equals(scope, StringComparison.OrdinalIgnoreCase) ||
                                d.DeclarationFilePath.Equals(scope, StringComparison.OrdinalIgnoreCase)).ToList();

                        if (toRemove.Count > 0)
                        {
                            // 从文件缓存中移除
                            foreach (var declaration in toRemove)
                            {
                                var fileCacheKey = GenerateCacheKey(declaration.DeclarationFilePath);
                                if (_fileDeclarations.TryGetValue(fileCacheKey, out var entry))
                                {
                                    entry.Declarations.RemoveAll(d => 
                                        d.Name.Equals(declaration.Name, StringComparison.OrdinalIgnoreCase) &&
                                        d.Scope.Equals(declaration.Scope, StringComparison.OrdinalIgnoreCase));
                                }
                            }

                            // 从索引中移除
                            RemoveFromIndices(toRemove);
                            
                            Interlocked.Increment(ref _removeOperations);
                            return true;
                        }
                    }

                    return false;
                }
                finally
                {
                    _rwLock.ExitWriteLock();
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 查询变量声明（支持模糊搜索）
        /// </summary>
        /// <param name="namePattern">变量名模式（支持通配符）</param>
        /// <param name="variableType">变量类型筛选</param>
        /// <param name="scope">作用域筛选</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>匹配的变量声明</returns>
        public async Task<IEnumerable<VariableDeclaration>> QueryDeclarationsAsync(
            string namePattern = null,
            Type variableType = null,
            string scope = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            return await Task.Run(() =>
            {
                _rwLock.EnterReadLock();
                try
                {
                    var results = new List<VariableDeclaration>();

                    foreach (var kvp in _variableIndex)
                    {
                        var variableName = kvp.Key;
                        var declarations = kvp.Value;

                        // 名称模式匹配
                        if (!string.IsNullOrEmpty(namePattern) && !IsMatchPattern(variableName, namePattern))
                            continue;

                        foreach (var declaration in declarations)
                        {
                            // 类型筛选
                            if (variableType != null && !declaration.IsTypeCompatible(variableType))
                                continue;

                            // 作用域筛选
                            if (!string.IsNullOrEmpty(scope) && !declaration.IsVisibleInScope(scope))
                                continue;

                            results.Add(declaration);
                        }
                    }

                    return results.AsEnumerable();
                }
                finally
                {
                    _rwLock.ExitReadLock();
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 获取所有已缓存的变量名
        /// </summary>
        /// <param name="scope">作用域筛选</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>变量名集合</returns>
        public async Task<IEnumerable<string>> GetAllVariableNamesAsync(string scope = null, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            return await Task.Run(() =>
            {
                _rwLock.EnterReadLock();
                try
                {
                    if (string.IsNullOrEmpty(scope))
                    {
                        return _variableIndex.Keys.ToList();
                    }

                    return _scopeIndex.TryGetValue(scope, out var names) ? names.ToList() : new List<string>();
                }
                finally
                {
                    _rwLock.ExitReadLock();
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 获取所有作用域
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>作用域集合</returns>
        public async Task<IEnumerable<string>> GetAllScopesAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            return await Task.Run(() =>
            {
                _rwLock.EnterReadLock();
                try
                {
                    return _scopeIndex.Keys.ToList();
                }
                finally
                {
                    _rwLock.ExitReadLock();
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 批量获取变量声明
        /// </summary>
        /// <param name="requests">批量请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>变量声明字典</returns>
        public async Task<Dictionary<string, VariableDeclaration>> BatchGetDeclarationsAsync(
            IEnumerable<(string variableName, string scope)> requests,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var results = new Dictionary<string, VariableDeclaration>();
            var requestList = requests?.ToList() ?? new List<(string, string)>();

            if (requestList.Count == 0)
                return results;

            var semaphore = new SemaphoreSlim(Math.Min(_options.WarmupConcurrency, requestList.Count));
            var tasks = requestList.Select(async request =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var result = await GetVariableDeclarationAsync(request.variableName, request.scope, cancellationToken);
                    var key = string.IsNullOrEmpty(request.scope) ? request.variableName : $"{request.scope}.{request.variableName}";
                    return new { Key = key, Result = result };
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var batchResults = await Task.WhenAll(tasks);

            foreach (var item in batchResults)
            {
                results[item.Key] = item.Result;
            }

            return results;
        }

        /// <summary>
        /// 批量存储变量声明
        /// </summary>
        /// <param name="declarations">声明字典</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>存储任务</returns>
        public async Task BatchSetDeclarationsAsync(Dictionary<string, IEnumerable<VariableDeclaration>> declarations, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (declarations == null || declarations.Count == 0)
                return;

            var semaphore = new SemaphoreSlim(Math.Min(_options.WarmupConcurrency, declarations.Count));
            var tasks = declarations.Select(async kvp =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    await SetVariableDeclarationsAsync(kvp.Key, kvp.Value, cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// 检查变量是否存在声明
        /// </summary>
        /// <param name="variableName">变量名</param>
        /// <param name="scope">作用域</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否存在</returns>
        public async Task<bool> ContainsVariableAsync(string variableName, string scope = null, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(variableName))
                return false;

            return await Task.Run(() =>
            {
                var declaration = GetVariableDeclarationAsync(variableName, scope, cancellationToken).Result;
                return declaration != null;
            }, cancellationToken);
        }

        /// <summary>
        /// 清空所有变量声明缓存
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>清空任务</returns>
        public async Task ClearAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            await Task.Run(() =>
            {
                _rwLock.EnterWriteLock();
                try
                {
                    _fileDeclarations.Clear();
                    _variableIndex.Clear();
                    _scopeIndex.Clear();
                    Interlocked.Increment(ref _clearOperations);
                }
                finally
                {
                    _rwLock.ExitWriteLock();
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 清理过期的变量声明缓存
        /// </summary>
        /// <param name="maxAge">最大缓存时间</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>清理的项数</returns>
        public async Task<int> CleanupExpiredAsync(TimeSpan maxAge, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            return await Task.Run(() =>
            {
                var expiredFiles = new List<string>();

                _rwLock.EnterWriteLock();
                try
                {
                    foreach (var kvp in _fileDeclarations)
                    {
                        if (kvp.Value.IsExpired(maxAge))
                        {
                            expiredFiles.Add(kvp.Key);
                        }
                    }

                    foreach (var file in expiredFiles)
                    {
                        if (_fileDeclarations.TryRemove(file, out var entry))
                        {
                            RemoveFromIndices(entry.Declarations);
                        }
                    }

                    if (expiredFiles.Count > 0)
                    {
                        Interlocked.Increment(ref _cleanupOperations);
                    }

                    return expiredFiles.Count;
                }
                finally
                {
                    _rwLock.ExitWriteLock();
                }
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
                    .UpdateItemCounts(0, Count, 0)
                    .UpdateMemoryUsage(0, MemoryUsage, 0)
                    .RecordOperation(CacheOperationType.Add, _addOperations)
                    .RecordOperation(CacheOperationType.Update, _updateOperations)
                    .RecordOperation(CacheOperationType.Remove, _removeOperations)
                    .RecordOperation(CacheOperationType.Clear, _clearOperations)
                    .RecordOperation(CacheOperationType.Cleanup, _cleanupOperations);
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
                _rwLock.EnterReadLock();
                try
                {
                    // 验证索引一致性
                    foreach (var kvp in _variableIndex)
                    {
                        var variableName = kvp.Key;
                        var declarations = kvp.Value;

                        foreach (var declaration in declarations)
                        {
                            // 检查声明的基本完整性
                            if (string.IsNullOrEmpty(declaration.Name))
                                return false;

                            if (!declaration.Name.Equals(variableName, StringComparison.OrdinalIgnoreCase))
                                return false;

                            // 检查作用域索引一致性
                            var scope = string.IsNullOrEmpty(declaration.Scope) ? "global" : declaration.Scope;
                            if (!_scopeIndex.TryGetValue(scope, out var scopeVariables) ||
                                !scopeVariables.Contains(variableName))
                            {
                                return false;
                            }
                        }
                    }

                    // 验证文件缓存一致性
                    foreach (var entry in _fileDeclarations.Values)
                    {
                        foreach (var declaration in entry.Declarations)
                        {
                            if (!_variableIndex.TryGetValue(declaration.Name, out var indexedDeclarations) ||
                                !indexedDeclarations.Any(d => d.GetQualifiedName() == declaration.GetQualifiedName()))
                            {
                                return false;
                            }
                        }
                    }

                    return true;
                }
                catch
                {
                    return false;
                }
                finally
                {
                    _rwLock.ExitReadLock();
                }
            }, cancellationToken);
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
                CacheKeyStrategy.ContentHash => filePath,
                CacheKeyStrategy.CompositeHash => $"{filePath}_{filePath.GetHashCode()}",
                _ => filePath
            };
        }

        /// <summary>
        /// 添加声明到索引
        /// </summary>
        /// <param name="declarations">声明集合</param>
        private void AddToIndices(IEnumerable<VariableDeclaration> declarations)
        {
            foreach (var declaration in declarations)
            {
                // 添加到变量索引
                _variableIndex.AddOrUpdate(
                    declaration.Name,
                    new List<VariableDeclaration> { declaration },
                    (key, existing) =>
                    {
                        if (!existing.Any(d => d.GetQualifiedName() == declaration.GetQualifiedName()))
                        {
                            existing.Add(declaration);
                        }
                        return existing;
                    });

                // 添加到作用域索引
                var scope = string.IsNullOrEmpty(declaration.Scope) ? "global" : declaration.Scope;
                _scopeIndex.AddOrUpdate(
                    scope,
                    new HashSet<string> { declaration.Name },
                    (key, existing) =>
                    {
                        existing.Add(declaration.Name);
                        return existing;
                    });
            }
        }

        /// <summary>
        /// 从索引中移除声明
        /// </summary>
        /// <param name="declarations">声明集合</param>
        private void RemoveFromIndices(IEnumerable<VariableDeclaration> declarations)
        {
            foreach (var declaration in declarations)
            {
                // 从变量索引中移除
                if (_variableIndex.TryGetValue(declaration.Name, out var existingDeclarations))
                {
                    existingDeclarations.RemoveAll(d => d.GetQualifiedName() == declaration.GetQualifiedName());
                    
                    if (existingDeclarations.Count == 0)
                    {
                        _variableIndex.TryRemove(declaration.Name, out _);
                    }
                }

                // 从作用域索引中移除
                var scope = string.IsNullOrEmpty(declaration.Scope) ? "global" : declaration.Scope;
                if (_scopeIndex.TryGetValue(scope, out var scopeVariables))
                {
                    // 检查是否还有其他声明在此作用域中
                    var hasOtherDeclarations = _variableIndex.TryGetValue(declaration.Name, out var allDeclarations) &&
                                             allDeclarations.Any(d => 
                                                 (string.IsNullOrEmpty(d.Scope) ? "global" : d.Scope).Equals(scope, StringComparison.OrdinalIgnoreCase) &&
                                                 d.GetQualifiedName() != declaration.GetQualifiedName());

                    if (!hasOtherDeclarations)
                    {
                        scopeVariables.Remove(declaration.Name);
                        
                        if (scopeVariables.Count == 0)
                        {
                            _scopeIndex.TryRemove(scope, out _);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 检查名称是否匹配模式
        /// </summary>
        /// <param name="name">名称</param>
        /// <param name="pattern">模式</param>
        /// <returns>是否匹配</returns>
        private static bool IsMatchPattern(string name, string pattern)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(pattern))
                return false;

            // 简单的通配符匹配
            if (pattern.Contains('*') || pattern.Contains('?'))
            {
                var regexPattern = pattern.Replace("*", ".*").Replace("?", ".");
                return Regex.IsMatch(name, $"^{regexPattern}$", RegexOptions.IgnoreCase);
            }

            // 部分匹配
            return name.Contains(pattern, StringComparison.OrdinalIgnoreCase);
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
                    EvictLeastRecentlyUsedFiles(0.1); // 淘汰10%的文件
                }

                // 检查项数限制
                if (_options.MaxCacheSize > 0 && Count > _options.MaxCacheSize)
                {
                    var excessCount = Count - _options.MaxCacheSize;
                    var filesToEvict = (int)Math.Ceiling((double)excessCount / 10); // 估算需要淘汰的文件数
                    EvictLeastRecentlyUsedFiles(filesToEvict);
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 淘汰最少使用的文件
        /// </summary>
        /// <param name="countOrRatio">要淘汰的文件数或比例</param>
        private void EvictLeastRecentlyUsedFiles(double countOrRatio)
        {
            var fileCount = _fileDeclarations.Count;
            if (fileCount == 0)
                return;

            int filesToEvict;
            if (countOrRatio >= 1.0)
            {
                filesToEvict = (int)countOrRatio;
            }
            else
            {
                filesToEvict = (int)(fileCount * countOrRatio);
            }

            if (filesToEvict <= 0)
                return;

            _rwLock.EnterWriteLock();
            try
            {
                // 获取最少使用的文件
                var leastUsedFiles = _fileDeclarations
                    .OrderBy(kvp => kvp.Value.LastAccessTime)
                    .Take(filesToEvict)
                    .Select(kvp => kvp.Key)
                    .ToList();

                // 移除这些文件的缓存
                foreach (var file in leastUsedFiles)
                {
                    if (_fileDeclarations.TryRemove(file, out var entry))
                    {
                        RemoveFromIndices(entry.Declarations);
                    }
                }
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// 检查是否已释放资源
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(VariableDeclarationCache));
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

            _rwLock.EnterWriteLock();
            try
            {
                _fileDeclarations.Clear();
                _variableIndex.Clear();
                _scopeIndex.Clear();
            }
            finally
            {
                _rwLock.ExitWriteLock();
                _rwLock.Dispose();
            }
        }
        #endregion

        #region 内部类型
        /// <summary>
        /// 文件声明缓存条目
        /// </summary>
        private sealed class CacheEntry
        {
            public List<VariableDeclaration> Declarations { get; }
            public DateTime CreatedTime { get; }
            public DateTime LastAccessTime { get; private set; }

            public CacheEntry(List<VariableDeclaration> declarations, DateTime createdTime)
            {
                Declarations = declarations;
                CreatedTime = createdTime;
                LastAccessTime = createdTime;
            }

            public bool IsExpired(TimeSpan maxAge)
            {
                return DateTime.UtcNow - CreatedTime > maxAge;
            }

            public void UpdateAccess()
            {
                LastAccessTime = DateTime.UtcNow;
            }

            public long EstimateSize()
            {
                return Declarations.Sum(d => EstimateDeclarationSize(d)) + 128; // 加上Entry本身的开销
            }

            private static long EstimateDeclarationSize(VariableDeclaration declaration)
            {
                long size = 0;
                size += (declaration.Name?.Length ?? 0) * 2;
                size += (declaration.DeclarationFilePath?.Length ?? 0) * 2;
                size += (declaration.Scope?.Length ?? 0) * 2;
                size += (declaration.Description?.Length ?? 0) * 2;
                size += declaration.Tags.Sum(t => t.Length * 2);
                size += declaration.ExtendedProperties.Count * 64;
                size += declaration.UsageStats.UsageRecords.Count * 64;
                return size + 256; // 基础对象开销
            }
        }
        #endregion
    }
}