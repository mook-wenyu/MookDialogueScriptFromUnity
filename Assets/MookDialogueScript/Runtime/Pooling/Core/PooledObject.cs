using System;

namespace MookDialogueScript.Pooling
{
    /// <summary>
    /// 池化对象包装器
    /// 自动管理对象的生命周期，支持using语法
    /// </summary>
    /// <typeparam name="T">池化对象类型</typeparam>
    public struct PooledObject<T> : IDisposable where T : class
    {
        private T _item;
        private readonly IObjectPool<T> _pool;

        /// <summary>
        /// 创建池化对象包装器
        /// </summary>
        internal PooledObject(T item, IObjectPool<T> pool)
        {
            _item = item;
            _pool = pool;
        }

        /// <summary>
        /// 获取被包装的对象
        /// </summary>
        public T Item => _item;

        /// <summary>
        /// 隐式转换到原对象类型
        /// </summary>
        public static implicit operator T(PooledObject<T> pooled) => pooled._item;

        /// <summary>
        /// 释放对象并归还到池
        /// </summary>
        public void Dispose()
        {
            if (_item != null && _pool != null)
            {
                _pool.Return(_item);
                _item = null;
            }
        }
    }
}