namespace MookDialogueScript.Pooling
{
    /// <summary>
    /// 池配置选项
    /// 提供对象池的配置参数
    /// </summary>
    public class PoolOptions
    {
        /// <summary>
        /// 初始池大小
        /// </summary>
        public int InitialSize { get; set; } = 16;
        
        /// <summary>
        /// 最大池大小
        /// </summary>
        public int MaxSize { get; set; } = 256;
        
        /// <summary>
        /// 是否启用线程本地缓存
        /// </summary>
        public bool EnableThreadLocalCache { get; set; } = true;
        
        /// <summary>
        /// 线程本地缓存大小
        /// </summary>
        public int ThreadLocalCacheSize { get; set; } = 8;
        
        /// <summary>
        /// 是否启用自动调整
        /// </summary>
        public bool EnableAutoTrim { get; set; } = true;
        
        /// <summary>
        /// 自动调整间隔（毫秒）
        /// </summary>
        public int TrimInterval { get; set; } = 120000;
        
        /// <summary>
        /// 目标池大小比率
        /// </summary>
        public float TargetPoolSizeRatio { get; set; } = 1.2f;
        
        /// <summary>
        /// 是否预热池
        /// </summary>
        public bool PrewarmPool { get; set; } = true;
        
        /// <summary>
        /// 默认配置
        /// </summary>
        public static PoolOptions Default => new();
        
        /// <summary>
        /// 高性能配置
        /// </summary>
        public static PoolOptions HighPerformance => new()
        {
            InitialSize = 32,
            MaxSize = 512,
            ThreadLocalCacheSize = 16,
            TrimInterval = 180000,
            TargetPoolSizeRatio = 1.5f
        };
        
        /// <summary>
        /// 内存优化配置
        /// </summary>
        public static PoolOptions MemoryOptimized => new()
        {
            InitialSize = 8,
            MaxSize = 64,
            ThreadLocalCacheSize = 4,
            TrimInterval = 60000,
            TargetPoolSizeRatio = 1.1f
        };
    }
}