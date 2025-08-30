using System;
using System.Collections.Generic;

namespace MookDialogueScript.Incremental
{
    /// <summary>
    /// 缓存统计信息结构
    /// 提供增量缓存系统的详细性能和使用统计
    /// </summary>
    public sealed class CacheStatistics
    {
        /// <summary>
        /// 统计创建时间
        /// </summary>
        public DateTime CreatedTime { get; private set; } = DateTime.UtcNow;

        /// <summary>
        /// 最后更新时间
        /// </summary>
        public DateTime LastUpdated { get; private set; } = DateTime.UtcNow;

        /// <summary>
        /// 缓存命中次数
        /// </summary>
        public long HitCount { get; private set; }

        /// <summary>
        /// 缓存未命中次数
        /// </summary>
        public long MissCount { get; private set; }

        /// <summary>
        /// 总访问次数
        /// </summary>
        public long TotalAccesses => HitCount + MissCount;

        /// <summary>
        /// 缓存命中率（0.0-1.0）
        /// </summary>
        public double HitRatio => TotalAccesses > 0 ? (double)HitCount / TotalAccesses : 0.0;

        /// <summary>
        /// 缓存项总数
        /// </summary>
        public int TotalItems { get; private set; }

        /// <summary>
        /// 解析结果缓存项数
        /// </summary>
        public int ParseResultItems { get; private set; }

        /// <summary>
        /// 变量声明缓存项数
        /// </summary>
        public int VariableDeclarationItems { get; private set; }

        /// <summary>
        /// 文件元数据缓存项数
        /// </summary>
        public int FileMetadataItems { get; private set; }

        /// <summary>
        /// 总内存使用量（字节）
        /// </summary>
        public long TotalMemoryUsage { get; private set; }

        /// <summary>
        /// 解析结果缓存内存使用量
        /// </summary>
        public long ParseResultMemoryUsage { get; private set; }

        /// <summary>
        /// 变量声明缓存内存使用量
        /// </summary>
        public long VariableDeclarationMemoryUsage { get; private set; }

        /// <summary>
        /// 文件元数据缓存内存使用量
        /// </summary>
        public long FileMetadataMemoryUsage { get; private set; }

        /// <summary>
        /// 缓存添加操作次数
        /// </summary>
        public long AddOperations { get; private set; }

        /// <summary>
        /// 缓存更新操作次数
        /// </summary>
        public long UpdateOperations { get; private set; }

        /// <summary>
        /// 缓存删除操作次数
        /// </summary>
        public long RemoveOperations { get; private set; }

        /// <summary>
        /// 缓存清空操作次数
        /// </summary>
        public long ClearOperations { get; private set; }

        /// <summary>
        /// 过期清理操作次数
        /// </summary>
        public long CleanupOperations { get; private set; }

        /// <summary>
        /// 清理的过期项总数
        /// </summary>
        public long ExpiredItemsRemoved { get; private set; }

        /// <summary>
        /// 文件变更检测次数
        /// </summary>
        public long FileChangeDetections { get; private set; }

        /// <summary>
        /// 缓存刷新次数
        /// </summary>
        public long CacheRefreshCount { get; private set; }

        /// <summary>
        /// 批量操作次数
        /// </summary>
        public long BatchOperations { get; private set; }

        /// <summary>
        /// 预热操作次数
        /// </summary>
        public long WarmupOperations { get; private set; }

        /// <summary>
        /// 持久化保存次数
        /// </summary>
        public long SaveOperations { get; private set; }

        /// <summary>
        /// 持久化加载次数
        /// </summary>
        public long LoadOperations { get; private set; }

        /// <summary>
        /// 完整性验证次数
        /// </summary>
        public long IntegrityValidations { get; private set; }

        /// <summary>
        /// 完整性验证失败次数
        /// </summary>
        public long IntegrityValidationFailures { get; private set; }

        /// <summary>
        /// 操作耗时统计
        /// </summary>
        public OperationTimingStats TimingStats { get; private set; } = new OperationTimingStats();

