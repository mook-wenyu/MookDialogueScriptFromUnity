using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MookDialogueScript.Incremental.Contracts;

namespace MookDialogueScript.Incremental.FileSystem
{
    /// <summary>
    /// 文件变更检测器实现
    /// 基于FileSystemWatcher实现文件系统监控，支持防抖动和批量处理
    /// </summary>
    public sealed class FileChangeDetector : IFileChangeDetector
    {
        #region 字段
        private readonly IncrementalCacheOptions _options;
        private FileSystemWatcher _watcher;
        private readonly Dictionary<string, FileMetadata> _fileMetadataCache;
        private readonly Dictionary<string, DateTime> _pendingChanges;
        private readonly Timer _debounceTimer;
        private readonly object _lock = new();
        private volatile bool _disposed;
        private volatile bool _isWatching;
        #endregion

        #region 属性
        /// <summary>
        /// 当前是否正在监控
        /// </summary>
        public bool IsWatching => _isWatching && !_disposed;

        /// <summary>
        /// 当前监控的路径
        /// </summary>
        public string WatchingPath { get; private set; }
        #endregion

        #region 事件
        /// <summary>
        /// 文件变更事件
        /// </summary>
        public event EventHandler<FileChangedEventArgs> FileChanged;
        #endregion

        #region 构造函数
        /// <summary>
        /// 初始化文件变更检测器
        /// </summary>
        /// <param name="options">缓存配置选项</param>
        public FileChangeDetector(IncrementalCacheOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _fileMetadataCache = new Dictionary<string, FileMetadata>(StringComparer.OrdinalIgnoreCase);
            _pendingChanges = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            
            // 初始化防抖动定时器
            _debounceTimer = new Timer(ProcessPendingChanges, null, Timeout.Infinite, Timeout.Infinite);
        }
        #endregion

