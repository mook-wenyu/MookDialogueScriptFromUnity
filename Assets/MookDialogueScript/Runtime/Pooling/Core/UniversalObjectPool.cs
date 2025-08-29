using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Runtime.CompilerServices;
using Unity.Profiling;

namespace MookDialogueScript.Pooling
{
    /// <summary>
    /// 通用高性能对象池
    /// 合并原有ConcurrentLexerPool和LexerPoolManager的功能
    /// 支持任何类型的对象池化管理
    /// </summary>
    /// <typeparam name="T">池化对象类型</typeparam>
    public class UniversalObjectPool<T> : IObjectPool<T> where T : class
    {
        #region 字段
        // 主池 - 使用ConcurrentBag实现无锁并发
        private readonly ConcurrentBag<T> _pool;

        // 线程本地缓存
        [ThreadStatic]
        private static Stack<T> _threadLocalPool;

        // 配置选项
        private readonly PoolOptions _options;

        // 对象工厂
        private readonly Func<T> _factory;
        private readonly Action<T> _resetAction;

        // 性能计数器
        private int _totalCreated;
        private int _totalBorrowed;
        private int _totalReturned;
        private int _totalRecycled;
        private int _currentActive;
        private int _peakActive;

        // 自适应调整
        private readonly Timer _trimTimer;
        private volatile bool _disposed;

        // Unity Profiler标记
        private readonly ProfilerMarker _rentMarker;
        private readonly ProfilerMarker _returnMarker;
        private readonly ProfilerMarker _createMarker;

        // 统计信息
        private PoolStatistics _statistics;
        #endregion

        #region 构造函数
        /// <summary>
        /// 创建通用对象池
        /// </summary>
        /// <param name="factory">对象创建工厂</param>
        /// <param name="resetAction">对象重置操作（可选）</param>
        /// <param name="options">池配置选项（可选）</param>
        public UniversalObjectPool(
            Func<T> factory, 
            Action<T> resetAction = null, 
            PoolOptions options = null)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _resetAction = resetAction;
            _options = options ?? PoolOptions.Default;

            _pool = new ConcurrentBag<T>();

            // 初始化统计信息
            InitializeStatistics();

            // 创建Unity Profiler标记
            var typeName = typeof(T).Name;
            _rentMarker = new ProfilerMarker($"ObjectPool<{typeName}>.Rent");
            _returnMarker = new ProfilerMarker($"ObjectPool<{typeName}>.Return");
            _createMarker = new ProfilerMarker($"ObjectPool<{typeName}>.Create");

            // 预热池
            if (_options.PrewarmPool)
            {
                WarmUp();
            }

            // 启动自动调整定时器
            if (_options.EnableAutoTrim)
            {
                _trimTimer = new Timer(
                    TrimCallback,
                    null,
                    _options.TrimInterval,
                    _options.TrimInterval
                );
            }
        }
        #endregion

        #region IObjectPool<T> 实现
        /// <summary>
        /// 租借对象
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Rent()
        {
            ThrowIfDisposed();

            using (_rentMarker.Auto())
            {
                T item = null;

                // 1. 尝试从线程本地缓存获取
                if (_options.EnableThreadLocalCache)
                {
                    item = TryGetFromThreadLocal();
                }

                // 2. 从主池获取
                if (item == null && _pool.TryTake(out item))
                {
                    // 从主池成功获取
                }

                // 3. 创建新实例
                if (item == null)
                {
                    using (_createMarker.Auto())
                    {
                        item = _factory();
                        Interlocked.Increment(ref _totalCreated);
                    }
                }

                // 更新统计
                int active = Interlocked.Increment(ref _currentActive);
                Interlocked.Increment(ref _totalBorrowed);

                // 更新峰值
                UpdatePeakActive(active);

                // 调用IPoolable接口方法
                if (item is IPoolable poolable)
                {
                    poolable.OnRentFromPool();
                }

                return item;
            }
        }

        /// <summary>
        /// 归还对象
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Return(T item)
        {
            if (item == null) return;
            ThrowIfDisposed();

            using (_returnMarker.Auto())
            {
                // 检查对象是否可以归还
                if (item is IPoolable poolable && !poolable.CanReturnToPool)
                {
                    Interlocked.Increment(ref _totalRecycled);
                    return;
                }

                // 重置对象状态
                if (item is IPoolable poolableReset)
                {
                    poolableReset.OnReturnToPool();
                }
                else
                {
                    _resetAction?.Invoke(item);
                }

                // 更新统计
                Interlocked.Decrement(ref _currentActive);
                Interlocked.Increment(ref _totalReturned);

                // 检查池大小限制
                if (_pool.Count >= _options.MaxSize)
                {
                    Interlocked.Increment(ref _totalRecycled);
                    return;
                }

                // 优先返回到线程本地池
                if (_options.EnableThreadLocalCache && TryReturnToThreadLocal(item))
                {
                    return;
                }

                // 返回到主池
                _pool.Add(item);
            }
        }

        /// <summary>
        /// 批量租借
        /// </summary>
        public T[] RentBatch(int count)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            ThrowIfDisposed();

