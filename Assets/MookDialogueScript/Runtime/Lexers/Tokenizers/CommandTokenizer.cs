using System.Runtime.CompilerServices;

namespace MookDialogueScript.Lexers
{
    /// <summary>
    /// 命令Token处理器，处理 《《 >> 命令标记
    /// 设计原则：单一职责 - 专注于命令标记的识别和状态管理
    /// </summary>
    public class CommandTokenizer : ITokenizer
    {
        public string Description => "命令Token处理器";
        public TokenType[] SupportedTokenTypes => new[] {TokenType.COMMAND_START, TokenType.COMMAND_END};

        /// <summary>
        /// 快速判断是否为命令标记起始
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CanHandle(CharStream stream, LexerState state)
        {
            // 仅在节点内容中处理命令标记
            if (!state.IsInNodeContent) return false;

            return stream.IsCommandStart() || stream.IsCommandEnd();
        }

        /// <summary>
        /// 处理命令Token
        /// </summary>
        public Token TryTokenize(CharStream stream, LexerState state)
        {
            if (!state.IsInNodeContent) return null;

            // 处理命令开始 <<
            if (stream.IsCommandStart())
            {
                int startLine = stream.Line;
                int startColumn = stream.Column;

                stream.Advance(); // 跳过第一个 <
                stream.Advance(); // 跳过第二个 <

                state.EnterCommandMode();
                return TokenFactory.CreateToken(TokenType.COMMAND_START, "<<", startLine, startColumn);
            }

            // 处理命令结束 >>
            if (stream.IsCommandEnd())
            {
                int startLine = stream.Line;
                int startColumn = stream.Column;

                stream.Advance(); // 跳过第一个 >
                stream.Advance(); // 跳过第二个 >

                state.ExitCommandMode();
                return TokenFactory.CreateToken(TokenType.COMMAND_END, ">>", startLine, startColumn);
            }

            return null;
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
