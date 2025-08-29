using System;
using System.Collections.Generic;

namespace MookDialogueScript.Incremental.Interfaces
{
    /// <summary>
    /// 增量缓存接口
    /// 提供缓存管理和增量更新能力
    /// </summary>
    public interface IIncrementalCache : IDisposable
    {
        /// <summary>
        /// 获取缓存项
        /// </summary>
        /// <typeparam name="T">缓存项类型</typeparam>
        /// <param name="key">缓存键</param>
        /// <returns>缓存项，如果不存在则返回默认值</returns>
        T Get<T>(string key);

        /// <summary>
        /// 设置缓存项
        /// </summary>
        /// <typeparam name="T">缓存项类型</typeparam>
        /// <param name="key">缓存键</param>
        /// <param name="value">缓存值</param>
        /// <param name="expiry">过期时间（可选）</param>
        void Set<T>(string key, T value, TimeSpan? expiry = null);

        /// <summary>
        /// 移除缓存项
        /// </summary>
        /// <param name="key">缓存键</param>
        /// <returns>如果成功移除则返回true</returns>
        bool Remove(string key);

        /// <summary>
        /// 清空所有缓存
        /// </summary>
        void Clear();

        /// <summary>
        /// 检查是否存在指定的缓存项
        /// </summary>
        /// <param name="key">缓存键</param>
        /// <returns>如果存在则返回true</returns>
        bool Contains(string key);

        /// <summary>
        /// 获取所有缓存键
        /// </summary>
        /// <returns>缓存键集合</returns>
        IEnumerable<string> GetKeys();

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        /// <returns>缓存统计</returns>
        CacheStatistics GetStatistics();

        /// <summary>
        /// 标记缓存项为脏数据（需要重新计算）
        /// </summary>
        /// <param name="key">缓存键</param>
        void MarkDirty(string key);

        /// <summary>
        /// 批量标记缓存项为脏数据
        /// </summary>
        /// <param name="keys">缓存键集合</param>
        void MarkDirtyBatch(IEnumerable<string> keys);
    }

    /// <summary>
    /// 泛型增量缓存接口
    /// 提供类型安全的缓存操作
    /// </summary>
    /// <typeparam name="TKey">键类型</typeparam>
    /// <typeparam name="TValue">值类型</typeparam>
    public interface IIncrementalCache<TKey, TValue> : IDisposable
    {
        /// <summary>
        /// 获取缓存项
        /// </summary>
        /// <param name="key">缓存键</param>
        /// <returns>缓存项，如果不存在则返回默认值</returns>
        TValue Get(TKey key);

        /// <summary>
        /// 尝试获取缓存项
        /// </summary>
        /// <param name="key">缓存键</param>
        /// <param name="value">输出的缓存值</param>
        /// <returns>如果成功获取则返回true</returns>
        bool TryGet(TKey key, out TValue value);

        /// <summary>
        /// 设置缓存项
        /// </summary>
        /// <param name="key">缓存键</param>
        /// <param name="value">缓存值</param>
        /// <param name="expiry">过期时间（可选）</param>
        void Set(TKey key, TValue value, TimeSpan? expiry = null);

        /// <summary>
        /// 获取或创建缓存项
        /// </summary>
        /// <param name="key">缓存键</param>
        /// <param name="valueFactory">值工厂函数</param>
        /// <param name="expiry">过期时间（可选）</param>
        /// <returns>缓存值</returns>
        TValue GetOrCreate(TKey key, Func<TKey, TValue> valueFactory, TimeSpan? expiry = null);

        /// <summary>
        /// 移除缓存项
        /// </summary>
        /// <param name="key">缓存键</param>
        /// <returns>如果成功移除则返回true</returns>
        bool Remove(TKey key);

        /// <summary>
        /// 清空所有缓存
        /// </summary>
        void Clear();

        /// <summary>
        /// 检查是否存在指定的缓存项
        /// </summary>
        /// <param name="key">缓存键</param>
        /// <returns>如果存在则返回true</returns>
        bool Contains(TKey key);

        /// <summary>
        /// 获取所有缓存键
        /// </summary>
        /// <returns>缓存键集合</returns>
        IEnumerable<TKey> GetKeys();

        /// <summary>
        /// 获取所有缓存值
        /// </summary>
        /// <returns>缓存值集合</returns>
        IEnumerable<TValue> GetValues();

        /// <summary>
        /// 获取所有缓存项
        /// </summary>
        /// <returns>键值对集合</returns>
        IEnumerable<KeyValuePair<TKey, TValue>> GetItems();

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        /// <returns>缓存统计</returns>
        CacheStatistics GetStatistics();

