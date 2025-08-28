using System;
using System.Collections.Generic;
using UnityEngine;

namespace MookDialogueScript.Lexers
{
    /// <summary>
    /// 全局Lexer池管理器 - 单例模式
    /// 提供整个应用的统一池访问点
    /// 支持多种池策略和性能监控
    /// </summary>
    public sealed class LexerPoolManager
    {
        #region 单例模式
        private static readonly Lazy<LexerPoolManager> _instance = new(() => new LexerPoolManager());

        /// <summary>
        /// 获取全局池管理器实例
        /// </summary>
        public static LexerPoolManager Instance => _instance.Value;

        private LexerPoolManager()
        {
            InitializePools();
        }
        #endregion

        #region 池实例
        private ConcurrentLexerPool _concurrentPool;
        private readonly Dictionary<string, object> _namedPools = new();
        #endregion

        #region 初始化
        private void InitializePools()
        {
            _concurrentPool = new ConcurrentLexerPool();
        }
        #endregion

        #region 公共API
        /// <summary>
        /// 从默认池租借Lexer
        /// </summary>
        public Lexer Rent()
        {
            return _concurrentPool?.Rent();
        }

        /// <summary>
        /// 归还Lexer到默认池
        /// </summary>
        public void Return(Lexer lexer)
        {
            _concurrentPool?.Return(lexer);
        }

        /// <summary>
        /// 租借一个自动归还的Lexer
        /// </summary>
        public IDisposable RentScoped(string source = null)
        {
            return _concurrentPool?.RentScoped(source);
        }

        /// <summary>
        /// 处理源代码并返回Token列表
        /// 自动管理Lexer的租借和归还
        /// </summary>
        public List<Token> Tokenize(string source)
        {
            using var scoped = RentScoped(source);
            var lexer = (Lexer)(PooledConcurrentLexer)scoped;

            return lexer?.Tokenize();
        }

        /// <summary>
        /// 批量处理多个源文件
        /// </summary>
        public List<List<Token>> TokenizeBatch(string[] sources)
        {
            var results = new List<List<Token>>(sources.Length);

            // 并发池支持批处理优化
            if (_concurrentPool != null)
            {
                var lexers = _concurrentPool.RentBatch(sources.Length);
                try
                {
                    for (int i = 0; i < sources.Length; i++)
                    {
                        lexers[i].Reset(sources[i]);
                        results.Add(lexers[i].Tokenize());
                    }
                }
                finally
                {
                    _concurrentPool.ReturnBatch(lexers);
                }
            }
            else
            {
                // 标准池逐个处理
                foreach (var source in sources)
                {
                    results.Add(Tokenize(source));
                }
            }

            return results;
        }
        #endregion

        #region 池管理
        /// <summary>
        /// 获取或创建命名池
        /// </summary>
        public T GetOrCreatePool<T>(string name, Func<T> factory) where T : class
        {
            lock (_namedPools)
            {
                if (_namedPools.TryGetValue(name, out var pool))
                {
                    return pool as T;
                }

                var newPool = factory();
                _namedPools[name] = newPool;
                return newPool;
            }
        }

        /// <summary>
        /// 创建一个新的并发池
        /// </summary>
        public ConcurrentLexerPool CreateConcurrentPool(ConcurrentLexerPoolOptions options = null)
        {
            return new ConcurrentLexerPool(options ?? ConcurrentLexerPoolOptions.Default);
        }

        /// <summary>
        /// 调整所有池的大小
        /// </summary>
        public void TrimAll()
        {
            _concurrentPool?.Trim();

            lock (_namedPools)
            {
                foreach (var pool in _namedPools.Values)
                {
                    if (pool is ConcurrentLexerPool concurrentPool)
                        concurrentPool.Trim();
                }
            }
        }

        /// <summary>
        /// 清空所有池
        /// </summary>
        public void ClearAll()
        {
            _concurrentPool?.Clear();

            lock (_namedPools)
            {
                foreach (var pool in _namedPools.Values)
                {
                    if (pool is ConcurrentLexerPool concurrentPool)
                        concurrentPool.Clear();
                }
            }
        }
        #endregion

        #region 统计和监控
        /// <summary>
        /// 获取池统计信息
        /// </summary>
        public PoolStatisticsSummary GetStatistics()
        {
            var summary = new PoolStatisticsSummary();

            if (_concurrentPool != null)
            {
                var stats = _concurrentPool.GetStatistics();
                summary.ConcurrentPoolStats = stats;
                summary.TotalActive += stats.ActiveCount;
                summary.TotalAvailable += stats.PoolSize;
            }

            lock (_namedPools)
            {
                summary.NamedPoolCount = _namedPools.Count;
            }

            return summary;
        }

        /// <summary>
        /// 输出性能报告到控制台
        /// </summary>
        public void PrintPerformanceReport()
        {
            var stats = GetStatistics();

            Debug.Log("=== Lexer Pool Performance Report ===");
            Debug.Log($"总活跃对象: {stats.TotalActive}");
            Debug.Log($"总可用对象: {stats.TotalAvailable}");
            Debug.Log($"命名池数量: {stats.NamedPoolCount}");

            if (stats.ConcurrentPoolStats.HasValue)
            {
                var s = stats.ConcurrentPoolStats.Value;
                Debug.Log($"并发池: {s}");
            }

            Debug.Log("=====================================");
        }
        #endregion

        #region 资源管理
        /// <summary>
        /// 释放所有池资源
        /// </summary>
        public void Dispose()
        {
            _concurrentPool?.Dispose();

            lock (_namedPools)
            {
                foreach (var pool in _namedPools.Values)
                {
                    if (pool is IDisposable disposable)
                        disposable.Dispose();
                }
                _namedPools.Clear();
            }
        }
        #endregion
    }

    /// <summary>
    /// 池统计摘要
    /// </summary>
    public struct PoolStatisticsSummary
    {
        public int TotalActive { get; set; }
        public int TotalAvailable { get; set; }
        public int NamedPoolCount { get; set; }
        public ConcurrentLexerPoolStatistics? ConcurrentPoolStats { get; set; }
    }

    /// <summary>
    /// Lexer池扩展方法
    /// </summary>
    public static class LexerPoolExtensions
    {
        /// <summary>
        /// 使用池化的Lexer处理源代码
        /// </summary>
        public static List<Token> TokenizeWithPool(this string source)
        {
            return LexerPoolManager.Instance.Tokenize(source);
        }

        /// <summary>
        /// 批量处理源代码
        /// </summary>
        public static List<List<Token>> TokenizeBatchWithPool(this string[] sources)
        {
            return LexerPoolManager.Instance.TokenizeBatch(sources);
        }

        /// <summary>
        /// 创建一个性能优化的Lexer实例
        /// </summary>
        public static Lexer CreatePooledLexer()
        {
            return LexerPoolManager.Instance.Rent();
        }

        /// <summary>
        /// 归还池化的Lexer
        /// </summary>
        public static void ReturnToPool(this Lexer lexer)
        {
            LexerPoolManager.Instance.Return(lexer);
        }
    }
}
