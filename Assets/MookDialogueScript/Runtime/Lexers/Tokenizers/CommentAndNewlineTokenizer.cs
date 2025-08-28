using System.Text;

namespace MookDialogueScript.Lexers
{
    /// <summary>
    /// 注释和换行Token处理器，处理复杂的注释和换行折叠逻辑
    /// 设计原则：单一职责 - 专注于注释和换行的处理
    /// 保持原有的复杂换行折叠和注释处理逻辑
    /// </summary>
    public class CommentAndNewlineTokenizer : ITokenizer
    {
        public int Priority => 1000;

        public string Description => "注释和换行Token处理器";
        public TokenType[] SupportedTokenTypes => new[] {TokenType.NEWLINE, TokenType.EOF};

        /// <summary>
        /// 快速判断是否为注释或换行起始
        /// </summary>
        public bool CanHandle(CharacterStream stream, LexerState state, CharacterClassifier classifier)
        {
            // 注释起始 //
            if (stream.IsCommentMark())
                return true;

            // 换行符
            if (stream.IsNewlineMark())
                return true;

            return false;
        }

        /// <summary>
        /// 处理注释和换行Token（保持原有复杂逻辑）
        /// </summary>
        public Token TryTokenize(CharacterStream stream, LexerState state, CharacterClassifier classifier)
        {
            return HandleNewlineAndComments(stream, state, classifier);
        }

        /// <summary>
        /// 处理换行字符、跳过连续的换行和注释行（保持原有逻辑）
        /// 复杂的逻辑：折叠连续的空行和注释行为一个NEWLINE Token
        /// </summary>
        private Token HandleNewlineAndComments(CharacterStream stream, LexerState state, CharacterClassifier classifier)
        {
            int currentLine = stream.Line;

            // 思路：推进"当前所在这一行"的行尾（含注释），并折叠后续的纯空行/纯注释行为一个NEWLINE；
            // 但遇到第一行"内容行"时，停在该行列首（不吞其前导空白），让缩进处理在列首发生。

            // 1) 先推进到当前行的换行位置（保留列首给后续行）
            if (stream.IsCommentMark())
            {
                // 跳过当前行注释内容
                while (stream.CurrentChar != '\0' && stream.CurrentChar != '\n' && stream.CurrentChar != '\r')
                {
                    stream.Advance();
                }
            }

            // 2) 如果是CRLF，先吃掉\r再由统一逻辑推进
            if (stream.IsCRLFMark())
            {
                stream.Advance();
            }
            if (stream.IsNewlineMark())
            {
                stream.Advance();
            }

            // 3) 折叠连续的"仅空白+注释+换行"的行
            while (true)
            {
                // 使用只读游标预判下一行是否为空/注释行
                int p = stream.Position;

                // 跳过前导空白（仅预读，不改变真实位置）
                char c = stream.GetCharAt(p);
                while (c is ' ' or '\t')
                {
                    p++;
                    c = stream.GetCharAt(p);
                }

                bool isCommentLine = c == '/' && stream.GetCharAt(p + 1) == '/';
                bool isEmptyLine = classifier.IsNewlineOrEOF(c);

                if (!(isCommentLine || isEmptyLine))
                {
                    // 下一行是内容行：停止折叠，保持指针在该行列首（不吃空白）
                    break;
                }

                // 将"预读到的这一行"（注释行或空行）整体消耗掉到换行符末尾
                // 真正推进：跳过前导空白
                while (stream.IsSpaceOrIndentMark())
                {
                    stream.Advance();
                }

                // 注释内容
                if (stream.IsCommentMark())
                {
                    while (stream.CurrentChar != '\0' && stream.CurrentChar != '\n' && stream.CurrentChar != '\r')
                    {
                        stream.Advance();
                    }
                }

                // CRLF处理
                if (stream.IsCRLFMark())
                {
                    stream.Advance();
                }

                if (stream.IsNewlineMark())
                {
                    stream.Advance();
                }
                else
                {
                    // EOF或没有换行可推进
                    break;
                }
            }

            // 4) EOF时返回EOF Token
            if (stream.IsEOFMark())
            {
                return TokenFactory.EOFToken(stream.Line, stream.Column);
            }

            // 5) 折叠结果统一返回一个NEWLINE
            return TokenFactory.NewLineToken(currentLine, stream.Column);
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
