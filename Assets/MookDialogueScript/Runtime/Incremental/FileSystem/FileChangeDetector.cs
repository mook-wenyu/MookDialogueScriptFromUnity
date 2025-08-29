using System;
using System.Collections.Generic;

namespace MookDialogueScript.Incremental.FileSystem
{
    /// <summary>
    /// 文件变更类型
    /// </summary>
    public enum FileChangeType
    {
        /// <summary>文件创建</summary>
        Created,
        
        /// <summary>文件修改</summary>
        Changed,
        
        /// <summary>文件修改（别名）</summary>
        Modified = Changed,
        
        /// <summary>文件删除</summary>
        Deleted,
        
        /// <summary>文件重命名</summary>
        Renamed,
        
        /// <summary>文件移动</summary>
        Moved
    }

    /// <summary>
    /// 文件变更事件参数
    /// </summary>
    public class FileChangeEventArgs : EventArgs
    {
        /// <summary>
        /// 文件路径
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// 变更类型
        /// </summary>
        public FileChangeType ChangeType { get; set; }

        /// <summary>
        /// 旧文件路径（用于重命名和移动）
        /// </summary>
        public string OldFilePath { get; set; }

        /// <summary>
        /// 变更时间
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 文件大小
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// 附加信息
        /// </summary>
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// 创建文件变更事件参数
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="changeType">变更类型</param>
        /// <param name="oldFilePath">旧文件路径</param>
        public FileChangeEventArgs(string filePath, FileChangeType changeType, string oldFilePath = null)
        {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            ChangeType = changeType;
            OldFilePath = oldFilePath;
        }

        public override string ToString()
        {
            var info = $"{ChangeType}: {FilePath}";
            if (!string.IsNullOrEmpty(OldFilePath))
            {
                info += $" (from {OldFilePath})";
            }
            return info;
        }
    }

    /// <summary>
    /// 文件变更检测器接口
    /// </summary>
    public interface IFileChangeDetector : IDisposable
    {
        /// <summary>
        /// 是否正在监控
        /// </summary>
        bool IsWatching { get; }

        /// <summary>
        /// 监控的路径
        /// </summary>
        string WatchPath { get; }

        /// <summary>
        /// 文件过滤模式
        /// </summary>
        string FilePattern { get; }

        /// <summary>
        /// 文件变更事件
        /// </summary>
        event EventHandler<FileChangeEventArgs> FileChanged;

        /// <summary>
        /// 开始监控指定路径
        /// </summary>
        /// <param name="path">监控路径</param>
        /// <param name="pattern">文件模式（如 "*.mds"）</param>
        /// <param name="includeSubdirectories">是否包含子目录</param>
        void StartWatching(string path, string pattern = "*.*", bool includeSubdirectories = true);

        /// <summary>
        /// 停止监控
        /// </summary>
        void StopWatching();

        /// <summary>
        /// 检查指定文件是否被监控
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>是否被监控</returns>
        bool IsFileWatched(string filePath);

        /// <summary>
        /// 获取监控统计信息
        /// </summary>
        /// <returns>监控统计</returns>
        FileWatcherStatistics GetStatistics();
    }

    /// <summary>
    /// 文件监控器统计信息
    /// </summary>
    public struct FileWatcherStatistics
    {
        /// <summary>
        /// 监控的文件数量
        /// </summary>
        public int WatchedFileCount { get; set; }

        /// <summary>
        /// 检测到的变更次数
        /// </summary>
        public long ChangeDetectionCount { get; set; }

        /// <summary>
        /// 变更事件触发次数
        /// </summary>
        public long EventTriggerCount { get; set; }

        /// <summary>
        /// 最后一次变更时间
        /// </summary>
        public DateTime LastChangeTime { get; set; }

        /// <summary>
        /// 监控开始时间
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// 错误次数
        /// </summary>
        public long ErrorCount { get; set; }

        public override string ToString()
        {
            return $"FileWatcher: Files={WatchedFileCount}, Changes={ChangeDetectionCount}, " +
                   $"Events={EventTriggerCount}, Errors={ErrorCount}";
        }
    }

