using System;
using System.Collections.Generic;
using System.Text;

namespace MookDialogueScript
{
    public class Lexer
    {
        private readonly string _source;
        private int _position;
        private int _line;
        private int _column;
        private char _currentChar;
        private readonly Stack<int> _indentStack;
        private int _currentIndent;
        private readonly List<Token> _tokens;
        // 待输出的连续DEDENT数量（用于多级缩进回退时逐个发出）
        private int _pendingDedent;

        // 状态标志
        private bool _isInNodeContent;   // 是否在 --- 到 === 集合内
        private bool _isInCommandMode;   // 是否在 << >> 命令内
        private bool _isInStringMode;    // 是否在字符串模式中
        private bool _isInInterpolation; // 是否在 { } 插值表达式内
        private char _stringQuoteType;   // 当前字符串模式的引号类型

        // 关键字
        private static readonly Dictionary<string, TokenType> _keywords = new(StringComparer.OrdinalIgnoreCase)
        {
            {"if", TokenType.IF},
            {"elif", TokenType.ELIF},
            {"else", TokenType.ELSE},
            {"endif", TokenType.ENDIF},
            {"true", TokenType.TRUE},
            {"false", TokenType.FALSE},
            {"var", TokenType.VAR},
            {"set", TokenType.SET},
            {"add", TokenType.ADD},
            {"sub", TokenType.SUB},
            {"mul", TokenType.MUL},
            {"div", TokenType.DIV},
            {"mod", TokenType.MOD},
            {"jump", TokenType.JUMP},
            {"wait", TokenType.WAIT},
            {"eq", TokenType.EQUALS},
            {"is", TokenType.EQUALS},
            {"neq", TokenType.NOT_EQUALS},
            {"gt", TokenType.GREATER},
            {"lt", TokenType.LESS},
            {"gte", TokenType.GREATER_EQUALS},
            {"lte", TokenType.LESS_EQUALS},
            {"and", TokenType.AND},
            {"or", TokenType.OR},
            {"not", TokenType.NOT},
            {"xor", TokenType.XOR},
        };

        public Lexer(string source)
        {
            _source = source;
            _position = 0;
            _line = 1;
            _column = 1;
            UpdateCurrentChar();
            _indentStack = new Stack<int>();
            _indentStack.Push(0);
            _tokens = new List<Token>();
            _currentIndent = 0;
            _pendingDedent = 0;

            // 初始化新的状态标志
            _isInNodeContent = false;
            _isInCommandMode = false;
            _isInStringMode = false;
            _isInInterpolation = false;
            _stringQuoteType = '\0';
        }

        /// <summary>
        /// 只读预判：在列首判断该行是否为空行或注释行（允许前导空白）；同时检测是否混合缩进。
        /// 不改变 _position/_column，仅基于 GetCharAt/Peek 读取。
        /// </summary>
        private void ClassifyLineAtColumn1(out bool isEmptyOrCommentLine, out bool hasMixedIndent)
        {
            isEmptyOrCommentLine = false;
            hasMixedIndent = false;
            int p = _position;

            bool sawSpace = false;
            bool sawTab = false;

            // 跳过前导空白并检测混合缩进
            char c = GetCharAt(p);
            while (c is ' ' or '\t')
            {
                if (c == ' ') sawSpace = true;
                else sawTab = true;
                p++;
                c = GetCharAt(p);
            }

            hasMixedIndent = sawSpace && sawTab;

            // 空行或到达文件末尾
            if (c is '\n' or '\r' or '\0')
            {
                isEmptyOrCommentLine = true;
                return;
            }

            // 注释行：前导空白后是 //
            if (c == '/' && GetCharAt(p + 1) == '/')
            {
                isEmptyOrCommentLine = true;
            }
        }

        /// <summary>
        /// 获取所有Token
        /// </summary>
        /// <returns>所有Token</returns>
        public List<Token> Tokenize()
        {
            Token token;
            do
            {
                token = GetNextToken();
                _tokens.Add(token);
            } while (token.Type != TokenType.EOF);

            return _tokens;
        }

