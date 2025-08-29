using System;
using UnityEngine;
using MookDialogueScript.Incremental;

namespace MookDialogueScript
{
    /// <summary>
    /// 增量缓存配置工厂
    /// 提供针对不同使用场景的预设缓存配置
    /// </summary>
    public static class CacheConfigurationFactory
    {
        /// <summary>
        /// 创建开发环境缓存配置
        /// 适合Unity编辑器中的开发和测试
        /// </summary>
        /// <returns>开发环境缓存配置</returns>
        public static IncrementalCacheOptions CreateDevelopmentConfig()
        {
            return new IncrementalCacheOptions
            {
                EnableCache = true,
                EnableFileWatcher = true,
                EnableParseResultCache = true,
                EnableVariableDeclarationCache = true,
                EnablePersistentCache = false, // 开发环境不需要持久化
                
                MaxCacheSize = 1000, // 较大的缓存容量
                MaxMemoryUsage = 100 * 1024 * 1024, // 100MB
                
                CacheExpiration = TimeSpan.FromHours(8), // 工作日内有效
                CleanupInterval = TimeSpan.FromMinutes(10), // 频繁清理
                FileChangeDebounceTime = TimeSpan.FromMilliseconds(200), // 快速响应文件变化
                
                MaxBatchSize = 100,
                WarmupConcurrency = Math.Max(4, Environment.ProcessorCount),
                
                EnableStatistics = true,
                LogLevel = CacheLogLevel.Debug, // 详细日志
                
                KeyStrategy = CacheKeyStrategy.FilePath,
                ValidateIntegrityOnStartup = true, // 开发环境验证完整性
                AutoSaveOnShutdown = false,
                ThreadSafetyMode = ThreadSafetyMode.Full
            };
        }

        /// <summary>
        /// 创建生产环境缓存配置
        /// 适合发布版本的运行时使用
        /// </summary>
        /// <returns>生产环境缓存配置</returns>
        public static IncrementalCacheOptions CreateProductionConfig()
        {
            return new IncrementalCacheOptions
            {
                EnableCache = true,
                EnableFileWatcher = false, // 生产环境通常不需要文件监控
                EnableParseResultCache = true,
                EnableVariableDeclarationCache = true,
                EnablePersistentCache = true, // 启用持久化提升启动性能
                
                MaxCacheSize = 500, // 适中的缓存容量
                MaxMemoryUsage = 50 * 1024 * 1024, // 50MB
                
                CacheExpiration = TimeSpan.FromDays(7), // 较长的过期时间
                CleanupInterval = TimeSpan.FromHours(1), // 不频繁清理
                FileChangeDebounceTime = TimeSpan.FromSeconds(1),
                
                MaxBatchSize = 50,
                WarmupConcurrency = Math.Max(2, Environment.ProcessorCount / 2),
                
                PersistentCachePath = System.IO.Path.Combine(Application.persistentDataPath, "DialogueCache"),
                SerializationFormat = CacheSerializationFormat.Binary,
                EnableCacheCompression = true,
                CompressionLevel = 6,
                
                EnableStatistics = false, // 生产环境减少统计开销
                LogLevel = CacheLogLevel.Error, // 只记录错误
                
                KeyStrategy = CacheKeyStrategy.ContentHash, // 内容哈希更可靠
                ValidateIntegrityOnStartup = false, // 快速启动
                AutoSaveOnShutdown = true,
                ThreadSafetyMode = ThreadSafetyMode.Full
            };
        }

        /// <summary>
        /// 创建测试环境缓存配置
        /// 适合单元测试和自动化测试
        /// </summary>
        /// <returns>测试环境缓存配置</returns>
        public static IncrementalCacheOptions CreateTestingConfig()
        {
            return new IncrementalCacheOptions
            {
                EnableCache = true,
                EnableFileWatcher = false, // 测试环境不需要文件监控
                EnableParseResultCache = true,
                EnableVariableDeclarationCache = true,
                EnablePersistentCache = false, // 测试环境使用内存缓存
                
                MaxCacheSize = 100, // 小容量缓存
                MaxMemoryUsage = 10 * 1024 * 1024, // 10MB
                
                CacheExpiration = TimeSpan.FromMinutes(30), // 短期缓存
                CleanupInterval = TimeSpan.FromMinutes(5), // 频繁清理
                FileChangeDebounceTime = TimeSpan.FromMilliseconds(100),
                
                MaxBatchSize = 20,
                WarmupConcurrency = 1, // 单线程测试
                
                EnableStatistics = true, // 测试需要统计
                LogLevel = CacheLogLevel.Warning,
                
                KeyStrategy = CacheKeyStrategy.FilePath,
                ValidateIntegrityOnStartup = false,
                AutoSaveOnShutdown = false,
                ThreadSafetyMode = ThreadSafetyMode.Full
            };
        }

