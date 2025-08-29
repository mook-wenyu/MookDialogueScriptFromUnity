using System;
using MookDialogueScript.Incremental;
using MookDialogueScript.Incremental.Contracts;

namespace MookDialogueScript.Parsing
{
    /// <summary>
    /// 解析上下文管理器
    /// 专门负责解析过程中的状态管理和上下文信息
    /// 包括节点状态、行号生成、嵌套级别等
    /// 支持增量缓存系统集成
    /// </summary>
    public class ParseContextManager : IDisposable
    {
        #region 字段
        private string _currentNodeName = "";
        private string _currentNodeNameLower = "";
        private string _lineTagPrefix = "";
        private int _lineCounter = 0;
        private int _currentNestingLevel = 0;
        
        // 缓存相关
        private IIncrementalCache _cacheManager;
        private string _currentFilePath = "";
        private bool _cacheEnabled = false;
        
        // 常量
        private const int MAX_SAFE_NESTING_LEVEL = 10;
        
        private bool _disposed;
        #endregion

        #region 属性
        /// <summary>
        /// 当前节点名称
        /// </summary>
        public string CurrentNodeName => _currentNodeName;

        /// <summary>
        /// 当前行计数器
        /// </summary>
        public int LineCounter => _lineCounter;

        /// <summary>
        /// 当前嵌套级别
        /// </summary>
        public int NestingLevel => _currentNestingLevel;
        
        /// <summary>
        /// 当前文件路径
        /// </summary>
        public string CurrentFilePath => _currentFilePath;
        
        /// <summary>
        /// 缓存是否已启用
        /// </summary>
        public bool IsCacheEnabled => _cacheEnabled && _cacheManager != null;
        
        /// <summary>
        /// 缓存管理器
        /// </summary>
        public IIncrementalCache CacheManager => _cacheManager;
        #endregion

        #region 公共方法
        /// <summary>
        /// 设置缓存管理器
        /// </summary>
        /// <param name="cacheManager">缓存管理器实例</param>
        public void SetCacheManager(IIncrementalCache cacheManager)
        {
            _cacheManager = cacheManager;
            _cacheEnabled = cacheManager != null;
        }

        /// <summary>
        /// 设置当前解析文件路径
        /// </summary>
        /// <param name="filePath">文件路径</param>
        public void SetCurrentFilePath(string filePath)
        {
            _currentFilePath = filePath ?? "";
        }

        /// <summary>
        /// 重置解析上下文
        /// </summary>
        public void Reset()
        {
            _currentNodeName = "";
            _currentNodeNameLower = "";
            _lineTagPrefix = "";
            _lineCounter = 0;
            _currentNestingLevel = 0;
            // 保留缓存管理器和文件路径
        }

        /// <summary>
        /// 完全重置，包括缓存相关信息
        /// </summary>
        public void FullReset()
        {
            Reset();
            _currentFilePath = "";
            _cacheManager = null;
            _cacheEnabled = false;
        }

        /// <summary>
        /// 进入节点解析
        /// </summary>
        /// <param name="nodeName">节点名称</param>
        public void EnterNode(string nodeName)
        {
            _currentNodeName = nodeName ?? "unnamed";
            _currentNodeNameLower = _currentNodeName.ToLower();
            _lineTagPrefix = "line:" + _currentNodeNameLower;
            _lineCounter = 0; // 重置行计数器
        }

        /// <summary>
        /// 退出节点解析
        /// </summary>
        public void ExitNode()
        {
            _currentNodeName = "";
            _currentNodeNameLower = "";
            _lineTagPrefix = "";
        }

        /// <summary>
        /// 进入嵌套层级
        /// </summary>
        public void EnterNesting()
        {
            _currentNestingLevel++;
            
            if (_currentNestingLevel > MAX_SAFE_NESTING_LEVEL)
            {
                MLogger.Warning($"警告: 嵌套层数过深（{_currentNestingLevel}层），可能影响性能");
            }
        }

        /// <summary>
        /// 退出嵌套层级
        /// </summary>
        public void ExitNesting()
        {
            if (_currentNestingLevel > 0)
            {
                _currentNestingLevel--;
            }
        }

