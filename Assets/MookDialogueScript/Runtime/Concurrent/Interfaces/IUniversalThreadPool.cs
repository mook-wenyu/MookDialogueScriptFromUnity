using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MookDialogueScript.Concurrent
{
    /// <summary>
    /// 通用线程池接口
    /// 提供高性能的任务调度和并发执行能力
    /// 支持工作窃取、自适应负载均衡和资源管理
    /// </summary>
    public interface IUniversalThreadPool : IDisposable
    {
        #region 基本属性
        /// <summary>
        /// 活跃线程数量
        /// </summary>
        int ActiveThreads { get; }

        /// <summary>
        /// 待处理任务数量
        /// </summary>
        int PendingTasks { get; }

        /// <summary>
        /// 最大并发度
        /// </summary>
        int MaxConcurrency { get; }

        /// <summary>
        /// 线程池是否已关闭
        /// </summary>
        bool IsShutdown { get; }
        #endregion

        #region 任务调度方法
        /// <summary>
        /// 提交无返回值任务
        /// </summary>
        /// <param name="action">要执行的任务</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>任务句柄</returns>
        Task SubmitAsync(Action action, CancellationToken cancellationToken = default);

        /// <summary>
        /// 提交有返回值任务
        /// </summary>
        /// <typeparam name="T">返回值类型</typeparam>
        /// <param name="func">要执行的函数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>包含结果的任务</returns>
        Task<T> SubmitAsync<T>(Func<T> func, CancellationToken cancellationToken = default);

        /// <summary>
        /// 并行执行多个任务
        /// </summary>
        /// <param name="actions">任务列表</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示所有任务完成的任务</returns>
        Task SubmitBatchAsync(IEnumerable<Action> actions, CancellationToken cancellationToken = default);

        /// <summary>
        /// 并行执行多个有返回值的任务
        /// </summary>
        /// <typeparam name="T">返回值类型</typeparam>
        /// <param name="funcs">函数列表</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>包含所有结果的任务</returns>
        Task<T[]> SubmitBatchAsync<T>(IEnumerable<Func<T>> funcs, CancellationToken cancellationToken = default);
        #endregion

        #region 高优先级任务
        /// <summary>
        /// 提交高优先级任务
        /// 高优先级任务将被优先执行
        /// </summary>
        /// <param name="action">要执行的任务</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>任务句柄</returns>
        Task SubmitHighPriorityAsync(Action action, CancellationToken cancellationToken = default);

        /// <summary>
        /// 提交高优先级有返回值任务
        /// </summary>
        /// <typeparam name="T">返回值类型</typeparam>
        /// <param name="func">要执行的函数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>包含结果的任务</returns>
        Task<T> SubmitHighPriorityAsync<T>(Func<T> func, CancellationToken cancellationToken = default);
        #endregion

        #region 生命周期管理
        /// <summary>
        /// 启动线程池
        /// </summary>
        void Start();

        /// <summary>
        /// 优雅关闭线程池
        /// 等待所有任务完成后关闭
        /// </summary>
        /// <param name="timeout">最大等待时间</param>
        /// <returns>是否成功关闭</returns>
        Task<bool> ShutdownAsync(TimeSpan timeout);

        /// <summary>
        /// 立即关闭线程池
        /// 取消所有待处理任务
        /// </summary>
        void ShutdownNow();

        /// <summary>
        /// 等待所有任务完成
        /// </summary>
        /// <param name="timeout">最大等待时间</param>
        /// <returns>是否所有任务都已完成</returns>
        Task<bool> AwaitTerminationAsync(TimeSpan timeout);
        #endregion

        #region 监控和统计
        /// <summary>
        /// 获取线程池统计信息
        /// </summary>
        /// <returns>统计信息</returns>
        ThreadPoolStatistics GetStatistics();

        /// <summary>
        /// 重置统计信息
        /// </summary>
        void ResetStatistics();

        /// <summary>
        /// 调整线程池大小
        /// </summary>
        /// <param name="coreSize">核心线程数</param>
        /// <param name="maxSize">最大线程数</param>
        void Resize(int coreSize, int maxSize);
        #endregion
    }

    /// <summary>
    /// 线程池统计信息
    /// </summary>
    public struct ThreadPoolStatistics
    {
        /// <summary>
        /// 总提交任务数
        /// </summary>
        public long TotalSubmittedTasks;

        /// <summary>
        /// 已完成任务数
        /// </summary>
        public long CompletedTasks;

        /// <summary>
        /// 失败任务数
        /// </summary>
        public long FailedTasks;

        /// <summary>
        /// 当前活跃线程数
        /// </summary>
        public int ActiveThreads;

        /// <summary>
        /// 当前待处理任务数
        /// </summary>
        public int PendingTasks;

        /// <summary>
        /// 峰值线程数
        /// </summary>
        public int PeakThreads;

        /// <summary>
        /// 平均任务执行时间（毫秒）
        /// </summary>
        public double AverageExecutionTime;

        /// <summary>
        /// 工作窃取次数
        /// </summary>
        public long WorkStealCount;

        /// <summary>
        /// 线程池利用率（0-1之间）
        /// </summary>
        public double Utilization;

        /// <summary>
        /// 格式化统计信息
        /// </summary>
        /// <returns>格式化的统计字符串</returns>
        public override string ToString()
        {
            return $"ThreadPool Stats: " +
                   $"Tasks[Submitted={TotalSubmittedTasks}, Completed={CompletedTasks}, Failed={FailedTasks}], " +
                   $"Threads[Active={ActiveThreads}, Peak={PeakThreads}], " +
                   $"Pending={PendingTasks}, WorkSteal={WorkStealCount}, " +
                   $"AvgExecTime={AverageExecutionTime:F2}ms, Utilization={Utilization:P2}";
        }
    }

    /// <summary>
    /// 线程池配置选项
    /// </summary>
    public class ThreadPoolOptions
    {
        /// <summary>
        /// 核心线程数，默认为CPU核心数
        /// </summary>
        public int CoreThreads { get; set; } = Environment.ProcessorCount;

        /// <summary>
        /// 最大线程数，默认为CPU核心数的2倍
        /// </summary>
        public int MaxThreads { get; set; } = Environment.ProcessorCount * 2;

        /// <summary>
        /// 空闲线程超时时间，默认60秒
        /// </summary>
        public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromSeconds(60);

        /// <summary>
        /// 任务队列最大大小，-1表示无限制
        /// </summary>
        public int MaxQueueSize { get; set; } = -1;

        /// <summary>
        /// 是否启用工作窃取，默认启用
        /// </summary>
        public bool EnableWorkStealing { get; set; } = true;

        /// <summary>
        /// 是否启用负载均衡，默认启用
        /// </summary>
        public bool EnableLoadBalancing { get; set; } = true;

        /// <summary>
        /// 统计信息收集间隔，默认5秒
        /// </summary>
        public TimeSpan StatisticsInterval { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 线程名称前缀
        /// </summary>
        public string ThreadNamePrefix { get; set; } = "UniversalThreadPool";

        /// <summary>
        /// 默认配置
        /// </summary>
        public static ThreadPoolOptions Default => new ThreadPoolOptions();

        /// <summary>
        /// 高性能配置
        /// 适用于CPU密集型任务
        /// </summary>
        public static ThreadPoolOptions HighPerformance => new ThreadPoolOptions
        {
            CoreThreads = Environment.ProcessorCount,
            MaxThreads = Environment.ProcessorCount,
            IdleTimeout = TimeSpan.FromMinutes(5),
            EnableWorkStealing = true,
            EnableLoadBalancing = true
        };

        /// <summary>
        /// 高并发配置
        /// 适用于I/O密集型任务
        /// </summary>
        public static ThreadPoolOptions HighConcurrency => new ThreadPoolOptions
        {
            CoreThreads = Environment.ProcessorCount * 2,
            MaxThreads = Environment.ProcessorCount * 4,
            IdleTimeout = TimeSpan.FromSeconds(30),
            EnableWorkStealing = true,
            EnableLoadBalancing = true
        };
    }
}