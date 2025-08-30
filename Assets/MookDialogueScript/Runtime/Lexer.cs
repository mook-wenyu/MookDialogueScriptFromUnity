using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace MookDialogueScript
{
    /// <summary>
    /// 词法分析器
    /// 标记为旧代码，不可编辑。
    /// </summary>
    public class Lexer
    {
        // 高性能数据结构：使用字符数组替代字符串
        private char[] _sourceChars;
        private int _sourceLength;

        // 三字符缓存：消除重复的数组访问
        private int _position;
        private char _currentChar;
        private char _nextChar;
        private char _prevChar;

        // 位置信息
        private int _line;
        private int _column;
        private readonly Stack<int> _indentStack;
        private int _currentIndent;
        private readonly List<Token> _tokens;
        // 待输出的连续DEDENT数量（用于多级缩进回退时逐个发出）
        private int _pendingDedent;


        // 同步锁：用于在重置与词法分析之间提供最小线程安全保障
        private readonly object _syncLock = new object();

        // 状态标志
        private bool _isInNodeContent;   // 是否在 --- 到 === 集合内
        private bool _isInCommandMode;   // 是否在 << >> 命令内
        private bool _isInStringMode;    // 是否在字符串模式中
        private bool _isInInterpolation; // 是否在 { } 插值表达式内
        private char _stringQuoteType;   // 当前字符串模式的引号类型

        // 预计算查找表：避免重复的字符分类计算
        private static readonly bool[] IsWhitespaceTable = new bool[128];
        private static readonly bool[] IsLetterTable = new bool[128];
        private static readonly bool[] IsDigitTable = new bool[128];
        private static readonly bool[] IsLetterOrDigitTable = new bool[128];

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

        static Lexer()
        {
            // 预计算 ASCII 字符的分类，避免运行时计算
            for (int i = 0; i < 128; i++)
            {
                char c = (char)i;
                IsWhitespaceTable[i] = char.IsWhiteSpace(c);
                IsLetterTable[i] = char.IsLetter(c);
                IsDigitTable[i] = char.IsDigit(c);
                IsLetterOrDigitTable[i] = char.IsLetterOrDigit(c);
            }
        }

        public Lexer()
        {
            // 初始化位置信息（源代码将通过 Parse 方法设置）
            _position = 0;
            _line = 1;
            _column = 1;

            // 初始化字符缓存为空状态
            _currentChar = '\0';
            _nextChar = '\0';
            _prevChar = '\0';

            // 初始化集合
            _indentStack = new Stack<int>();
            _indentStack.Push(0);
            _tokens = new List<Token>();
            _currentIndent = 0;
            _pendingDedent = 0;

            // 初始化状态标志
            _isInNodeContent = false;
            _isInCommandMode = false;
            _isInStringMode = false;
            _isInInterpolation = false;
            _stringQuoteType = '\0';
        }

        /// <summary>
        /// 重置当前 Lexer 实例以复用对象处理新的源代码字符串。
        /// - 复用内部集合（List/Stack），避免不必要的 GC 分配
        /// - 重置所有状态（位置、行列、缩进栈、模式标志、字符缓存等）
        /// - 线程安全：使用最小锁保护 Parse/Tokenize 互斥执行
        /// </summary>
        /// <param name="source">新的源代码字符串</param>
        public void Reset(string source)
        {
            lock (_syncLock)
            {
                // 防御式编程：允许传入 null
                source ??= string.Empty;

                // 1) 更新源字符缓冲（新建字符数组，但复用其他集合）
                _sourceChars = source.ToCharArray();
                _sourceLength = _sourceChars.Length;

                // 2) 重置位置与位置信息
                _position = 0;
                _line = 1;
                _column = 1;

                // 3) 重置字符缓存（三字符缓存）
                _currentChar = '\0';
                _nextChar = '\0';
                _prevChar = '\0';
                UpdateCharacterCache();

                // 4) 重置缩进状态（复用栈实例）
                _indentStack.Clear();
                _indentStack.Push(0);
                _currentIndent = 0;
                _pendingDedent = 0;

                // 5) 重置模式标志
                _isInNodeContent = false;
                _isInCommandMode = false;
                _isInStringMode = false;
                _isInInterpolation = false;
                _stringQuoteType = '\0';

                // 6) 清空已生成 Token（复用 List 实例）
                _tokens.Clear();
            }
        }


        /// <summary>
        /// 更新字符缓存：一次性更新三个字符，最大化缓存效率
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateCharacterCache()
        {
            // 批量边界检查，减少分支预测失败
            if (_position < _sourceLength)
            {
                _currentChar = _sourceChars[_position];
                _nextChar = _position + 1 < _sourceLength ? _sourceChars[_position + 1] : '\0';
            }
            else
            {
                _currentChar = '\0';
                _nextChar = '\0';
            }

            _prevChar = _position > 0 ? _sourceChars[_position - 1] : '\0';
        }

        /// <summary>
        /// 高性能字符访问：直接返回缓存的字符，零开销
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private char GetCharAt(int position)
        {
            // 优化常见情况：访问当前位置或相邻位置
            if (position == _position) return _currentChar;
            if (position == _position + 1) return _nextChar;
            if (position == _position - 1) return _prevChar;

            // 非缓存位置的快速访问
            return position < _sourceLength ? _sourceChars[position] : '\0';
        }

        /// <summary>
        /// 零开销的 Peek 操作：直接返回缓存的下一个字符
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private char Peek() => _nextChar;

        /// <summary>
        /// 零开销的 Previous 操作：直接返回缓存的前一个字符
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private char Previous() => _prevChar;

        /// <summary>
        /// 高性能前进操作：更新位置和缓存
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Advance()
        {
            _position++;
            _column++;

            // 优化的缓存更新：利用已有的 _nextChar
            _prevChar = _currentChar;
            _currentChar = _nextChar;
            _nextChar = _position + 1 < _sourceLength ? _sourceChars[_position + 1] : '\0';
        }

        /// <summary>
        /// 高性能空白字符检查：使用预计算查找表
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsWhitespaceFast(char c)
        {
            return c < 128 ? IsWhitespaceTable[c] : char.IsWhiteSpace(c);
        }

        /// <summary>
        /// 高性能字母检查：使用预计算查找表
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsLetterFast(char c)
        {
            return c < 128 ? IsLetterTable[c] : char.IsLetter(c);
        }

        /// <summary>
        /// 高性能数字检查：使用预计算查找表
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsDigitFast(char c)
        {
            return c < 128 ? IsDigitTable[c] : char.IsDigit(c);
        }

        /// <summary>
        /// 高性能字母或数字检查：使用预计算查找表
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsLetterOrDigitFast(char c)
        {
            return c < 128 ? IsLetterOrDigitTable[c] : char.IsLetterOrDigit(c);
        }

        /// <summary>
        /// 优化的标识符起始字符检查
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsIdentifierStart(char c)
        {
            return IsLetterFast(c) || c == '_';
        }

        /// <summary>
        /// 优化的标识符组成字符检查
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsIdentifierPart(char c)
        {
            return IsLetterOrDigitFast(c) || c == '_';
        }

        /// <summary>
        /// 高性能字符串范围获取：使用 Span 避免不必要的分配
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string GetRange(int start, int end)
        {
            int length = end - start;
            if (length <= 0) return string.Empty;

            // 边界检查
            if (start < 0 || end > _sourceLength || start > end)
            {
                throw new ArgumentOutOfRangeException($"Invalid range: start={start}, end={end}, sourceLength={_sourceLength}");
            }

            // 使用 Span 进行高效的字符串创建
            return new string(_sourceChars.AsSpan(start, length));
        }

        /// <summary>
        /// 优化的命令开始检查
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsCommandStart()
        {
            return _currentChar == '<' && _nextChar == '<';
        }

        /// <summary>
        /// 优化的命令结束检查
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsCommandEnd()
        {
            return _currentChar == '>' && _nextChar == '>';
        }

        /// <summary>
        /// 换行符
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsNewline(char c)
        {
            return c is '\n' or '\r';
        }

        /// <summary>
        /// 换行符或者文件末尾
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsNewlineOrEOF(char c)
        {
            return c is '\n' or '\r' or '\0';
        }

        /// <summary>
        /// 空格或缩进
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsSpaceOrIndent(char c)
        {
            return c is ' ' or '\t';
        }

        /// <summary>
        /// 批量字符序列检查：优化的节点标记检测
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsNodeStart()
        {
            return _currentChar == '-' && _nextChar == '-' &&
                   GetCharAt(_position + 2) == '-';
        }

        /// <summary>
        /// 批量字符序列检查：优化的节点结束检测
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsNodeEnd()
        {
            return _currentChar == '=' && _nextChar == '=' &&
                   GetCharAt(_position + 2) == '=';
        }

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
            if (IsNewlineOrEOF(c))
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
        /// <returns>Token列表的独立副本，避免外部引用受Reset影响</returns>
        public List<Token> Tokenize()
        {
            lock (_syncLock)
            {
                Token token;
                do
                {
                    token = GetNextToken();
                    _tokens.Add(token);
                } while (token.Type != TokenType.EOF);

                // 返回独立副本，防止外部代码持有内部_tokens引用
                return new List<Token>(_tokens);
            }
        }

        /// <summary>
        /// 优化的空白字符跳过：使用缓存字符和快速检查
        /// </summary>
        private void SkipWhitespace()
        {
            while (_currentChar != '\0' && IsWhitespaceFast(_currentChar) &&
                   _currentChar != '\n' && _currentChar != '\r')
            {
                Advance();
            }
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
        /// 判断是否需要跳过缩进处理
        /// </summary>
        private bool ShouldSkipIndentation()
        {
            // 在行首仅对 EOF/换行跳过缩进处理；注释判定由调用方负责
            return IsNewlineOrEOF(_currentChar);
        }

        /// <summary>
        /// 统计当前行的缩进数量
        /// </summary>
        private int CountIndentation()
        {
            var indent = 0;
            bool seenSpace = false;
            bool seenTab = false;
            while (IsSpaceOrIndent(_currentChar))
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
            return TokenFactory.IndentToken(_line, _column);
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
                return TokenFactory.DedentToken(_line, _column);
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

            // 保存完整状态用于缩进计算后的恢复
            int savedPosition = _position;
            int savedColumn = _column;
            int savedLine = _line;
            char savedCurrentChar = _currentChar;
            char savedNextChar = _nextChar;
            char savedPrevChar = _prevChar;

            int indent = CountIndentation();

            // 完整恢复状态
            _position = savedPosition;
            _column = savedColumn;
            _line = savedLine;
            _currentChar = savedCurrentChar;
            _nextChar = savedNextChar;
            _prevChar = savedPrevChar;

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
            if (IsNewline(_currentChar))
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
                while (c is ' ' or '\t')
                {
                    p++;
                    c = GetCharAt(p);
                }

                bool isCommentLine = c == '/' && GetCharAt(p + 1) == '/';
                bool isEmptyLine = IsNewlineOrEOF(c);

                if (!(isCommentLine || isEmptyLine))
                {
                    // 下一行是内容行：停止折叠，保持指针在该行列首（不吃空白）
                    break;
                }

                // 将“预读到的这一行”（注释行或空行）整体消耗掉到换行符末尾
                // 真正推进：跳过前导空白
                while (IsSpaceOrIndent(_currentChar))
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

                if (IsNewline(_currentChar))
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
                    return TokenFactory.DedentToken(_line, _column);
                }
                if (_indentStack.Count > 1)
                {
                    _indentStack.Pop();
                    _currentIndent = _indentStack.Peek();
                    return TokenFactory.DedentToken(_line, _column);
                }
                return TokenFactory.EOFToken(_line, _column);
            }

            // 5) 折叠结果统一返回一个 NEWLINE
            return TokenFactory.NewLineToken(currentLine, _column);
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
                    return TokenFactory.IdentifierToken(key, startLine, startColumn);
                }

                MLogger.Warning($"词法警告: 第{startLine}行，第{startColumn}列，元数据缺少键名");
                // 仍然返回Token，让上层处理
            }
            else if (_currentChar is '\r' or '\n')
            {
                if (!string.IsNullOrEmpty(key))
                {
                    // 键值
                    return TokenFactory.TextToken(key, startLine, startColumn);
                }

                MLogger.Warning($"词法警告: 第{startLine}行，第{startColumn}列，元数据缺少键值");
            }

            // 如果没有冒号，这不是有效的元数据格式
            MLogger.Warning($"词法警告: 第{startLine}行，第{startColumn}列，无效的元数据格式");
            return null;
        }

        /// <summary>
        /// 优化的数字处理：减少重复的字符分类检查
        /// </summary>
        private Token HandleNumber()
        {
            if (!IsDigitFast(_currentChar)) return null;

            int startPosition = _position;
            int startLine = _line;
            int startColumn = _column;
            bool hasDecimalPoint = false;

            // 使用缓存字符进行快速处理
            while (_currentChar != '\0' && (IsDigitFast(_currentChar) || _currentChar == '.'))
            {
                if (_currentChar == '.')
                {
                    if (hasDecimalPoint) break;
                    hasDecimalPoint = true;
                }
                Advance();
            }
            return TokenFactory.NumberToken(GetRange(startPosition, _position), startLine, startColumn);
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
                    if (_currentChar == ':' && pre != '\0' && !IsWhitespaceFast(pre))
                    {
                        break;
                    }
                    // 在集合内，文本会被 << >> # 等字符截断
                    if (IsCommandStart())
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
            return TokenFactory.TextToken(resultBuilder.ToString(), startLine, startColumn);
        }

        /// <summary>
        /// 判断当前字符是否是当前字符串模式的结束引号
        /// </summary>
        private bool IsClosingQuote(char c)
        {
            if (!_isInStringMode) return false;

            return _stringQuoteType == c;
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
            return TokenFactory.VariableToken(GetRange(startPosition, _position), startLine, startColumn);
        }

        /// <summary>
        /// 优化的标识符处理：使用快速字符分类
        /// </summary>
        private Token HandleIdentifierOrKeyword()
        {
            if (!IsIdentifierStart(_currentChar)) return null;

            int startPosition = _position;
            int startLine = _line;
            int startColumn = _column;

            // 快速处理标识符字符
            Advance(); // 跳过第一个字符（已验证）

            while (_currentChar != '\0' && IsIdentifierPart(_currentChar))
            {
                Advance();
            }

            string text = GetRange(startPosition, _position);

            // 检查是否是关键字
            if (_keywords.TryGetValue(text, out var type))
            {
                return TokenFactory.CreateToken(type, text, startLine, startColumn);
            }
            return TokenFactory.IdentifierToken(text, startLine, startColumn);
        }

        /// <summary>
        /// 处理命令内容（《《 >> 之间的表达式）
        /// </summary>
        private Token HandleCommand()
        {
            if (!_isInNodeContent) return null;

            if (IsCommandStart())
            {
                // 处理命令开始
                int startLine = _line;
                int startColumn = _column;
                Advance(); // 跳过第一个 <
                Advance(); // 跳过第二个 <
                _isInCommandMode = true;
                return TokenFactory.CreateToken(TokenType.COMMAND_START, "<<", startLine, startColumn);
            }
            if (IsCommandEnd())
            {
                // 处理命令结束
                int startLine = _line;
                int startColumn = _column;
                Advance(); // 跳过第一个 >
                Advance(); // 跳过第二个 >
                _isInCommandMode = false;
                return TokenFactory.CreateToken(TokenType.COMMAND_END, ">>", startLine, startColumn);
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
                return TokenFactory.DedentToken(_line, _column);
            }

            // 如果已到输入末尾，优先补齐所有未关闭的缩进
            if (_currentChar == '\0')
            {
                if (_indentStack.Count > 1)
                {
                    _indentStack.Pop();
                    _currentIndent = _indentStack.Peek();
                    return TokenFactory.DedentToken(_line, _column);
                }
                return TokenFactory.EOFToken(_line, _column);
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
                    if (IsWhitespaceFast(_currentChar) && _currentChar != '\n' && _currentChar != '\r')
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
                            return TokenFactory.EOFToken(_line, _column);
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
                    && IsNodeEnd())
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
                        return TokenFactory.CreateToken(TokenType.NODE_END, "===", startLine, startColumn);
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
                    && !IsCommandStart()
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
                        return TokenFactory.CreateToken(TokenType.METADATA_SEPARATOR, ":", startLine, startColumn);
                    }
                    // 在集合内，这是普通冒号
                    return TokenFactory.CreateToken(TokenType.COLON, ":", startLine, startColumn);
                }

                // 在集合外，处理节点标记 ---（且不在命令/插值/字符串模式）
                if (!_isInNodeContent && !_isInCommandMode && !_isInInterpolation && !_isInStringMode)
                {
                    // 检查是否是节点标记 ---
                    if (IsNodeStart())
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
                            return TokenFactory.CreateToken(TokenType.NODE_START, "---", startLine, startColumn);
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
                    return TokenFactory.CreateToken(TokenType.QUOTE, quoteChar.ToString(), startLine, startColumn);
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
                    return TokenFactory.CreateToken(TokenType.QUOTE, quoteChar.ToString(), startLine, startColumn);
                }

                // 12. 处理操作符和标点符号
                switch (_currentChar)
                {
                    case '(':
                    {
                        int startLine = _line;
                        int startColumn = _column;
                        Advance();
                        return TokenFactory.CreateToken(TokenType.LEFT_PAREN, "(", startLine, startColumn);
                    }
                    case ')':
                    {
                        int startLine = _line;
                        int startColumn = _column;
                        Advance();
                        return TokenFactory.CreateToken(TokenType.RIGHT_PAREN, ")", startLine, startColumn);
                    }
                    case '{':
                    {
                        int startLine = _line;
                        int startColumn = _column;
                        Advance();
                        _isInInterpolation = true;
                        return TokenFactory.CreateToken(TokenType.LEFT_BRACE, "{", startLine, startColumn);
                    }
                    case '}':
                    {
                        int startLine = _line;
                        int startColumn = _column;
                        Advance();
                        _isInInterpolation = false;
                        return TokenFactory.CreateToken(TokenType.RIGHT_BRACE, "}", startLine, startColumn);
                    }
                    case '[':
                    {
                        int startLine = _line;
                        int startColumn = _column;
                        Advance();
                        return TokenFactory.CreateToken(TokenType.LEFT_BRACKET, "[", startLine, startColumn);
                    }
                    case ']':
                    {
                        int startLine = _line;
                        int startColumn = _column;
                        Advance();
                        return TokenFactory.CreateToken(TokenType.RIGHT_BRACKET, "]", startLine, startColumn);
                    }
                    case '#':
                    {
                        int startLine = _line;
                        int startColumn = _column;
                        Advance();
                        return TokenFactory.CreateToken(TokenType.HASH, "#", startLine, startColumn);
                    }
                    case '.':
                    {
                        // 需要区分小数点和成员访问的点号
                        // 如果下一个字符是数字，且前面没有数字，则这可能是小数点
                        // 但这里我们在表达式/命令模式下，简单处理为DOT token
                        int startLine = _line;
                        int startColumn = _column;
                        Advance();
                        
                        return TokenFactory.CreateToken(TokenType.DOT, ".", startLine, startColumn);
                    }
                    case ',':
                    {
                        int startLine = _line;
                        int startColumn = _column;
                        Advance();
                        
                        return TokenFactory.CreateToken(TokenType.COMMA, ",", startLine, startColumn);
                    }
                    case '+':
                    {
                        int startLine = _line;
                        int startColumn = _column;
                        Advance();
                        return TokenFactory.CreateToken(TokenType.PLUS, "+", startLine, startColumn);
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
                            return TokenFactory.CreateToken(TokenType.ARROW, "->", startLine, startColumn);
                        }
                        return TokenFactory.CreateToken(TokenType.MINUS, "-", startLine, startColumn);
                    }
                    case '*':
                    {
                        int startLine = _line;
                        int startColumn = _column;
                        Advance();
                        return TokenFactory.CreateToken(TokenType.MULTIPLY, "*", startLine, startColumn);
                    }
                    case '/':
                    {
                        int startLine = _line;
                        int startColumn = _column;
                        Advance();
                        return TokenFactory.CreateToken(TokenType.DIVIDE, "/", startLine, startColumn);
                    }
                    case '%':
                    {
                        int startLine = _line;
                        int startColumn = _column;
                        Advance();
                        return TokenFactory.CreateToken(TokenType.MODULO, "%", startLine, startColumn);
                    }
                    case '=':
                    {
                        int startLine = _line;
                        int startColumn = _column;
                        Advance();
                        if (_currentChar == '=')
                        {
                            Advance();
                            return TokenFactory.CreateToken(TokenType.EQUALS, "==", startLine, startColumn);
                        }
                        return TokenFactory.CreateToken(TokenType.ASSIGN, "=", startLine, startColumn);
                    }
                    case '!':
                    {
                        int startLine = _line;
                        int startColumn = _column;
                        Advance();
                        if (_currentChar == '=')
                        {
                            Advance();
                            return TokenFactory.CreateToken(TokenType.NOT_EQUALS, "!=", startLine, startColumn);
                        }
                        return TokenFactory.CreateToken(TokenType.NOT, "!", startLine, startColumn);
                    }
                    case '>':
                    {
                        int startLine = _line;
                        int startColumn = _column;
                        Advance();
                        if (_currentChar == '=')
                        {
                            Advance();
                            return TokenFactory.CreateToken(TokenType.GREATER_EQUALS, ">=", startLine, startColumn);
                        }
                        return TokenFactory.CreateToken(TokenType.GREATER, ">", startLine, startColumn);
                    }
                    case '<':
                    {
                        int startLine = _line;
                        int startColumn = _column;
                        Advance();
                        if (_currentChar == '=')
                        {
                            Advance();
                            return TokenFactory.CreateToken(TokenType.LESS_EQUALS, "<=", startLine, startColumn);
                        }
                        return TokenFactory.CreateToken(TokenType.LESS, "<", startLine, startColumn);
                    }
                    case '&':
                    {
                        int startLine = _line;
                        int startColumn = _column;
                        Advance();
                        if (_currentChar == '&')
                        {
                            Advance();
                            return TokenFactory.CreateToken(TokenType.AND, "&&", startLine, startColumn);
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
                            return TokenFactory.CreateToken(TokenType.OR, "||", startLine, startColumn);
                        }
                        break;
                    }
                }

                // 其他情况
                return HandleText();
            }

            // 文件结束
            return TokenFactory.EOFToken(_line, _column);
        }
    }
}
