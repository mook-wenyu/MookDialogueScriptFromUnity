using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

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
        private bool _isInStringMode;   // 是否在字符串模式中
        private char _stringQuoteType;  // 当前字符串模式的引号类型
        private bool _isInOptionTextMode; // 是否在选项文本模式中（->后）


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
            _isInStringMode = false;
            _stringQuoteType = '\0';
            _isInOptionTextMode = false;
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
#if DEBUG
                Debug.Log($"{token}");
#endif
            } while (token.Type != TokenType.EOF);

            return _tokens;
        }

        /// <summary>
        /// 获取最后一个生成的Token
        /// </summary>
        /// <returns>最后一个Token，如果没有则返回null</returns>
        private Token GetLastToken()
        {
            return _tokens.Count > 0 ? _tokens[_tokens.Count - 1] : null;
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
                Debug.LogError($"词法错误: 第{_line}行，第{_column}列，无效的缩进");
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

            // 如果遇到换行，退出选项文本模式
            _isInOptionTextMode = false;
            // 如果在字符串模式下遇到换行，这通常是一个错误，但我们仍然退出该模式
            if (_isInStringMode)
            {
                Debug.LogWarning($"词法警告: 第{_line}行，第{_column}列，字符串未闭合就遇到了换行");
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
                    while (_currentChar == ' ' || _currentChar == '\t')
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
                else if (_currentChar == '\n' || _currentChar == '\r')
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
                if (_currentChar == '\n' || _currentChar == '\r')
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
                Debug.LogError($"词法错误: 第{_line}行，第{_column}列，不一致的缩进");
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
        /// 处理文本
        /// </summary>
        private Token HandleText()
        {
            int startLine = _line;
            int startColumn = _column;

            // 根据当前模式确定处理规则
            TextProcessingRules rules = GetTextProcessingRules();

            // 使用StringBuilder直接构建最终结果，避免二次处理
            StringBuilder resultBuilder = new StringBuilder(64);
            bool hasContent = false;

            // 文本处理主循环
            while (_currentChar != '\0' && _currentChar != '\n' && _currentChar != '\r')
            {
                // 1. 处理转义字符
                if (_currentChar == '\\')
                {
                    ProcessEscapeSequence(resultBuilder, rules);
                    hasContent = true;
                    continue;
                }

                // 2. 检查是否遇到截断字符
                if (Array.IndexOf(rules.TruncateChars, _currentChar) >= 0)
                {
                    // 特殊处理字符串模式下的结束引号
                    if (_isInStringMode && IsClosingQuote(_currentChar))
                    {
                        return FinalizeTextOrQuoteToken(resultBuilder, hasContent, startLine, startColumn);
                    }

                    // 处理选项文本模式下遇到左括号需要退出
                    if (_isInOptionTextMode && (_currentChar == '[' || _currentChar == '【'))
                    {
                        _isInOptionTextMode = false;
                        break;
                    }

                    break;
                }

                // 3. 正常字符处理
                resultBuilder.Append(_currentChar);
                Advance();
                hasContent = true;
            }

            // 如果遇到换行，检查是否需要退出选项文本模式
            if (_currentChar is '\n' or '\r')
            {
                if (_isInOptionTextMode)
                {
                    _isInOptionTextMode = false;
                }
            }

            // 4. 生成最终文本Token
            string result = resultBuilder.ToString();

            return new Token(TokenType.TEXT, result, startLine, startColumn);
        }

        /// <summary>
        /// 处理转义序列
        /// </summary>
        private void ProcessEscapeSequence(StringBuilder builder, TextProcessingRules rules)
        {
            char nextChar = Peek();
            if (Array.IndexOf(rules.EscapeChars, nextChar) >= 0)
            {
                Advance(); // 跳过反斜杠
                builder.Append(nextChar);
                Advance();
            }
            else
            {
                // 非特殊转义字符，保留反斜杠
                builder.Append(_currentChar);
                Advance();
            }
        }

        /// <summary>
        /// 根据内容返回文本Token或引号Token
        /// </summary>
        private Token FinalizeTextOrQuoteToken(StringBuilder builder, bool hasContent, int startLine, int startColumn)
        {
            if (hasContent)
            {
                // 如果已经有内容，先返回文本
                return new Token(TokenType.TEXT, builder.ToString(), startLine, startColumn);
            }
            else
            {
                // 没有内容，直接返回引号Token
                char quoteChar = _currentChar;
                Advance();
                _isInStringMode = false;
                _stringQuoteType = '\0';
                return new Token(TokenType.QUOTE, quoteChar.ToString(), startLine, startColumn);
            }
        }

        /// <summary>
        /// 根据当前模式获取处理规则
        /// </summary>
        private TextProcessingRules GetTextProcessingRules()
        {
            if (_isInStringMode)
            {
                char closingQuote = _stringQuoteType;
                switch (_stringQuoteType)
                {
                    case '\u2018': closingQuote = '\u2019'; break; // 中文单引号
                    case '\u201C': closingQuote = '\u201D'; break; // 中文双引号
                }
                return new TextProcessingRules(
                    truncateChars: new[] { '{', closingQuote },
                    escapeChars: new[] { '{', '}', '\\', closingQuote }
                );
            }

            if (_isInOptionTextMode)
            {
                // 选项文本模式下的截断字符包括左方括号，转义字符不变
                return new TextProcessingRules(
                    truncateChars: new[] { '{', '[', '【' },
                    escapeChars: new[] { '[', ']', '{', '}', '\\', '【', '】' }
                );
            }

            // 默认模式
            return new TextProcessingRules(
                truncateChars: new[] { '#', ':', '：', '{' },
                escapeChars: new[] { '#', ':', '：', '{', '}', '\\' }
            );
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
        /// 获取下一个Token并消耗它
        /// </summary>
        /// <returns>下一个Token</returns>
        private Token GetNextToken()
        {
            while (_currentChar != '\0')
            {

                // 处理行首缩进
                if (_column == 1)
                {
                    // 跳过注释行和空行前的缩进处理
                    if (_currentChar == '/' && Peek() == '/' ||
                        _currentChar == '\n' || _currentChar == '\r')
                    {
                        // 对于注释行和空行，不处理缩进，直接进入注释/换行处理逻辑
                    }
                    else
                    {
                        Token indentToken = HandleIndentation();
                        if (indentToken != null)
                        {
                            return indentToken;
                        }
                    }
                }

                // 检查是否应该基于当前模式和上一个Token处理文本
                if (ShouldProcessText())
                {
                    return HandleText();
                }

                // 跳过空白字符
                if (char.IsWhiteSpace(_currentChar) && _currentChar != '\n' && _currentChar != '\r')
                {
                    SkipWhitespace();
                    continue;
                }

                // 处理注释和换行的组合
                if (_currentChar == '/' && Peek() == '/' || _currentChar == '\n' || _currentChar == '\r')
                {
                    // 如果是注释，先跳过当前行的注释内容
                    if (_currentChar == '/' && Peek() == '/')
                    {
                        SkipComment();

                        // 如果注释后没有遇到换行，继续下一个循环
                        if (_currentChar != '\n' && _currentChar != '\r')
                        {
                            if (_currentChar == '\0')
                            {
                                return new Token(TokenType.EOF, string.Empty, _line, _column);
                            }
                            continue;
                        }
                    }

                    // 处理换行和后续可能的连续空行和注释行
                    return HandleNewlineAndComments();
                }

                // 处理转义字符
                if (_currentChar == '\\')
                {
                    return HandleText();
                }

                // 处理变量
                if (_currentChar == '$' || _currentChar == '￥')
                {
                    return HandleVariable();
                }

                // 处理标识符、关键字和命令
                if (IsIdentifierStart(_currentChar))
                {
                    return HandleIdentifierOrKeyword();
                }

                // 处理数字
                if (char.IsDigit(_currentChar))
                {
                    return HandleNumber();
                }

                // 处理字符串（只有在非字符串状态下才处理开始引号）
                if (!_isInStringMode && (_currentChar == '\'' || _currentChar == '"' ||
                    _currentChar == '\u2018' || _currentChar == '\u201C'))
                {
                    // 创建一个QUOTE类型的Token
                    char quoteChar = _currentChar;
                    int startLine = _line;
                    int startColumn = _column;

                    // 进入字符串模式
                    _isInStringMode = true;
                    _stringQuoteType = quoteChar;

                    // 消耗掉引号
                    Advance();

                    return new Token(TokenType.QUOTE, quoteChar.ToString(), startLine, startColumn);
                }

                // 先检查是否是字符串模式下的结束引号
                if (_isInStringMode && IsClosingQuote(_currentChar))
                {
                    // 创建一个QUOTE类型的Token
                    char quoteChar = _currentChar;
                    int startLine = _line;
                    int startColumn = _column;

                    // 消耗掉引号
                    Advance();

                    // 退出字符串模式
                    _isInStringMode = false;
                    _stringQuoteType = '\0';

                    return new Token(TokenType.QUOTE, quoteChar.ToString(), startLine, startColumn);
                }

                int startPosition = _position;
                // 处理操作符和标点符号
                switch (_currentChar)
                {
                    case ':':
                    case '：':
                        Advance();
                        if (_currentChar is ':' or '：')
                        {
                            Advance();
                            return new Token(TokenType.DOUBLE_COLON, GetRange(startPosition, _position), _line, _column - 2);
                        }
                        // 不直接处理后面的文本，只返回冒号标记
                        return new Token(TokenType.COLON, GetRange(startPosition, _position), _line, _column - 1);

                    case '-':
                        Advance();
                        if (_currentChar is '>' or '》')
                        {
                            Advance();
                            // 遇到箭头，进入选项文本模式
                            _isInOptionTextMode = true;
                            // 不直接处理后面的文本，只返回箭头标记
                            return new Token(TokenType.ARROW, GetRange(startPosition, _position), _line, _column - 2);
                        }
                        if (_currentChar == '-')
                        {
                            Advance();
                            if (_currentChar == '-')
                            {
                                Advance();
                                return new Token(TokenType.NODE_START, GetRange(startPosition, _position), _line, _column - 3);
                            }
                            // 双减号作为文本处理
                            _position -= 2; // 回退两个字符，回到第一个减号位置
                            _column -= 2;
                            UpdateCurrentChar();
                            return HandleText();
                        }
                        return new Token(TokenType.MINUS, GetRange(startPosition, _position), _line, _column - 1);

                    case '=':
                        Advance();
                        if (_currentChar is '>' or '》')
                        {
                            Advance();
                            return new Token(TokenType.JUMP, GetRange(startPosition, _position), _line, _column - 2);
                        }
                        if (_currentChar == '=')
                        {
                            Advance();
                            if (_currentChar == '=')
                            {
                                Advance();
                                return new Token(TokenType.NODE_END, GetRange(startPosition, _position), _line, _column - 3);
                            }
                            return new Token(TokenType.EQUALS, GetRange(startPosition, _position), _line, _column - 2);
                        }
                        return new Token(TokenType.ASSIGN, GetRange(startPosition, _position), _line, _column - 1);

                    case '+': Advance(); return new Token(TokenType.PLUS, "+", _line, _column - 1);
                    case '*': Advance(); return new Token(TokenType.MULTIPLY, "*", _line, _column - 1);
                    case '/': Advance(); return new Token(TokenType.DIVIDE, "/", _line, _column - 1);
                    case '%': Advance(); return new Token(TokenType.MODULO, "%", _line, _column - 1);

                    case '(':
                    case '（':
                        Advance();
                        return new Token(TokenType.LEFT_PAREN, GetRange(startPosition, _position), _line, _column - 1);

                    case ')':
                    case '）':
                        Advance();
                        return new Token(TokenType.RIGHT_PAREN, GetRange(startPosition, _position), _line, _column - 1);

                    case '[':
                    case '【':
                        // 如果在选项文本模式下，退出该模式
                        if (_isInOptionTextMode)
                        {
                            _isInOptionTextMode = false;
                        }
                        Advance();
                        return new Token(TokenType.LEFT_BRACKET, GetRange(startPosition, _position), _line, _column - 1);

                    case ']':
                    case '】':
                        Advance();
                        return new Token(TokenType.RIGHT_BRACKET, GetRange(startPosition, _position), _line, _column - 1);

                    case '{':
                        Advance();
                        return new Token(TokenType.LEFT_BRACE, "{", _line, _column - 1);

                    case '}':
                        Advance();
                        return new Token(TokenType.RIGHT_BRACE, "}", _line, _column - 1);

                    case ',':
                    case '，':
                        Advance();
                        return new Token(TokenType.COMMA, GetRange(startPosition, _position), _line, _column - 1);
                    case '#':
                        Advance();
                        return new Token(TokenType.HASH, "#", _line, _column - 1);

                    case '!':
                    case '！':
                        Advance();
                        if (_currentChar == '=')
                        {
                            Advance();
                            return new Token(TokenType.NOT_EQUALS, GetRange(startPosition, _position), _line, _column - 2);
                        }
                        return new Token(TokenType.NOT, GetRange(startPosition, _position), _line, _column - 1);

                    case '>':
                    case '》':
                        Advance();
                        if (_currentChar == '=')
                        {
                            Advance();
                            return new Token(TokenType.GREATER_EQUALS, GetRange(startPosition, _position), _line, _column - 2);
                        }
                        return new Token(TokenType.GREATER, GetRange(startPosition, _position), _line, _column - 1);

                    case '<':
                    case '《':
                        Advance();
                        if (_currentChar == '=')
                        {
                            Advance();
                            return new Token(TokenType.LESS_EQUALS, GetRange(startPosition, _position), _line, _column - 2);
                        }
                        return new Token(TokenType.LESS, GetRange(startPosition, _position), _line, _column - 1);

                    case '&':
                        Advance();
                        if (_currentChar == '&')
                        {
                            Advance();
                            return new Token(TokenType.AND, "&&", _line, _column - 2);
                        }
                        // 作为文本处理
                        _position -= 1; // 回退一个字符
                        _column -= 1;
                        UpdateCurrentChar();
                        return HandleText();

                    case '|':
                        Advance();
                        if (_currentChar == '|')
                        {
                            Advance();
                            return new Token(TokenType.OR, "||", _line, _column - 2);
                        }
                        // 作为文本处理
                        _position -= 1; // 回退一个字符
                        _column -= 1;
                        UpdateCurrentChar();
                        return HandleText();

                    case '^':
                        Advance();
                        return new Token(TokenType.XOR, "^", _line, _column - 1);

                    default:
                        // 如果是其他字符，作为Text处理
                        return HandleText();
                }
            }

            // 处理文件结束
            return new Token(TokenType.EOF, string.Empty, _line, _column);
        }

        /// <summary>
        /// 检查当前是否应该处理文本，根据不同的模式与上一个Token类型
        /// </summary>
        private bool ShouldProcessText()
        {
            // 获取上一个Token
            Token lastToken = GetLastToken();

            // 如果没有前一个Token，或者当前位置在行首，不处理文本
            if (lastToken == null || _column == 1)
                return false;

            // 处理特定的模式和Token组合
            // 1. 字符串模式: 上一个Token是引号或右大括号
            if (_isInStringMode)
            {
                return lastToken.Type == TokenType.QUOTE ||
                       lastToken.Type == TokenType.RIGHT_BRACE;
            }

            // 2. 选项文本模式: 上一个Token是箭头或右大括号
            if (_isInOptionTextMode)
            {
                return lastToken.Type == TokenType.ARROW ||
                       lastToken.Type == TokenType.RIGHT_BRACE;
            }

            return false;
        }
    }
}
