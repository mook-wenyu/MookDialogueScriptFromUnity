using System;
using System.Collections.Generic;
using System.Text;

namespace MookDialogueScript
{
    /// <summary>
    /// 定义文本处理规则，包括截断字符和转义字符
    /// </summary>
    public readonly struct TextProcessingRules
    {
        public char[] TruncateChars { get; }
        public char[] EscapeChars { get; }

        public TextProcessingRules(char[] truncateChars, char[] escapeChars)
        {
            TruncateChars = truncateChars;
            EscapeChars = escapeChars;
        }
    }

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
        
        // 新的状态标志
        private bool _isInNodeContent;     // 是否在 --- 到 === 集合内
        private bool _isInCommandMode;     // 是否在 << >> 命令内
        private bool _isInStringMode;      // 是否在字符串模式中
        private char _stringQuoteType;     // 当前字符串模式的引号类型

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
            {"call", TokenType.CALL},
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
            
            // 初始化新的状态标志
            _isInNodeContent = false;
            _isInCommandMode = false;
            _isInStringMode = false;
            _stringQuoteType = '\0';
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
                
                // 验证集合外内容的合法性
                if (ValidateOutsideContent(token))
                {
                    _tokens.Add(token);
                    MLogger.Debug($"{token}");
                }
                else
                {
                    // 非法的Token被忽略，但继续处理
                    MLogger.Debug($"忽略非法Token: {token}");
                    
                    // 如果不是EOF，继续处理下一个Token
                    if (token.Type != TokenType.EOF)
                        continue;
                    else
                    {
                        // EOF必须添加
                        _tokens.Add(token);
                    }
                }
                
            } while (token.Type != TokenType.EOF);

            return _tokens;
        }

        /// <summary>
        /// 获取最后一个生成的Token
        /// </summary>
        /// <returns>最后一个Token，如果没有则返回null</returns>
        private Token GetLastToken()
        {
            return _tokens.Count > 0 ? _tokens[^1] : null;
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
            while (_currentChar != '\0' && _currentChar != ':' && _currentChar != '：' && 
                   _currentChar != '\n' && _currentChar != '\r')
            {
                Advance();
            }

            string key = GetRange(startPosition, _position).Trim();
            
            // 检查是否遇到冒号
            if (_currentChar is ':' or '：')
            {
                // 这里暂时只验证键名格式，值的验证在后续处理中进行
                if (string.IsNullOrWhiteSpace(key))
                {
                    MLogger.Warning($"词法警告: 第{startLine}行，第{startColumn}列，元数据缺少键名");
                    // 仍然返回Token，让上层处理
                }
                
                return new Token(TokenType.IDENTIFIER, key, startLine, startColumn);
            }
            
            // 如果没有冒号，这不是有效的元数据格式
            MLogger.Warning($"词法警告: 第{startLine}行，第{startColumn}列，无效的元数据格式，缺少冒号");
            return new Token(TokenType.TEXT, key, startLine, startColumn);
        }

        /// <summary>
        /// 验证集合外内容是否合法
        /// </summary>
        /// <param name="token">要验证的Token</param>
        /// <returns>是否合法</returns>
        private bool ValidateOutsideContent(Token token)
        {
            // 如果在集合内，不需要验证
            if (_isInNodeContent)
                return true;

            // 允许的Token类型
            switch (token.Type)
            {
                case TokenType.IDENTIFIER:      // 元数据键名
                case TokenType.METADATA_SEPARATOR: // 冒号
                case TokenType.TEXT:            // 元数据值
                case TokenType.NEWLINE:         // 换行
                case TokenType.EOF:             // 文件结束
                case TokenType.INDENT:          // 缩进
                case TokenType.DEDENT:          // 取消缩进
                case TokenType.NODE_START:      // 节点开始 ---
                    return true;

                // 不允许的内容
                case TokenType.ARROW:           // 选项 ->
                    MLogger.Warning($"词法警告: 第{token.Line}行，第{token.Column}列，选项只能在集合内使用");
                    return false;

                case TokenType.COMMAND_START:   // 命令 <<
                case TokenType.COMMAND_END:     // 命令 >>
                    MLogger.Warning($"词法警告: 第{token.Line}行，第{token.Column}列，命令只能在集合内使用");
                    return false;

                case TokenType.HASH:            // 标签 #
                    MLogger.Warning($"词法警告: 第{token.Line}行，第{token.Column}列，标签只能在集合内使用");
                    return false;

                case TokenType.VARIABLE:        // 变量 $var
                    MLogger.Warning($"词法警告: 第{token.Line}行，第{token.Column}列，变量只能在集合内使用");
                    return false;

                case TokenType.LEFT_BRACE:      // 变量插值 {
                case TokenType.RIGHT_BRACE:     // 变量插值 }
                    MLogger.Warning($"词法警告: 第{token.Line}行，第{token.Column}列，变量插值只能在集合内使用");
                    return false;

                case TokenType.COLON:           // 普通冒号（非元数据分隔符）
                    MLogger.Warning($"词法警告: 第{token.Line}行，第{token.Column}列，对话格式只能在集合内使用");
                    return false;

                default:
                    // 其他不明确的内容也警告
                    MLogger.Warning($"词法警告: 第{token.Line}行，第{token.Column}列，此内容只能在集合内使用: {token.Type}");
                    return false;
            }
        }

        /// <summary>
        /// 获取倒数第n个Token
        /// </summary>
        /// <param name="index">倒数第几个，从1开始</param>
        /// <returns>对应位置的Token，如果index超出范围则返回null</returns>
        public Token GetTokenFromLast(int index = 1)
        {
            if (index <= 0 || index > _tokens.Count)
                return null;
            return _tokens[^index];
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
            return _currentChar is '\0' or '\n' or '\r' or '/';
        }

        /// <summary>
        /// 统计当前行的缩进数量
        /// </summary>
        private int CountIndentation()
        {
            int indent = 0;
            while (_currentChar is ' ' or '\t')
            {
                indent++;
                Advance();
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
            // 只删除一个缩进级别
            if (_indentStack.Count > 1 && _indentStack.Peek() > indent)
            {
                _indentStack.Pop();
                // 更新当前缩进为栈顶缩进值
                _currentIndent = _indentStack.Peek();

                return new Token(TokenType.DEDENT, string.Empty, _line, _column);
            }

            // 如果缩进不匹配任何已知级别，报错并尝试恢复
            if (_indentStack.Count == 0 || (_indentStack.Count > 0 && _indentStack.Peek() != indent))
            {
                MLogger.Error($"词法错误: 第{_line}行，第{_column}列，无效的缩进");
                // 尝试恢复 - 添加当前缩进到栈中
                if (_indentStack.Count == 0)
                {
                    _indentStack.Push(indent);
                }
                _currentIndent = indent;
            }

            return new Token(TokenType.DEDENT, string.Empty, _line, _column);
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
        /// 处理换行字符、跳过连续的换行和注释行
        /// </summary>
        /// <returns>换行Token或EOF Token</returns>
        private Token HandleNewlineAndComments()
        {
            // 记录当前行号用于生成Token
            int currentLine = _line;

            // 如果遇到换行，处理状态退出
            if (_isInCommandMode)
            {
                MLogger.Error($"词法错误: 第{_line}行，第{_column}列，命令未闭合就遇到了换行");
                _isInCommandMode = false;
            }
            
            // 如果在字符串模式下遇到换行，这通常是一个错误，但我们仍然退出该模式
            if (_isInStringMode)
            {
                MLogger.Error($"词法错误: 第{_line}行，第{_column}列，字符串未闭合就遇到了换行");
                _isInStringMode = false;
                _stringQuoteType = '\0';
            }

            // 处理当前换行
            if (_currentChar == '\r' && Peek() == '\n')
            {
                Advance();
            }
            Advance();
            _line++;
            _column = 1;

            // 循环处理连续的空行和注释行
            while (true)
            {
                // 如果列号为1（行首），预检查缩进
                if (_column == 1)
                {
                    // 查看当前行是否是空行或注释行
                    bool isCommentOrEmptyLine = false;

                    // 暂存当前位置
                    int savedPosition = _position;
                    int savedColumn = _column;

                    // 跳过空白字符计算缩进
                    int indent = 0;
                    while (_currentChar is ' ' or '\t')
                    {
                        indent++;
                        Advance();
                    }

                    // 检查跳过空白后是否是注释或空行
                    if (_currentChar == '/' && Peek() == '/' ||
                        _currentChar == '\n' || _currentChar == '\r' ||
                        _currentChar == '\0')
                    {
                        isCommentOrEmptyLine = true;
                    }

                    // 如果不是注释行或空行，且缩进与当前缩进不同，则回退并退出循环
                    if (!isCommentOrEmptyLine && indent != _currentIndent)
                    {
                        // 恢复位置，让GetNextToken处理缩进
                        _position = savedPosition;
                        _column = savedColumn;
                        UpdateCurrentChar();
                        break;
                    }

                    // 如果是注释行或空行，继续处理
                    if (isCommentOrEmptyLine)
                    {
                        // 对于连续的注释行和空行，跳过缩进处理
                    }
                }

                // 跳过行首空白
                SkipWhitespace();

                // 检查是否是注释行
                if (_currentChar == '/' && Peek() == '/')
                {
                    // 跳过这一行的注释内容
                    while (_currentChar != '\0' && _currentChar != '\n' && _currentChar != '\r')
                    {
                        Advance();
                    }

                    // 如果注释后没有换行了，结束循环
                    if (_currentChar != '\n' && _currentChar != '\r')
                    {
                        break;
                    }
                }
                // 检查是否是空行
                else if (_currentChar is '\n' or '\r')
                {
                    // 处理换行（空行）
                }
                else
                {
                    // 不是空行也不是注释行，结束循环
                    break;
                }

                // 处理换行
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
                    break; // 如果没有换行了，结束循环
                }
            }

            // 如果处理完换行和注释后到达了文件末尾，直接返回EOF
            if (_currentChar == '\0')
            {
                return new Token(TokenType.EOF, string.Empty, _line, _column);
            }

            // 返回一个NEWLINE token，无论有几个连续的换行和注释
            return new Token(TokenType.NEWLINE, "\\n", currentLine, _column);
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
        /// 处理缩进
        /// </summary>
        private Token HandleIndentation()
        {
            // 保存当前位置
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
            else if (indent < _currentIndent)
                return HandleIndentDecrease(indent);
            else if (indent != _currentIndent)
            {
                MLogger.Error($"词法错误: 第{_line}行，第{_column}列，不一致的缩进");
                // 尝试恢复 - 使用当前缩进
                _currentIndent = indent;
            }

            return null;
        }

        /// <summary>
        /// 处理数字
        /// </summary>
        private Token HandleNumber()
        {
            int startPosition = _position;
            int startLine = _line;
            int startColumn = _column;
            bool hasDecimalPoint = false;

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
            StringBuilder resultBuilder = new StringBuilder(64);

            // 在集合内的文本处理
            while (_currentChar != '\0' && _currentChar != '\n' && _currentChar != '\r')
            {
                // 处理转义字符
                if (_currentChar == '\\')
                {
                    char nextChar = Peek();
                    // 支持的转义字符: \: \<< \\
                    if (nextChar is ':' or '：' or '<' or '\\')
                    {
                        Advance(); // 跳过反斜杠
                        resultBuilder.Append(nextChar);
                        Advance();
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
                // 在集合内，文本会被 << >> # 等字符截断
                if (_currentChar is '<' && Peek() == '<')
                {
                    // 遇到命令开始，停止文本处理
                    break;
                }
                
                if (_currentChar == '#')
                {
                    // 遇到标签，停止文本处理
                    break;
                }

                if (_currentChar == '{')
                {
                    // 遇到变量插值，停止文本处理
                    break;
                }

                // 正常字符处理
                resultBuilder.Append(_currentChar);
                Advance();
            }

            string result = resultBuilder.ToString();
            
            // 处理角色名判断（冒号前无空格）
            if (!_isInCommandMode && !_isInStringMode && result.Contains(':'))
            {
                int colonIndex = result.IndexOf(':');
                if (colonIndex > 0)
                {
                    string beforeColon = result[..colonIndex];
                    // 检查冒号前是否有空格
                    if (!beforeColon.EndsWith(' ') && !beforeColon.EndsWith('\t'))
                    {
                        // 这是角色名，只返回冒号前的部分
                        string characterName = beforeColon.Trim();
                        // 调整位置到冒号处
                        int rollbackCount = result.Length - colonIndex;
                        for (int i = 0; i < rollbackCount; i++)
                        {
                            _position--;
                            _column--;
                        }
                        UpdateCurrentChar();
                        
                        return new Token(TokenType.TEXT, characterName, startLine, startColumn);
                    }
                }
            }
            
            return new Token(TokenType.TEXT, result, startLine, startColumn);
        }

        /// <summary>
        /// 判断当前字符是否是当前字符串模式的结束引号
        /// </summary>
        private bool IsClosingQuote(char c)
        {
            if (!_isInStringMode) return false;

            switch (_stringQuoteType)
            {
                case '\u2018': return c == '\u2019'; // 中文单引号
                case '\u201C': return c == '\u201D'; // 中文双引号
                case '\'': return c == '\'';         // 英文单引号
                case '"': return c == '"';           // 英文双引号
                default: return false;
            }
        }

        /// <summary>
        /// 处理变量
        /// </summary>
        private Token HandleVariable()
        {
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
            if (_keywords.TryGetValue(text, out TokenType type))
            {
                return new Token(type, text, startLine, startColumn);
            }

            // 所有非关键字的标识符都作为IDENTIFIER处理
            return new Token(TokenType.IDENTIFIER, text, startLine, startColumn);
        }

        /// <summary>
        /// 处理命令内容（<< >> 之间的表达式）
        /// </summary>
        private Token HandleCommand()
        {
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
            else if (_currentChar == '>' && Peek() == '>')
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
            while (_currentChar != '\0')
            {
                // 1. 处理行首缩进（仅在集合内需要）
                if (_column == 1 && _isInNodeContent)
                {
                    if (_currentChar != '/' || Peek() != '/')
                    {
                        Token indentToken = HandleIndentation();
                        if (indentToken != null)
                        {
                            return indentToken;
                        }
                    }
                }

                // 2. 跳过空白字符（非文本模式下）
                if (!_isInNodeContent || _isInCommandMode)
                {
                    if (char.IsWhiteSpace(_currentChar) && _currentChar != '\n' && _currentChar != '\r')
                    {
                        SkipWhitespace();
                        continue;
                    }
                }

                // 3. 处理注释和换行
                if (_currentChar == '/' && Peek() == '/' || _currentChar == '\n' || _currentChar == '\r')
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

                // 4. 处理命令 << >>
                Token commandToken = HandleCommand();
                if (commandToken != null)
                {
                    return commandToken;
                }

                // 5. 处理节点标记 --- 和 ===
                if (_currentChar == '-' && Peek() == '-')
                {
                    int startPosition = _position;
                    int startLine = _line;
                    int startColumn = _column;
                    
                    Advance();
                    Advance();
                    if (_currentChar == '-')
                    {
                        Advance();
                        _isInNodeContent = true;
                        return new Token(TokenType.NODE_START, "---", startLine, startColumn);
                    }
                    else
                    {
                        // 不是三个减号，回退并当做文本处理
                        _position = startPosition;
                        _column = startColumn;
                        UpdateCurrentChar();
                        return HandleText();
                    }
                }

                if (_currentChar == '=' && Peek() == '=')
                {
                    int startPosition = _position;
                    int startLine = _line;
                    int startColumn = _column;
                    
                    Advance();
                    Advance();
                    if (_currentChar == '=')
                    {
                        Advance();
                        _isInNodeContent = false;
                        return new Token(TokenType.NODE_END, "===", startLine, startColumn);
                    }
                    else
                    {
                        // 处理 == 操作符
                        return new Token(TokenType.EQUALS, "==", startLine, startColumn);
                    }
                }

                // 6. 处理元数据模式或特殊的冒号
                if (!_isInNodeContent && IsIdentifierStart(_currentChar))
                {
                    // 可能是元数据键名
                    return HandleMetadata();
                }

                if (_currentChar is ':' or '：')
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

                // 7. 处理选项箭头 ->
                if (_currentChar == '-' && Peek() == '>')
                {
                    int startLine = _line;
                    int startColumn = _column;
                    Advance();
                    Advance();
                    return new Token(TokenType.ARROW, "->", startLine, startColumn);
                }

                // 8. 处理变量
                if (_currentChar is '$' or '￥')
                {
                    return HandleVariable();
                }

                // 9. 处理标识符和关键字
                if (IsIdentifierStart(_currentChar))
                {
                    return HandleIdentifierOrKeyword();
                }

                // 10. 处理数字
                if (char.IsDigit(_currentChar))
                {
                    return HandleNumber();
                }

                // 11. 处理字符串引号（仅在命令模式下）
                if (_isInCommandMode && _currentChar is '\'' or '"' or '\u2018' or '\u201C')
                {
                    if (!_isInStringMode)
                    {
                        char quoteChar = _currentChar;
                        int startLine = _line;
                        int startColumn = _column;
                        _isInStringMode = true;
                        _stringQuoteType = quoteChar;
                        Advance();
                        return new Token(TokenType.QUOTE, quoteChar.ToString(), startLine, startColumn);
                    }
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
                    case '（':
                        {
                            int startLine = _line;
                            int startColumn = _column;
                            Advance();
                            return new Token(TokenType.LEFT_PAREN, "(", startLine, startColumn);
                        }
                    case ')':
                    case '）':
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
                            return new Token(TokenType.LEFT_BRACE, "{", startLine, startColumn);
                        }
                    case '}':
                        {
                            int startLine = _line;
                            int startColumn = _column;
                            Advance();
                            return new Token(TokenType.RIGHT_BRACE, "}", startLine, startColumn);
                        }
                    case '#':
                        {
                            int startLine = _line;
                            int startColumn = _column;
                            Advance();
                            return new Token(TokenType.HASH, "#", startLine, startColumn);
                        }
                    case ',':
                    case '，':
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
                            return new Token(TokenType.ASSIGN, "=", startLine, startColumn);
                        }
                    case '!':
                    case '！':
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
                            // 单个&作为文本处理
                            _position--;
                            _column--;
                            UpdateCurrentChar();
                            return HandleText();
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
                            // 单个|作为文本处理
                            _position--;
                            _column--;
                            UpdateCurrentChar();
                            return HandleText();
                        }
                }

                // 13. 如果在集合内且不在命令模式，当做文本处理
                if (_isInNodeContent && !_isInCommandMode)
                {
                    return HandleText();
                }

                // 14. 其他字符作为文本处理
                return HandleText();
            }

            // 文件结束
            return new Token(TokenType.EOF, string.Empty, _line, _column);
        }
    }
}
