using System;
using System.Collections.Generic;

namespace MookDialogueScript.Incremental
{
    /// <summary>
    /// 增量缓存配置选项
    /// 定义缓存系统的各种参数和行为配置
    /// </summary>
    public sealed class IncrementalCacheOptions
    {
        /// <summary>
        /// 是否启用缓存系统
        /// </summary>
        public bool EnableCache { get; set; } = true;

        /// <summary>
        /// 是否启用文件系统监控
        /// </summary>
        public bool EnableFileWatcher { get; set; } = true;

        /// <summary>
        /// 是否启用解析结果缓存
        /// </summary>
        public bool EnableParseResultCache { get; set; } = true;

        /// <summary>
        /// 是否启用变量声明缓存
        /// </summary>
        public bool EnableVariableDeclarationCache { get; set; } = true;

        /// <summary>
        /// 是否启用持久化缓存
        /// </summary>
        public bool EnablePersistentCache { get; set; } = true;

        /// <summary>
        /// 最大缓存大小（内存中的项数）
        /// </summary>
        public int MaxCacheSize { get; set; } = 1000;

        /// <summary>
        /// 最大内存使用量（字节）
        /// </summary>
        public long MaxMemoryUsage { get; set; } = 100 * 1024 * 1024; // 100MB

        /// <summary>
        /// 缓存过期时间
        /// </summary>
        public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromHours(24);

        /// <summary>
        /// 清理过期缓存的间隔时间
        /// </summary>
        public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(30);

        /// <summary>
        /// 文件变更检测的延迟时间（防抖动）
        /// </summary>
        public TimeSpan FileChangeDebounceTime { get; set; } = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// 批处理操作的最大项数
        /// </summary>
        public int MaxBatchSize { get; set; } = 100;

        /// <summary>
        /// 预热缓存的最大并发数
        /// </summary>
        public int WarmupConcurrency { get; set; } = Environment.ProcessorCount;

        /// <summary>
        /// 持久化缓存的存储路径
        /// </summary>
        public string PersistentCachePath { get; set; }

        /// <summary>
        /// 缓存序列化格式
        /// </summary>
        public CacheSerializationFormat SerializationFormat { get; set; } = CacheSerializationFormat.Binary;

        /// <summary>
        /// 是否启用缓存压缩
        /// </summary>
        public bool EnableCacheCompression { get; set; } = true;

        /// <summary>
        /// 缓存压缩级别（1-9）
        /// </summary>
        public int CompressionLevel { get; set; } = 6;

        /// <summary>
        /// 是否启用缓存统计收集
        /// </summary>
        public bool EnableStatistics { get; set; } = true;

        /// <summary>
        /// 统计数据保留时间
        /// </summary>
        public TimeSpan StatisticsRetention { get; set; } = TimeSpan.FromDays(7);

        /// <summary>
        /// 日志记录级别
        /// </summary>
        public CacheLogLevel LogLevel { get; set; } = CacheLogLevel.Warning;

        /// <summary>
        /// 监控的文件扩展名集合
        /// </summary>
        public HashSet<string> MonitoredFileExtensions { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mds", ".md", ".dialogue"
        };

        /// <summary>
        /// 排除监控的目录模式
        /// </summary>
        public HashSet<string> ExcludedDirectoryPatterns { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".git", ".svn", "bin", "obj", "temp", "tmp"
        };

        /// <summary>
        /// 排除监控的文件模式
        /// </summary>
        public HashSet<string> ExcludedFilePatterns { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "*.tmp", "*.temp", "*.bak", "*~"
        };

        /// <summary>
        /// 缓存键生成策略
        /// </summary>
        public CacheKeyStrategy KeyStrategy { get; set; } = CacheKeyStrategy.FilePath;

        /// <summary>
        /// 是否在启动时验证缓存完整性
        /// </summary>
        public bool ValidateIntegrityOnStartup { get; set; } = true;

        /// <summary>
        /// 是否在关闭时自动保存缓存
        /// </summary>
        public bool AutoSaveOnShutdown { get; set; } = true;

        /// <summary>
        /// 线程安全模式
        /// </summary>
        public ThreadSafetyMode ThreadSafetyMode { get; set; } = ThreadSafetyMode.Full;

        /// <summary>
        /// 创建默认配置
        /// </summary>
        /// <returns>默认配置实例</returns>
        public static IncrementalCacheOptions CreateDefault()
        {
            return new IncrementalCacheOptions();
        }

        /// <summary>
        /// 创建高性能配置（启用所有优化）
        /// </summary>
        /// <returns>高性能配置实例</returns>
        public static IncrementalCacheOptions CreateHighPerformance()
        {
            return new IncrementalCacheOptions
            {
                MaxCacheSize = 5000,
                MaxMemoryUsage = 500 * 1024 * 1024, // 500MB
                WarmupConcurrency = Environment.ProcessorCount * 2,
                EnableCacheCompression = true,
                CompressionLevel = 3, // 快速压缩
                CleanupInterval = TimeSpan.FromMinutes(15),
                FileChangeDebounceTime = TimeSpan.FromMilliseconds(200),
                ThreadSafetyMode = ThreadSafetyMode.Full
            };
        }

        /// <summary>
        /// 创建内存优化配置（最小化内存使用）
        /// </summary>
        /// <returns>内存优化配置实例</returns>
        public static IncrementalCacheOptions CreateMemoryOptimized()
        {
            return new IncrementalCacheOptions
            {
                MaxCacheSize = 500,
                MaxMemoryUsage = 50 * 1024 * 1024, // 50MB
                EnableCacheCompression = true,
                CompressionLevel = 9, // 最大压缩
                CleanupInterval = TimeSpan.FromMinutes(10),
                CacheExpiration = TimeSpan.FromHours(6),
                EnablePersistentCache = false // 减少磁盘使用
            };
        }

