using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MookDialogueScript.Incremental.Contracts
{
    /// <summary>
    /// 文件变更检测器接口
    /// 负责检测文件系统中对话脚本文件的变更状态
    /// </summary>
    public interface IFileChangeDetector : IDisposable
    {
        /// <summary>
        /// 文件变更事件
        /// 当检测到文件变更时触发
        /// </summary>
        event EventHandler<FileChangedEventArgs> FileChanged;

        /// <summary>
        /// 开始监控指定目录或文件
        /// </summary>
        /// <param name="path">要监控的路径</param>
        /// <param name="includeSubdirectories">是否包含子目录</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>监控任务</returns>
        Task StartWatchingAsync(string path, bool includeSubdirectories = true, CancellationToken cancellationToken = default);

        /// <summary>
        /// 停止监控
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>停止任务</returns>
        Task StopWatchingAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 检查单个文件是否已变更
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="lastModified">上次修改时间</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否已变更</returns>
        Task<bool> IsFileChangedAsync(string filePath, DateTime lastModified, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取文件元数据
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>文件元数据</returns>
        Task<FileMetadata> GetFileMetadataAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// 批量检查多个文件的变更状态
        /// </summary>
        /// <param name="fileInfos">文件信息集合</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>变更状态字典</returns>
        Task<Dictionary<string, bool>> BatchCheckFilesAsync(
            IEnumerable<(string filePath, DateTime lastModified)> fileInfos, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 当前是否正在监控
        /// </summary>
        bool IsWatching { get; }

        /// <summary>
        /// 当前监控的路径
        /// </summary>
        string WatchingPath { get; }
    }

    /// <summary>
    /// 文件变更事件参数
    /// </summary>
    public class FileChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 变更的文件路径
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// 变更类型
        /// </summary>
        public FileChangeType ChangeType { get; }

        /// <summary>
        /// 变更时间
        /// </summary>
        public DateTime ChangeTime { get; }

        /// <summary>
        /// 旧的文件元数据（如果可用）
        /// </summary>
        public FileMetadata OldMetadata { get; }

        /// <summary>
        /// 新的文件元数据
        /// </summary>
        public FileMetadata NewMetadata { get; }

        public FileChangedEventArgs(string filePath, FileChangeType changeType, DateTime changeTime, 
            FileMetadata oldMetadata = null, FileMetadata newMetadata = null)
        {
            FilePath = filePath;
            ChangeType = changeType;
            ChangeTime = changeTime;
            OldMetadata = oldMetadata;
            NewMetadata = newMetadata;
        }
    }

    /// <summary>
    /// 文件变更类型
    /// </summary>
    public enum FileChangeType
    {
        /// <summary>
        /// 文件已创建
        /// </summary>
        Created,

        /// <summary>
        /// 文件已修改
        /// </summary>
        Modified,

        /// <summary>
        /// 文件已删除
        /// </summary>
        Deleted,

        /// <summary>
        /// 文件已重命名
        /// </summary>
        Renamed
    }
}