        /// <summary>
        /// 错误统计
        /// </summary>
        public ErrorStatistics ErrorStats { get; private set; } = new ErrorStatistics();

        /// <summary>
        /// 性能指标
        /// </summary>
        public PerformanceMetrics PerformanceMetrics { get; private set; } = new PerformanceMetrics();

        /// <summary>
        /// 自定义统计数据
        /// </summary>
        public Dictionary<string, object> CustomStats { get; private set; } = new Dictionary<string, object>();

        /// <summary>
        /// 创建空的统计信息
        /// </summary>
        /// <returns>空统计信息实例</returns>
        public static CacheStatistics CreateEmpty()
        {
            return new CacheStatistics();
        }

        /// <summary>
        /// 增加命中计数
        /// </summary>
        /// <param name="count">增加的数量</param>
        /// <returns>更新后的统计信息</returns>
        public CacheStatistics AddHits(long count = 1)
        {
            var newStats = Clone();
            newStats.HitCount += count;
            newStats.LastUpdated = DateTime.UtcNow;
            return newStats;
        }

        /// <summary>
        /// 增加未命中计数
        /// </summary>
        /// <param name="count">增加的数量</param>
        /// <returns>更新后的统计信息</returns>
        public CacheStatistics AddMisses(long count = 1)
        {
            var newStats = Clone();
            newStats.MissCount += count;
            newStats.LastUpdated = DateTime.UtcNow;
            return newStats;
        }

        /// <summary>
        /// 更新缓存项数量
        /// </summary>
        /// <param name="parseResults">解析结果项数</param>
        /// <param name="variableDeclarations">变量声明项数</param>
        /// <param name="fileMetadata">文件元数据项数</param>
        /// <returns>更新后的统计信息</returns>
        public CacheStatistics UpdateItemCounts(int parseResults, int variableDeclarations, int fileMetadata)
        {
            var newStats = Clone();
            newStats.ParseResultItems = parseResults;
            newStats.VariableDeclarationItems = variableDeclarations;
            newStats.FileMetadataItems = fileMetadata;
            newStats.TotalItems = parseResults + variableDeclarations + fileMetadata;
            newStats.LastUpdated = DateTime.UtcNow;
            return newStats;
        }

        /// <summary>
        /// 更新内存使用量
        /// </summary>
        /// <param name="parseResultMemory">解析结果内存使用量</param>
        /// <param name="variableDeclarationMemory">变量声明内存使用量</param>
        /// <param name="fileMetadataMemory">文件元数据内存使用量</param>
        /// <returns>更新后的统计信息</returns>
        public CacheStatistics UpdateMemoryUsage(long parseResultMemory, long variableDeclarationMemory, long fileMetadataMemory)
        {
            var newStats = Clone();
            newStats.ParseResultMemoryUsage = parseResultMemory;
            newStats.VariableDeclarationMemoryUsage = variableDeclarationMemory;
            newStats.FileMetadataMemoryUsage = fileMetadataMemory;
            newStats.TotalMemoryUsage = parseResultMemory + variableDeclarationMemory + fileMetadataMemory;
            newStats.LastUpdated = DateTime.UtcNow;
            return newStats;
        }

        /// <summary>
        /// 记录操作
        /// </summary>
        /// <param name="operationType">操作类型</param>
        /// <param name="count">操作次数</param>
        /// <returns>更新后的统计信息</returns>
        public CacheStatistics RecordOperation(CacheOperationType operationType, long count = 1)
        {
            var newStats = Clone();
            switch (operationType)
            {
                case CacheOperationType.Add:
                    newStats.AddOperations += count;
                    break;
                case CacheOperationType.Update:
                    newStats.UpdateOperations += count;
                    break;
                case CacheOperationType.Remove:
                    newStats.RemoveOperations += count;
                    break;
                case CacheOperationType.Clear:
                    newStats.ClearOperations += count;
                    break;
                case CacheOperationType.Cleanup:
                    newStats.CleanupOperations += count;
                    break;
                case CacheOperationType.FileChange:
                    newStats.FileChangeDetections += count;
                    break;
                case CacheOperationType.Refresh:
                    newStats.CacheRefreshCount += count;
                    break;
                case CacheOperationType.Batch:
                    newStats.BatchOperations += count;
                    break;
                case CacheOperationType.Warmup:
                    newStats.WarmupOperations += count;
                    break;
                case CacheOperationType.Save:
                    newStats.SaveOperations += count;
                    break;
                case CacheOperationType.Load:
                    newStats.LoadOperations += count;
                    break;
                case CacheOperationType.IntegrityValidation:
                    newStats.IntegrityValidations += count;
                    break;
            }
            newStats.LastUpdated = DateTime.UtcNow;
            return newStats;
        }