        /// <summary>
        /// 获取指定位置的字符，如果位置超出范围则返回'\0'
        /// </summary>
        /// <param name="position">要获取字符的位置</param>
        /// <returns>指定位置的字符，或者'\0'（如果位置超出范围）</returns>
        private char GetCharAt(int position)
        {
            return position < _source.Length ? _source[position] : '\0';
        }

        /// <summary>
        /// 获取指定范围的字符串
        /// </summary>
        private string GetRange(int start, int end)
        {
            return _source[start..end];
        }

        /// <summary>
        /// 检查当前位置是否被转义（前面有反斜杠）
        /// </summary>
        private bool IsEscaped()
        {
            if (_position == 0) return false;

            // 检查前一个字符是否是反斜杠
            char prevChar = GetCharAt(_position - 1);
            if (prevChar != '\\') return false;

            // 计算连续反斜杠的数量，只有奇数个反斜杠才表示转义
            int backslashCount = 0;
            int pos = _position - 1;
            while (pos >= 0 && GetCharAt(pos) == '\\')
            {
                backslashCount++;
                pos--;
            }

            // 奇数个反斜杠表示转义，偶数个表示反斜杠本身被转义
            return backslashCount % 2 == 1;
        }

        /// <summary>
        /// 更新当前字符
        /// </summary>
        private void UpdateCurrentChar()
        {
            _currentChar = GetCharAt(_position);
        }

        /// <summary>
        /// 前进一个字符
        /// </summary>
        private void Advance()
        {
            // #if UNITY_EDITOR
            // Debug.Log(_currentChar);
            // #endif

            _position++;
            _column++;
            UpdateCurrentChar();
        }

        /// <summary>
        /// 查看下一个字符
        /// </summary>
        private char Peek()
        {
            return GetCharAt(_position + 1);
        }

        /// <summary>
        /// </summary>
        private char Previous()
        {
            int pre = _position - 1;
            return pre >= 0 ? GetCharAt(pre) : '\0';
        }

        /// <summary>
        /// 判断字符是否为标识符起始字符（字母或下划线）
        /// </summary>
        private bool IsIdentifierStart(char c)
        {
            return char.IsLetter(c) || c == '_';
        }

        /// <summary>
        /// 判断字符是否为标识符组成部分（字母、数字或下划线）
        /// </summary>
        private bool IsIdentifierPart(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        /// <summary>
        /// 判断是否需要跳过缩进处理
        /// </summary>
        private bool ShouldSkipIndentation()
        {
            // 在行首仅对 EOF/换行跳过缩进处理；注释判定由调用方负责
            return _currentChar is '\0' or '\n' or '\r';
        }

        /// <summary>
        /// 统计当前行的缩进数量
        /// </summary>
        private int CountIndentation()
        {
            var indent = 0;
            bool seenSpace = false;
            bool seenTab = false;
            while (_currentChar is ' ' or '\t')
            {
                if (_currentChar == ' ') seenSpace = true;
                else if (_currentChar == '\t') seenTab = true;
                indent++;
                Advance();
            }
            if (seenSpace && seenTab)
            {
                MLogger.Warning($"词法警告: 第{_line}行，检测到混合缩进（空格与Tab）");
            }
            return indent;
        }

        /// <summary>
        /// 处理缩进增加的情况
        /// </summary>
        private Token HandleIndentIncrease(int indent)
        {
            _indentStack.Push(indent);
            _currentIndent = indent;
            return new Token(TokenType.INDENT, string.Empty, _line, _column);
        }

        /// <summary>
        /// 处理缩进减少的情况
        /// </summary>
        private Token HandleIndentDecrease(int indent)
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
                // 先返回一个 DEDENT，其余的记录到 pending 里，保持“每次只返回一个 Token”
                _pendingDedent = popCount - 1;
                return new Token(TokenType.DEDENT, string.Empty, _line, _column);
            }

            // 如果没有发生弹栈，但目标缩进与现有层级仍不一致，说明缩进不匹配
            if (_indentStack.Count == 0 || (_indentStack.Count > 0 && _indentStack.Peek() != indent))
            {
                // 避免产生“幽灵层级”：不将该缩进压栈，仅记录错误并保持现有缩进栈不变
                MLogger.Error($"词法错误: 第{_line}行，第{_column}列，无效的缩进（未匹配任何已知缩进层级）");
                // 保持 _currentIndent = _indentStack.Peek() 的值，不返回任何缩进Token
                return null;
            }