        /// <summary>
        /// 创建调试配置（详细日志和统计）
        /// </summary>
        /// <returns>调试配置实例</returns>
        public static IncrementalCacheOptions CreateDebug()
        {
            return new IncrementalCacheOptions
            {
                LogLevel = CacheLogLevel.Debug,
                EnableStatistics = true,
                StatisticsRetention = TimeSpan.FromDays(30),
                ValidateIntegrityOnStartup = true,
                ThreadSafetyMode = ThreadSafetyMode.Full
            };
        }

        /// <summary>
        /// 验证配置的有效性
        /// </summary>
        /// <returns>验证结果和错误信息</returns>
        public (bool isValid, string errorMessage) Validate()
        {
            if (MaxCacheSize <= 0)
                return (false, "MaxCacheSize 必须大于 0");

            if (MaxMemoryUsage <= 0)
                return (false, "MaxMemoryUsage 必须大于 0");

            if (MaxBatchSize <= 0)
                return (false, "MaxBatchSize 必须大于 0");

            if (WarmupConcurrency <= 0)
                return (false, "WarmupConcurrency 必须大于 0");

            if (CompressionLevel < 1 || CompressionLevel > 9)
                return (false, "CompressionLevel 必须在 1-9 范围内");

            if (EnablePersistentCache && string.IsNullOrWhiteSpace(PersistentCachePath))
                return (false, "启用持久化缓存时，PersistentCachePath 不能为空");

            return (true, null);
        }

        /// <summary>
        /// 克隆配置对象
        /// </summary>
        /// <returns>配置对象的副本</returns>
        public IncrementalCacheOptions Clone()
        {
            return new IncrementalCacheOptions
            {
                EnableCache = EnableCache,
                EnableFileWatcher = EnableFileWatcher,
                EnableParseResultCache = EnableParseResultCache,
                EnableVariableDeclarationCache = EnableVariableDeclarationCache,
                EnablePersistentCache = EnablePersistentCache,
                MaxCacheSize = MaxCacheSize,
                MaxMemoryUsage = MaxMemoryUsage,
                CacheExpiration = CacheExpiration,
                CleanupInterval = CleanupInterval,
                FileChangeDebounceTime = FileChangeDebounceTime,
                MaxBatchSize = MaxBatchSize,
                WarmupConcurrency = WarmupConcurrency,
                PersistentCachePath = PersistentCachePath,
                SerializationFormat = SerializationFormat,
                EnableCacheCompression = EnableCacheCompression,
                CompressionLevel = CompressionLevel,
                EnableStatistics = EnableStatistics,
                StatisticsRetention = StatisticsRetention,
                LogLevel = LogLevel,
                MonitoredFileExtensions = new HashSet<string>(MonitoredFileExtensions, StringComparer.OrdinalIgnoreCase),
                ExcludedDirectoryPatterns = new HashSet<string>(ExcludedDirectoryPatterns, StringComparer.OrdinalIgnoreCase),
                ExcludedFilePatterns = new HashSet<string>(ExcludedFilePatterns, StringComparer.OrdinalIgnoreCase),
                KeyStrategy = KeyStrategy,
                ValidateIntegrityOnStartup = ValidateIntegrityOnStartup,
                AutoSaveOnShutdown = AutoSaveOnShutdown,
                ThreadSafetyMode = ThreadSafetyMode
            };
        }
    }

    /// <summary>
    /// 缓存序列化格式
    /// </summary>
    public enum CacheSerializationFormat
    {
        /// <summary>
        /// 二进制格式（最快，最小）
        /// </summary>
        Binary,

        /// <summary>
        /// JSON格式（可读，兼容性好）
        /// </summary>
        Json,

        /// <summary>
        /// MessagePack格式（快速，紧凑）
        /// </summary>
        MessagePack
    }

    /// <summary>
    /// 缓存日志级别
    /// </summary>
    public enum CacheLogLevel
    {
        /// <summary>
        /// 调试信息
        /// </summary>
        Debug,

        /// <summary>
        /// 信息
        /// </summary>
        Info,

        /// <summary>
        /// 警告
        /// </summary>
        Warning,

        /// <summary>
        /// 错误
        /// </summary>
        Error,

        /// <summary>
        /// 禁用日志
        /// </summary>
        None
    }

    /// <summary>
    /// 缓存键生成策略
    /// </summary>
    public enum CacheKeyStrategy
    {
        /// <summary>
        /// 使用文件路径作为键
        /// </summary>
        FilePath,

        /// <summary>
        /// 使用文件路径哈希作为键
        /// </summary>
        FilePathHash,

        /// <summary>
        /// 使用文件内容哈希作为键
        /// </summary>
        ContentHash,

        /// <summary>
        /// 使用组合哈希（路径+内容）作为键
        /// </summary>
        CompositeHash
    }

    /// <summary>
    /// 线程安全模式
    /// </summary>
    public enum ThreadSafetyMode
    {
        /// <summary>
        /// 无线程安全（仅单线程使用）
        /// </summary>
        None,

        /// <summary>
        /// 基本线程安全（读操作安全）
        /// </summary>
        Basic,

        /// <summary>
        /// 完全线程安全（所有操作安全）
        /// </summary>
        Full
    }
}