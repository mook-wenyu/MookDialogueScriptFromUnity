using System;

namespace MookDialogueScript.Parsing
{
    /// <summary>
    /// 解析上下文管理器
    /// 专门负责解析过程中的状态管理和上下文信息
    /// 包括节点状态、行号生成、嵌套级别等
    /// </summary>
    public class ParseContextManager : IDisposable
    {
        #region 字段
        private string _currentNodeName = "";
        private string _currentNodeNameLower = "";
        private string _lineTagPrefix = "";
        private int _lineCounter = 0;
        private int _currentNestingLevel = 0;
        
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
        #endregion

        #region 公共方法
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
                    Reset();
                }
                _disposed = true;
            }
        }
        #endregion
    }
}