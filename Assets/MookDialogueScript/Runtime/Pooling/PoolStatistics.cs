using System;

namespace MookDialogueScript.Pooling
{
    /// <summary>
    /// 池统计信息结构
    /// 提供对象池的性能监控数据
    /// </summary>
    public struct PoolStatistics
    {
        /// <summary>
        /// 池类型名称
        /// </summary>
        public string TypeName { get; set; }
        
        /// <summary>
        /// 池名称
        /// </summary>
        public string PoolName { get; set; }
        
        /// <summary>
        /// 当前池大小
        /// </summary>
        public int PoolSize { get; set; }
        
        /// <summary>
        /// 活跃对象数量
        /// </summary>
        public int ActiveCount { get; set; }
        
        /// <summary>
        /// 历史最大活跃数量
        /// </summary>
        public int PeakActiveCount { get; set; }
        
        /// <summary>
        /// 总创建数量
        /// </summary>
        public int TotalCreated { get; set; }
        
        /// <summary>
        /// 总租借次数
        /// </summary>
        public int TotalBorrowed { get; set; }
        
        /// <summary>
        /// 总归还次数
        /// </summary>
        public int TotalReturned { get; set; }
        
        /// <summary>
        /// 总回收数量
        /// </summary>
        public int TotalRecycled { get; set; }
        
        /// <summary>
        /// 缓存命中率
        /// </summary>
        public float HitRate { get; set; }
        
        /// <summary>
        /// 创建时间戳
        /// </summary>
        public DateTime CreatedAt { get; set; }
        
        /// <summary>
        /// 最后访问时间
        /// </summary>
        public DateTime LastAccessAt { get; set; }
        
        /// <summary>
        /// 计算缓存命中率
        /// </summary>
        public void UpdateHitRate()
        {
            HitRate = TotalBorrowed > 0 
                ? (float)(TotalBorrowed - TotalCreated) / TotalBorrowed 
                : 0f;
        }
        
        /// <summary>
        /// 格式化统计信息
        /// </summary>
        public override string ToString()
        {
            return $"Pool[{TypeName}:{PoolName}] " +
                   $"Size={PoolSize}, Active={ActiveCount}/{PeakActiveCount}, " +
                   $"Created={TotalCreated}, Hit={HitRate:P2}";
        }
    }
}