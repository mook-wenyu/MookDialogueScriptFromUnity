using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using MookDialogueScript.Incremental;
using MookDialogueScript.Incremental.Core;
using MookDialogueScript.Incremental.Contracts;

namespace MookDialogueScript.Integration
{
    /// <summary>
    /// 增量缓存集成示例
    /// 展示如何在对话系统中集成和使用增量缓存功能
    /// </summary>
    public class IncrementalCacheIntegration : MonoBehaviour
    {
        [Header("缓存配置")]
        [SerializeField] private bool enableCache = true;
        [SerializeField] private bool enableFileWatcher = true;
        [SerializeField] private int maxCacheSize = 1000;
        [SerializeField] private int maxMemoryUsageMB = 100;

        [Header("监控路径")]
        [SerializeField] private string dialogueScriptPath = "Assets/Resources/DialogueScripts";

        private IIncrementalCache _cacheManager;
        private bool _initialized = false;

        #region Unity生命周期
        /// <summary>
        /// 启动时初始化缓存系统
        /// </summary>
        private async void Start()
        {
            await InitializeCacheAsync();
        }

        /// <summary>
        /// 销毁时清理资源
        /// </summary>
        private void OnDestroy()
        {
            CleanupCache();
        }

        /// <summary>
        /// 应用程序暂停时保存缓存
        /// </summary>
        /// <param name="pauseStatus">暂停状态</param>
        private async void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && _cacheManager != null)
            {
                await _cacheManager.SaveAsync();
            }
        }

        /// <summary>
        /// 应用程序焦点变化时处理缓存
        /// </summary>
        /// <param name="hasFocus">是否有焦点</param>
        private async void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && _cacheManager != null)
            {
                await _cacheManager.SaveAsync();
            }
        }
        #endregion

        #region 公共方法
        /// <summary>
        /// 初始化缓存系统
        /// </summary>
        public async Task InitializeCacheAsync()
        {
            if (_initialized || !enableCache)
                return;

            try
            {
                // 创建缓存配置
                var options = CreateCacheOptions();

                // 创建缓存管理器
                _cacheManager = new IncrementalCacheManager(options);

                // 订阅缓存变更事件
                _cacheManager.CacheChanged += OnCacheChanged;

                // 初始化缓存系统
                await _cacheManager.InitializeAsync();

                // 启动文件监控
                if (enableFileWatcher && System.IO.Directory.Exists(dialogueScriptPath))
                {
                    await _cacheManager.FileDetector.StartWatchingAsync(dialogueScriptPath, true);
                }

                // 预热缓存
                await PreloadCommonFiles();

                _initialized = true;
                Debug.Log("增量缓存系统初始化完成");
            }
            catch (Exception ex)
            {
                Debug.LogError($"初始化增量缓存系统失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 获取文件的解析结果（带缓存）
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="forceRefresh">是否强制刷新</param>
        /// <returns>解析结果</returns>
        public async Task<ParseResult> GetParseResultAsync(string filePath, bool forceRefresh = false)
        {
            if (_cacheManager == null)
                return null;

            try
            {
                // 刷新文件缓存
                await _cacheManager.RefreshFileAsync(filePath, forceRefresh);

                // 获取文件元数据
                var fileMetadata = await _cacheManager.FileDetector.GetFileMetadataAsync(filePath);
                if (fileMetadata == null)
                    return null;

                // 从缓存获取解析结果
                return await _cacheManager.ParseResultCache.GetAsync(filePath, fileMetadata);
            }
            catch (Exception ex)
            {
                Debug.LogError($"获取解析结果失败 {filePath}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取变量声明（带缓存）
        /// </summary>
        /// <param name="variableName">变量名</param>
        /// <param name="scope">作用域</param>
        /// <returns>变量声明</returns>
        public async Task<VariableDeclaration> GetVariableDeclarationAsync(string variableName, string scope = null)
        {
            if (_cacheManager == null)
                return null;

            try
            {
                return await _cacheManager.VariableCache.GetVariableDeclarationAsync(variableName, scope);
            }
            catch (Exception ex)
            {
                Debug.LogError($"获取变量声明失败 {variableName}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 预热常用文件的缓存
        /// </summary>
        /// <param name="filePaths">文件路径数组</param>
        public async Task WarmupCacheAsync(string[] filePaths)
        {
            if (_cacheManager == null || filePaths == null)
                return;

            try
            {
                await _cacheManager.WarmupAsync(filePaths);
                Debug.Log($"预热缓存完成，文件数量: {filePaths.Length}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"预热缓存失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        /// <returns>缓存统计信息</returns>
        public CacheStatistics GetCacheStatistics()
        {
            return _cacheManager?.Statistics;
        }

        /// <summary>
        /// 生成缓存报告
        /// </summary>
        /// <returns>缓存报告</returns>
        public async Task<CacheReport> GenerateCacheReportAsync()
        {
            if (_cacheManager == null)
                return null;

            try
            {
                return await _cacheManager.GenerateReportAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"生成缓存报告失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 清空所有缓存
        /// </summary>
        public async Task ClearAllCacheAsync()
        {
            if (_cacheManager == null)
                return;

            try
            {
                await _cacheManager.ClearAllAsync();
                Debug.Log("已清空所有缓存");
            }
            catch (Exception ex)
            {
                Debug.LogError($"清空缓存失败: {ex.Message}");
            }
        }
        #endregion

        #region 私有方法
        /// <summary>
        /// 创建缓存配置选项
        /// </summary>
        /// <returns>缓存配置选项</returns>
        private IncrementalCacheOptions CreateCacheOptions()
        {
            return new IncrementalCacheOptions
            {
                EnableCache = enableCache,
                EnableFileWatcher = enableFileWatcher,
                EnableParseResultCache = true,
                EnableVariableDeclarationCache = true,
                EnablePersistentCache = true,

                MaxCacheSize = maxCacheSize,
                MaxMemoryUsage = maxMemoryUsageMB * 1024 * 1024,

                CacheExpiration = TimeSpan.FromHours(24),
                CleanupInterval = TimeSpan.FromMinutes(30),
                FileChangeDebounceTime = TimeSpan.FromMilliseconds(500),

                MaxBatchSize = 100,
                WarmupConcurrency = Environment.ProcessorCount,

                PersistentCachePath = System.IO.Path.Combine(Application.persistentDataPath, "DialogueCache"),
                SerializationFormat = CacheSerializationFormat.Binary,
                EnableCacheCompression = true,
                CompressionLevel = 6,

                EnableStatistics = true,
                LogLevel = Application.isEditor ? CacheLogLevel.Info : CacheLogLevel.Warning,

                KeyStrategy = CacheKeyStrategy.FilePath,
                ValidateIntegrityOnStartup = true,
                AutoSaveOnShutdown = true,
                ThreadSafetyMode = ThreadSafetyMode.Full
            };
        }

        /// <summary>
        /// 预加载常用文件
        /// </summary>
        /// <returns>预加载任务</returns>
        private async Task PreloadCommonFiles()
        {
            if (_cacheManager == null || !System.IO.Directory.Exists(dialogueScriptPath))
                return;

            try
            {
                // 查找所有对话脚本文件
                var scriptFiles = System.IO.Directory.GetFiles(
                    dialogueScriptPath,
                    "*.mds",
                    System.IO.SearchOption.AllDirectories);

                // 限制预热文件数量
                var filesToWarmup = scriptFiles.Take(50).ToArray();

                if (filesToWarmup.Length > 0)
                {
                    await _cacheManager.WarmupAsync(filesToWarmup);
                    Debug.Log($"预热缓存完成，文件数量: {filesToWarmup.Length}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"预热缓存失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 缓存变更事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">缓存变更事件参数</param>
        private void OnCacheChanged(object sender, CacheChangedEventArgs e)
        {
            Debug.Log($"缓存变更: {e.FilePath} - {e.ChangeType} ({e.ItemType})");
        }

        /// <summary>
        /// 清理缓存资源
        /// </summary>
        private void CleanupCache()
        {
            if (_cacheManager != null)
            {
                try
                {
                    _cacheManager.CacheChanged -= OnCacheChanged;
                    _cacheManager.Dispose();
                    _cacheManager = null;
                    _initialized = false;
                    Debug.Log("增量缓存系统已清理");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"清理缓存系统失败: {ex.Message}");
                }
            }
        }
        #endregion

        #region Unity编辑器支持
#if UNITY_EDITOR
        /// <summary>
        /// 在Inspector中显示缓存状态
        /// </summary>
        [Header("运行时状态 (只读)")]
        [SerializeField, TextArea(3, 10)]
        private string cacheStatus = "缓存未初始化";

        /// <summary>
        /// 更新Inspector显示的状态信息
        /// </summary>
        private void Update()
        {
            if (!Application.isPlaying)
                return;

            UpdateCacheStatusDisplay();
        }

        /// <summary>
        /// 更新缓存状态显示
        /// </summary>
        private void UpdateCacheStatusDisplay()
        {
            if (_cacheManager == null)
            {
                cacheStatus = "缓存未初始化";
                return;
            }

            try
            {
                var stats = _cacheManager.Statistics;
                cacheStatus = $@"缓存状态: {(_initialized ? "已初始化" : "未初始化")}
文件监控: {(_cacheManager.FileDetector.IsWatching ? "活跃" : "非活跃")}
监控路径: {_cacheManager.FileDetector.WatchingPath ?? "无"}

统计信息:
- 总项数: {stats.TotalItems:N0}
- 内存使用: {stats.TotalMemoryUsage / (1024.0 * 1024.0):F2} MB
- 命中率: {stats.HitRatio:P2}
- 总访问: {stats.TotalAccesses:N0}
- 解析结果: {stats.ParseResultItems:N0}
- 变量声明: {stats.VariableDeclarationItems:N0}

操作统计:
- 添加: {stats.AddOperations:N0}
- 更新: {stats.UpdateOperations:N0}
- 删除: {stats.RemoveOperations:N0}
- 清理: {stats.CleanupOperations:N0}";
            }
            catch (Exception ex)
            {
                cacheStatus = $"获取状态失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 编辑器菜单: 生成缓存报告
        /// </summary>
        [UnityEditor.MenuItem("Tools/MookDialogue/Generate Cache Report")]
        private static async void GenerateReportMenuItem()
        {
            var integration = FindObjectOfType<IncrementalCacheIntegration>();
            if (integration == null)
            {
                Debug.LogWarning("场景中没有找到 IncrementalCacheIntegration 组件");
                return;
            }

            var report = await integration.GenerateCacheReportAsync();
            if (report != null)
            {
                var reportText = $@"缓存系统报告
==================
生成时间: {report.GeneratedTime:yyyy-MM-dd HH:mm:ss}
健康状态: {report.HealthStatus}
效率评分: {report.Statistics.CalculateEfficiencyScore():F1}/100

{report.Statistics.GetPerformanceSummary()}
{report.Statistics.GetOperationSummary()}

建议:
{string.Join("\n", report.Recommendations.Select(r => "- " + r))}

警告:
{string.Join("\n", report.Warnings.Select(w => "- " + w))}";

                Debug.Log(reportText);
            }
        }
#endif
        #endregion
    }
}