            var items = new T[count];
            for (int i = 0; i < count; i++)
            {
                items[i] = Rent();
            }
            return items;
        }

        /// <summary>
        /// 批量归还
        /// </summary>
        public void ReturnBatch(T[] items)
        {
            if (items == null) return;

            foreach (var item in items)
            {
                Return(item);
            }
        }

        /// <summary>
        /// 创建作用域对象（使用新的通用包装器）
        /// 提供更好的类型安全性和API一致性
        /// </summary>
        public ScopedPoolable<T> RentScopedPoolable()
        {
            var item = Rent();
            var scopeHandler = new PooledObject<T>(item, this);
            return new ScopedPoolable<T>(item, scopeHandler);
        }

        /// <summary>
        /// 创建作用域对象（原有API，保持兼容性）
        /// </summary>
        public IDisposable RentScoped(out T item)
        {
            item = Rent();
            return new PooledObject<T>(item, this);
        }

        /// <summary>
        /// 当前池大小
        /// </summary>
        public int PoolSize => _pool.Count;

        /// <summary>
        /// 活跃对象数量
        /// </summary>
        public int ActiveCount => _currentActive;

        /// <summary>
        /// 调整池大小
        /// </summary>
        public void Trim()
        {
            int currentActive = _currentActive;
            int currentPoolSize = _pool.Count;

            // 计算目标大小
            int targetSize = (int)(currentActive * _options.TargetPoolSizeRatio);
            targetSize = Math.Max(targetSize, _options.InitialSize);
            targetSize = Math.Min(targetSize, _options.MaxSize);

            // 移除多余对象
            int toRemove = currentPoolSize - targetSize;
            if (toRemove > 0)
            {
                for (int i = 0; i < toRemove; i++)
                {
                    if (_pool.TryTake(out var item))
                    {
                        if (item is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }
                        Interlocked.Increment(ref _totalRecycled);
                    }
                }
            }
        }

        /// <summary>
        /// 清空池
        /// </summary>
        public void Clear()
        {
            while (_pool.TryTake(out var item))
            {
                if (item is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                Interlocked.Increment(ref _totalRecycled);
            }
        }

        /// <summary>
        /// 获取统计信息
        /// </summary>
        public PoolStatistics GetStatistics()
        {
            _statistics.PoolSize = _pool.Count;
            _statistics.ActiveCount = _currentActive;
            _statistics.PeakActiveCount = _peakActive;
            _statistics.TotalCreated = _totalCreated;
            _statistics.TotalBorrowed = _totalBorrowed;
            _statistics.TotalReturned = _totalReturned;
            _statistics.TotalRecycled = _totalRecycled;
            _statistics.LastAccessAt = DateTime.UtcNow;
            _statistics.UpdateHitRate();

            return _statistics;
        }
        #endregion

        #region 线程本地缓存
        private T TryGetFromThreadLocal()
        {
            var localPool = _threadLocalPool;
            if (localPool == null)
            {
                _threadLocalPool = localPool = new Stack<T>(_options.ThreadLocalCacheSize);
            }

            return localPool.Count > 0 ? localPool.Pop() : null;
        }

        private bool TryReturnToThreadLocal(T item)
        {
            var localPool = _threadLocalPool;
            if (localPool == null)
            {
                _threadLocalPool = localPool = new Stack<T>(_options.ThreadLocalCacheSize);
            }

            if (localPool.Count < _options.ThreadLocalCacheSize)
            {
                localPool.Push(item);
                return true;
            }

            return false;
        }
        #endregion

        #region 私有辅助方法
        private void InitializeStatistics()
        {
            _statistics = new PoolStatistics
            {
                TypeName = typeof(T).Name,
                PoolName = _options.ToString(),
                CreatedAt = DateTime.UtcNow,
                LastAccessAt = DateTime.UtcNow
            };
        }

        private void WarmUp()
        {
            var warmUpCount = Math.Min(_options.InitialSize, _options.MaxSize);
            for (int i = 0; i < warmUpCount; i++)
            {
                var item = _factory();
                _pool.Add(item);
                Interlocked.Increment(ref _totalCreated);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdatePeakActive(int currentActive)
        {
            int peak;
            do
            {
                peak = _peakActive;
                if (currentActive <= peak) break;
            } while (Interlocked.CompareExchange(ref _peakActive, currentActive, peak) != peak);
        }

        private void TrimCallback(object state)
        {
            if (!_disposed)
            {
                try
                {
                    Trim();
                }
                catch
                {
                    // 忽略自动调整过程中的异常
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException($"UniversalObjectPool<{typeof(T).Name}>");
            }
        }
        #endregion

        #region IDisposable 实现
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _trimTimer?.Dispose();

                    // 清理主池
                    while (_pool.TryTake(out var item))
                    {
                        if (item is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }
                    }

                    // 清理线程本地池
                    var localPool = _threadLocalPool;
                    if (localPool != null)
                    {
                        while (localPool.Count > 0)
                        {
                            var item = localPool.Pop();
                            if (item is IDisposable disposable)
                            {
                                disposable.Dispose();
                            }
                        }
                        _threadLocalPool = null;
                    }
                }

                _disposed = true;
            }
        }
        #endregion
    }
}