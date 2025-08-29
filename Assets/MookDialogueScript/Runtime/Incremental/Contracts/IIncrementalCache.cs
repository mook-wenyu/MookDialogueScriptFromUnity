using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MookDialogueScript.Incremental.Contracts
{
    /// <summary>
    /// 增量缓存主接口
    /// 提供统一的增量缓存管理功能，协调解析结果缓存、变量缓存等子系统
    /// </summary>
    public interface IIncrementalCache : IDisposable
    {
        /// <summary>
        /// 缓存配置选项
        /// </summary>
        IncrementalCacheOptions Options { get; }

        /// <summary>
        /// 文件变更检测器
        /// </summary>
        IFileChangeDetector FileDetector { get; }

        /// <summary>
        /// 解析结果缓存
        /// </summary>
        IParseResultCache ParseResultCache { get; }

        /// <summary>
        /// 变量声明缓存
        /// </summary>
        IVariableDeclarationCache VariableCache { get; }

        /// <summary>
        /// 缓存统计信息
        /// </summary>
        CacheStatistics Statistics { get; }

        /// <summary>
        /// 缓存变更事件
        /// 当任何缓存项发生变更时触发
        /// </summary>
        event EventHandler<CacheChangedEventArgs> CacheChanged;

        /// <summary>
        /// 初始化缓存系统
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>初始化任务</returns>
        Task InitializeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 检查并刷新指定文件的缓存
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="forceRefresh">是否强制刷新</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>刷新任务</returns>
        Task<bool> RefreshFileAsync(string filePath, bool forceRefresh = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// 批量刷新多个文件的缓存
        /// </summary>
        /// <param name="filePaths">文件路径集合</param>
        /// <param name="forceRefresh">是否强制刷新</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>刷新结果字典</returns>
        Task<Dictionary<string, bool>> BatchRefreshAsync(
            IEnumerable<string> filePaths, 
            bool forceRefresh = false, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 清理过期的缓存项
        /// </summary>
        /// <param name="maxAge">最大缓存时间</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>清理的项数</returns>
        Task<int> CleanupExpiredAsync(TimeSpan maxAge, CancellationToken cancellationToken = default);

        /// <summary>
        /// 清空所有缓存
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>清空任务</returns>
        Task ClearAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 预热缓存（预加载常用文件）
        /// </summary>
        /// <param name="filePaths">要预热的文件路径集合</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>预热任务</returns>
        Task WarmupAsync(IEnumerable<string> filePaths, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取缓存使用报告
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>缓存报告</returns>
        Task<CacheReport> GenerateReportAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 保存缓存到持久化存储
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>保存任务</returns>
        Task SaveAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 从持久化存储加载缓存
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>加载任务</returns>
        Task LoadAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 检查缓存的完整性
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>完整性检查结果</returns>
        Task<CacheIntegrityResult> ValidateIntegrityAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 缓存变更事件参数
    /// </summary>
    public class CacheChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 变更的文件路径
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// 变更类型
        /// </summary>
        public CacheChangeType ChangeType { get; }

        /// <summary>
        /// 变更时间
        /// </summary>
        public DateTime ChangeTime { get; }

        /// <summary>
        /// 受影响的缓存类型
        /// </summary>
        public CacheItemType ItemType { get; }

        public CacheChangedEventArgs(string filePath, CacheChangeType changeType, 
            CacheItemType itemType, DateTime changeTime)
        {
            FilePath = filePath;
            ChangeType = changeType;
            ItemType = itemType;
            ChangeTime = changeTime;
        }
    }

    /// <summary>
    /// 缓存变更类型
    /// </summary>
    public enum CacheChangeType
    {
        /// <summary>
        /// 缓存项已添加
        /// </summary>
        Added,

        /// <summary>
        /// 缓存项已更新
        /// </summary>
        Updated,

        /// <summary>
        /// 缓存项已移除
        /// </summary>
        Removed,

        /// <summary>
        /// 缓存已清空
        /// </summary>
        Cleared
    }

    /// <summary>
    /// 缓存项类型
    /// </summary>
    public enum CacheItemType
    {
        /// <summary>
        /// 解析结果
        /// </summary>
        ParseResult,

        /// <summary>
        /// 变量声明
        /// </summary>
        VariableDeclaration,

        /// <summary>
        /// 文件元数据
        /// </summary>
        FileMetadata
    }
}