        /// <summary>
        /// 记录清理的过期项数
        /// </summary>
        /// <param name="count">清理的项数</param>
        /// <returns>更新后的统计信息</returns>
        public CacheStatistics RecordExpiredItemsRemoved(long count)
        {
            var newStats = Clone();
            newStats.ExpiredItemsRemoved += count;
            newStats.LastUpdated = DateTime.UtcNow;
            return newStats;
        }

        /// <summary>
        /// 记录完整性验证失败
        /// </summary>
        /// <param name="count">失败次数</param>
        /// <returns>更新后的统计信息</returns>
        public CacheStatistics RecordIntegrityValidationFailure(long count = 1)
        {
            var newStats = Clone();
            newStats.IntegrityValidationFailures += count;
            newStats.LastUpdated = DateTime.UtcNow;
            return newStats;
        }

        /// <summary>
        /// 添加自定义统计数据
        /// </summary>
        /// <param name="key">统计键</param>
        /// <param name="value">统计值</param>
        /// <returns>更新后的统计信息</returns>
        public CacheStatistics WithCustomStat(string key, object value)
        {
            var newStats = Clone();
            newStats.CustomStats = new Dictionary<string, object>(CustomStats);
            newStats.CustomStats[key] = value;
            newStats.LastUpdated = DateTime.UtcNow;
            return newStats;
        }

        /// <summary>
        /// 获取性能摘要
        /// </summary>
        /// <returns>性能摘要字符串</returns>
        public string GetPerformanceSummary()
        {
            return $"缓存性能摘要: " +
                   $"命中率: {HitRatio:P2}, " +
                   $"总项数: {TotalItems:N0}, " +
                   $"内存使用: {TotalMemoryUsage / (1024.0 * 1024.0):F2} MB, " +
                   $"总访问: {TotalAccesses:N0}";
        }

        /// <summary>
        /// 获取操作摘要
        /// </summary>
        /// <returns>操作摘要字符串</returns>
        public string GetOperationSummary()
        {
            return $"操作摘要: " +
                   $"添加: {AddOperations:N0}, " +
                   $"更新: {UpdateOperations:N0}, " +
                   $"删除: {RemoveOperations:N0}, " +
                   $"清理: {CleanupOperations:N0}, " +
                   $"刷新: {CacheRefreshCount:N0}";
        }

        /// <summary>
        /// 计算缓存效率分数（0-100）
        /// </summary>
        /// <returns>效率分数</returns>
        public double CalculateEfficiencyScore()
        {
            if (TotalAccesses == 0)
                return 0.0;

            // 基础分数基于命中率
            double baseScore = HitRatio * 60.0;

            // 内存效率加分（每MB使用的项数）
            double memoryEfficiency = TotalItems / Math.Max(TotalMemoryUsage / (1024.0 * 1024.0), 1.0);
            double memoryScore = Math.Min(memoryEfficiency / 10.0 * 20.0, 20.0);

            // 操作效率加分（成功操作比例）
            double totalOperations = AddOperations + UpdateOperations + RemoveOperations;
            double operationEfficiency = totalOperations > 0 ? 
                (AddOperations + UpdateOperations) / (double)totalOperations : 1.0;
            double operationScore = operationEfficiency * 20.0;

            return Math.Min(baseScore + memoryScore + operationScore, 100.0);
        }

