using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

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

        private static readonly Dictionary<string, TokenType> Keywords = new Dictionary<string, TokenType>(StringComparer.OrdinalIgnoreCase)
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
            {"=>", TokenType.JUMP},
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

        /// <summary>
        /// 获取所有Token
        /// </summary>
        /// <returns>所有Token</returns>
        public List<Token> Tokenize()
        {
            List<Token> tokens = new List<Token>();
            Token token;
            do
            {
                token = GetNextToken();
                tokens.Add(token);
#if DEBUG
                Debug.Log($@"{token}");
#endif
            } while (token.Type != TokenType.EOF);

            return tokens;
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
        /// 更新当前字符
        /// </summary>
        private void UpdateCurrentChar()
        {
            _currentChar = GetCharAt(_position);
        }

        public Lexer(string source)
        {
            _source = source;
            _position = 0;
            _line = 1;
            _column = 1;
            _currentChar = GetCharAt(_position);
            _indentStack = new Stack<int>();
            _indentStack.Push(0);
            _currentIndent = 0;
        }

        /// <summary>
        /// 前进一个字符
        /// </summary>
        private void Advance()
        {
            _position++;
            _column++;
            _currentChar = GetCharAt(_position);
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
            return _currentChar == '\0' || _currentChar == '\n' || _currentChar == '\r' || _currentChar == '/';
        }

        /// <summary>
        /// 统计当前行的缩进数量
        /// </summary>
        private int CountIndentation()
        {
            int indent = 0;
            while (_currentChar == ' ' || _currentChar == '\t')
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
            return new Token(TokenType.INDENT, "", _line, _column);
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

                return new Token(TokenType.DEDENT, "", _line, _column);
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

            return new Token(TokenType.DEDENT, "", _line, _column);
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
                return new Token(TokenType.EOF, "", _line, _column);
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
            char savedCurrentChar = _currentChar;

            int indent = CountIndentation();

            // 恢复位置
            _position = savedPosition;
            _column = savedColumn;
            _currentChar = savedCurrentChar;

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
            int numberLength = _position - startPosition;
            string result = numberLength > 0 ? _source.Substring(startPosition, numberLength) : string.Empty;
            return new Token(TokenType.NUMBER, result, startLine, startColumn);
        }

        /// <summary>
        /// 处理字符串
        /// </summary>
        private Token HandleString()
        {
            char quoteType = _currentChar;
            char closingQuote = quoteType;

            // 确定对应的闭合引号
            switch (quoteType)
            {
                case '\u2018': closingQuote = '\u2019'; break; // 中文单引号
                case '\u201C': closingQuote = '\u201D'; break; // 中文双引号
                case '\'': closingQuote = '\''; break;         // 英文单引号
                case '"': closingQuote = '"'; break;           // 英文双引号
            }

            int startLine = _line;
            int startColumn = _column;

            Advance(); // 跳过开引号
            int contentStart = _position; // 记录内容开始位置

            // 快速扫描查找闭合引号
            bool hasEscapedQuote = false;
            while (_currentChar != '\0' && _currentChar != closingQuote)
            {
                if (_currentChar == '\\' && Peek() == closingQuote)
                {
                    hasEscapedQuote = true;
                    Advance(); // 跳过反斜杠
                }
                Advance();
            }

            string result;
            if (!hasEscapedQuote) // 如果没有转义字符，可以直接使用Substring
            {
                int contentLength = _position - contentStart;
                result = contentLength > 0 ? _source.Substring(contentStart, contentLength) : string.Empty;
            }
            else // 有转义字符，需要处理
            {
                // 保存当前位置，回到内容开始位置，重新处理
                int savedPosition = _position;
                int savedLine = _line;
                int savedColumn = _column;
                char savedCurrentChar = _currentChar;

                // 回到内容开始位置
                _position = contentStart;
                _currentChar = GetCharAt(_position);

                StringBuilder sb = new StringBuilder(32);
                while (_position < savedPosition && _currentChar != closingQuote)
                {
                    if (_currentChar == '\\' && Peek() == closingQuote)
                    {
                        Advance(); // 跳过反斜杠
                    }
                    sb.Append(_currentChar);
                    Advance();
                }

                result = sb.ToString();

                // 恢复位置
                _position = savedPosition;
                _line = savedLine;
                _column = savedColumn;
                _currentChar = savedCurrentChar;
            }

            if (_currentChar == closingQuote)
            {
                Advance(); // 跳过闭引号
                return new Token(TokenType.STRING, result, startLine, startColumn);
            }
            else
            {
                Debug.LogError($"词法错误: 第{startLine}行，第{startColumn}列，未闭合的字符串，期望 {closingQuote}");
                // 尝试恢复 - 将收集到的字符串内容作为Token返回
                return new Token(TokenType.STRING, result, startLine, startColumn);
            }
        }



        /// <summary>
        /// 处理文本
        /// </summary>
        private Token HandleText()
        {
            int startPosition = _position;
            int startLine = _line;
            int startColumn = _column;
            bool hasEscapeChars = false;

            while (_currentChar != '\0' && _currentChar != '\n' && _currentChar != '\r')
            {
                // 处理转义字符
                if (_currentChar == '\\')
                {
                    char nextChar = Peek();
                    if (nextChar == '#' || nextChar == ':' || nextChar == '：' ||
                        nextChar == '[' || nextChar == ']' || nextChar == '{' ||
                        nextChar == '}' || nextChar == '\\')
                    {
                        hasEscapeChars = true;
                        Advance(); // 跳过反斜杠
                        Advance(); // 跳过被转义的字符
                        continue;
                    }

                    // 如果不是特殊字符，保留反斜杠
                    Advance();
                    continue;
                }

                // 遇到非转义的特殊字符时截断
                if (_currentChar == '#' || _currentChar == ':' || _currentChar == '：' ||
                    _currentChar == '{' || _currentChar == '[')
                {
                    break;
                }

                Advance();
            }

            string result;
            if (!hasEscapeChars) // 如果没有转义字符，可以直接使用Substring
            {
                int textLength = _position - startPosition;
                if (textLength > 0)
                {
                    result = _source.Substring(startPosition, textLength).Trim();
                }
                else
                {
                    result = string.Empty;
                }
            }
            else // 有转义字符，需要重新处理
            {
                // 保存当前位置
                int savedPosition = _position;
                int savedLine = _line;
                int savedColumn = _column;
                char savedCurrentChar = _currentChar;

                // 回到文本开始位置
                _position = startPosition;
                _currentChar = GetCharAt(_position);

                StringBuilder sb = new StringBuilder(64);
                while (_position < savedPosition)
                {
                    // 处理转义字符
                    if (_currentChar == '\\')
                    {
                        char nextChar = Peek();
                        if (nextChar == '#' || nextChar == ':' || nextChar == '：' ||
                            nextChar == '[' || nextChar == ']' || nextChar == '{' ||
                            nextChar == '}' || nextChar == '\\')
                        {
                            Advance(); // 跳过反斜杠
                            sb.Append(_currentChar);
                            Advance();
                            continue;
                        }

                        // 如果不是特殊字符，保留反斜杠
                        sb.Append(_currentChar);
                        Advance();
                        continue;
                    }

                    sb.Append(_currentChar);
                    Advance();
                }

                result = sb.ToString().Trim();

                // 恢复位置
                _position = savedPosition;
                _line = savedLine;
                _column = savedColumn;
                _currentChar = savedCurrentChar;
            }

            return new Token(TokenType.TEXT, result, startLine, startColumn);
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

            int varLength = _position - startPosition;
            string result = varLength > 0 ? _source.Substring(startPosition, varLength) : string.Empty;
            return new Token(TokenType.VARIABLE, result, startLine, startColumn);
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

            int identLength = _position - startPosition;
            if (identLength == 0)
            {
                return new Token(TokenType.IDENTIFIER, string.Empty, startLine, startColumn);
            }

            string text = _source.Substring(startPosition, identLength);

            // 检查是否是关键字（忽略大小写）
            if (Keywords.TryGetValue(text, out TokenType type))
            {
                return new Token(type, text, startLine, startColumn);
            }

            // 所有非关键字的标识符都作为IDENTIFIER处理
            return new Token(TokenType.IDENTIFIER, text, startLine, startColumn);
        }

        /// <summary>
        /// 预览下一个Token而不消耗它
        /// </summary>
        /// <returns>下一个Token</returns>
        public Token PeekNextToken()
        {
            // 保存当前状态
            int savedPosition = _position;
            int savedLine = _line;
            int savedColumn = _column;
            char savedCurrentChar = _currentChar;
            int savedCurrentIndent = _currentIndent;

            // 获取下一个Token
            var nextToken = GetNextToken();

            // 恢复状态
            _position = savedPosition;
            _line = savedLine;
            _column = savedColumn;
            _currentChar = savedCurrentChar;
            _currentIndent = savedCurrentIndent;

            return nextToken;
        }

        /// <summary>
        /// 获取下一个Token并消耗它
        /// </summary>
        /// <returns>下一个Token</returns>
        public Token GetNextToken()
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
                                return new Token(TokenType.EOF, "", _line, _column);
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

                // 处理字符串
                if (_currentChar == '\'' || _currentChar == '"' ||
                    _currentChar == '\u2018' || _currentChar == '\u201C')
                {
                    return HandleString();
                }

                // 处理操作符和标点符号
                switch (_currentChar)
                {
                    case ':':
                    case '：':
                        Advance();
                        if (_currentChar == ':' || _currentChar == '：')
                        {
                            Advance();
                            return new Token(TokenType.DOUBLE_COLON, "::", _line, _column - 2);
                        }
                        // 不直接处理后面的文本，只返回冒号标记
                        return new Token(TokenType.COLON, ":", _line, _column - 1);

                    case '-':
                        Advance();
                        if (_currentChar == '>' || _currentChar == '》')
                        {
                            Advance();
                            // 不直接处理后面的文本，只返回箭头标记
                            return new Token(TokenType.ARROW, "->", _line, _column - 2);
                        }
                        if (_currentChar == '-')
                        {
                            Advance();
                            if (_currentChar == '-')
                            {
                                Advance();
                                return new Token(TokenType.NODE_START, "---", _line, _column - 3);
                            }
                            // 双减号作为文本处理
                            _position -= 2; // 回退两个字符，回到第一个减号位置
                            _column -= 2;
                            _currentChar = '-';
                            return HandleText();
                        }
                        return new Token(TokenType.MINUS, "-", _line, _column - 1);

                    case '=':
                        Advance();
                        if (_currentChar == '>' || _currentChar == '》')
                        {
                            Advance();
                            return new Token(TokenType.JUMP, "=>", _line, _column - 2);
                        }
                        if (_currentChar == '=')
                        {
                            Advance();
                            if (_currentChar == '=')
                            {
                                Advance();
                                return new Token(TokenType.NODE_END, "===", _line, _column - 3);
                            }
                            return new Token(TokenType.EQUALS, "==", _line, _column - 2);
                        }
                        return new Token(TokenType.ASSIGN, "=", _line, _column - 1);

                    case '+': Advance(); return new Token(TokenType.PLUS, "+", _line, _column - 1);
                    case '*': Advance(); return new Token(TokenType.MULTIPLY, "*", _line, _column - 1);
                    case '/': Advance(); return new Token(TokenType.DIVIDE, "/", _line, _column - 1);
                    case '%': Advance(); return new Token(TokenType.MODULO, "%", _line, _column - 1);

                    case '(':
                    case '（':
                        Advance();
                        return new Token(TokenType.LEFT_PAREN, "(", _line, _column - 1);

                    case ')':
                    case '）':
                        Advance();
                        return new Token(TokenType.RIGHT_PAREN, ")", _line, _column - 1);

                    case '[':
                    case '【':
                        Advance();
                        return new Token(TokenType.LEFT_BRACKET, "[", _line, _column - 1);

                    case ']':
                    case '】':
                        Advance();
                        return new Token(TokenType.RIGHT_BRACKET, "]", _line, _column - 1);

                    case '{':
                        Advance();
                        return new Token(TokenType.LEFT_BRACE, "{", _line, _column - 1);

                    case '}':
                        Advance();
                        return new Token(TokenType.RIGHT_BRACE, "}", _line, _column - 1);

                    case ',':
                    case '，':
                        Advance();
                        return new Token(TokenType.COMMA, ",", _line, _column - 1);
                    case '#': Advance(); return new Token(TokenType.HASH, "#", _line, _column - 1);

                    case '!':
                    case '！':
                        Advance();
                        if (_currentChar == '=')
                        {
                            Advance();
                            return new Token(TokenType.NOT_EQUALS, "!=", _line, _column - 2);
                        }
                        return new Token(TokenType.NOT, "!", _line, _column - 1);

                    case '>':
                    case '》':
                        Advance();
                        if (_currentChar == '=')
                        {
                            Advance();
                            return new Token(TokenType.GREATER_EQUALS, ">=", _line, _column - 2);
                        }
                        return new Token(TokenType.GREATER, ">", _line, _column - 1);

                    case '<':
                    case '《':
                        Advance();
                        if (_currentChar == '=')
                        {
                            Advance();
                            return new Token(TokenType.LESS_EQUALS, "<=", _line, _column - 2);
                        }
                        return new Token(TokenType.LESS, "<", _line, _column - 1);

                    case '&':
                        Advance();
                        if (_currentChar == '&')
                        {
                            Advance();
                            return new Token(TokenType.AND, "&&", _line, _column - 2);
                        }
                        Debug.LogError($"词法错误: 第{_line}行，第{_column - 1}列，无效的字符序列 '&'，期望 '&&'");
                        // 尝试恢复 - 返回单个&的Token
                        return new Token(TokenType.TEXT, "&", _line, _column - 1);

                    case '|':
                        Advance();
                        if (_currentChar == '|')
                        {
                            Advance();
                            return new Token(TokenType.OR, "||", _line, _column - 2);
                        }
                        Debug.LogError($"词法错误: 第{_line}行，第{_column - 1}列，无效的字符序列 '|'，期望 '||'");
                        // 尝试恢复 - 返回单个|的Token
                        return new Token(TokenType.TEXT, "|", _line, _column - 1);

                    case '^':
                        Advance();
                        return new Token(TokenType.XOR, "^", _line, _column - 1);

                    default:
                        // 如果是其他字符，作为Text处理
                        return HandleText();
                }
            }

            // 处理文件结束
            return new Token(TokenType.EOF, "", _line, _column);
        }
    }
}