        /// <summary>
        /// 创建禁用缓存的配置
        /// 用于性能比较或问题排查
        /// </summary>
        /// <returns>禁用缓存的配置</returns>
        public static IncrementalCacheOptions CreateDisabledConfig()
        {
            return new IncrementalCacheOptions
            {
                EnableCache = false,
                EnableFileWatcher = false,
                EnableParseResultCache = false,
                EnableVariableDeclarationCache = false,
                EnablePersistentCache = false,
                
                MaxCacheSize = 0,
                MaxMemoryUsage = 0,
                
                EnableStatistics = false,
                LogLevel = CacheLogLevel.None,
                
                ThreadSafetyMode = ThreadSafetyMode.None
            };
        }

        /// <summary>
        /// 创建性能优先的缓存配置
        /// 最大化性能，适合高频使用场景
        /// </summary>
        /// <returns>性能优先缓存配置</returns>
        public static IncrementalCacheOptions CreatePerformanceConfig()
        {
            return new IncrementalCacheOptions
            {
                EnableCache = true,
                EnableFileWatcher = true,
                EnableParseResultCache = true,
                EnableVariableDeclarationCache = true,
                EnablePersistentCache = true,
                
                MaxCacheSize = 2000, // 大容量缓存
                MaxMemoryUsage = 200 * 1024 * 1024, // 200MB
                
                CacheExpiration = TimeSpan.FromDays(30), // 长期缓存
                CleanupInterval = TimeSpan.FromHours(4), // 减少清理频率
                FileChangeDebounceTime = TimeSpan.FromMilliseconds(50), // 快速响应
                
                MaxBatchSize = 200,
                WarmupConcurrency = Environment.ProcessorCount, // 最大并发
                
                PersistentCachePath = System.IO.Path.Combine(Application.persistentDataPath, "DialogueCache"),
                SerializationFormat = CacheSerializationFormat.Binary,
                EnableCacheCompression = false, // 禁用压缩提升性能
                
                EnableStatistics = true, // 监控性能
                LogLevel = CacheLogLevel.Warning,
                
                KeyStrategy = CacheKeyStrategy.ContentHash,
                ValidateIntegrityOnStartup = false, // 快速启动
                AutoSaveOnShutdown = true,
                ThreadSafetyMode = ThreadSafetyMode.Full
            };
        }

        /// <summary>
        /// 创建内存友好的缓存配置
        /// 适合内存受限的环境
        /// </summary>
        /// <returns>内存友好缓存配置</returns>
        public static IncrementalCacheOptions CreateMemoryFriendlyConfig()
        {
            return new IncrementalCacheOptions
            {
                EnableCache = true,
                EnableFileWatcher = false, // 减少内存占用
                EnableParseResultCache = true,
                EnableVariableDeclarationCache = false, // 减少缓存项
                EnablePersistentCache = true,
                
                MaxCacheSize = 50, // 小容量缓存
                MaxMemoryUsage = 5 * 1024 * 1024, // 5MB
                
                CacheExpiration = TimeSpan.FromHours(2), // 短期缓存
                CleanupInterval = TimeSpan.FromMinutes(2), // 频繁清理
                FileChangeDebounceTime = TimeSpan.FromSeconds(1),
                
                MaxBatchSize = 10,
                WarmupConcurrency = 1, // 单线程减少内存占用
                
                PersistentCachePath = System.IO.Path.Combine(Application.persistentDataPath, "DialogueCache"),
                SerializationFormat = CacheSerializationFormat.Binary,
                EnableCacheCompression = true, // 启用压缩节省内存
                CompressionLevel = 9, // 最大压缩
                
                EnableStatistics = false, // 减少内存占用
                LogLevel = CacheLogLevel.Error,
                
                KeyStrategy = CacheKeyStrategy.FilePath,
                ValidateIntegrityOnStartup = false,
                AutoSaveOnShutdown = true,
                ThreadSafetyMode = ThreadSafetyMode.Basic
            };
        }

        /// <summary>
        /// 根据当前运行环境自动选择合适的缓存配置
        /// </summary>
        /// <returns>适合当前环境的缓存配置</returns>
        public static IncrementalCacheOptions CreateAutoConfig()
        {
            // 根据运行环境选择合适的配置
#if UNITY_EDITOR
            return CreateDevelopmentConfig();
#elif DEBUG || DEVELOPMENT_BUILD
            return CreateTestingConfig();
#else
            return CreateProductionConfig();
#endif
        }

        /// <summary>
        /// 创建自定义缓存配置
        /// </summary>
        /// <param name="customizer">配置自定义函数</param>
        /// <returns>自定义缓存配置</returns>
        public static IncrementalCacheOptions CreateCustomConfig(Action<IncrementalCacheOptions> customizer)
        {
            var options = CreateAutoConfig();
            customizer?.Invoke(options);
            return options;
        }
    }
}