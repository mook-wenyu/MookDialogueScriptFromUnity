using System.Text;

namespace MookDialogueScript.Lexers
{
    /// <summary>
    /// 文本Token处理器，处理默认的文本内容
    /// 设计原则：单一职责 - 专注于文本内容的处理和转义逻辑
    /// </summary>
    public class TextTokenizer : ITokenizer
    {
        public string Description => "文本内容Token处理器";
        public TokenType[] SupportedTokenTypes => new[] {TokenType.TEXT, TokenType.IDENTIFIER};

        /// <summary>
        /// 作为默认处理器，总是可以处理
        /// </summary>
        public bool CanHandle(CharacterStream stream, LexerState state, CharacterClassifier classifier)
        {
            // 在节点内容中，且不在其他特殊模式下，可以处理文本
            if (state.IsInNodeContent
                && !state.IsInCommandMode
                && !state.IsInStringMode
                && !state.IsInInterpolation)
            {
                // 排除一些明确的截断字符
                char c = stream.CurrentChar;
                if (c != '\0' && c != '\n' && c != '\r'
                    && c != ':' && c != '#' && c != '{'
                    && !stream.IsCommandStart()
                    && !stream.IsOptionMark())
                {
                    return true;
                }
            }

            // 在节点外部，处理元数据
            if (!state.IsInNodeContent && !stream.IsEOFMark())
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 处理文本Token
        /// </summary>
        public Token TryTokenize(CharacterStream stream, LexerState state, CharacterClassifier classifier)
        {
            // 在节点内容中处理文本
            if (state.IsInNodeContent)
            {
                return HandleText(stream, state, classifier);
            }

            // 在节点外部，处理元数据键值对
            if (!state.IsInNodeContent && stream.CurrentChar != ':')
            {
                return HandleMetadata(stream, state, classifier);
            }

            return null;
        }

        /// <summary>
        /// 处理文本内容（保持原有的复杂转义逻辑）
        /// </summary>
        private Token HandleText(CharacterStream stream, LexerState _, CharacterClassifier classifier)
        {
            var start = SourceLocation.FromStream(stream);
            var resultBuilder = new StringBuilder(64);

            // 在集合内的文本处理
            while (stream.CurrentChar != '\0' && stream.CurrentChar != '\n' && stream.CurrentChar != '\r')
            {
                // 普通文本模式
                // 处理转义字符
                if (stream.CurrentChar == '\\')
                {
                    char nextChar = stream.NextChar;
                    // 支持的转义字符
                    if (nextChar is ':' or '#' or '{' or '}' or '<' or '>' or '\'' or '"' or '\\' or '-' or '=')
                    {
                        stream.Advance(); // 跳过反斜杠

                        // 特殊处理：检查是否是\---或\===
                        if (nextChar == '-' && stream.GetCharAt(stream.Position + 1) == '-' && stream.GetCharAt(stream.Position + 2) == '-')
                        {
                            // \--- -> ---
                            resultBuilder.Append("---");
                            stream.Advance(); // 跳过第一个-
                            stream.Advance(); // 跳过第二个-
                            stream.Advance(); // 跳过第三个-
                        }
                        else if (nextChar == '=' && stream.GetCharAt(stream.Position + 1) == '=' && stream.GetCharAt(stream.Position + 2) == '=')
                        {
                            // \=== -> ===
                            resultBuilder.Append("===");
                            stream.Advance(); // 跳过第一个=
                            stream.Advance(); // 跳过第二个=
                            stream.Advance(); // 跳过第三个=
                        }
                        else
                        {
                            // 其他单字符转义
                            resultBuilder.Append(nextChar);
                            stream.Advance();
                        }
                    }
                    else
                    {
                        // 其他情况保留反斜杠
                        resultBuilder.Append(stream.CurrentChar);
                        stream.Advance();
                    }
                    continue;
                }

                // 检查截断字符
                char pre = stream.PreviousChar;
                if (stream.CurrentChar == ':' && pre != '\0' && !classifier.IsWhitespace(pre))
                {
                    break;
                }

                // 在集合内，文本会被<< >> # 等字符截断
                if (stream.IsCommandStart())
                {
                    // 遇到命令开始，停止文本处理
                    break;
                }
                if (stream.CurrentChar is '#' or '{')
                {
                    // 遇到标签或文本插值，停止文本处理
                    break;
                }

                // 正常字符处理
                resultBuilder.Append(stream.CurrentChar);
                stream.Advance();
            }

            return TokenFactory.TextToken(resultBuilder.ToString(), start.Line, start.Column);
        }

        /// <summary>
        /// 处理元数据键值对
        /// </summary>
        private Token HandleMetadata(CharacterStream stream, LexerState state, CharacterClassifier classifier)
        {
            var start = SourceLocation.FromStream(stream);

            // 收集键名（冒号前的内容）
            while (stream.CurrentChar != '\0'
                   && stream.CurrentChar != ':'
                   && stream.CurrentChar != '\n'
                   && stream.CurrentChar != '\r')
            {
                stream.Advance();
            }

            string key = stream.GetRange(start.Position, stream.Position).Trim();

            // 检查是否遇到冒号
            if (stream.CurrentChar is ':')
            {
                // 这里暂时只验证键名格式，值的验证在后续处理中进行
                if (!string.IsNullOrEmpty(key))
                {
                    return TokenFactory.IdentifierToken(key, start.Line, start.Column);
                }

                MLogger.Warning($"词法警告: 第{start.Line}行，第{start.Column}列，元数据缺少键名");
            }
            else if (stream.IsNewlineMark())
            {
                if (!string.IsNullOrEmpty(key))
                {
                    // 键值
                    return TokenFactory.TextToken(key, start.Line, start.Column);
                }

                MLogger.Warning($"词法警告: 第{start.Line}行，第{start.Column}列，元数据缺少键值");
            }

            // 如果没有冒号，这不是有效的元数据格式
            MLogger.Warning($"词法警告: 第{start.Line}行，第{start.Column}列，无效的元数据格式");
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