    /// <summary>
    /// 默认的文件变更检测器实现
    /// </summary>
    public class DefaultFileChangeDetector : IFileChangeDetector
    {
        private System.IO.FileSystemWatcher _watcher;
        private volatile bool _disposed;
        private FileWatcherStatistics _statistics;

        /// <summary>
        /// 是否正在监控
        /// </summary>
        public bool IsWatching { get; private set; }

        /// <summary>
        /// 监控的路径
        /// </summary>
        public string WatchPath { get; private set; }

        /// <summary>
        /// 文件过滤模式
        /// </summary>
        public string FilePattern { get; private set; }

        /// <summary>
        /// 文件变更事件
        /// </summary>
        public event EventHandler<FileChangeEventArgs> FileChanged;

        /// <summary>
        /// 开始监控指定路径
        /// </summary>
        public void StartWatching(string path, string pattern = "*.*", bool includeSubdirectories = true)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DefaultFileChangeDetector));

            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Path cannot be null or empty", nameof(path));

            if (!System.IO.Directory.Exists(path))
                throw new System.IO.DirectoryNotFoundException($"Directory not found: {path}");

            StopWatching();

            _watcher = new System.IO.FileSystemWatcher(path, pattern)
            {
                IncludeSubdirectories = includeSubdirectories,
                NotifyFilter = System.IO.NotifyFilters.FileName | 
                              System.IO.NotifyFilters.LastWrite | 
                              System.IO.NotifyFilters.CreationTime
            };

            _watcher.Changed += OnFileSystemEvent;
            _watcher.Created += OnFileSystemEvent;
            _watcher.Deleted += OnFileSystemEvent;
            _watcher.Renamed += OnFileRenamed;

            _watcher.EnableRaisingEvents = true;
            IsWatching = true;
            WatchPath = path;
            FilePattern = pattern;
            _statistics.StartTime = DateTime.UtcNow;
        }

        /// <summary>
        /// 停止监控
        /// </summary>
        public void StopWatching()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
                _watcher = null;
            }

            IsWatching = false;
            WatchPath = null;
            FilePattern = null;
        }

        /// <summary>
        /// 检查指定文件是否被监控
        /// </summary>
        public bool IsFileWatched(string filePath)
        {
            if (!IsWatching || string.IsNullOrEmpty(filePath))
                return false;

            try
            {
                var fullPath = System.IO.Path.GetFullPath(filePath);
                var watchPath = System.IO.Path.GetFullPath(WatchPath);
                
                return fullPath.StartsWith(watchPath, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取监控统计信息
        /// </summary>
        public FileWatcherStatistics GetStatistics()
        {
            return _statistics;
        }

        private void OnFileSystemEvent(object sender, System.IO.FileSystemEventArgs e)
        {
            try
            {
                var changeType = e.ChangeType switch
                {
                    System.IO.WatcherChangeTypes.Created => FileChangeType.Created,
                    System.IO.WatcherChangeTypes.Changed => FileChangeType.Changed,
                    System.IO.WatcherChangeTypes.Deleted => FileChangeType.Deleted,
                    _ => FileChangeType.Changed
                };

                var args = new FileChangeEventArgs(e.FullPath, changeType);
                
                _statistics.ChangeDetectionCount++;
                _statistics.LastChangeTime = DateTime.UtcNow;

                FileChanged?.Invoke(this, args);
                _statistics.EventTriggerCount++;
            }
            catch
            {
                _statistics.ErrorCount++;
            }
        }

        private void OnFileRenamed(object sender, System.IO.RenamedEventArgs e)
        {
            try
            {
                var args = new FileChangeEventArgs(e.FullPath, FileChangeType.Renamed, e.OldFullPath);
                
                _statistics.ChangeDetectionCount++;
                _statistics.LastChangeTime = DateTime.UtcNow;

                FileChanged?.Invoke(this, args);
                _statistics.EventTriggerCount++;
            }
            catch
            {
                _statistics.ErrorCount++;
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                StopWatching();
                _disposed = true;
            }
        }
    }
}