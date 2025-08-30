using System.Text;

namespace MookDialogueScript.Lexers
{
    /// <summary>
    /// 字符串Token处理器，处理字符串引号和内容
    /// 设计原则：单一职责 - 专注于字符串相关Token的识别
    /// </summary>
    public class StringTokenizer : ITokenizer
    {
        public string Description => "字符串Token处理器";
        public TokenType[] SupportedTokenTypes => new[] {TokenType.QUOTE, TokenType.TEXT};

        /// <summary>
        /// 快速判断是否为字符串相关Token
        /// </summary>
        public bool CanHandle(CharStream stream, LexerState state)
        {
            char currentChar = stream.CurrentChar;

            // 字符串开始引号（不在字符串模式中）
            if (!state.IsInStringMode && CharClassifier.IsQuote(currentChar))
                return true;

            // 字符串结束引号（在字符串模式中且匹配引号类型）
            if (state.IsInStringMode && IsClosingQuote(currentChar, state.StringQuoteType))
                return true;

            // 字符串内容（在字符串模式中，但不是结束引号且不是插值起始）
            if (state.IsInStringMode && !IsClosingQuote(currentChar, state.StringQuoteType)
                                     && !state.IsInInterpolation && currentChar != '{')
                return true;

            return false;
        }

        /// <summary>
        /// 处理字符串Token
        /// </summary>
        public Token TryTokenize(CharStream stream, LexerState state)
        {
            char currentChar = stream.CurrentChar;

            // 处理字符串开始引号
            if (!state.IsInStringMode && CharClassifier.IsQuote(currentChar))
            {
                return HandleStringStart(stream, state, currentChar);
            }

            // 处理字符串结束引号
            if (state.IsInStringMode && IsClosingQuote(currentChar, state.StringQuoteType))
            {
                return HandleStringEnd(stream, state, currentChar);
            }

            // 处理字符串内容
            if (state.IsInStringMode && !IsClosingQuote(currentChar, state.StringQuoteType)
                                     && !state.IsInInterpolation && currentChar != '{')
            {
                return HandleStringContent(stream, state);
            }

            return null;
        }

        /// <summary>
        /// 处理字符串开始引号
        /// </summary>
        private Token HandleStringStart(CharStream stream, LexerState state, char quoteChar)
        {
            int startLine = stream.Line;
            int startColumn = stream.Column;

            stream.Advance();
            state.EnterStringMode(quoteChar);

            return TokenFactory.CreateToken(TokenType.QUOTE, quoteChar.ToString(), startLine, startColumn);
        }

        /// <summary>
        /// 处理字符串结束引号
        /// </summary>
        private Token HandleStringEnd(CharStream stream, LexerState state, char quoteChar)
        {
            int startLine = stream.Line;
            int startColumn = stream.Column;

            stream.Advance();
            state.ExitStringMode();

            return TokenFactory.CreateToken(TokenType.QUOTE, quoteChar.ToString(), startLine, startColumn);
        }

        /// <summary>
        /// 处理字符串内容（保持原有的复杂转义逻辑）
        /// </summary>
        private Token HandleStringContent(CharStream stream, LexerState state)
        {
            int startLine = stream.Line;
            int startColumn = stream.Column;
            var resultBuilder = new StringBuilder(64);

            while (stream.CurrentChar != '\0' && stream.CurrentChar != '\n' && stream.CurrentChar != '\r')
            {
                // 处理转义字符
                if (stream.CurrentChar == '\\')
                {
                    char next = stream.NextChar;
                    if (next is '{' or '}' or '\'' or '"' or '\\')
                    {
                        stream.Advance(); // 跳过反斜杠
                        resultBuilder.Append(next);
                    }
                    else
                    {
                        // 其他情况保留反斜杠
                        resultBuilder.Append(stream.CurrentChar);
                    }
                    stream.Advance();
                    continue;
                }

                // 遇到结束引号或插值起始，停止
                if (IsClosingQuote(stream.CurrentChar, state.StringQuoteType) || stream.CurrentChar == '{')
                {
                    break;
                }

                // 正常字符处理
                resultBuilder.Append(stream.CurrentChar);
                stream.Advance();
            }

            return TokenFactory.TextToken(resultBuilder.ToString(), startLine, startColumn);
        }

        /// <summary>
        /// 判断当前字符是否是指定类型的结束引号
        /// </summary>
        private bool IsClosingQuote(char c, char quoteType)
        {
            return c == quoteType;
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
