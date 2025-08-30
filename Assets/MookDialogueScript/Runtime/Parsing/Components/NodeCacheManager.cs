using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace MookDialogueScript.Parsing
{
    /// <summary>
    /// AST节点缓存管理器
    /// 专门负责AST节点的缓存和性能优化
    /// 线程安全设计，支持高并发场景
    /// </summary>
    public class NodeCacheManager : IDisposable
    {
        #region 缓存字段
        // 各类型节点缓存
        private readonly ConcurrentDictionary<string, NumberNode> _numberCache = new();
        private readonly ConcurrentDictionary<string, BooleanNode> _booleanCache = new();
        private readonly ConcurrentDictionary<string, VariableNode> _variableCache = new();
        private readonly ConcurrentDictionary<string, IdentifierNode> _identifierCache = new();
        private readonly ConcurrentDictionary<int, ExpressionNode> _expressionCache = new();

        // 性能统计（原子操作）
        private long _expressionCacheHits;
        private long _expressionCacheMisses;
        private long _nodeCacheHits;
        private long _nodeCacheMisses;

        // 清理定时器
        private readonly Timer _trimTimer;
        private volatile bool _disposed;
        #endregion

        #region NodeCache 实现
        /// <summary>
        /// 获取或创建数值节点
        /// </summary>
        public NumberNode GetOrCreateNumberNode(double value, int line, int column)
        {
            var key = CreateNodeKey(value.ToString(CultureInfo.CurrentCulture), line, column);

            if (_numberCache.TryGetValue(key, out var cached))
            {
                Interlocked.Increment(ref _nodeCacheHits);
                return cached;
            }

            Interlocked.Increment(ref _nodeCacheMisses);

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
        #endregion

        #region 私有辅助方法
        /// <summary>
        /// 创建节点缓存键
        /// </summary>
        private string CreateNodeKey(string value, int line, int column)
        {
            return $"{value}:{line}:{column}";
        }

        /// <summary>
        /// 计算命中率
        /// </summary>
        private double CalculateHitRate(long hits, long misses)
        {
            var total = hits + misses;
            return total == 0 ? 0.0 : (double)hits / total * 100.0;
        }
        #endregion

        #region IDisposable 实现
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing)
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
}
