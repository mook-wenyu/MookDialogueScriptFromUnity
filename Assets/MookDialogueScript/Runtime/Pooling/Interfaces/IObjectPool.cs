using System;

namespace MookDialogueScript.Pooling
{
    /// <summary>
    /// 通用对象池接口
    /// 支持任何类型对象的池化管理
    /// </summary>
    /// <typeparam name="T">池化对象类型</typeparam>
    public interface IObjectPool<T> : IDisposable where T : class
    {
        /// <summary>
        /// 租借对象
        /// </summary>
        T Rent();
        
        /// <summary>
        /// 归还对象
        /// </summary>
        void Return(T item);
        
        /// <summary>
        /// 批量租借
        /// </summary>
        T[] RentBatch(int count);
        
        /// <summary>
        /// 批量归还
        /// </summary>
        void ReturnBatch(T[] items);
        
        /// <summary>
        /// 创建作用域对象（自动归还）
        /// </summary>
        IDisposable RentScoped(out T item);
        
        /// <summary>
        /// 当前池大小
        /// </summary>
        int PoolSize { get; }
        
        /// <summary>
        /// 活跃对象数量
        /// </summary>
        int ActiveCount { get; }
        
        /// <summary>
        /// 调整池大小
        /// </summary>
        void Trim();
        
        /// <summary>
        /// 清空池
        /// </summary>
        void Clear();
        
        /// <summary>
        /// 获取统计信息
        /// </summary>
        PoolStatistics GetStatistics();
    }
}