using System.Runtime.CompilerServices;

namespace MookDialogueScript.Lexers
{
    /// <summary>
    /// 节点标记Token处理器，处理 --- 和 === 节点标记
    /// 设计原则：单一职责 - 专注于节点标记的识别和状态管理
    /// </summary>
    public class NodeMarkerTokenizer : ITokenizer
    {
        public string Description => "节点标记Token处理器";
        public TokenType[] SupportedTokenTypes => new[] {TokenType.NODE_START, TokenType.NODE_END};

        /// <summary>
        /// 快速判断是否为节点标记起始
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CanHandle(CharStream stream, LexerState state)
        {
            // 节点开始标记 --- （仅在节点外部）
            if (!state.IsInNodeContent && stream.IsNodeStartMark())
                return true;

            // 节点结束标记 === （仅在节点内部）
            if (state.IsInNodeContent && stream.IsNodeEndMark())
                return true;

            return false;
        }

        /// <summary>
        /// 处理节点标记Token
        /// </summary>
        public Token TryTokenize(CharStream stream, LexerState state)
        {
            // 处理节点开始标记 ---
            if (!state.IsInNodeContent && stream.IsNodeStartMark())
            {
                // 检查是否被转义
                if (IsEscaped(stream))
                    return null;

                int startLine = stream.Line;
                int startColumn = stream.Column;

                // 消费 ---
                stream.Advance();
                stream.Advance();
                stream.Advance();

                // 更新状态
                state.EnterNodeContent();

                return TokenFactory.CreateToken(TokenType.NODE_START, "---", startLine, startColumn);
            }

            // 处理节点结束标记 ===
            if (state.IsInNodeContent && stream.IsNodeEndMark())
            {
                // 检查是否被转义
                if (IsEscaped(stream))
                    return null;

                int startLine = stream.Line;
                int startColumn = stream.Column;

                // 消费 ===
                stream.Advance();
                stream.Advance();
                stream.Advance();

                // 更新状态
                state.ExitNodeContent();

                return TokenFactory.CreateToken(TokenType.NODE_END, "===", startLine, startColumn);
            }

            return null;
        }

        /// <summary>
        /// 检查当前位置是否被转义（前面有奇数个反斜杠）
        /// 保持原有的转义检查逻辑
        /// </summary>
        private bool IsEscaped(CharStream stream)
        {
            if (stream.Position == 0) return false;

            // 检查前一个字符是否是反斜杠
            char prevChar = stream.GetCharAt(stream.Position - 1);
            if (prevChar != '\\') return false;

            // 计算连续反斜杠的数量，只有奇数个反斜杠才表示转义
            int backslashCount = 0;
            int pos = stream.Position - 1;
            while (pos >= 0 && stream.GetCharAt(pos) == '\\')
            {
                backslashCount++;
                pos--;
            }

            // 奇数个反斜杠表示转义，偶数个表示反斜杠本身被转义
            return backslashCount % 2 == 1;
        }

        public void Clear()
        {

        }

        public void Dispose()
        {
            Clear();
        }
    }
}
