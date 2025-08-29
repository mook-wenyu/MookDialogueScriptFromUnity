using System;
using System.Collections.Generic;

namespace MookDialogueScript.Pooling
{
    /// <summary>
    /// 池管理器接口
    /// 负责管理多个对象池和全局池策略
    /// </summary>
    public interface IPoolManager : IDisposable
    {
        /// <summary>
        /// 获取或创建指定类型的池
        /// </summary>
        IObjectPool<T> GetOrCreatePool<T>(string name = null, PoolOptions options = null) 
            where T : class, new();
            
        /// <summary>
        /// 获取指定类型的池
        /// </summary>
        IObjectPool<T> GetPool<T>(string name = null) where T : class;
        
        /// <summary>
        /// 移除指定池
        /// </summary>
        bool RemovePool<T>(string name = null) where T : class;
        
        /// <summary>
        /// 获取所有池的统计信息
        /// </summary>
        Dictionary<string, PoolStatistics> GetAllStatistics();
        
        /// <summary>
        /// 调整所有池的大小
        /// </summary>
        void TrimAll();
        
        /// <summary>
        /// 清空所有池
        /// </summary>
        void ClearAll();
        
        /// <summary>
        /// 获取池数量
        /// </summary>
        int PoolCount { get; }
    }
}