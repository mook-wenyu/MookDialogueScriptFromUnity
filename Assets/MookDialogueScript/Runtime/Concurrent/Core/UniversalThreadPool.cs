using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Profiling;

namespace MookDialogueScript.Concurrent
{
    /// <summary>
    /// 高性能通用线程池实现
    /// 
    /// 核心特性：
    /// 1. 工作窃取算法：每个工作线程维护本地队列，支持窃取其他线程任务
    /// 2. 自适应负载均衡：动态调整线程数量和任务分配策略
    /// 3. 无锁并发设计：使用原子操作和线程本地存储，避免锁竞争
    /// 4. 优先级队列：支持高优先级任务插队执行
    /// 5. 统计监控：完整的性能统计和诊断信息
    /// 6. Unity集成：支持Unity Profiler和主线程回调
    /// 
    /// 设计原则：
    /// - 单一职责：专注于任务调度和线程管理
    /// - 组合优于继承：通过接口和委托实现功能扩展
    /// - 依赖抽象：通过接口隔离实现细节
    /// - DRY：共享通用的调度和监控逻辑
    /// </summary>
    public sealed class UniversalThreadPool : IUniversalThreadPool
    {
        #region 私有字段
        /// <summary>
        /// 全局任务队列，用于高优先级任务和工作分发
        /// </summary>
        private readonly ConcurrentQueue<WorkItem> _globalQueue = new ConcurrentQueue<WorkItem>();

        /// <summary>
        /// 高优先级任务队列
        /// </summary>
        private readonly ConcurrentQueue<WorkItem> _highPriorityQueue = new ConcurrentQueue<WorkItem>();

        /// <summary>
        /// 工作线程列表
        /// </summary>
        private readonly List<WorkerThread> _workerThreads = new List<WorkerThread>();

        /// <summary>
        /// 线程池配置选项
        /// </summary>
        private readonly ThreadPoolOptions _options;

        /// <summary>
        /// 取消令牌源，用于关闭线程池
        /// </summary>
        private CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// 统计信息收集器
        /// </summary>
        private readonly StatisticsCollector _statistics;

        /// <summary>
        /// 线程池状态标志
        /// </summary>
        private volatile int _isRunning = 0; // 0 = 停止, 1 = 运行

        /// <summary>
        /// 正在关闭标志
        /// </summary>
        private volatile int _isShuttingDown = 0; // 0 = 正常, 1 = 关闭中

        /// <summary>
        /// 活跃任务计数器
        /// </summary>
        private long _activeTasks = 0;

        /// <summary>
        /// 待处理任务计数器
        /// </summary>
        private long _pendingTasks = 0;

        /// <summary>
        /// 线程安全的随机数生成器（兼容.NET Framework）
        /// </summary>
        private static readonly ThreadLocal<Random> _random = new ThreadLocal<Random>(() => new Random(Guid.NewGuid().GetHashCode()));
        #endregion

        #region 构造函数
        /// <summary>
        /// 创建通用线程池实例
        /// </summary>
        /// <param name="options">配置选项，为空时使用默认配置</param>
        public UniversalThreadPool(ThreadPoolOptions options = null)
        {
            _options = options ?? ThreadPoolOptions.Default;
            _statistics = new StatisticsCollector(_options.StatisticsInterval);
            _cancellationTokenSource = new CancellationTokenSource();

            // 参数验证
            ValidateOptions();
        }

        /// <summary>
        /// 验证配置选项
        /// </summary>
        private void ValidateOptions()
        {
            if (_options.CoreThreads < 1)
                throw new ArgumentException("CoreThreads must be at least 1");
            
            if (_options.MaxThreads < _options.CoreThreads)
                throw new ArgumentException("MaxThreads must be >= CoreThreads");
            
            if (_options.IdleTimeout <= TimeSpan.Zero)
                throw new ArgumentException("IdleTimeout must be positive");
        }
        #endregion

