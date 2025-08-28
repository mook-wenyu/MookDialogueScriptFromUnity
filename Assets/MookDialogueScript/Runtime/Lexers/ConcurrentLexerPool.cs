using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Runtime.CompilerServices;
using Unity.Profiling;
using UnityEngine.Profiling;

namespace MookDialogueScript.Lexers
{
    /// <summary>
    /// 高性能并发词法分析器对象池
    /// 设计原则：
    /// 1. 无锁设计，使用ConcurrentBag实现高并发
    /// 2. 线程本地存储优化，减少竞争
    /// 3. 自适应扩容和收缩
    /// 4. Unity Profiler集成
    /// </summary>
    public class ConcurrentLexerPool : IDisposable
    {
        #region 字段
        // 主池 - 使用ConcurrentBag实现无锁并发
        private readonly ConcurrentBag<Lexer> _pool;

        // 线程本地缓存 - 每个线程维护自己的小池
        [ThreadStatic]
        private static Stack<Lexer> _threadLocalPool;

        private readonly ConcurrentLexerPoolOptions _options;

        // 性能计数器 - 使用原子操作
        private int _totalCreated;
        private int _totalBorrowed;
        private int _totalReturned;
        private int _totalRecycled;
        private int _currentActive;
        private int _peakActive;

        // 共享组件
        private readonly CharacterClassifier _sharedClassifier;

        // 自适应调整
        private readonly Timer _trimTimer;
        private volatile bool _disposed;

        // Unity Profiler采样器
        private static readonly ProfilerMarker _rentMarker = new ProfilerMarker("LexerPool.Rent");
        private static readonly ProfilerMarker _returnMarker = new ProfilerMarker("LexerPool.Return");
        private static readonly ProfilerMarker _createMarker = new ProfilerMarker("LexerPool.Create");
        #endregion

        #region 构造函数
        /// <summary>
        /// 使用默认选项创建并发对象池
        /// </summary>
        public ConcurrentLexerPool() : this(ConcurrentLexerPoolOptions.Default)
        {
        }

        /// <summary>
        /// 使用指定选项创建并发对象池
        /// </summary>
        public ConcurrentLexerPool(ConcurrentLexerPoolOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _pool = new ConcurrentBag<Lexer>();

            // 创建共享的线程安全组件
            _sharedClassifier = LexerFactory.CreateCharacterClassifier();

            // 预热池
            WarmUp();

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

        #region 核心API
        /// <summary>
        /// 租借一个Lexer实例（线程安全）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Lexer Rent()
        {
            ThrowIfDisposed();

            using (_rentMarker.Auto())
            {
                Lexer lexer = null;

                // 1. 首先尝试从线程本地池获取
                if (_options.EnableThreadLocalCache)
                {
                    lexer = TryGetFromThreadLocal();
                }

                // 2. 从主池获取
                if (lexer == null && _pool.TryTake(out lexer))
                {
                    // 成功从主池获取
                }

                // 3. 创建新实例
                if (lexer == null)
                {
                    using (_createMarker.Auto())
                    {
                        lexer = CreateLexer();
                        Interlocked.Increment(ref _totalCreated);
                    }
                }

                // 更新统计
                int active = Interlocked.Increment(ref _currentActive);
                Interlocked.Increment(ref _totalBorrowed);

                // 更新峰值（使用CAS操作）
                int peak;
                do
                {
                    peak = _peakActive;
                    if (active <= peak) break;
                } while (Interlocked.CompareExchange(ref _peakActive, active, peak) != peak);

                return lexer;
            }
        }

        /// <summary>
        /// 归还Lexer实例（线程安全）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Return(Lexer lexer)
        {
            if (lexer == null) return;

            ThrowIfDisposed();

            using (_returnMarker.Auto())
            {
                // 重置状态
                lexer.Reset(string.Empty);

                // 更新统计
                Interlocked.Decrement(ref _currentActive);
                Interlocked.Increment(ref _totalReturned);

                // 检查池大小
                if (_pool.Count >= _options.MaxSize)
                {
                    // 池已满，回收对象
                    Interlocked.Increment(ref _totalRecycled);
                    return;
                }

                // 优先返回到线程本地池
                if (_options.EnableThreadLocalCache && TryReturnToThreadLocal(lexer))
                {
                    return;
                }

                // 返回到主池
                _pool.Add(lexer);
            }
        }

        /// <summary>
        /// 租借多个Lexer实例（批处理优化）
        /// </summary>
        public Lexer[] RentBatch(int count)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));