        /// <summary>
        /// 标记缓存项为脏数据（需要重新计算）
        /// </summary>
        /// <param name="key">缓存键</param>
        void MarkDirty(TKey key);

        /// <summary>
        /// 批量标记缓存项为脏数据
        /// </summary>
        /// <param name="keys">缓存键集合</param>
        void MarkDirtyBatch(IEnumerable<TKey> keys);

        /// <summary>
        /// 检查缓存项是否为脏数据
        /// </summary>
        /// <param name="key">缓存键</param>
        /// <returns>如果为脏数据则返回true</returns>
        bool IsDirty(TKey key);

        /// <summary>
        /// 刷新指定的缓存项
        /// </summary>
        /// <param name="key">缓存键</param>
        /// <param name="valueFactory">值工厂函数</param>
        /// <returns>刷新后的值</returns>
        TValue Refresh(TKey key, Func<TKey, TValue> valueFactory);

        /// <summary>
        /// 批量刷新缓存项
        /// </summary>
        /// <param name="keys">要刷新的键集合</param>
        /// <param name="valueFactory">值工厂函数</param>
        void RefreshBatch(IEnumerable<TKey> keys, Func<TKey, TValue> valueFactory);
    }

    /// <summary>
    /// 缓存统计信息
    /// </summary>
    public struct CacheStatistics
    {
        /// <summary>
        /// 缓存项总数
        /// </summary>
        public int TotalItems { get; set; }

        /// <summary>
        /// 缓存命中次数
        /// </summary>
        public long Hits { get; set; }

        /// <summary>
        /// 缓存未命中次数
        /// </summary>
        public long Misses { get; set; }

        /// <summary>
        /// 缓存命中率
        /// </summary>
        public float HitRate => Hits + Misses > 0 ? (float)Hits / (Hits + Misses) : 0f;

        /// <summary>
        /// 内存使用量（字节）
        /// </summary>
        public long MemoryUsage { get; set; }

        /// <summary>
        /// 过期清理次数
        /// </summary>
        public long EvictionCount { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// 最后访问时间
        /// </summary>
        public DateTime LastAccessTime { get; set; }

        /// <summary>
        /// 脏数据项数量
        /// </summary>
        public int DirtyItems { get; set; }

        /// <summary>
        /// 刷新次数
        /// </summary>
        public long RefreshCount { get; set; }

        public override string ToString()
        {
            return $"Cache: Items={TotalItems}, HitRate={HitRate:P2}, " +
                   $"Memory={MemoryUsage}B, Dirty={DirtyItems}, Refreshes={RefreshCount}";
        }
    }

    /// <summary>
    /// 增量缓存统计信息
    /// </summary>
    public struct IncrementalCacheStatistics
    {
        /// <summary>
        /// 基础缓存统计
        /// </summary>
        public CacheStatistics BaseStatistics { get; set; }

        /// <summary>
        /// 当前缓存项数量
        /// </summary>
        public int CurrentItems { get; set; }

        /// <summary>
        /// 缓存命中次数
        /// </summary>
        public long Hits { get; set; }

        /// <summary>
        /// 缓存未命中次数
        /// </summary>
        public long Misses { get; set; }

        /// <summary>
        /// 清理操作次数
        /// </summary>
        public long Cleanups { get; set; }

        /// <summary>
        /// 刷新操作次数
        /// </summary>
        public long Refreshes { get; set; }

        /// <summary>
        /// 增量更新次数
        /// </summary>
        public long IncrementalUpdateCount { get; set; }

        /// <summary>
        /// 批量更新次数
        /// </summary>
        public long BatchUpdateCount { get; set; }

        /// <summary>
        /// 失效次数
        /// </summary>
        public long InvalidationCount { get; set; }

        /// <summary>
        /// 级联更新次数
        /// </summary>
        public long CascadeUpdateCount { get; set; }

        /// <summary>
        /// 平均更新时间（毫秒）
        /// </summary>
        public double AverageUpdateTime { get; set; }

        /// <summary>
        /// 缓存效率（命中率 × 更新频率的倒数）
        /// </summary>
        public double CacheEfficiency { get; set; }

        /// <summary>
        /// 缓存命中率（便利属性）
        /// </summary>
        public float HitRate => BaseStatistics.HitRate;

        public override string ToString()
        {
            return $"IncrementalCache: {BaseStatistics}, " +
                   $"Incremental={IncrementalUpdateCount}, Batch={BatchUpdateCount}, " +
                   $"Invalidations={InvalidationCount}, Efficiency={CacheEfficiency:F2}";
        }
    }
}