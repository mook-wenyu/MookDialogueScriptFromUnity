using System;

namespace MookDialogueScript.Lexers
{
    /// <summary>
    /// 词法分析器状态管理实现
    /// 设计原则：单一职责 - 专注于状态管理和切换逻辑
    /// </summary>
    public class LexerState : IDisposable
    {
        private bool _isInNodeContent;
        private bool _isInCommandMode;
        private bool _isInStringMode;
        private bool _isInInterpolation;
        private char _stringQuoteType;

        public bool IsInNodeContent
        {
            get => _isInNodeContent;
            set => _isInNodeContent = value;
        }

        public bool IsInCommandMode
        {
            get => _isInCommandMode;
            set => _isInCommandMode = value;
        }

        public bool IsInStringMode
        {
            get => _isInStringMode;
            set => _isInStringMode = value;
        }

        public bool IsInInterpolation
        {
            get => _isInInterpolation;
            set => _isInInterpolation = value;
        }

        public char StringQuoteType
        {
            get => _stringQuoteType;
            set => _stringQuoteType = value;
        }

        /// <summary>
        /// 创建当前状态的副本
        /// </summary>
        public LexerState Clone()
        {
            return new LexerState
            {
                _isInNodeContent = this._isInNodeContent,
                _isInCommandMode = this._isInCommandMode,
                _isInStringMode = this._isInStringMode,
                _isInInterpolation = this._isInInterpolation,
                _stringQuoteType = this._stringQuoteType
            };
        }

        /// <summary>
        /// 进入节点内容模式
        /// </summary>
        public void EnterNodeContent()
        {
            _isInNodeContent = true;
        }

        /// <summary>
        /// 退出节点内容模式
        /// </summary>
        public void ExitNodeContent()
        {
            _isInNodeContent = false;
        }

        /// <summary>
        /// 进入命令模式
        /// </summary>
        public void EnterCommandMode()
        {
            _isInCommandMode = true;
        }

        /// <summary>
        /// 退出命令模式
        /// </summary>
        public void ExitCommandMode()
        {
            _isInCommandMode = false;
        }

        /// <summary>
        /// 进入字符串模式
        /// </summary>
        public void EnterStringMode(char quoteType)
        {
            _isInStringMode = true;
            _stringQuoteType = quoteType;
        }

        /// <summary>
        /// 退出字符串模式
        /// </summary>
        public void ExitStringMode()
        {
            _isInStringMode = false;
            _stringQuoteType = '\0';
        }

        /// <summary>
        /// 进入插值表达式模式
        /// </summary>
        public void EnterInterpolation()
        {
            _isInInterpolation = true;
        }

        /// <summary>
        /// 退出插值表达式模式
        /// </summary>
        public void ExitInterpolation()
        {
            _isInInterpolation = false;
        }

        /// <summary>
        /// 验证当前状态的有效性
        /// </summary>
        public bool IsValidState()
        {
            // 基本状态一致性检查
            if (_isInStringMode && _stringQuoteType == '\0')
                return false;

            if (!_isInStringMode && _stringQuoteType != '\0')
                return false;

            return true;
        }

        public void Clear()
        {
            _isInNodeContent = false;
            _isInCommandMode = false;
            _isInStringMode = false;
            _isInInterpolation = false;
            _stringQuoteType = '\0';
        }

        public void Dispose()
        {
            Clear();
        }
    }
}
