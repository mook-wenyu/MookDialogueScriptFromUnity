using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace MookDialogueScript.Parsing
{
    /// <summary>
    /// AST节点缓存管理器
    /// 专门负责AST节点的缓存和性能优化
    /// 线程安全设计，支持高并发场景
    /// </summary>
    public class NodeCacheManager : INodeCache, IDisposable
    {
        #region 缓存字段
        // 各类型节点缓存
        private readonly ConcurrentDictionary<string, NumberNode> _numberCache;
        private readonly ConcurrentDictionary<string, BooleanNode> _booleanCache;
        private readonly ConcurrentDictionary<string, VariableNode> _variableCache;
        private readonly ConcurrentDictionary<string, IdentifierNode> _identifierCache;
        private readonly ConcurrentDictionary<int, ExpressionNode> _expressionCache;
        
        // 缓存配置
        private readonly CacheOptions _options;
        
        // 性能统计（原子操作）
        private long _expressionCacheHits = 0;
        private long _expressionCacheMisses = 0;
        private long _nodeCacheHits = 0;
        private long _nodeCacheMisses = 0;
        
        // 清理定时器
        private readonly Timer _trimTimer;
        private volatile bool _disposed;
        #endregion

        #region 构造函数
        /// <summary>
        /// 使用默认配置创建缓存管理器
        /// </summary>
        public NodeCacheManager() : this(CacheOptions.Default)
        {
        }

        /// <summary>
        /// 使用指定配置创建缓存管理器
        /// </summary>
        public NodeCacheManager(CacheOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            
            _numberCache = new ConcurrentDictionary<string, NumberNode>();
            _booleanCache = new ConcurrentDictionary<string, BooleanNode>();
            _variableCache = new ConcurrentDictionary<string, VariableNode>();
            _identifierCache = new ConcurrentDictionary<string, IdentifierNode>();
            _expressionCache = new ConcurrentDictionary<int, ExpressionNode>();
            
            // 启动自动清理定时器
            if (_options.EnableAutoTrim)
            {
                _trimTimer = new Timer(
                    TrimCallback,
                    null,
                    _options.TrimInterval,
                    _options.TrimInterval
                );
            }
        }
        #endregion

        #region INodeCache 实现
        /// <summary>
        /// 获取或创建数值节点
        /// </summary>
        public NumberNode GetOrCreateNumberNode(double value, int line, int column)
        {
            var key = CreateNodeKey(value.ToString(), line, column);
            
            if (_numberCache.TryGetValue(key, out var cached))
            {
                Interlocked.Increment(ref _nodeCacheHits);
                return cached;
            }
            
            Interlocked.Increment(ref _nodeCacheMisses);
            
            // 检查缓存大小限制
            if (_numberCache.Count >= _options.MaxNodeCacheSize)
            {
                EvictNodeCache(_numberCache);
            }
            
            var node = new NumberNode(value, line, column);
            _numberCache.TryAdd(key, node);
            return node;
        }

        /// <summary>
        /// 获取或创建布尔节点
        /// </summary>
        public BooleanNode GetOrCreateBooleanNode(bool value, int line, int column)
        {
            var key = CreateNodeKey(value.ToString(), line, column);
            
            if (_booleanCache.TryGetValue(key, out var cached))
            {
                Interlocked.Increment(ref _nodeCacheHits);
                return cached;
            }
            
            Interlocked.Increment(ref _nodeCacheMisses);
            
            if (_booleanCache.Count >= _options.MaxNodeCacheSize)
            {
                EvictNodeCache(_booleanCache);
            }
            
            var node = new BooleanNode(value, line, column);
            _booleanCache.TryAdd(key, node);
            return node;
        }

        /// <summary>
        /// 获取或创建变量节点
        /// </summary>
        public VariableNode GetOrCreateVariableNode(string name, int line, int column)
        {
            var key = CreateNodeKey(name, line, column);
            
            if (_variableCache.TryGetValue(key, out var cached))
            {
                Interlocked.Increment(ref _nodeCacheHits);
                return cached;
            }
            
            Interlocked.Increment(ref _nodeCacheMisses);
            
            if (_variableCache.Count >= _options.MaxNodeCacheSize)
            {
                EvictNodeCache(_variableCache);
            }
            
            var node = new VariableNode(name, line, column);
            _variableCache.TryAdd(key, node);
            return node;
        }

        /// <summary>
        /// 获取或创建标识符节点
        /// </summary>
        public IdentifierNode GetOrCreateIdentifierNode(string name, int line, int column)
        {
            var key = CreateNodeKey(name, line, column);
            
            if (_identifierCache.TryGetValue(key, out var cached))
            {
                Interlocked.Increment(ref _nodeCacheHits);
                return cached;
            }
            
            Interlocked.Increment(ref _nodeCacheMisses);
            
            if (_identifierCache.Count >= _options.MaxNodeCacheSize)
            {
                EvictNodeCache(_identifierCache);
            }
            
            var node = new IdentifierNode(name, line, column);
            _identifierCache.TryAdd(key, node);
            return node;
        }

        /// <summary>
        /// 尝试获取缓存的表达式节点
        /// </summary>
        public bool TryGetCachedExpression(int cacheKey, out ExpressionNode expression)
        {
            if (_expressionCache.TryGetValue(cacheKey, out expression))
            {
                Interlocked.Increment(ref _expressionCacheHits);
                return true;
            }
            
            Interlocked.Increment(ref _expressionCacheMisses);
            return false;
        }

        /// <summary>
        /// 缓存表达式节点
        /// </summary>
        public void CacheExpression(int cacheKey, ExpressionNode expression)
        {
            if (expression == null) return;
            
            // 检查缓存大小限制
            if (_expressionCache.Count >= _options.MaxExpressionCacheSize)
            {
                EvictExpressionCache();
            }
            
            _expressionCache.TryAdd(cacheKey, expression);
        }

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        public Dictionary<string, object> GetStatistics()
        {
            return new Dictionary<string, object>
            {
                ["ExpressionCacheSize"] = _expressionCache.Count,
                ["NumberCacheSize"] = _numberCache.Count,
                ["BooleanCacheSize"] = _booleanCache.Count,
                ["VariableCacheSize"] = _variableCache.Count,
                ["IdentifierCacheSize"] = _identifierCache.Count,
                ["ExpressionCacheHits"] = _expressionCacheHits,
                ["ExpressionCacheMisses"] = _expressionCacheMisses,
                ["NodeCacheHits"] = _nodeCacheHits,
                ["NodeCacheMisses"] = _nodeCacheMisses,
                ["ExpressionHitRate"] = CalculateHitRate(_expressionCacheHits, _expressionCacheMisses),
                ["NodeHitRate"] = CalculateHitRate(_nodeCacheHits, _nodeCacheMisses)
            };
        }

        /// <summary>
        /// 清理所有缓存
        /// </summary>
        public void Clear()
        {
            _expressionCache.Clear();
            _numberCache.Clear();
            _booleanCache.Clear();
            _variableCache.Clear();
            _identifierCache.Clear();
            
            Interlocked.Exchange(ref _expressionCacheHits, 0);
            Interlocked.Exchange(ref _expressionCacheMisses, 0);
            Interlocked.Exchange(ref _nodeCacheHits, 0);
            Interlocked.Exchange(ref _nodeCacheMisses, 0);
        }

        /// <summary>
        /// 调整缓存大小
        /// </summary>
        public void Trim()
        {
            TrimExpressionCacheIfNeeded(_expressionCache, _options.MaxExpressionCacheSize);
            TrimNodeCacheIfNeeded(_numberCache, _options.MaxNodeCacheSize);
            TrimNodeCacheIfNeeded(_booleanCache, _options.MaxNodeCacheSize);
            TrimNodeCacheIfNeeded(_variableCache, _options.MaxNodeCacheSize);
            TrimNodeCacheIfNeeded(_identifierCache, _options.MaxNodeCacheSize);
        }
        #endregion

        #region 私有辅助方法
        /// <summary>
        /// 创建节点缓存键
        /// </summary>
        private string CreateNodeKey(string value, int line, int column)
        {
            return _options.IncludeLocationInKey ? $"{value}:{line}:{column}" : value;
        }

        /// <summary>
        /// 清理节点缓存
        /// </summary>
        private void EvictNodeCache<T>(ConcurrentDictionary<string, T> cache) where T : class
        {
            if (cache.Count == 0) return;
            
            var removeCount = Math.Max(1, cache.Count / 10); // 移除10%
            var removed = 0;
            
            foreach (var key in cache.Keys)
            {
                if (cache.TryRemove(key, out _))
                {
                    removed++;
                    if (removed >= removeCount) break;
                }
            }
        }

        /// <summary>
        /// 清理表达式缓存
        /// </summary>
        private void EvictExpressionCache()
        {
            if (_expressionCache.Count == 0) return;
            
            var removeCount = Math.Max(1, _expressionCache.Count / 10);
            var removed = 0;
            
            foreach (var key in _expressionCache.Keys)
            {
                if (_expressionCache.TryRemove(key, out _))
                {
                    removed++;
                    if (removed >= removeCount) break;
                }
            }
        }

        /// <summary>
        /// 根据需要调整缓存大小
        /// </summary>
        private void TrimIfNeeded<T>(ConcurrentDictionary<T, object> cache, int maxSize) where T : notnull
        {
            if (cache.Count > maxSize * 1.2) // 超过120%时才清理
            {
                var targetSize = (int)(maxSize * 0.8); // 清理到80%
                var toRemove = cache.Count - targetSize;
                var removed = 0;
                
                foreach (var key in cache.Keys)
                {
                    if (cache.TryRemove(key, out _))
                    {
                        removed++;
                        if (removed >= toRemove) break;
                    }
                }
            }
        }

        /// <summary>
        /// 调整表达式缓存大小
        /// </summary>
        private void TrimExpressionCacheIfNeeded(ConcurrentDictionary<int, ExpressionNode> cache, int maxSize)
        {
            if (cache.Count > maxSize * 1.2)
            {
                var targetSize = (int)(maxSize * 0.8);
                var toRemove = cache.Count - targetSize;
                var removed = 0;
                
                foreach (var key in cache.Keys)
                {
                    if (cache.TryRemove(key, out _))
                    {
                        removed++;
                        if (removed >= toRemove) break;
                    }
                }
            }
        }

        /// <summary>
        /// 调整节点缓存大小
        /// </summary>
        private void TrimNodeCacheIfNeeded<T>(ConcurrentDictionary<string, T> cache, int maxSize) where T : class
        {
            if (cache.Count > maxSize * 1.2)
            {
                var targetSize = (int)(maxSize * 0.8);
                var toRemove = cache.Count - targetSize;
                var removed = 0;
                
                foreach (var key in cache.Keys)
                {
                    if (cache.TryRemove(key, out _))
                    {
                        removed++;
                        if (removed >= toRemove) break;
                    }
                }
            }
        }

        /// <summary>
        /// 计算命中率
        /// </summary>
        private double CalculateHitRate(long hits, long misses)
        {
            var total = hits + misses;
            return total == 0 ? 0.0 : (double)hits / total * 100.0;
        }

        /// <summary>
        /// 定时清理回调
        /// </summary>
        private void TrimCallback(object state)
        {
            if (!_disposed)
            {
                try
                {
                    Trim();
                }
                catch
                {
                    // 忽略清理过程中的异常
                }
            }
        }
        #endregion

        #region IDisposable 实现
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _trimTimer?.Dispose();
                    Clear();
                }
                _disposed = true;
            }
        }
        #endregion
    }

    /// <summary>
    /// 缓存配置选项
    /// </summary>
    public class CacheOptions
    {
        /// <summary>
        /// 最大表达式缓存大小
        /// </summary>
        public int MaxExpressionCacheSize { get; set; } = 1000;
        
        /// <summary>
        /// 最大节点缓存大小
        /// </summary>
        public int MaxNodeCacheSize { get; set; } = 500;
        
        /// <summary>
        /// 是否启用自动调整
        /// </summary>
        public bool EnableAutoTrim { get; set; } = true;
        
        /// <summary>
        /// 自动调整间隔（毫秒）
        /// </summary>
        public int TrimInterval { get; set; } = 300000; // 5分钟
        
        /// <summary>
        /// 缓存键是否包含位置信息
        /// </summary>
        public bool IncludeLocationInKey { get; set; } = true;
        
        /// <summary>
        /// 默认配置
        /// </summary>
        public static CacheOptions Default => new();
        
        /// <summary>
        /// 高性能配置
        /// </summary>
        public static CacheOptions HighPerformance => new()
        {
            MaxExpressionCacheSize = 2000,
            MaxNodeCacheSize = 1000,
            TrimInterval = 600000 // 10分钟
        };
        
        /// <summary>
        /// 内存优化配置
        /// </summary>
        public static CacheOptions MemoryOptimized => new()
        {
            MaxExpressionCacheSize = 200,
            MaxNodeCacheSize = 100,
            TrimInterval = 120000 // 2分钟
        };
    }
}