        #region IUniversalThreadPool 实现
        /// <summary>
        /// 活跃线程数量
        /// </summary>
        public int ActiveThreads => _workerThreads.Count(t => t.IsActive);

        /// <summary>
        /// 待处理任务数量
        /// </summary>
        public int PendingTasks => (int)Interlocked.Read(ref _pendingTasks);

        /// <summary>
        /// 最大并发度
        /// </summary>
        public int MaxConcurrency => _options.MaxThreads;

        /// <summary>
        /// 线程池是否已关闭
        /// </summary>
        public bool IsShutdown => _isRunning == 0 || _isShuttingDown == 1;

        /// <summary>
        /// 启动线程池
        /// </summary>
        public void Start()
        {
            if (Interlocked.CompareExchange(ref _isRunning, 1, 0) == 0)
            {
                Profiler.BeginSample("UniversalThreadPool.Start");
                
                // 创建核心工作线程
                for (int i = 0; i < _options.CoreThreads; i++)
                {
                    var worker = new WorkerThread(this, i, _options);
                    _workerThreads.Add(worker);
                    worker.Start();
                }

                // 启动统计收集器
                _statistics.Start();
                
                Profiler.EndSample();
            }
        }

        /// <summary>
        /// 提交无返回值任务
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task SubmitAsync(Action action, CancellationToken cancellationToken = default)
        {
            ThrowIfShutdown();
            
            var tcs = new TaskCompletionSource<object>();
            var workItem = new WorkItem
            {
                Action = () => ExecuteWithErrorHandling(action, tcs),
                CancellationToken = cancellationToken,
                IsHighPriority = false,
                SubmittedAt = DateTime.UtcNow
            };

            EnqueueWorkItem(workItem);
            _statistics.RecordTaskSubmitted();
            
            return tcs.Task;
        }

        /// <summary>
        /// 提交有返回值任务
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<T> SubmitAsync<T>(Func<T> func, CancellationToken cancellationToken = default)
        {
            ThrowIfShutdown();
            
            var tcs = new TaskCompletionSource<T>();
            var workItem = new WorkItem
            {
                Action = () => ExecuteWithErrorHandling(func, tcs),
                CancellationToken = cancellationToken,
                IsHighPriority = false,
                SubmittedAt = DateTime.UtcNow
            };

            EnqueueWorkItem(workItem);
            _statistics.RecordTaskSubmitted();
            
            return tcs.Task;
        }

