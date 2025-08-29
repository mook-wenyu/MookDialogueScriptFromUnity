using System;

namespace MookDialogueScript.Pooling
{
    /// <summary>
    /// 通用作用域池化对象包装器
    /// 替代所有特化的Scoped类（如ScopedLexer、ScopedParser）
    /// 提供统一的自动归还机制，支持using语法
    /// </summary>
    /// <typeparam name="T">被包装的对象类型</typeparam>
    public sealed class ScopedPoolable<T> : IDisposable where T : class
    {
        private T _item;
        private readonly IDisposable _scopeHandler;
        private bool _disposed;

        /// <summary>
        /// 创建作用域池化对象包装器
        /// </summary>
        /// <param name="item">被包装的对象实例</param>
        /// <param name="scopeHandler">作用域处理器，负责自动归还对象</param>
        internal ScopedPoolable(T item, IDisposable scopeHandler)
        {
            _item = item ?? throw new ArgumentNullException(nameof(item));
            _scopeHandler = scopeHandler ?? throw new ArgumentNullException(nameof(scopeHandler));
        }

        /// <summary>
        /// 获取被包装的对象实例
        /// 如果对象已被释放，将抛出ObjectDisposedException
        /// </summary>
        public T Item 
        { 
            get
            {
                ThrowIfDisposed();
                return _item;
            }
        }

        /// <summary>
        /// 隐式转换到原对象类型
        /// 支持直接将ScopedPoolable<T>用作T类型
        /// </summary>
        /// <param name="scoped">作用域包装器</param>
        public static implicit operator T(ScopedPoolable<T> scoped)
        {
            return scoped?.Item;
        }

        /// <summary>
        /// 释放对象并自动归还到池
        /// 实现IDisposable接口，支持using语法
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
                    _scopeHandler?.Dispose();
                }
                catch
                {
                    // 忽略归还过程中的异常，避免Dispose方法抛出异常
                }
                
                _item = null;
                _disposed = true;
            }
        }

        /// <summary>
        /// 检查对象是否已被释放
        /// </summary>
        /// <exception cref="ObjectDisposedException">对象已被释放时抛出</exception>
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException($"ScopedPoolable<{typeof(T).Name}>");
            }
        }
    }
}