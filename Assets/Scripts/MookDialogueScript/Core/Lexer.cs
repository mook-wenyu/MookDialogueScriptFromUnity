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
        private readonly List<int> _indentStack;
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

        public Lexer(string source)
        {
            _source = source;
            _position = 0;
            _line = 1;
            _column = 1;
            _currentChar = _position < _source.Length ? _source[_position] : '\0';
            _indentStack = new List<int> { 0 };
            _currentIndent = 0;
        }

        /// <summary>
        /// 前进一个字符
        /// </summary>
        private void Advance()
        {
            _position++;
            _column++;
            _currentChar = _position < _source.Length ? _source[_position] : '\0';
        }

        /// <summary>
        /// 查看下一个字符
        /// </summary>
        private char Peek()
        {
            int peekPos = _position + 1;
            return peekPos < _source.Length ? _source[peekPos] : '\0';
        }

        /// <summary>
        /// 查看前方n个字符
        /// </summary>
        private char LookAhead(int n)
        {
            int peekPos = _position + n;
            return peekPos < _source.Length ? _source[peekPos] : '\0';
        }

        /// <summary>
        /// 判断字符是否为中文字符
        /// </summary>
        private bool IsChineseChar(char c)
        {
            return c >= '\u4e00' && c <= '\u9fa5';
        }

        /// <summary>
        /// 判断字符是否为标识符起始字符（字母、下划线或中文）
        /// </summary>
        private bool IsIdentifierStart(char c)
        {
            return char.IsLetter(c) || c == '_' || IsChineseChar(c);
        }

        /// <summary>
        /// 判断字符是否为标识符组成部分（字母、数字、下划线或中文）
        /// </summary>
        private bool IsIdentifierPart(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_' || IsChineseChar(c);
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
            _indentStack.Add(indent);
            _currentIndent = indent;
            return new Token(TokenType.INDENT, "", _line, _column);
        }

        /// <summary>
        /// 处理缩进减少的情况
        /// </summary>
        private Token HandleIndentDecrease(int indent)
        {
            // 找到正确的缩进级别
            while (_indentStack.Count > 0 && _indentStack[_indentStack.Count - 1] > indent)
            {
                _indentStack.RemoveAt(_indentStack.Count - 1);
            }

            if (_indentStack.Count == 0 || _indentStack[_indentStack.Count - 1] != indent)
            {
                Debug.LogError($"词法错误: 第{_line}行，第{_column}列，无效的缩进");
                // 尝试恢复 - 添加当前缩进到栈中
                if (_indentStack.Count == 0)
                {
                    _indentStack.Add(indent);
                }
            }

            _currentIndent = indent;
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
        /// 跳过注释
        /// </summary>
        private void SkipComment()
        {
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
            int indent = CountIndentation();

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
            StringBuilder result = new StringBuilder(16); // 预分配合理容量
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
                result.Append(_currentChar);
                Advance();
            }

            return new Token(TokenType.NUMBER, result.ToString(), _line, _column);
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

            Advance(); // 跳过开引号
            StringBuilder result = new StringBuilder(32); // 预分配合理容量

            while (_currentChar != '\0' && _currentChar != closingQuote)
            {
                if (_currentChar == '\\' && Peek() == closingQuote)
                {
                    Advance(); // 跳过反斜杠
                    result.Append(_currentChar);
                }
                else
                {
                    result.Append(_currentChar);
                }
                Advance();
            }

            if (_currentChar == closingQuote)
            {
                Advance(); // 跳过闭引号
                return new Token(TokenType.STRING, result.ToString(), _line, _column);
            }
            else
            {
                Debug.LogError($"词法错误: 第{_line}行，第{_column}列，未闭合的字符串，期望 {closingQuote}");
                // 尝试恢复 - 将收集到的字符串内容作为Token返回
                return new Token(TokenType.STRING, result.ToString(), _line, _column);
            }
        }

        /// <summary>
        /// 处理转义字符
        /// </summary>
        private bool HandleEscapeChar(StringBuilder result)
        {
            char nextChar = Peek();
            if (nextChar == '#' || nextChar == ':' || nextChar == '：' ||
                nextChar == '[' || nextChar == ']' || nextChar == '{' ||
                nextChar == '}' || nextChar == '\\')
            {
                // 跳过反斜杠
                Advance();
                // 直接添加被转义的字符
                result.Append(_currentChar);
                Advance();
                return true;
            }
            return false;
        }

        /// <summary>
        /// 处理文本
        /// </summary>
        private Token HandleText()
        {
            StringBuilder result = new StringBuilder(64); // 预分配合理容量

            while (_currentChar != '\0' && _currentChar != '\n' && _currentChar != '\r')
            {
                // 处理转义字符
                if (_currentChar == '\\')
                {
                    if (HandleEscapeChar(result))
                        continue;

                    // 如果不是特殊字符，保留反斜杠
                    result.Append(_currentChar);
                    Advance();
                    continue;
                }

                // 遇到非转义的特殊字符时截断
                if (_currentChar == '#' || _currentChar == '{' || _currentChar == '[')
                {
                    break;
                }

                result.Append(_currentChar);
                Advance();
            }

            return new Token(TokenType.TEXT, result.ToString().Trim(), _line, _column);
        }

        /// <summary>
        /// 处理变量
        /// </summary>
        private Token HandleVariable()
        {
            Advance(); // 跳过$符号
            StringBuilder result = new StringBuilder(32); // 预分配合理容量

            if (_currentChar != '\0' && IsIdentifierStart(_currentChar))
            {
                result.Append(_currentChar);
                Advance();

                while (_currentChar != '\0' && IsIdentifierPart(_currentChar))
                {
                    result.Append(_currentChar);
                    Advance();
                }
            }

            return new Token(TokenType.VARIABLE, result.ToString(), _line, _column - 1);
        }

        /// <summary>
        /// 处理标识符或关键字
        /// </summary>
        private Token HandleIdentifierOrKeyword()
        {
            StringBuilder result = new StringBuilder(32); // 预分配合理容量

            // 确保第一个字符是有效的标识符起始字符
            if (_currentChar != '\0' && IsIdentifierStart(_currentChar))
            {
                result.Append(_currentChar);
                Advance();

                // 收集剩余的标识符字符
                while (_currentChar != '\0' && IsIdentifierPart(_currentChar))
                {
                    result.Append(_currentChar);
                    Advance();
                }
            }

            string text = result.ToString();

            // 检查是否是关键字（忽略大小写）
            if (Keywords.TryGetValue(text, out TokenType type))
            {
                return new Token(type, text, _line, _column);
            }

            // 所有非关键字的标识符都作为IDENTIFIER处理
            return new Token(TokenType.IDENTIFIER, text, _line, _column);
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
                    Token indentToken = HandleIndentation();
                    if (indentToken != null)
                        return indentToken;
                }

                // 跳过空白字符
                if (char.IsWhiteSpace(_currentChar) && _currentChar != '\n' && _currentChar != '\r')
                {
                    SkipWhitespace();
                    continue;
                }

                // 处理注释
                if (_currentChar == '/' && Peek() == '/')
                {
                    SkipComment();
                    continue;
                }

                // 处理换行
                if (_currentChar == '\n' || _currentChar == '\r')
                {
                    if (_currentChar == '\r' && Peek() == '\n')
                    {
                        Advance();
                    }
                    Advance();
                    _line++;
                    _column = 1;
                    return new Token(TokenType.NEWLINE, "\\n", _line - 1, _column);
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