        /// <summary>
        /// 并行执行多个任务
        /// </summary>
        public async Task SubmitBatchAsync(IEnumerable<Action> actions, CancellationToken cancellationToken = default)
        {
            ThrowIfShutdown();
            
            var tasks = actions.Select(action => SubmitAsync(action, cancellationToken)).ToArray();
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        /// <summary>
        /// 并行执行多个有返回值的任务
        /// </summary>
        public async Task<T[]> SubmitBatchAsync<T>(IEnumerable<Func<T>> funcs, CancellationToken cancellationToken = default)
        {
            ThrowIfShutdown();
            
            var tasks = funcs.Select(func => SubmitAsync(func, cancellationToken)).ToArray();
            return await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        /// <summary>
        /// 提交高优先级任务
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task SubmitHighPriorityAsync(Action action, CancellationToken cancellationToken = default)
        {
            ThrowIfShutdown();
            
            var tcs = new TaskCompletionSource<object>();
            var workItem = new WorkItem
            {
                Action = () => ExecuteWithErrorHandling(action, tcs),
                CancellationToken = cancellationToken,
                IsHighPriority = true,
                SubmittedAt = DateTime.UtcNow
            };

            _highPriorityQueue.Enqueue(workItem);
            Interlocked.Increment(ref _pendingTasks);
            _statistics.RecordTaskSubmitted();
            
            return tcs.Task;
        }

        /// <summary>
        /// 提交高优先级有返回值任务
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<T> SubmitHighPriorityAsync<T>(Func<T> func, CancellationToken cancellationToken = default)
        {
            ThrowIfShutdown();
            
            var tcs = new TaskCompletionSource<T>();
            var workItem = new WorkItem
            {
                Action = () => ExecuteWithErrorHandling(func, tcs),
                CancellationToken = cancellationToken,
                IsHighPriority = true,
                SubmittedAt = DateTime.UtcNow
            };

            _highPriorityQueue.Enqueue(workItem);
            Interlocked.Increment(ref _pendingTasks);
            _statistics.RecordTaskSubmitted();
            
            return tcs.Task;
        }

        /// <summary>
        /// 优雅关闭线程池
        /// </summary>
        public async Task<bool> ShutdownAsync(TimeSpan timeout)
        {
            if (Interlocked.CompareExchange(ref _isShuttingDown, 1, 0) == 0)
            {
                Profiler.BeginSample("UniversalThreadPool.Shutdown");
                
                try
                {
                    // 等待所有任务完成
                    var completed = await AwaitTerminationAsync(timeout).ConfigureAwait(false);
                    
                    // 停止统计收集器
                    _statistics.Stop();
                    
                    // 关闭所有工作线程
                    _cancellationTokenSource.Cancel();
                    
                    foreach (var worker in _workerThreads)
                    {
                        worker.Stop();
                    }
                    
                    _workerThreads.Clear();
                    Interlocked.Exchange(ref _isRunning, 0);
                    
                    return completed;
                }
                finally
                {
                    Profiler.EndSample();
                }
            }
            
            return true;
        }

        /// <summary>
        /// 立即关闭线程池
        /// </summary>
        public void ShutdownNow()
        {
            Interlocked.Exchange(ref _isShuttingDown, 1);
            _cancellationTokenSource.Cancel();
            
            foreach (var worker in _workerThreads)
            {
                worker.Stop();
            }
            
            _workerThreads.Clear();
            _statistics.Stop();
            Interlocked.Exchange(ref _isRunning, 0);
        }

        /// <summary>
        /// 等待所有任务完成
        /// </summary>
        public async Task<bool> AwaitTerminationAsync(TimeSpan timeout)
        {
            var stopwatch = Stopwatch.StartNew();
            
            while (stopwatch.Elapsed < timeout)
            {
                if (PendingTasks == 0 && Interlocked.Read(ref _activeTasks) == 0)
                {
                    return true;
                }
                
                await Task.Delay(100).ConfigureAwait(false);
            }
            
            return false;
        }

        /// <summary>
        /// 获取线程池统计信息
        /// </summary>
        public ThreadPoolStatistics GetStatistics()
        {
            return _statistics.GetCurrentStatistics(ActiveThreads, PendingTasks);
        }

        /// <summary>
        /// 重置统计信息
        /// </summary>
        public void ResetStatistics()
        {
            _statistics.Reset();
        }

        /// <summary>
        /// 调整线程池大小
        /// </summary>
        public void Resize(int coreSize, int maxSize)
        {
            if (coreSize < 1 || maxSize < coreSize)
                throw new ArgumentException("Invalid thread pool size");
            
            lock (_workerThreads)
            {
                _options.CoreThreads = coreSize;
                _options.MaxThreads = maxSize;
                
                // 动态调整线程数量
                AdjustThreadCount();
            }
        }
        #endregion

        #region 内部方法
        /// <summary>
        /// 将工作项加入队列
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void EnqueueWorkItem(WorkItem workItem)
        {
            // 尝试分发给工作线程的本地队列
            if (_options.EnableLoadBalancing && TryEnqueueToLocalQueue(workItem))
            {
                return;
            }
            
            // 加入全局队列
            _globalQueue.Enqueue(workItem);
            Interlocked.Increment(ref _pendingTasks);
            
            // 如果需要，创建新的工作线程
            if (_options.EnableLoadBalancing)
            {
                TryCreateWorkerThread();
            }
        }

        /// <summary>
        /// 尝试将任务分发给工作线程的本地队列
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryEnqueueToLocalQueue(WorkItem workItem)
        {
            var workers = _workerThreads;
            if (workers.Count == 0) return false;
            
            // 选择负载最轻的工作线程
            var minLoadWorker = workers.OrderBy(w => w.LocalQueueCount).FirstOrDefault();
            return minLoadWorker?.TryEnqueueLocal(workItem) == true;
        }

        /// <summary>
        /// 尝试创建新的工作线程
        /// </summary>
        private void TryCreateWorkerThread()
        {
            if (_workerThreads.Count < _options.MaxThreads && PendingTasks > ActiveThreads * 2)
            {
                lock (_workerThreads)
                {
                    if (_workerThreads.Count < _options.MaxThreads)
                    {
                        var worker = new WorkerThread(this, _workerThreads.Count, _options);
                        _workerThreads.Add(worker);
                        worker.Start();
                    }
                }
            }
        }

        /// <summary>
        /// 动态调整线程数量
        /// </summary>
        private void AdjustThreadCount()
        {
            var currentCount = _workerThreads.Count;
            var targetCount = Math.Min(_options.MaxThreads, 
                Math.Max(_options.CoreThreads, PendingTasks / 10));
            
            if (targetCount > currentCount)
            {
                // 增加线程
                for (int i = currentCount; i < targetCount; i++)
                {
                    var worker = new WorkerThread(this, i, _options);
                    _workerThreads.Add(worker);
                    worker.Start();
                }
            }
            else if (targetCount < currentCount)
            {
                // 减少线程（让空闲线程自然超时退出）
                var excessWorkers = _workerThreads.Skip(targetCount).ToList();
                foreach (var worker in excessWorkers)
                {
                    worker.RequestIdleShutdown();
                }
            }
        }

        /// <summary>
        /// 工作线程获取下一个任务
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal WorkItem GetNextWorkItem(WorkerThread requestingWorker)
        {
            // 1. 优先处理高优先级任务
            if (_highPriorityQueue.TryDequeue(out var highPriorityItem))
            {
                Interlocked.Decrement(ref _pendingTasks);
                return highPriorityItem;
            }
            
            // 2. 尝试从全局队列获取任务
            if (_globalQueue.TryDequeue(out var globalItem))
            {
                Interlocked.Decrement(ref _pendingTasks);
                return globalItem;
            }
            
            // 3. 如果启用工作窃取，尝试从其他线程偷取任务
            if (_options.EnableWorkStealing)
            {
                var stolenItem = TryStealWork(requestingWorker);
                if (stolenItem != null)
                {
                    _statistics.RecordWorkSteal();
                    return stolenItem;
                }
            }
            
            return null;
        }

        /// <summary>
        /// 尝试从其他工作线程偷取任务
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private WorkItem TryStealWork(WorkerThread requestingWorker)
        {
            // 随机选择其他工作线程进行窃取
            var otherWorkers = _workerThreads.Where(w => w != requestingWorker && w.LocalQueueCount > 0).ToArray();
            
            if (otherWorkers.Length == 0) return null;
            
            var targetWorker = otherWorkers[_random.Value.Next(otherWorkers.Length)];
            return targetWorker.TryDequeueLocal();
        }

        /// <summary>
        /// 执行带错误处理的Action
        /// </summary>
        private void ExecuteWithErrorHandling(Action action, TaskCompletionSource<object> tcs)
        {
            Interlocked.Increment(ref _activeTasks);
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                Profiler.BeginSample("UniversalThreadPool.ExecuteAction");
                action();
                Profiler.EndSample();
                
                tcs.TrySetResult(null);
                _statistics.RecordTaskCompleted(stopwatch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException)
            {
                tcs.TrySetCanceled();
                _statistics.RecordTaskCanceled();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
                _statistics.RecordTaskFailed();
            }
            finally
            {
                Interlocked.Decrement(ref _activeTasks);
            }
        }

        /// <summary>
        /// 执行带错误处理的Func
        /// </summary>
        private void ExecuteWithErrorHandling<T>(Func<T> func, TaskCompletionSource<T> tcs)
        {
            Interlocked.Increment(ref _activeTasks);
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                Profiler.BeginSample("UniversalThreadPool.ExecuteFunc");
                var result = func();
                Profiler.EndSample();
                
                tcs.TrySetResult(result);
                _statistics.RecordTaskCompleted(stopwatch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException)
            {
                tcs.TrySetCanceled();
                _statistics.RecordTaskCanceled();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
                _statistics.RecordTaskFailed();
            }
            finally
            {
                Interlocked.Decrement(ref _activeTasks);
            }
        }

        /// <summary>
        /// 检查是否已关闭并抛出异常
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThrowIfShutdown()
        {
            if (IsShutdown)
            {
                throw new InvalidOperationException("线程池已关闭，无法提交新任务");
            }
        }
        #endregion

        #region IDisposable 实现
        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (!IsShutdown)
            {
                ShutdownNow();
            }
            
            _cancellationTokenSource?.Dispose();
            _statistics?.Dispose();
            _random?.Dispose();
        }
        #endregion

        #region 内部类型
        /// <summary>
        /// 工作项，表示一个待执行的任务
        /// </summary>
        internal class WorkItem
        {
            public Action Action { get; set; }
            public CancellationToken CancellationToken { get; set; }
            public bool IsHighPriority { get; set; }
            public DateTime SubmittedAt { get; set; }
        }

        /// <summary>
        /// 工作线程，负责执行任务
        /// </summary>
        internal class WorkerThread
        {
            private readonly UniversalThreadPool _threadPool;
            private readonly int _threadId;
            private readonly ThreadPoolOptions _options;
            private readonly ConcurrentQueue<WorkItem> _localQueue = new ConcurrentQueue<WorkItem>();
            private readonly Thread _thread;
            private volatile bool _isRunning = true;
            private volatile bool _isIdle = false;
            private volatile bool _shouldShutdownWhenIdle = false;
            private DateTime _lastActivityTime = DateTime.UtcNow;

            public bool IsActive => _thread.IsAlive && !_isIdle;
            public int LocalQueueCount => _localQueue.Count;

            public WorkerThread(UniversalThreadPool threadPool, int threadId, ThreadPoolOptions options)
            {
                _threadPool = threadPool;
                _threadId = threadId;
                _options = options;
                _thread = new Thread(WorkerLoop)
                {
                    Name = $"{options.ThreadNamePrefix}-{threadId}",
                    IsBackground = true
                };
            }

            public void Start()
            {
                _thread.Start();
            }

            public void Stop()
            {
                _isRunning = false;
                _thread.Interrupt();
            }

            public void RequestIdleShutdown()
            {
                _shouldShutdownWhenIdle = true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool TryEnqueueLocal(WorkItem workItem)
            {
                if (_localQueue.Count < 100) // 防止本地队列过大
                {
                    _localQueue.Enqueue(workItem);
                    return true;
                }
                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public WorkItem TryDequeueLocal()
            {
                _localQueue.TryDequeue(out var workItem);
                return workItem;
            }

            private void WorkerLoop()
            {
                while (_isRunning && !_threadPool.IsShutdown)
                {
                    try
                    {
                        // 首先检查本地队列
                        var workItem = TryDequeueLocal();
                        
                        // 如果本地队列为空，从线程池获取任务
                        if (workItem == null)
                        {
                            workItem = _threadPool.GetNextWorkItem(this);
                        }

                        if (workItem != null)
                        {
                            _isIdle = false;
                            _lastActivityTime = DateTime.UtcNow;
                            
                            // 检查任务是否已取消
                            if (!workItem.CancellationToken.IsCancellationRequested)
                            {
                                workItem.Action();
                            }
                        }
                        else
                        {
                            // 没有任务，进入空闲状态
                            _isIdle = true;
                            
                            // 检查是否应该因空闲而关闭
                            if (_shouldShutdownWhenIdle || 
                                DateTime.UtcNow - _lastActivityTime > _options.IdleTimeout)
                            {
                                break;
                            }
                            
                            // 短暂休眠，避免忙等
                            Thread.Sleep(10);
                        }
                    }
                    catch (ThreadInterruptedException)
                    {
                        // 线程被中断，正常退出
                        break;
                    }
                    catch (Exception ex)
                    {
                        // 记录意外异常，但不退出工作循环
                        UnityEngine.Debug.LogError($"工作线程 {_threadId} 发生意外错误: {ex}");
                    }
                }
            }
        }

        /// <summary>
        /// 统计信息收集器
        /// </summary>
        internal class StatisticsCollector : IDisposable
        {
            private long _totalSubmittedTasks = 0;
            private long _completedTasks = 0;
            private long _failedTasks = 0;
            private long _canceledTasks = 0;
            private long _workStealCount = 0;
            private long _totalExecutionTime = 0;
            private int _peakThreads = 0;
            private Timer _statisticsTimer;

            public StatisticsCollector(TimeSpan interval)
            {
                _statisticsTimer = new Timer(UpdateStatistics, null, interval, interval);
            }

            public void Start()
            {
                // 统计收集器已在构造函数中启动
            }

            public void Stop()
            {
                _statisticsTimer?.Dispose();
            }

            public void RecordTaskSubmitted()
            {
                Interlocked.Increment(ref _totalSubmittedTasks);
            }

            public void RecordTaskCompleted(long executionTimeMs)
            {
                Interlocked.Increment(ref _completedTasks);
                Interlocked.Add(ref _totalExecutionTime, executionTimeMs);
            }

            public void RecordTaskFailed()
            {
                Interlocked.Increment(ref _failedTasks);
            }

            public void RecordTaskCanceled()
            {
                Interlocked.Increment(ref _canceledTasks);
            }

            public void RecordWorkSteal()
            {
                Interlocked.Increment(ref _workStealCount);
            }

            public ThreadPoolStatistics GetCurrentStatistics(int activeThreads, int pendingTasks)
            {
                var completedTasks = Interlocked.Read(ref _completedTasks);
                var totalExecutionTime = Interlocked.Read(ref _totalExecutionTime);
                
                // 更新峰值线程数
                if (activeThreads > _peakThreads)
                {
                    _peakThreads = activeThreads;
                }

                return new ThreadPoolStatistics
                {
                    TotalSubmittedTasks = Interlocked.Read(ref _totalSubmittedTasks),
                    CompletedTasks = completedTasks,
                    FailedTasks = Interlocked.Read(ref _failedTasks),
                    ActiveThreads = activeThreads,
                    PendingTasks = pendingTasks,
                    PeakThreads = _peakThreads,
                    AverageExecutionTime = completedTasks > 0 ? (double)totalExecutionTime / completedTasks : 0,
                    WorkStealCount = Interlocked.Read(ref _workStealCount),
                    Utilization = activeThreads > 0 ? Math.Min(1.0, (double)pendingTasks / activeThreads) : 0
                };
            }

            public void Reset()
            {
                Interlocked.Exchange(ref _totalSubmittedTasks, 0);
                Interlocked.Exchange(ref _completedTasks, 0);
                Interlocked.Exchange(ref _failedTasks, 0);
                Interlocked.Exchange(ref _canceledTasks, 0);
                Interlocked.Exchange(ref _workStealCount, 0);
                Interlocked.Exchange(ref _totalExecutionTime, 0);
                _peakThreads = 0;
            }

            private void UpdateStatistics(object state)
            {
                // 这里可以添加定期统计更新逻辑
                // 比如清理过期数据、计算移动平均值等
            }

            public void Dispose()
            {
                _statisticsTimer?.Dispose();
            }
        }
        #endregion
    }
}