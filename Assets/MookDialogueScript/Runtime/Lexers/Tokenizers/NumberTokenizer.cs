using System.Runtime.CompilerServices;

namespace MookDialogueScript.Lexers
{
    /// <summary>
    /// 数字Token处理器，负责识别和解析数字Token
    /// 设计原则：单一职责 - 专注于数字Token的识别和生成
    /// </summary>
    public class NumberTokenizer : ITokenizer
    {
        public string Description => "数字Token处理器";
        public TokenType[] SupportedTokenTypes => new[] {TokenType.NUMBER};

        /// <summary>
        /// 快速判断是否为数字起始
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CanHandle(CharStream stream, LexerState state)
        {
            return !stream.IsEOFMark() && CharClassifier.IsDigit(stream.CurrentChar);
        }

        /// <summary>
        /// 处理数字Token，保持原有的优化逻辑
        /// </summary>
        public Token TryTokenize(CharStream stream, LexerState state)
        {
            if (!CanHandle(stream, state)) return null;

            var start = SourceLocation.FromStream(stream);
            bool hasDecimalPoint = false;

            // 使用缓存字符进行快速处理
            while (!stream.IsEOFMark() && (CharClassifier.IsDigit(stream.CurrentChar) || stream.CurrentChar == '.'))
            {
                if (stream.CurrentChar == '.')
                {
                    if (hasDecimalPoint) break; // 第二个小数点，停止
                    hasDecimalPoint = true;
                }
                stream.Advance();
            }

            string numberText = stream.GetRange(start.Position, stream.Position);
            return TokenFactory.NumberToken(numberText, start.Line, start.Column);
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