        /// <summary>
        /// 生成自动行号标签
        /// </summary>
        /// <returns>行号标签</returns>
        public string GenerateLineTag()
        {
            _lineCounter++;
            
            // 使用高性能字符串拼接
            if (string.IsNullOrEmpty(_lineTagPrefix))
            {
                return $"line:{_lineCounter}";
            }
            
            return string.Concat(_lineTagPrefix, _lineCounter.ToString());
        }

        /// <summary>
        /// 获取当前上下文信息
        /// </summary>
        /// <returns>上下文信息字符串</returns>
        public string GetContextInfo()
        {
            return $"Node: {_currentNodeName}, Line: {_lineCounter}, Nesting: {_currentNestingLevel}";
        }

        /// <summary>
        /// 检查是否在有效节点内
        /// </summary>
        /// <returns>是否在节点内</returns>
        public bool IsInNode()
        {
            return !string.IsNullOrEmpty(_currentNodeName);
        }

        /// <summary>
        /// 检查嵌套级别是否安全
        /// </summary>
        /// <returns>是否安全</returns>
        public bool IsSafeNestingLevel()
        {
            return _currentNestingLevel <= MAX_SAFE_NESTING_LEVEL;
        }

        /// <summary>
        /// 尝试从缓存获取解析结果
        /// </summary>
        /// <param name="filePath">文件路径（可选，默认使用当前文件路径）</param>
        /// <returns>解析结果，如果缓存未命中或未启用则返回null</returns>
        public async System.Threading.Tasks.Task<ParseResult> TryGetFromCacheAsync(string filePath = null)
        {
            if (!IsCacheEnabled)
                return null;

            try
            {
                var targetPath = filePath ?? _currentFilePath;
                if (string.IsNullOrEmpty(targetPath))
                    return null;

                var fileMetadata = await _cacheManager.FileDetector.GetFileMetadataAsync(targetPath);
                if (fileMetadata == null)
                    return null;

                return await _cacheManager.ParseResultCache.GetAsync(targetPath, fileMetadata);
            }
            catch (Exception ex)
            {
                MLogger.Warning($"从缓存获取解析结果失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 将解析结果存储到缓存
        /// </summary>
        /// <param name="parseResult">解析结果</param>
        /// <param name="filePath">文件路径（可选，默认使用当前文件路径）</param>
        /// <returns>是否存储成功</returns>
        public async System.Threading.Tasks.Task<bool> TryStoreToCacheAsync(ParseResult parseResult, string filePath = null)
        {
            if (!IsCacheEnabled || parseResult == null)
                return false;

            try
            {
                var targetPath = filePath ?? _currentFilePath;
                if (string.IsNullOrEmpty(targetPath))
                    return false;

                await _cacheManager.ParseResultCache.SetAsync(targetPath, parseResult);
                return true;
            }
            catch (Exception ex)
            {
                MLogger.Warning($"存储解析结果到缓存失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 检查文件是否需要重新解析
        /// </summary>
        /// <param name="filePath">文件路径（可选，默认使用当前文件路径）</param>
        /// <returns>是否需要重新解析</returns>
        public async System.Threading.Tasks.Task<bool> NeedsReparseAsync(string filePath = null)
        {
            if (!IsCacheEnabled)
                return true; // 缓存未启用，总是需要解析

            try
            {
                var targetPath = filePath ?? _currentFilePath;
                if (string.IsNullOrEmpty(targetPath))
                    return true;

                var fileMetadata = await _cacheManager.FileDetector.GetFileMetadataAsync(targetPath);
                if (fileMetadata == null)
                    return true; // 文件不存在，需要解析

                return !await _cacheManager.ParseResultCache.ContainsValidAsync(targetPath, fileMetadata);
            }
            catch (Exception ex)
            {
                MLogger.Warning($"检查是否需要重新解析失败: {ex.Message}");
                return true; // 出错时默认需要解析
            }
        }
        #endregion

        #region IDisposable 实现
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    FullReset(); // 使用完全重置清理所有状态
                }
                _disposed = true;
            }
        }
        #endregion
    }
}