        /// <summary>
        /// 转换为字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString()
        {
            return $"缓存统计: {GetPerformanceSummary()}";
        }

        /// <summary>
        /// 克隆当前统计信息
        /// </summary>
        /// <returns>克隆的统计信息实例</returns>
        private CacheStatistics Clone()
        {
            return new CacheStatistics
            {
                CreatedTime = this.CreatedTime,
                LastUpdated = this.LastUpdated,
                HitCount = this.HitCount,
                MissCount = this.MissCount,
                TotalItems = this.TotalItems,
                ParseResultItems = this.ParseResultItems,
                VariableDeclarationItems = this.VariableDeclarationItems,
                FileMetadataItems = this.FileMetadataItems,
                TotalMemoryUsage = this.TotalMemoryUsage,
                ParseResultMemoryUsage = this.ParseResultMemoryUsage,
                VariableDeclarationMemoryUsage = this.VariableDeclarationMemoryUsage,
                FileMetadataMemoryUsage = this.FileMetadataMemoryUsage,
                AddOperations = this.AddOperations,
                UpdateOperations = this.UpdateOperations,
                RemoveOperations = this.RemoveOperations,
                ClearOperations = this.ClearOperations,
                CleanupOperations = this.CleanupOperations,
                ExpiredItemsRemoved = this.ExpiredItemsRemoved,
                FileChangeDetections = this.FileChangeDetections,
                CacheRefreshCount = this.CacheRefreshCount,
                BatchOperations = this.BatchOperations,
                WarmupOperations = this.WarmupOperations,
                SaveOperations = this.SaveOperations,
                LoadOperations = this.LoadOperations,
                IntegrityValidations = this.IntegrityValidations,
                IntegrityValidationFailures = this.IntegrityValidationFailures,
                TimingStats = this.TimingStats,
                ErrorStats = this.ErrorStats,
                PerformanceMetrics = this.PerformanceMetrics,
                CustomStats = new Dictionary<string, object>(this.CustomStats)
            };
        }
    }

    /// <summary>
    /// 操作耗时统计
    /// </summary>
    public sealed class OperationTimingStats
    {
        /// <summary>
        /// 平均读取耗时（毫秒）
        /// </summary>
        public double AverageReadTime { get; private set; }

        /// <summary>
        /// 平均写入耗时（毫秒）
        /// </summary>
        public double AverageWriteTime { get; private set; }

        /// <summary>
        /// 平均清理耗时（毫秒）
        /// </summary>
        public double AverageCleanupTime { get; private set; }

        /// <summary>
        /// 平均验证耗时（毫秒）
        /// </summary>
        public double AverageValidationTime { get; private set; }

        /// <summary>
        /// 最长操作耗时（毫秒）
        /// </summary>
        public double MaxOperationTime { get; private set; }

        /// <summary>
        /// 最短操作耗时（毫秒）
        /// </summary>
        public double MinOperationTime { get; private set; }
    }

    /// <summary>
    /// 错误统计
    /// </summary>
    public sealed class ErrorStatistics
    {
        /// <summary>
        /// 总错误次数
        /// </summary>
        public long TotalErrors { get; private set; }

        /// <summary>
        /// 读取错误次数
        /// </summary>
        public long ReadErrors { get; private set; }

        /// <summary>
        /// 写入错误次数
        /// </summary>
        public long WriteErrors { get; private set; }

        /// <summary>
        /// 文件系统错误次数
        /// </summary>
        public long FileSystemErrors { get; private set; }

        /// <summary>
        /// 序列化错误次数
        /// </summary>
        public long SerializationErrors { get; private set; }

        /// <summary>
        /// 验证错误次数
        /// </summary>
        public long ValidationErrors { get; private set; }

        /// <summary>
        /// 最近错误时间
        /// </summary>
        public DateTime? LastErrorTime { get; private set; }

        /// <summary>
        /// 最近错误消息
        /// </summary>
        public string LastErrorMessage { get; private set; }
    }

