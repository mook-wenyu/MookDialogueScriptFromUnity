using System;

namespace MookDialogueScript.Pooling
{
    /// <summary>
    /// 可池化对象接口
    /// 定义对象池化的生命周期管理
    /// </summary>
    public interface IPoolable : IDisposable
    {
        /// <summary>
        /// 重置对象状态（归还到池时调用）
        /// </summary>
        void OnReturnToPool();
        
        /// <summary>
        /// 对象从池中取出时调用
        /// </summary>
        void OnRentFromPool();
        
        /// <summary>
        /// 检查对象是否可以归还到池
        /// </summary>
        bool CanReturnToPool { get; }
    }
}