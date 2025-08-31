using System;
using System.Collections.Generic;

namespace MookDialogueScript.Lexers
{
    /// <summary>
    /// 缩进处理实现，管理缩进层级和INDENT/DEDENT Token生成
    /// 设计原则：单一职责 - 专注于缩进逻辑处理
    /// 保持原有的复杂缩进处理逻辑和性能优化
    /// </summary>
    public class IndentationHandler : IDisposable
    {
        private readonly Stack<int> _indentStack;
        private int _currentIndent;
        private int _pendingDedent;

        public bool HasPendingDedents => _pendingDedent > 0;
        public int CurrentIndentLevel => _currentIndent;
        public int IndentStackDepth => _indentStack.Count;

        public IndentationHandler()
        {
            _indentStack = new Stack<int>();
            Init();
        }

        /// <summary>
        /// 重置缩进处理器到初始状态
        /// </summary>
        public void Init()
        {
            _indentStack.Clear();
            _indentStack.Push(0); // 基础缩进层级
            _currentIndent = 0;
            _pendingDedent = 0;
        }

        /// <summary>
        /// 处理行首缩进并生成相应的INDENT/DEDENT Token
        /// 保持原有的复杂缩进处理逻辑
        /// </summary>
        public Token HandleIndentation(CharStream stream, LexerState state)
        {
            // 仅在节点内容内处理缩进
            if (stream.Column != 1 || !state.IsInNodeContent) return null;

            // 保存位置用于缩进计算后的恢复
            var savedLocation = stream.GetCurrentLocation();

            int indent = CountIndentation(stream);

            // 精确恢复到保存的位置
            stream.RestoreLocation(savedLocation);

            if (ShouldSkipIndentation(stream))
                return null;

            if (indent > _currentIndent)
                return HandleIndentIncrease(indent, stream.Line, stream.Column);
            if (indent < _currentIndent)
                return HandleIndentDecrease(indent, stream.Line, stream.Column);

            if (indent != _currentIndent)
            {
                MLogger.Error($"词法错误: 第{stream.Line}行，第{stream.Column}列，不一致的缩进");
                return null;
            }

            // 缩进层级无变化
            return null;
        }

        /// <summary>
        /// 获取下一个待输出的DEDENT Token
        /// </summary>
        public Token GetPendingDedentToken(int line, int column)
        {
            if (_pendingDedent <= 0) return null;

            _pendingDedent--;
            return TokenFactory.DedentToken(line, column);
        }

        /// <summary>
        /// 处理文件结束时的缩进清理
        /// </summary>
        public Token HandleEOFIndentation(int line, int column)
        {
            if (_pendingDedent > 0)
            {
                _pendingDedent--;
                return TokenFactory.DedentToken(line, column);
            }

            if (_indentStack.Count > 1)
            {
                _indentStack.Pop();
                _currentIndent = _indentStack.Peek();
                return TokenFactory.DedentToken(line, column);
            }

            return null;
        }

        /// <summary>
        /// 验证缩进是否匹配已知的缩进层级
        /// </summary>
        public bool IsValidIndentLevel(int indentLevel)
        {
            foreach (int level in _indentStack)
            {
                if (level == indentLevel)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 统计当前行的缩进数量（保持原有逻辑）
        /// </summary>
        private int CountIndentation(CharStream stream)
        {
            var indent = 0;
            bool seenSpace = false;
            bool seenTab = false;

            // 创建临时位置跟踪，避免修改原始流状态
            while (stream.IsSpaceOrIndentMark())
            {
                if (stream.CurrentChar == ' ') seenSpace = true;
                else if (stream.CurrentChar == '\t') seenTab = true;
                indent++;
                stream.Advance(); // 注意：这会修改流状态，需要后续恢复
            }

            if (seenSpace && seenTab)
            {
                MLogger.Warning($"词法警告: 第{stream.Line}行，检测到混合缩进（空格与Tab）");
            }

            return indent;
        }

        /// <summary>
        /// 判断是否需要跳过缩进处理
        /// </summary>
        private bool ShouldSkipIndentation(CharStream stream)
        {
            // 在行首仅对 EOF/换行跳过缩进处理；注释判定由调用方负责
            return stream.IsNewlineOrEOFMark();
        }

        /// <summary>
        /// 处理缩进增加的情况
        /// </summary>
        private Token HandleIndentIncrease(int indent, int line, int column)
        {
            _indentStack.Push(indent);
            _currentIndent = indent;
            return TokenFactory.IndentToken(line, column);
        }

        /// <summary>
        /// 处理缩进减少的情况（保持原有复杂逻辑）
        /// </summary>
        private Token HandleIndentDecrease(int indent, int line, int column)
        {
            // 计算需要回退的层级数，并逐个发出DEDENT
            int popCount = 0;
            while (_indentStack.Count > 1 && _indentStack.Peek() > indent)
            {
                _indentStack.Pop();
                popCount++;
            }
            _currentIndent = _indentStack.Peek();

            if (popCount > 0)
            {
                // 先返回一个DEDENT，其余的记录到pending里，保持“每次只返回一个 Token”
                _pendingDedent = popCount - 1;
                return TokenFactory.DedentToken(line, column);
            }

            // 如果没有发生弹栈，但目标缩进与现有层级仍不一致，说明缩进不匹配
            if (_indentStack.Count == 0 || (_indentStack.Count > 0 && _indentStack.Peek() != indent))
            {
                MLogger.Error($"词法错误: 第{line}行，第{column}列，无效的缩进（未匹配任何已知缩进层级）");
            }

            // 未发生弹栈且缩进匹配时，不返回 Token
            return null;
        }

        public void Clear()
        {
            _indentStack.Clear();
            _currentIndent = 0;
            _pendingDedent = 0;
        }

        public void Dispose()
        {
            Clear();
        }
    }
}
