using System;

namespace MookDialogueScript.Incremental
{
    /// <summary>
    /// 增量缓存配置选项
    /// 用于配置增量缓存管理器的行为参数
    /// </summary>
    public class IncrementalCacheOptions
    {
        /// <summary>
        /// 最大缓存项数量
        /// 默认值：5000
        /// </summary>
        public int MaxItems { get; set; } = 5000;

        /// <summary>
        /// 默认TTL（生存时间）
        /// 默认值：1小时
        /// </summary>
        public TimeSpan? DefaultTtl { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// 是否启用统计功能
        /// 默认值：true
        /// </summary>
        public bool EnableStatistics { get; set; } = true;

        /// <summary>
        /// 自动清理间隔时间
        /// 默认值：5分钟
        /// </summary>
        public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// 是否启用依赖关系追踪
        /// 默认值：true
        /// </summary>
        public bool EnableDependencyTracking { get; set; } = true;

        /// <summary>
        /// 是否启用级联失效
        /// 当依赖项失效时，自动失效相关的依赖者
        /// 默认值：true
        /// </summary>
        public bool EnableCascadeInvalidation { get; set; } = true;

        /// <summary>
        /// 最大依赖深度
        /// 用于限制级联失效的递归深度，防止循环依赖导致的无限递归
        /// 默认值：10
        /// </summary>
        public int MaxDependencyDepth { get; set; } = 10;

        /// <summary>
        /// 是否启用自动刷新
        /// 当缓存项过期时是否自动刷新
        /// 默认值：false
        /// </summary>
        public bool EnableAutoRefresh { get; set; } = false;

        /// <summary>
        /// 自动刷新间隔时间
        /// 默认值：30分钟
        /// </summary>
        public TimeSpan AutoRefreshInterval { get; set; } = TimeSpan.FromMinutes(30);

        /// <summary>
        /// 内存压力阈值（字节）
        /// 当估算内存使用超过此阈值时，触发额外的清理操作
        /// 默认值：50MB
        /// </summary>
        public long MemoryPressureThreshold { get; set; } = 50 * 1024 * 1024; // 50MB

        /// <summary>
        /// 是否启用内存监控
        /// 默认值：true
        /// </summary>
        public bool EnableMemoryMonitoring { get; set; } = true;

        /// <summary>
        /// 预热缓存大小
        /// 在缓存初始化时预分配的容量
        /// 默认值：100
        /// </summary>
        public int WarmupCacheSize { get; set; } = 100;

        /// <summary>
        /// 缓存项最小访问间隔
        /// 用于防止频繁访问相同项时的重复统计
        /// 默认值：1秒
        /// </summary>
        public TimeSpan MinAccessInterval { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// 默认配置实例
        /// </summary>
        public static IncrementalCacheOptions Default => new IncrementalCacheOptions();

        /// <summary>
        /// 高性能配置
        /// 适用于高并发、大数据量场景
        /// </summary>
        public static IncrementalCacheOptions HighPerformance => new IncrementalCacheOptions
        {
            MaxItems = 10000,
            DefaultTtl = TimeSpan.FromHours(6),
            CleanupInterval = TimeSpan.FromMinutes(10),
            EnableDependencyTracking = true,
            EnableCascadeInvalidation = true,
            MaxDependencyDepth = 15,
            MemoryPressureThreshold = 100 * 1024 * 1024, // 100MB
            WarmupCacheSize = 500,
            MinAccessInterval = TimeSpan.FromMilliseconds(500)
        };

        /// <summary>
        /// 内存优化配置
        /// 适用于内存受限的环境
        /// </summary>
        public static IncrementalCacheOptions MemoryOptimized => new IncrementalCacheOptions
        {
            MaxItems = 1000,
            DefaultTtl = TimeSpan.FromMinutes(30),
            CleanupInterval = TimeSpan.FromMinutes(2),
            EnableDependencyTracking = false,
            EnableCascadeInvalidation = false,
            MaxDependencyDepth = 5,
            MemoryPressureThreshold = 10 * 1024 * 1024, // 10MB
            WarmupCacheSize = 50,
            MinAccessInterval = TimeSpan.FromSeconds(2)
        };

        /// <summary>
        /// 开发调试配置
        /// 适用于开发和调试环境
        /// </summary>
        public static IncrementalCacheOptions Development => new IncrementalCacheOptions
        {
            MaxItems = 500,
            DefaultTtl = TimeSpan.FromMinutes(10),
            CleanupInterval = TimeSpan.FromMinutes(1),
            EnableStatistics = true,
            EnableDependencyTracking = true,
            EnableCascadeInvalidation = true,
            MaxDependencyDepth = 8,
            EnableMemoryMonitoring = true,
            WarmupCacheSize = 20,
            MinAccessInterval = TimeSpan.FromMilliseconds(100)
        };

        /// <summary>
        /// 禁用配置
        /// 最小化缓存功能，主要用于测试或禁用缓存的场景
        /// </summary>
        public static IncrementalCacheOptions Disabled => new IncrementalCacheOptions
        {
            MaxItems = 10,
            DefaultTtl = TimeSpan.FromSeconds(1),
            CleanupInterval = TimeSpan.Zero,
            EnableStatistics = false,
            EnableDependencyTracking = false,
            EnableCascadeInvalidation = false,
            EnableAutoRefresh = false,
            EnableMemoryMonitoring = false,
            WarmupCacheSize = 0,
            MinAccessInterval = TimeSpan.Zero
        };

        /// <summary>
        /// 克隆当前配置
        /// </summary>
        /// <returns>配置副本</returns>
        public IncrementalCacheOptions Clone()
        {
            return new IncrementalCacheOptions
            {
                MaxItems = MaxItems,
                DefaultTtl = DefaultTtl,
                EnableStatistics = EnableStatistics,
                CleanupInterval = CleanupInterval,
                EnableDependencyTracking = EnableDependencyTracking,
                EnableCascadeInvalidation = EnableCascadeInvalidation,
                MaxDependencyDepth = MaxDependencyDepth,
                EnableAutoRefresh = EnableAutoRefresh,
                AutoRefreshInterval = AutoRefreshInterval,
                MemoryPressureThreshold = MemoryPressureThreshold,
                EnableMemoryMonitoring = EnableMemoryMonitoring,
                WarmupCacheSize = WarmupCacheSize,
                MinAccessInterval = MinAccessInterval
            };
        }

        /// <summary>
        /// 验证配置的有效性
        /// </summary>
        /// <returns>验证结果</returns>
        public ValidationResult Validate()
        {
            var result = new ValidationResult();

            if (MaxItems <= 0)
            {
                result.AddError($"MaxItems 必须大于 0，当前值：{MaxItems}");
            }

            if (MaxDependencyDepth <= 0)
            {
                result.AddError($"MaxDependencyDepth 必须大于 0，当前值：{MaxDependencyDepth}");
            }

            if (CleanupInterval < TimeSpan.Zero)
            {
                result.AddError($"CleanupInterval 不能为负数，当前值：{CleanupInterval}");
            }

            if (MemoryPressureThreshold <= 0)
            {
                result.AddError($"MemoryPressureThreshold 必须大于 0，当前值：{MemoryPressureThreshold}");
            }

            if (WarmupCacheSize < 0)
            {
                result.AddError($"WarmupCacheSize 不能为负数，当前值：{WarmupCacheSize}");
            }

            if (MinAccessInterval < TimeSpan.Zero)
            {
                result.AddError($"MinAccessInterval 不能为负数，当前值：{MinAccessInterval}");
            }

            return result;
        }

        public override string ToString()
        {
            return $"IncrementalCacheOptions: MaxItems={MaxItems}, TTL={DefaultTtl}, " +
                   $"Cleanup={CleanupInterval}, Dependencies={EnableDependencyTracking}, " +
                   $"Cascade={EnableCascadeInvalidation}, MaxDepth={MaxDependencyDepth}";
        }
    }

    /// <summary>
    /// 配置验证结果
    /// </summary>
    public class ValidationResult
    {
        private readonly System.Collections.Generic.List<string> _errors = new System.Collections.Generic.List<string>();
        private readonly System.Collections.Generic.List<string> _warnings = new System.Collections.Generic.List<string>();

        /// <summary>
        /// 是否有错误
        /// </summary>
        public bool HasErrors => _errors.Count > 0;

        /// <summary>
        /// 是否有警告
        /// </summary>
        public bool HasWarnings => _warnings.Count > 0;

        /// <summary>
        /// 错误列表
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<string> Errors => _errors.AsReadOnly();

        /// <summary>
        /// 警告列表
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<string> Warnings => _warnings.AsReadOnly();

        /// <summary>
        /// 是否验证通过（无错误）
        /// </summary>
        public bool IsValid => !HasErrors;

        /// <summary>
        /// 添加错误信息
        /// </summary>
        /// <param name="message">错误信息</param>
        public void AddError(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _errors.Add(message);
            }
        }

        /// <summary>
        /// 添加警告信息
        /// </summary>
        /// <param name="message">警告信息</param>
        public void AddWarning(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _warnings.Add(message);
            }
        }

        public override string ToString()
        {
            var result = $"验证结果: Valid={IsValid}";
            if (HasErrors)
            {
                result += $", Errors={_errors.Count}";
            }
            if (HasWarnings)
            {
                result += $", Warnings={_warnings.Count}";
            }
            return result;
        }
    }
}