            // 未发生弹栈且缩进匹配时，不返回 Token
            return null;
        }

        /// <summary>
        /// 处理缩进
        /// </summary>
        private Token HandleIndentation()
        {
            if (_column != 1) return null;
            // 仅在集合内处理缩进
            if (!_isInNodeContent) return null;

            // 保存当前位置用于缩进计算后的恢复
            int savedPosition = _position;
            int savedColumn = _column;

            int indent = CountIndentation();

            // 恢复位置
            _position = savedPosition;
            _column = savedColumn;
            UpdateCurrentChar();

            if (ShouldSkipIndentation())
                return null;

            if (indent > _currentIndent)
                return HandleIndentIncrease(indent);
            if (indent < _currentIndent)
                return HandleIndentDecrease(indent);
            if (indent != _currentIndent)
            {
                MLogger.Error($"词法错误: 第{_line}行，第{_column}列，不一致的缩进");
                // 尝试恢复 - 使用当前缩进
                _currentIndent = indent;
            }

            return null;
        }

        /// <summary>
        /// 跳过空白字符
        /// </summary>
        private void SkipWhitespace()
        {
            while (_currentChar != '\0' && char.IsWhiteSpace(_currentChar) && _currentChar != '\n' && _currentChar != '\r')
            {
                Advance();
            }
        }

        /// <summary>
        /// 处理换行字符、跳过连续地换行和注释行
        /// </summary>
        /// <returns>换行Token或EOF Token</returns>
        private Token HandleNewlineAndComments()
        {
            int currentLine = _line;

            // 思路：推进“当前所在这一行”的行尾（含注释），并折叠后续的纯空行/纯注释行为一个 NEWLINE；
            // 但遇到第一行“内容行”时，停在该行列首（不吞其前导空白），让缩进处理在列首发生。

            // 1) 先推进到当前行的换行位置（保留列首给后续行）
            if (_currentChar == '/' && Peek() == '/')
            {
                // 跳过当前行注释内容
                while (_currentChar != '\0' && _currentChar != '\n' && _currentChar != '\r')
                {
                    Advance();
                }
            }

            // 2) 如果是 CRLF，先吃掉 \r 再由统一逻辑推进
            if (_currentChar == '\r' && Peek() == '\n')
            {
                Advance();
            }
            if (_currentChar is '\n' or '\r')
            {
                Advance();
                _line++;
                _column = 1;
            }

            // 3) 折叠连续的“仅空白+注释+换行”的行
            while (true)
            {
                // 使用只读游标预判下一行是否为空/注释行
                int p = _position;

                // 跳过前导空白（仅预读，不改变真实位置）
                char c = GetCharAt(p);
                while (c == ' ' || c == '\t')
                {
                    p++;
                    c = GetCharAt(p);
                }

                bool isCommentLine = c == '/' && GetCharAt(p + 1) == '/';
                bool isEmptyLine = c is '\n' or '\r' or '\0';

                if (!(isCommentLine || isEmptyLine))
                {
                    // 下一行是内容行：停止折叠，保持指针在该行列首（不吃空白）
                    break;
                }

                // 将“预读到的这一行”（注释行或空行）整体消耗掉到换行符末尾
                // 真正推进：跳过前导空白
                while (_currentChar is ' ' or '\t')
                {
                    Advance();
                }

                // 注释内容
                if (_currentChar == '/' && Peek() == '/')
                {
                    while (_currentChar != '\0' && _currentChar != '\n' && _currentChar != '\r')
                    {
                        Advance();
                    }
                }

                // CRLF 处理
                if (_currentChar == '\r' && Peek() == '\n')
                {
                    Advance();
                }

                if (_currentChar is '\n' or '\r')
                {
                    Advance();
                    _line++;
                    _column = 1;
                }
                else
                {
                    // EOF 或没有换行可推进
                    break;
                }
            }

            // 4) EOF 时优先补齐 DEDENT，再返回 EOF
            if (_currentChar == '\0')
            {
                if (_pendingDedent > 0)
                {
                    _pendingDedent--;
                    return new Token(TokenType.DEDENT, string.Empty, _line, _column);
                }
                if (_indentStack.Count > 1)
                {
                    _indentStack.Pop();
                    _currentIndent = _indentStack.Peek();
                    return new Token(TokenType.DEDENT, string.Empty, _line, _column);
                }
                return new Token(TokenType.EOF, string.Empty, _line, _column);
            }

            // 5) 折叠结果统一返回一个 NEWLINE
            return new Token(TokenType.NEWLINE, "\n", currentLine, _column);
        }

