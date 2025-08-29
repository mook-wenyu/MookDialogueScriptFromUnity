using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MookDialogueScript.Pooling
{
    /// <summary>
    /// 全局池管理器
    /// 统一管理所有类型的对象池，合并原LexerPoolManager功能
    /// 采用单例模式，提供全局访问点
    /// </summary>
    public sealed class GlobalPoolManager : IPoolManager
    {
        #region 单例模式
        private static readonly Lazy<GlobalPoolManager> _instance = new(() => new GlobalPoolManager());

        /// <summary>
        /// 获取全局池管理器实例
        /// </summary>
        public static GlobalPoolManager Instance => _instance.Value;

        private GlobalPoolManager()
        {
            _pools = new ConcurrentDictionary<string, IDisposable>();
        }
        #endregion

        #region 字段
        private readonly ConcurrentDictionary<string, IDisposable> _pools;
        private volatile bool _disposed;
        #endregion

        #region 属性
        /// <summary>
        /// 池数量
        /// </summary>
        public int PoolCount => _pools.Count;
        #endregion

        #region IPoolManager 实现
        /// <summary>
        /// 获取或创建指定类型的池
        /// </summary>
        public IObjectPool<T> GetOrCreatePool<T>(string name = null, PoolOptions options = null) 
            where T : class, new()
        {
            return GetOrCreatePool<T>(name, () => new T(), null, options);
        }

        /// <summary>
        /// 获取或创建指定类型的池（带工厂方法）
        /// </summary>
        public IObjectPool<T> GetOrCreatePool<T>(
            string name, 
            Func<T> factory, 
            Action<T> resetAction = null, 
            PoolOptions options = null) where T : class
        {
            ThrowIfDisposed();

            var poolKey = CreatePoolKey<T>(name);
            
            if (_pools.TryGetValue(poolKey, out var existingPool))
            {
                return (IObjectPool<T>)existingPool;
            }

            // 创建新池
            var newPool = new UniversalObjectPool<T>(
                factory ?? (() => Activator.CreateInstance<T>()),
                resetAction,
                options ?? PoolOptions.Default
            );

            // 尝试添加到字典
            var addedPool = (IObjectPool<T>)_pools.GetOrAdd(poolKey, newPool);
            
            // 如果添加的不是我们创建的池，说明有竞争条件，需要释放我们创建的池
            if (addedPool != newPool)
            {
                newPool.Dispose();
            }

            return addedPool;
        }

        /// <summary>
        /// 获取指定类型的池
        /// </summary>
        public IObjectPool<T> GetPool<T>(string name = null) where T : class
        {
            ThrowIfDisposed();

            var poolKey = CreatePoolKey<T>(name);
            
            if (_pools.TryGetValue(poolKey, out var pool))
            {
                return (IObjectPool<T>)pool;
            }

            return null;
        }

        /// <summary>
        /// 移除指定池
        /// </summary>
        public bool RemovePool<T>(string name = null) where T : class
        {
            ThrowIfDisposed();

            var poolKey = CreatePoolKey<T>(name);
            
            if (_pools.TryRemove(poolKey, out var pool))
            {
                if (pool is IDisposable disposablePool)
                {
                    disposablePool.Dispose();
                }
                
                return true;
            }

            return false;
        }

        /// <summary>
        /// 获取所有池的统计信息
        /// 直接从各个池获取最新统计，避免数据重复
        /// </summary>
        public Dictionary<string, PoolStatistics> GetAllStatistics()
        {
            ThrowIfDisposed();

            var statistics = new Dictionary<string, PoolStatistics>();

            foreach (var kvp in _pools)
            {
                if (kvp.Value is IObjectPool<object> pool)
                {
                    try
                    {
                        var stats = pool.GetStatistics();
                        statistics[kvp.Key] = stats;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"获取池 {kvp.Key} 统计信息失败: {ex.Message}");
                    }
                }
            }

            return statistics;
        }

        /// <summary>
        /// 调整所有池的大小
        /// </summary>
        public void TrimAll()
        {
            ThrowIfDisposed();

            foreach (var pool in _pools.Values)
            {
                if (pool is IObjectPool<object> objectPool)
                {
                    try
                    {
                        objectPool.Trim();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"池调整失败: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 清空所有池
        /// </summary>
        public void ClearAll()
        {
            ThrowIfDisposed();

            foreach (var pool in _pools.Values)
            {
                if (pool is IObjectPool<object> objectPool)
                {
                    try
                    {
                        objectPool.Clear();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"池清空失败: {ex.Message}");
                    }
                }
            }
        }
        #endregion

        #region 便捷方法
        /// <summary>
        /// 直接从默认池租借对象
        /// </summary>
        public T Rent<T>() where T : class, new()
        {
            var pool = GetOrCreatePool<T>();
            return pool.Rent();
        }

        /// <summary>
        /// 直接归还对象到默认池
        /// </summary>
        public void Return<T>(T item) where T : class, new()
        {
            var pool = GetPool<T>();
            pool?.Return(item);
        }

        /// <summary>
        /// 创建作用域对象（使用新的通用包装器）
        /// </summary>
        public ScopedPoolable<T> RentScopedPoolable<T>() where T : class, new()
        {
            var pool = GetOrCreatePool<T>();
            var scopeHandler = pool.RentScoped(out var item);
            return new ScopedPoolable<T>(item, scopeHandler);
        }

        /// <summary>
        /// 批量处理 - 通用版本
        /// </summary>
        public TResult[] ProcessBatch<TItem, TResult>(
            TItem[] items,
            Func<TItem, TResult> processor) where TItem : class, new()
        {
            if (items == null || processor == null)
                throw new ArgumentNullException();

            var results = new TResult[items.Length];
            var pool = GetOrCreatePool<TItem>();

            var pooledItems = pool.RentBatch(items.Length);
            try
            {
                for (int i = 0; i < items.Length; i++)
                {
                    // 这里可以根据需要实现更复杂的逻辑
                    results[i] = processor(items[i]);
                }
            }
            finally
            {
                pool.ReturnBatch(pooledItems);
            }

            return results;
        }
        #endregion

        #region 监控和诊断
        /// <summary>
        /// 获取性能报告
        /// </summary>
        public string GetPerformanceReport()
        {
            ThrowIfDisposed();

            var statistics = GetAllStatistics();
            var report = new System.Text.StringBuilder();

            report.AppendLine("=== 全局对象池性能报告 ===");
            report.AppendLine($"池总数: {PoolCount}");
            report.AppendLine($"生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine();

            if (statistics.Count > 0)
            {
                var totalActive = statistics.Values.Sum(s => s.ActiveCount);
                var totalAvailable = statistics.Values.Sum(s => s.PoolSize);
                var avgHitRate = statistics.Values.Average(s => s.HitRate);

                report.AppendLine($"总体统计:");
                report.AppendLine($"  活跃对象: {totalActive}");
                report.AppendLine($"  可用对象: {totalAvailable}");
                report.AppendLine($"  平均命中率: {avgHitRate:P2}");
                report.AppendLine();

                report.AppendLine("各池详细信息:");
                foreach (var kvp in statistics.OrderByDescending(s => s.Value.TotalBorrowed))
                {
                    report.AppendLine($"  {kvp.Key}: {kvp.Value}");
                }
            }
            else
            {
                report.AppendLine("暂无池统计信息");
            }

            report.AppendLine("=============================");

            return report.ToString();
        }

        /// <summary>
        /// 输出性能报告到控制台
        /// </summary>
        public void PrintPerformanceReport()
        {
            Debug.Log(GetPerformanceReport());
        }

        /// <summary>
        /// 获取指定类型的池信息
        /// </summary>
        public string GetPoolInfo<T>(string name = null) where T : class
        {
            var pool = GetPool<T>(name);
            if (pool == null)
            {
                return $"池 {CreatePoolKey<T>(name)} 不存在";
            }

            var stats = pool.GetStatistics();
            return $"池 {CreatePoolKey<T>(name)}: {stats}";
        }
        #endregion

        #region 私有辅助方法
        private string CreatePoolKey<T>(string name) where T : class
        {
            var typeName = typeof(T).Name;
            return string.IsNullOrEmpty(name) ? typeName : $"{typeName}:{name}";
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(GlobalPoolManager));
            }
        }
        #endregion

        #region IDisposable 实现
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 释放所有池
                    foreach (var pool in _pools.Values)
                    {
                        if (pool is IDisposable disposablePool)
                        {
                            try
                            {
                                disposablePool.Dispose();
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"释放池时发生错误: {ex.Message}");
                            }
                        }
                    }

                    _pools.Clear();
                }

                _disposed = true;
            }
        }
        #endregion
    }
}