        #region IFileChangeDetector 实现
        /// <summary>
        /// 开始监控指定目录或文件
        /// </summary>
        /// <param name="path">要监控的路径</param>
        /// <param name="includeSubdirectories">是否包含子目录</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>监控任务</returns>
        public async Task StartWatchingAsync(string path, bool includeSubdirectories = true, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("路径不能为空", nameof(path));

            if (!Directory.Exists(path) && !File.Exists(path))
                throw new DirectoryNotFoundException($"路径不存在: {path}");

            await Task.Run(() =>
            {
                lock (_lock)
                {
                    // 停止当前监控
                    StopWatchingInternal();

                    try
                    {
                        // 确定监控路径和过滤器
                        string watchPath;
                        string filter;

                        if (File.Exists(path))
                        {
                            watchPath = Path.GetDirectoryName(path) ?? path;
                            filter = Path.GetFileName(path);
                        }
                        else
                        {
                            watchPath = path;
                            filter = "*.*";
                        }

                        // 创建文件系统监控器
                        _watcher = new FileSystemWatcher(watchPath, filter)
                        {
                            IncludeSubdirectories = includeSubdirectories,
                            NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.LastWrite | 
                                          NotifyFilters.FileName | NotifyFilters.Size
                        };

                        // 订阅事件
                        _watcher.Created += OnFileChanged;
                        _watcher.Changed += OnFileChanged;
                        _watcher.Deleted += OnFileChanged;
                        _watcher.Renamed += OnFileRenamed;
                        _watcher.Error += OnWatcherError;

                        // 启动监控
                        _watcher.EnableRaisingEvents = true;
                        _isWatching = true;
                        WatchingPath = path;

                        // 预加载文件元数据缓存
                        _ = Task.Run(() => PreloadFileMetadata(watchPath, includeSubdirectories), cancellationToken);
                    }
                    catch
                    {
                        _watcher?.Dispose();
                        _watcher = null;
                        throw;
                    }
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 停止监控
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>停止任务</returns>
        public async Task StopWatchingAsync(CancellationToken cancellationToken = default)
        {
            await Task.Run(() =>
            {
                lock (_lock)
                {
                    StopWatchingInternal();
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 检查单个文件是否已变更
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="lastModified">上次修改时间</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否已变更</returns>
        public async Task<bool> IsFileChangedAsync(string filePath, DateTime lastModified, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(filePath))
                return false;

            return await Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(filePath))
                        return true; // 文件不存在，认为已变更

                    var fileInfo = new FileInfo(filePath);
                    return fileInfo.LastWriteTimeUtc > lastModified;
                }
                catch
                {
                    return true; // 出现异常，保守认为已变更
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 获取文件元数据
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>文件元数据</returns>
        public async Task<FileMetadata> GetFileMetadataAsync(string filePath, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(filePath))
                return null;

            return await Task.Run(() =>
            {
                try
                {
                    // 检查缓存
                    lock (_lock)
                    {
                        if (_fileMetadataCache.TryGetValue(filePath, out var cached))
                        {
                            return cached;
                        }
                    }

                    // 创建新的元数据
                    var fileInfo = new FileInfo(filePath);
                    var metadata = FileMetadata.FromFileInfo(fileInfo);

                    // 缓存元数据
                    lock (_lock)
                    {
                        _fileMetadataCache[filePath] = metadata;
                    }

                    return metadata;
                }
                catch
                {
                    return FileMetadata.CreateEmpty(filePath);
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 批量检查多个文件的变更状态
        /// </summary>
        /// <param name="fileInfos">文件信息集合</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>变更状态字典</returns>
        public async Task<Dictionary<string, bool>> BatchCheckFilesAsync(
            IEnumerable<(string filePath, DateTime lastModified)> fileInfos,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var results = new Dictionary<string, bool>();
            var fileInfosList = fileInfos?.ToList() ?? new List<(string, DateTime)>();

            if (fileInfosList.Count == 0)
                return results;

            // 并行检查文件变更状态
            var semaphore = new SemaphoreSlim(_options.WarmupConcurrency);
            var tasks = fileInfosList.Select(async fileInfo =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var isChanged = await IsFileChangedAsync(fileInfo.filePath, fileInfo.lastModified, cancellationToken);
                    return new { FilePath = fileInfo.filePath, IsChanged = isChanged };
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var checkResults = await Task.WhenAll(tasks);

            foreach (var result in checkResults)
            {
                results[result.FilePath] = result.IsChanged;
            }

            return results;
        }
        #endregion

        #region 私有方法
        /// <summary>
        /// 停止监控（内部方法）
        /// </summary>
        private void StopWatchingInternal()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Created -= OnFileChanged;
                _watcher.Changed -= OnFileChanged;
                _watcher.Deleted -= OnFileChanged;
                _watcher.Renamed -= OnFileRenamed;
                _watcher.Error -= OnWatcherError;
                _watcher.Dispose();
                _watcher = null;
            }

            _isWatching = false;
            WatchingPath = null;

            // 停止防抖动定时器
            _debounceTimer.Change(Timeout.Infinite, Timeout.Infinite);

            // 处理剩余的待处理变更
            ProcessPendingChanges(null);
        }

        /// <summary>
        /// 预加载文件元数据缓存
        /// </summary>
        /// <param name="path">路径</param>
        /// <param name="includeSubdirectories">是否包含子目录</param>
        private void PreloadFileMetadata(string path, bool includeSubdirectories)
        {
            try
            {
                var searchOption = includeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var files = Directory.EnumerateFiles(path, "*.*", searchOption)
                    .Where(file => ShouldMonitorFile(file))
                    .Take(1000); // 限制预加载文件数量

                Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = _options.WarmupConcurrency }, file =>
                {
                    try
                    {
                        var metadata = FileMetadata.FromFilePath(file);
                        lock (_lock)
                        {
                            _fileMetadataCache[file] = metadata;
                        }
                    }
                    catch
                    {
                        // 忽略预加载失败的文件
                    }
                });
            }
            catch
            {
                // 预加载失败不应该影响主要功能
            }
        }

        /// <summary>
        /// 检查是否应该监控指定文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>是否应该监控</returns>
        private bool ShouldMonitorFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            var fileName = Path.GetFileName(filePath);
            var fileExtension = Path.GetExtension(filePath);

            // 检查文件扩展名
            if (_options.MonitoredFileExtensions.Count > 0 && 
                !_options.MonitoredFileExtensions.Contains(fileExtension))
            {
                return false;
            }

            // 检查排除的文件模式
            foreach (var pattern in _options.ExcludedFilePatterns)
            {
                if (IsMatchPattern(fileName, pattern))
                    return false;
            }

            // 检查排除的目录模式
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                foreach (var pattern in _options.ExcludedDirectoryPatterns)
                {
                    if (directory.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 检查文件名是否匹配模式
        /// </summary>
        /// <param name="fileName">文件名</param>
        /// <param name="pattern">匹配模式</param>
        /// <returns>是否匹配</returns>
        private static bool IsMatchPattern(string fileName, string pattern)
        {
            if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(pattern))
                return false;

            // 简单的通配符匹配
            if (pattern.Contains('*'))
            {
                var regex = pattern.Replace("*", ".*").Replace("?", ".");
                return System.Text.RegularExpressions.Regex.IsMatch(fileName, $"^{regex}$", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            return fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 文件变更事件处理
        /// </summary>
        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            if (_disposed || !ShouldMonitorFile(e.FullPath))
                return;

            lock (_lock)
            {
                _pendingChanges[e.FullPath] = DateTime.UtcNow;
                
                // 重置防抖动定时器
                _debounceTimer.Change(_options.FileChangeDebounceTime, Timeout.InfiniteTimeSpan);
            }
        }

        /// <summary>
        /// 文件重命名事件处理
        /// </summary>
        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            if (_disposed)
                return;

            // 处理旧文件删除
            if (ShouldMonitorFile(e.OldFullPath))
            {
                lock (_lock)
                {
                    _fileMetadataCache.Remove(e.OldFullPath);
                    _pendingChanges[e.OldFullPath] = DateTime.UtcNow;
                }

                FireFileChangedEvent(e.OldFullPath, FileChangeType.Deleted);
            }

            // 处理新文件创建
            if (ShouldMonitorFile(e.FullPath))
            {
                lock (_lock)
                {
                    _pendingChanges[e.FullPath] = DateTime.UtcNow;
                    
                    // 重置防抖动定时器
                    _debounceTimer.Change(_options.FileChangeDebounceTime, Timeout.InfiniteTimeSpan);
                }
            }
        }

        /// <summary>
        /// 文件监控错误事件处理
        /// </summary>
        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            // 记录错误但继续运行
            // 在实际应用中，这里应该记录日志
        }

        /// <summary>
        /// 处理待处理的文件变更
        /// </summary>
        /// <param name="state">定时器状态</param>
        private void ProcessPendingChanges(object state)
        {
            Dictionary<string, DateTime> changesToProcess;

            lock (_lock)
            {
                if (_pendingChanges.Count == 0)
                    return;

                changesToProcess = new Dictionary<string, DateTime>(_pendingChanges);
                _pendingChanges.Clear();
            }

            // 批量处理文件变更
            _ = Task.Run(async () =>
            {
                foreach (var kvp in changesToProcess)
                {
                    try
                    {
                        await ProcessFileChange(kvp.Key);
                    }
                    catch
                    {
                        // 忽略处理单个文件变更时的错误
                    }
                }
            });
        }

        /// <summary>
        /// 处理单个文件变更
        /// </summary>
        /// <param name="filePath">文件路径</param>
        private async Task ProcessFileChange(string filePath)
        {
            try
            {
                FileMetadata oldMetadata = null;
                FileMetadata newMetadata = null;
                FileChangeType changeType;

                lock (_lock)
                {
                    _fileMetadataCache.TryGetValue(filePath, out oldMetadata);
                }

                if (File.Exists(filePath))
                {
                    newMetadata = await GetFileMetadataAsync(filePath);
                    changeType = oldMetadata == null ? FileChangeType.Created : FileChangeType.Modified;
                }
                else
                {
                    changeType = FileChangeType.Deleted;
                    lock (_lock)
                    {
                        _fileMetadataCache.Remove(filePath);
                    }
                }

                FireFileChangedEvent(filePath, changeType, oldMetadata, newMetadata);
            }
            catch
            {
                // 处理文件变更失败，忽略错误
            }
        }

        /// <summary>
        /// 触发文件变更事件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="changeType">变更类型</param>
        /// <param name="oldMetadata">旧元数据</param>
        /// <param name="newMetadata">新元数据</param>
        private void FireFileChangedEvent(string filePath, FileChangeType changeType, 
            FileMetadata oldMetadata = null, FileMetadata newMetadata = null)
        {
            try
            {
                var eventArgs = new FileChangedEventArgs(filePath, changeType, DateTime.UtcNow, oldMetadata, newMetadata);
                FileChanged?.Invoke(this, eventArgs);
            }
            catch
            {
                // 事件处理失败不应该影响检测器本身
            }
        }

        /// <summary>
        /// 检查是否已释放资源
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FileChangeDetector));
        }
        #endregion

        #region IDisposable 实现
        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            lock (_lock)
            {
                StopWatchingInternal();
                _debounceTimer.Dispose();
                _fileMetadataCache.Clear();
                _pendingChanges.Clear();
            }
        }
        #endregion
    }
}