        /// <summary>
        /// 跳过注释和连续的注释行
        /// </summary>
        private void SkipComment()
        {
            // 跳过当前行的注释
            while (_currentChar != '\0' && _currentChar != '\n' && _currentChar != '\r')
            {
                Advance();
            }
        }

        /// <summary>
        /// 处理元数据键值对
        /// </summary>
        private Token HandleMetadata()
        {
            int startLine = _line;
            int startColumn = _column;
            int startPosition = _position;

            // 收集键名（冒号前的内容）
            while (_currentChar != '\0'
                   && _currentChar != ':'
                   && _currentChar != '\n'
                   && _currentChar != '\r')
            {
                Advance();
            }

            string key = GetRange(startPosition, _position).Trim();

            // 检查是否遇到冒号
            if (_currentChar is ':')
            {
                // 这里暂时只验证键名格式，值的验证在后续处理中进行
                if (!string.IsNullOrEmpty(key))
                {
                    return new Token(TokenType.IDENTIFIER, key, startLine, startColumn);
                }

                MLogger.Warning($"词法警告: 第{startLine}行，第{startColumn}列，元数据缺少键名");
                // 仍然返回Token，让上层处理
            }
            else if (_currentChar is '\r' or '\n')
            {
                if (!string.IsNullOrEmpty(key))
                {
                    // 键值
                    return new Token(TokenType.TEXT, key, startLine, startColumn);
                }

                MLogger.Warning($"词法警告: 第{startLine}行，第{startColumn}列，元数据缺少键值");
            }

            // 如果没有冒号，这不是有效的元数据格式
            MLogger.Warning($"词法警告: 第{startLine}行，第{startColumn}列，无效的元数据格式");
            return null;
        }

        /// <summary>
        /// 处理数字
        /// </summary>
        private Token HandleNumber()
        {
            if (!char.IsDigit(_currentChar)) return null;

            int startPosition = _position;
            int startLine = _line;
            int startColumn = _column;
            var hasDecimalPoint = false;

            while (_currentChar != '\0' &&
                   (char.IsDigit(_currentChar) || _currentChar == '.'))
            {
                if (_currentChar == '.')
                {
                    if (hasDecimalPoint)
                        break;
                    hasDecimalPoint = true;
                }
                Advance();
            }

            // 直接从源字符串切片，避免创建新字符串
            return new Token(TokenType.NUMBER, GetRange(startPosition, _position), startLine, startColumn);
        }

