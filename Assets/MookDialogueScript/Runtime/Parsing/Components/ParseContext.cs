using System;

namespace MookDialogueScript.Parsing
{
    /// <summary>
    /// 解析上下文管理器
    /// 专门负责解析过程中的状态管理和上下文信息
    /// 包括节点状态、行号生成、嵌套级别等
    /// 支持增量缓存系统集成
    /// </summary>
    public class ParseContext : IDisposable
    {
        private string _currentNodeName = "";
        private string _currentNodeNameLower = "";
        private string _lineTagPrefix = "";
        private int _lineCounter;
        private int _currentNestingLevel;

        // 常量
        private const int MAX_SAFE_NESTING_LEVEL = 10;

        private bool _disposed;

        /// <summary>
        /// 重置解析上下文
        /// </summary>
        public void Clear()
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
                    Clear();
                }
                _disposed = true;
            }
        }
    }
}