    /// <summary>
    /// 性能指标
    /// </summary>
    public sealed class PerformanceMetrics
    {
        /// <summary>
        /// 吞吐量（操作/秒）
        /// </summary>
        public double ThroughputPerSecond { get; private set; }

        /// <summary>
        /// 延迟百分位数（P50, P95, P99）
        /// </summary>
        public Dictionary<int, double> LatencyPercentiles { get; private set; } = new Dictionary<int, double>();

        /// <summary>
        /// 内存增长率（字节/秒）
        /// </summary>
        public double MemoryGrowthRate { get; private set; }

        /// <summary>
        /// 缓存周转率（新增项/总项）
        /// </summary>
        public double CacheTurnoverRate { get; private set; }

        /// <summary>
        /// CPU使用率（估算）
        /// </summary>
        public double EstimatedCpuUsage { get; private set; }
    }

    /// <summary>
    /// 缓存操作类型
    /// </summary>
    public enum CacheOperationType
    {
        /// <summary>
        /// 添加操作
        /// </summary>
        Add,

        /// <summary>
        /// 更新操作
        /// </summary>
        Update,

        /// <summary>
        /// 移除操作
        /// </summary>
        Remove,

        /// <summary>
        /// 清空操作
        /// </summary>
        Clear,

        /// <summary>
        /// 清理操作
        /// </summary>
        Cleanup,

        /// <summary>
        /// 文件变更操作
        /// </summary>
        FileChange,

        /// <summary>
        /// 刷新操作
        /// </summary>
        Refresh,

        /// <summary>
        /// 批量操作
        /// </summary>
        Batch,

        /// <summary>
        /// 预热操作
        /// </summary>
        Warmup,

        /// <summary>
        /// 保存操作
        /// </summary>
        Save,

        /// <summary>
        /// 加载操作
        /// </summary>
        Load,

        /// <summary>
        /// 完整性验证操作
        /// </summary>
        IntegrityValidation
    }

    /// <summary>
    /// 缓存报告
    /// </summary>
    public sealed class CacheReport
    {
        /// <summary>
        /// 报告生成时间
        /// </summary>
        public DateTime GeneratedTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 统计信息
        /// </summary>
        public CacheStatistics Statistics { get; set; } = CacheStatistics.CreateEmpty();

        /// <summary>
        /// 缓存健康状态
        /// </summary>
        public CacheHealthStatus HealthStatus { get; set; } = CacheHealthStatus.Good;

        /// <summary>
        /// 建议信息
        /// </summary>
        public List<string> Recommendations { get; set; } = new List<string>();

        /// <summary>
        /// 警告信息
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>
        /// 详细分析结果
        /// </summary>
        public Dictionary<string, object> Analysis { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// 缓存完整性验证结果
    /// </summary>
    public sealed class CacheIntegrityResult
    {
        /// <summary>
        /// 验证是否通过
        /// </summary>
        public bool IsValid { get;  set; }

        /// <summary>
        /// 验证时间
        /// </summary>
        public DateTime ValidationTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 检查的项总数
        /// </summary>
        public int TotalItemsChecked { get; set; }

        /// <summary>
        /// 发现的错误数
        /// </summary>
        public int ErrorsFound { get; set; }

        /// <summary>
        /// 错误详情列表
        /// </summary>
        public List<string> ErrorDetails { get; set; } = new List<string>();

        /// <summary>
        /// 修复的错误数
        /// </summary>
        public int ErrorsFixed { get; set; }

        /// <summary>
        /// 验证耗时
        /// </summary>
        public TimeSpan ValidationDuration { get; set; }
    }

    /// <summary>
    /// 缓存健康状态
    /// </summary>
    public enum CacheHealthStatus
    {
        /// <summary>
        /// 良好
        /// </summary>
        Good,

        /// <summary>
        /// 警告
        /// </summary>
        Warning,

        /// <summary>
        /// 错误
        /// </summary>
        Error,

        /// <summary>
        /// 严重错误
        /// </summary>
        Critical
    }
}