        /// <summary>
        /// 处理文本内容（在集合内的所有文本）
        /// </summary>
        private Token HandleText()
        {
            int startLine = _line;
            int startColumn = _column;
            var resultBuilder = new StringBuilder(64);

            // 在集合内的文本处理
            while (_currentChar != '\0' && _currentChar != '\n' && _currentChar != '\r')
            {
                if (_isInStringMode)
                {
                    // 字符串 有引号
                    // 处理转义字符
                    if (_currentChar == '\\')
                    {
                        char next = Peek();
                        if (next is '{' or '}' or '\'' or '"' or '\\')
                        {
                            Advance(); // 跳过反斜杠
                            resultBuilder.Append(next);
                        }
                        else
                        {
                            // 其他情况保留反斜杠
                            resultBuilder.Append(_currentChar);
                        }
                        Advance();
                        continue;
                    }

                    // 遇到结束引号或插值起始，停止由外层生成对应Token
                    if (IsClosingQuote(_currentChar) || _currentChar == '{')
                    {
                        break;
                    }
                }
                else
                {
                    // 文本
                    // 处理转义字符
                    if (_currentChar == '\\')
                    {
                        char nextChar = Peek();
                        // 支持的转义字符:
                        if (nextChar is ':' or '#' or '{' or '}' or '<' or '>' or '\'' or '"' or '\\' or '-' or '=')
                        {
                            Advance(); // 跳过反斜杠

                            // 特殊处理：检查是否是 \--- 或 \===
                            if (nextChar == '-' && GetCharAt(_position + 1) == '-' && GetCharAt(_position + 2) == '-')
                            {
                                // \--- -> ---
                                resultBuilder.Append("---");
                                Advance(); // 跳过第一个 -
                                Advance(); // 跳过第二个 -
                                Advance(); // 跳过第三个 -
                            }
                            else if (nextChar == '=' && GetCharAt(_position + 1) == '=' && GetCharAt(_position + 2) == '=')
                            {
                                // \=== -> ===
                                resultBuilder.Append("===");
                                Advance(); // 跳过第一个 =
                                Advance(); // 跳过第二个 =
                                Advance(); // 跳过第三个 =
                            }
                            else
                            {
                                // 其他单字符转义
                                resultBuilder.Append(nextChar);
                                Advance();
                            }
                        }
                        else
                        {
                            // 其他情况保留反斜杠
                            resultBuilder.Append(_currentChar);
                            Advance();
                        }
                        continue;
                    }

                    // 检查截断字符
                    char pre = Previous();
                    if (_currentChar == ':' && pre != '\0' && !char.IsWhiteSpace(pre))
                    {
                        break;
                    }
                    // 在集合内，文本会被 << >> # 等字符截断
                    if (_currentChar is '<' && Peek() == '<')
                    {
                        // 遇到命令开始，停止文本处理
                        break;
                    }
                    if (_currentChar is '#' or '{')
                    {
                        // 遇到标签或文本插值，停止文本处理
                        break;
                    }
                }

                // 正常字符处理
                resultBuilder.Append(_currentChar);
                Advance();
            }

            return new Token(TokenType.TEXT, resultBuilder.ToString(), startLine, startColumn);
        }

        /// <summary>
        /// 判断当前字符是否是当前字符串模式的结束引号
        /// </summary>
        private bool IsClosingQuote(char c)
        {
            if (!_isInStringMode) return false;

            return _stringQuoteType switch
            {
                '\'' => c == '\'', // 英文单引号
                '"' => c == '"',   // 英文双引号
                _ => false
            };
        }

        /// <summary>
        /// 处理变量
        /// </summary>
        private Token HandleVariable()
        {
            if (!_isInNodeContent || _currentChar != '$') return null;

            int startLine = _line;
            int startColumn = _column;
            Advance(); // 跳过$符号
            int startPosition = _position;

            if (_currentChar != '\0' && IsIdentifierStart(_currentChar))
            {
                Advance();

                while (_currentChar != '\0' && IsIdentifierPart(_currentChar))
                {
                    Advance();
                }
            }

            return new Token(TokenType.VARIABLE, GetRange(startPosition, _position), startLine, startColumn);
        }

        /// <summary>
        /// 处理标识符或关键字
        /// </summary>
        private Token HandleIdentifierOrKeyword()
        {
            if (!IsIdentifierStart(_currentChar)) return null;

            int startPosition = _position;
            int startLine = _line;
            int startColumn = _column;

            // 确保第一个字符是有效的标识符起始字符
            if (_currentChar != '\0' && IsIdentifierStart(_currentChar))
            {
                Advance();

                // 收集剩余的标识符字符
                while (_currentChar != '\0' && IsIdentifierPart(_currentChar))
                {
                    Advance();
                }
            }

            string text = GetRange(startPosition, _position);

            // 检查是否是关键字（忽略大小写）
            if (_keywords.TryGetValue(text, out var type))
            {
                return new Token(type, text, startLine, startColumn);
            }

            // 所有非关键字的标识符都作为IDENTIFIER处理
            return new Token(TokenType.IDENTIFIER, text, startLine, startColumn);
        }