            var lexers = new Lexer[count];
            for (int i = 0; i < count; i++)
            {
                lexers[i] = Rent();
            }
            return lexers;
        }

        /// <summary>
        /// 批量归还Lexer实例
        /// </summary>
        public void ReturnBatch(Lexer[] lexers)
        {
            if (lexers == null) return;

            foreach (var lexer in lexers)
            {
                Return(lexer);
            }
        }

        /// <summary>
        /// 创建一个自动归还的Lexer作用域
        /// </summary>
        public PooledConcurrentLexer RentScoped(string source = null)
        {
            var lexer = Rent();
            if (!string.IsNullOrEmpty(source))
            {
                lexer.Reset(source);
            }
            return new PooledConcurrentLexer(lexer, this);
        }
        #endregion

        #region 线程本地缓存
        private Lexer TryGetFromThreadLocal()
        {
            var localPool = _threadLocalPool;
            if (localPool == null)
            {
                _threadLocalPool = localPool = new Stack<Lexer>(_options.ThreadLocalCacheSize);
            }

            return localPool.Count > 0 ? localPool.Pop() : null;
        }

        private bool TryReturnToThreadLocal(Lexer lexer)
        {
            var localPool = _threadLocalPool;
            if (localPool == null)
            {
                _threadLocalPool = localPool = new Stack<Lexer>(_options.ThreadLocalCacheSize);
            }

            if (localPool.Count < _options.ThreadLocalCacheSize)
            {
                localPool.Push(lexer);
                return true;
            }

            return false;
        }
        #endregion

        #region 池管理
        private void WarmUp()
        {
            var warmUpCount = Math.Min(_options.InitialSize, _options.MaxSize);
            for (int i = 0; i < warmUpCount; i++)
            {
                _pool.Add(CreateLexer());
                Interlocked.Increment(ref _totalCreated);
            }
        }

        private Lexer CreateLexer()
        {
            // 某些组件可以在线程间共享（如果它们是无状态或线程安全的）
            return new Lexer(
                LexerFactory.CreateCharacterStream(),
                _sharedClassifier, // 共享分类器
                LexerFactory.CreateLexerState(),
                LexerFactory.CreateIndentationHandler(),
                LexerFactory.CreateTokenizers()
            );
        }

        private void TrimCallback(object state)
        {
            if (_disposed) return;

            try
            {
                Trim();
            }
            catch
            {
                // 忽略调整过程中的异常
            }
        }

        /// <summary>
        /// 根据使用情况自动调整池大小
        /// </summary>
        public void Trim()
        {
            int currentActive = _currentActive;
            int currentPoolSize = _pool.Count;

            // 计算目标大小
            int targetSize = (int)(currentActive * _options.TargetPoolSizeRatio);
            targetSize = Math.Max(targetSize, _options.InitialSize);
            targetSize = Math.Min(targetSize, _options.MaxSize);

            // 如果池太大，移除一些对象
            int toRemove = currentPoolSize - targetSize;
            if (toRemove > 0)
            {
                for (int i = 0; i < toRemove; i++)
                {
                    if (_pool.TryTake(out _))
                    {
                        Interlocked.Increment(ref _totalRecycled);
                    }
                }
            }
        }

        /// <summary>
        /// 清空池中所有对象
        /// </summary>
        public void Clear()
        {
            while (_pool.TryTake(out _))
            {
                Interlocked.Increment(ref _totalRecycled);
            }
        }
        #endregion

        #region 统计和监控
        /// <summary>
        /// 获取池统计信息（线程安全）
        /// </summary>
        public ConcurrentLexerPoolStatistics GetStatistics()
        {
            int totalBorrowed = _totalBorrowed;
            int totalCreated = _totalCreated;

            return new ConcurrentLexerPoolStatistics
            {
                PoolSize = _pool.Count,
                ActiveCount = _currentActive,
                PeakActiveCount = _peakActive,
                TotalCreated = totalCreated,
                TotalBorrowed = totalBorrowed,
                TotalReturned = _totalReturned,
                TotalRecycled = _totalRecycled,
                HitRate = totalBorrowed > 0
                    ? (float)(totalBorrowed - totalCreated) / totalBorrowed
                    : 0f,
                ThreadCount = Environment.ProcessorCount // 估算值
            };
        }

        /// <summary>
        /// 当前池中可用的对象数量
        /// </summary>
        public int AvailableCount => _pool.Count;

        /// <summary>
        /// 当前活跃的对象数量
        /// </summary>
        public int ActiveCount => _currentActive;
        #endregion

        #region IDisposable
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing)
        {
            if (_disposed) return;

            _disposed = true;

            if (disposing)
            {
                _trimTimer?.Dispose();

                // 清理池中所有Lexer实例
                while (_pool.TryTake(out var lexer))
                {
                    lexer.Dispose();
                }

                // 清理线程本地池（如果存在）
                var localPool = _threadLocalPool;
                if (localPool != null)
                {
                    while (localPool.Count > 0)
                    {
                        var lexer = localPool.Pop();
                        lexer.Dispose();
                    }
                    _threadLocalPool = null;
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }
        #endregion
    }

    /// <summary>
    /// 并发池配置选项
    /// </summary>
    public class ConcurrentLexerPoolOptions
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
        public int TrimInterval { get; set; } = 120000; // 120秒

        /// <summary>
        /// 目标池大小比率（相对于活跃对象数）
        /// </summary>
        public float TargetPoolSizeRatio { get; set; } = 1.2f;

        /// <summary>
        /// 默认配置
        /// </summary>
        public static ConcurrentLexerPoolOptions Default => new();
    }

    /// <summary>
    /// 并发池统计信息
    /// </summary>
    public struct ConcurrentLexerPoolStatistics
    {
        public int PoolSize { get; set; }
        public int ActiveCount { get; set; }
        public int PeakActiveCount { get; set; }
        public int TotalCreated { get; set; }
        public int TotalBorrowed { get; set; }
        public int TotalReturned { get; set; }
        public int TotalRecycled { get; set; }
        public float HitRate { get; set; }
        public int ThreadCount { get; set; }

        public override string ToString()
        {
            return $"ConcurrentPool[Size={PoolSize}, Active={ActiveCount}/{PeakActiveCount}, " +
                   $"Created={TotalCreated}, Hit={HitRate:P2}, Threads={ThreadCount}]";
        }
    }

    /// <summary>
    /// 自动归还的并发Lexer包装器
    /// </summary>
    public struct PooledConcurrentLexer : IDisposable
    {
        private Lexer _lexer;
        private readonly ConcurrentLexerPool _pool;

        internal PooledConcurrentLexer(Lexer lexer, ConcurrentLexerPool pool)
        {
            _lexer = lexer;
            _pool = pool;
        }

        public Lexer Lexer => _lexer;

        public static implicit operator Lexer(PooledConcurrentLexer pooled) => pooled._lexer;

        public void Dispose()
        {
            if (_lexer != null && _pool != null)
            {
                _pool.Return(_lexer);
                _lexer = null;
            }
        }
    }
}