        /// <summary>
        /// 处理命令内容（《《 >> 之间的表达式）
        /// </summary>
        private Token HandleCommand()
        {
            if (!_isInNodeContent) return null;

            if (_currentChar == '<' && Peek() == '<')
            {
                // 处理命令开始
                int startLine = _line;
                int startColumn = _column;
                Advance(); // 跳过第一个 <
                Advance(); // 跳过第二个 <
                _isInCommandMode = true;
                return new Token(TokenType.COMMAND_START, "<<", startLine, startColumn);
            }
            if (_currentChar == '>' && Peek() == '>')
            {
                // 处理命令结束
                int startLine = _line;
                int startColumn = _column;
                Advance(); // 跳过第一个 >
                Advance(); // 跳过第二个 >
                _isInCommandMode = false;
                return new Token(TokenType.COMMAND_END, ">>", startLine, startColumn);
            }
            return null;
        }

        /// <summary>
        /// 获取下一个Token并消耗它
        /// </summary>
        /// <returns>下一个Token</returns>
        private Token GetNextToken()
        {
            // 先输出 pending 的 DEDENT，以保持“每次只返回一个 Token”
            if (_pendingDedent > 0)
            {
                _pendingDedent--;
                return new Token(TokenType.DEDENT, string.Empty, _line, _column);
            }

            // 如果已到输入末尾，优先补齐所有未关闭的缩进
            if (_currentChar == '\0')
            {
                if (_indentStack.Count > 1)
                {
                    _indentStack.Pop();
                    _currentIndent = _indentStack.Peek();
                    return new Token(TokenType.DEDENT, string.Empty, _line, _column);
                }
                return new Token(TokenType.EOF, string.Empty, _line, _column);
            }

            while (_currentChar != '\0')
            {
                // 1. 处理行首缩进（仅在集合内需要）
                if (_column == 1 && _isInNodeContent)
                {
                    // 列首预判：空行/注释行直接批量跳过并返回一个 NEWLINE
                    ClassifyLineAtColumn1(out bool isEmptyOrComment, out bool hasMixedIndent);
                    if (hasMixedIndent)
                    {
                        MLogger.Warning($"词法警告: 第{_line}行，检测到混合缩进（空格与制表符混用）");
                    }
                    if (isEmptyOrComment)
                    {
                        return HandleNewlineAndComments();
                    }

                    // 内容行才进行缩进比较
                    var indentToken = HandleIndentation();
                    if (indentToken != null)
                    {
                        return indentToken;
                    }

                    // 缩进层级未变化（或已完成变化后的下一次进入）：
                    // 消费本行与当前缩进对应的前导空白，使后续能从第一个非空白字符开始识别（例如 '->' 识别为 ARROW）。
                    while (_currentChar is ' ' or '\t')
                    {
                        Advance();
                    }
                }

                // 2. 跳过空白字符（非文本模式下或在插值表达式内）
                if (!_isInNodeContent || _isInCommandMode || _isInInterpolation)
                {
                    if (char.IsWhiteSpace(_currentChar) && _currentChar != '\n' && _currentChar != '\r')
                    {
                        SkipWhitespace();
                        continue;
                    }
                }

                // 3. 处理注释和换行
                if (((_currentChar == '/' && Peek() == '/')) || _currentChar == '\n' || _currentChar == '\r')
                {
                    if (_currentChar == '/' && Peek() == '/')
                    {
                        SkipComment();
                        if (_currentChar == '\0')
                        {
                            return new Token(TokenType.EOF, string.Empty, _line, _column);
                        }
                        if (_currentChar != '\n' && _currentChar != '\r')
                        {
                            continue;
                        }
                    }
                    return HandleNewlineAndComments();
                }

                // 处理元数据模式或特殊的冒号
                if (!_isInNodeContent && IsIdentifierStart(_currentChar))
                {
                    // 是元数据
                    var meta = HandleMetadata();
                    if (meta != null)
                    {
                        return meta;
                    }
                }

                // 处理节点标记 ===（仅在节点内容内，且不在命令/插值/字符串模式）
                if (_isInNodeContent && !_isInCommandMode && !_isInInterpolation && !_isInStringMode
                    && _currentChar == '=' && Peek() == '=' && GetCharAt(_position + 2) == '=')
                {
                    // 检查是否被转义（前面有反斜杠）
                    if (!IsEscaped())
                    {
                        int startLine = _line;
                        int startColumn = _column;

                        Advance();
                        Advance();
                        Advance();
                        _isInNodeContent = false;
                        return new Token(TokenType.NODE_END, "===", startLine, startColumn);
                    }
                }

                // 处理引号文本
                if (_isInStringMode && !IsClosingQuote(_currentChar) && !_isInInterpolation && _currentChar != '{')
                {
                    return HandleText();
                }

                // 处理普通文本
                if (_isInNodeContent
                    && !_isInInterpolation
                    && !_isInCommandMode
                    && _currentChar != ':'
                    && _currentChar != '#'
                    && _currentChar != '{'
                    && !(_currentChar == '<' && Peek() == '<')
                    && !(_currentChar == '-' && Peek() == '>'))
                {
                    return HandleText();
                }

                if (_currentChar is ':')
                {
                    int startLine = _line;
                    int startColumn = _column;
                    Advance();

                    // 在元数据模式下，这是分隔符
                    if (!_isInNodeContent)
                    {
                        return new Token(TokenType.METADATA_SEPARATOR, ":", startLine, startColumn);
                    }

                    // 在集合内，这是普通冒号
                    return new Token(TokenType.COLON, ":", startLine, startColumn);
                }

                // 在集合外，处理节点标记 ---（且不在命令/插值/字符串模式）
                if (!_isInNodeContent && !_isInCommandMode && !_isInInterpolation && !_isInStringMode)
                {
                    // 检查是否是节点标记 ---
                    if (_currentChar == '-' && Peek() == '-' && GetCharAt(_position + 2) == '-')
                    {
                        // 检查是否被转义（前面有反斜杠）
                        if (!IsEscaped())
                        {
                            int startLine = _line;
                            int startColumn = _column;

                            // --- 识别为 NODE_START
                            Advance();
                            Advance();
                            Advance();
                            _isInNodeContent = true;
                            return new Token(TokenType.NODE_START, "---", startLine, startColumn);
                        }
                    }
                }

                // 处理数字
                var num = HandleNumber();
                if (num != null)
                {
                    return num;
                }

                // 处理命令 << >>
                var commandToken = HandleCommand();
                if (commandToken != null)
                {
                    return commandToken;
                }

                // 处理变量
                var hVar = HandleVariable();
                if (hVar != null)
                {
                    return hVar;
                }

                // 处理标识符和关键字
                var idOrKey = HandleIdentifierOrKeyword();
                if (idOrKey != null)
                {
                    return idOrKey;
                }

                // 处理字符串引号
                if (!_isInStringMode && _currentChar is '\'' or '"')
                {
                    char quoteChar = _currentChar;
                    int startLine = _line;
                    int startColumn = _column;
                    _isInStringMode = true;
                    _stringQuoteType = quoteChar;
                    Advance();
                    return new Token(TokenType.QUOTE, quoteChar.ToString(), startLine, startColumn);
                }

                // 处理字符串结束引号
                if (_isInStringMode && IsClosingQuote(_currentChar))
                {
                    char quoteChar = _currentChar;
                    int startLine = _line;
                    int startColumn = _column;
                    Advance();
                    _isInStringMode = false;
                    _stringQuoteType = '\0';
                    return new Token(TokenType.QUOTE, quoteChar.ToString(), startLine, startColumn);
                }

                // 12. 处理操作符和标点符号
                switch (_currentChar)
                {
                    case '(':
                    {
                        int startLine = _line;
                        int startColumn = _column;
                        Advance();
                        return new Token(TokenType.LEFT_PAREN, "(", startLine, startColumn);
                    }
                    case ')':
                    {
                        int startLine = _line;
                        int startColumn = _column;
                        Advance();
                        return new Token(TokenType.RIGHT_PAREN, ")", startLine, startColumn);
                    }
                    case '{':
                    {
                        int startLine = _line;
                        int startColumn = _column;
                        Advance();
                        _isInInterpolation = true;
                        return new Token(TokenType.LEFT_BRACE, "{", startLine, startColumn);
                    }
                    case '}':
                    {
                        int startLine = _line;
                        int startColumn = _column;
                        Advance();
                        _isInInterpolation = false;
                        return new Token(TokenType.RIGHT_BRACE, "}", startLine, startColumn);
                    }
                    case '[':
                    {
                        int startLine = _line;
                        int startColumn = _column;
                        Advance();
                        return new Token(TokenType.LEFT_BRACKET, "[", startLine, startColumn);
                    }
                    case ']':
                    {
                        int startLine = _line;
                        int startColumn = _column;
                        Advance();
                        return new Token(TokenType.RIGHT_BRACKET, "]", startLine, startColumn);
                    }
                    case '#':
                    {
                        int startLine = _line;
                        int startColumn = _column;
                        Advance();
                        return new Token(TokenType.HASH, "#", startLine, startColumn);
                    }
                    case '.':
                    {
                        // 需要区分小数点和成员访问的点号
                        // 如果下一个字符是数字，且前面没有数字，则这可能是小数点
                        // 但这里我们在表达式/命令模式下，简单处理为DOT token
                        int startLine = _line;
                        int startColumn = _column;
                        Advance();
                        return new Token(TokenType.DOT, ".", startLine, startColumn);
                    }
                    case ',':
                    {
                        int startLine = _line;
                        int startColumn = _column;
                        Advance();
                        return new Token(TokenType.COMMA, ",", startLine, startColumn);
                    }
                    case '+':
                    {
                        int startLine = _line;
                        int startColumn = _column;
                        Advance();
                        return new Token(TokenType.PLUS, "+", startLine, startColumn);
                    }
                    case '-':
                    {
                        int startLine = _line;
                        int startColumn = _column;
                        Advance();
                        if (_currentChar == '>')
                        {
                            // -> 识别为 ARROW
                            Advance();
                            return new Token(TokenType.ARROW, "->", startLine, startColumn);
                        }
                        return new Token(TokenType.MINUS, "-", startLine, startColumn);
                    }
                    case '*':
                    {
                        int startLine = _line;
                        int startColumn = _column;
                        Advance();
                        return new Token(TokenType.MULTIPLY, "*", startLine, startColumn);
                    }
                    case '/':
                    {
                        int startLine = _line;
                        int startColumn = _column;
                        Advance();
                        return new Token(TokenType.DIVIDE, "/", startLine, startColumn);
                    }
                    case '%':
                    {
                        int startLine = _line;
                        int startColumn = _column;
                        Advance();
                        return new Token(TokenType.MODULO, "%", startLine, startColumn);
                    }
                    case '=':
                    {
                        int startLine = _line;
                        int startColumn = _column;
                        Advance();
                        if (_currentChar == '=')
                        {
                            Advance();
                            return new Token(TokenType.EQUALS, "==", startLine, startColumn);
                        }
                        return new Token(TokenType.ASSIGN, "=", startLine, startColumn);
                    }
                    case '!':
                    {
                        int startLine = _line;
                        int startColumn = _column;
                        Advance();
                        if (_currentChar == '=')
                        {
                            Advance();
                            return new Token(TokenType.NOT_EQUALS, "!=", startLine, startColumn);
                        }
                        return new Token(TokenType.NOT, "!", startLine, startColumn);
                    }
                    case '>':
                    {
                        int startLine = _line;
                        int startColumn = _column;
                        Advance();
                        if (_currentChar == '=')
                        {
                            Advance();
                            return new Token(TokenType.GREATER_EQUALS, ">=", startLine, startColumn);
                        }
                        return new Token(TokenType.GREATER, ">", startLine, startColumn);
                    }
                    case '<':
                    {
                        int startLine = _line;
                        int startColumn = _column;
                        Advance();
                        if (_currentChar == '=')
                        {
                            Advance();
                            return new Token(TokenType.LESS_EQUALS, "<=", startLine, startColumn);
                        }
                        return new Token(TokenType.LESS, "<", startLine, startColumn);
                    }
                    case '&':
                    {
                        int startLine = _line;
                        int startColumn = _column;
                        Advance();
                        if (_currentChar == '&')
                        {
                            Advance();
                            return new Token(TokenType.AND, "&&", startLine, startColumn);
                        }
                        break;
                    }
                    case '|':
                    {
                        int startLine = _line;
                        int startColumn = _column;
                        Advance();
                        if (_currentChar == '|')
                        {
                            Advance();
                            return new Token(TokenType.OR, "||", startLine, startColumn);
                        }
                        break;
                    }
                }

                // 其他情况
                return HandleText();
            }

            // 文件结束
            return new Token(TokenType.EOF, string.Empty, _line, _column);
        }
    }
}
