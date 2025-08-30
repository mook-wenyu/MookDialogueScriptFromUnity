using System;
using System.Runtime.CompilerServices;

namespace MookDialogueScript.Lexers
{
    /// <summary>
    /// 高性能字符流实现，保持三字符缓存优化和内联方法
    /// 设计原则：单一职责 - 专注于字符流管理和性能优化
    /// </summary>
    public class CharStream : IDisposable
    {
        // 高性能数据结构：使用字符数组替代字符串
        private char[] _sourceChars;
        private int _sourceLength;

        // 位置信息
        private int _position;
        private int _line;
        private int _column;

        // 三字符缓存：消除重复的数组访问，保持原有优化
        private char _currentChar;
        private char _nextChar;
        private char _prevChar;

        public char CurrentChar => _currentChar;
        public char NextChar => _nextChar;
        public char PreviousChar => _prevChar;
        public int Position => _position;
        public int Line => _line;
        public int Column => _column;
        public bool IsAtEnd => _currentChar == '\0';
        public int Length => _sourceLength;

        public CharStream()
        {
            Reset(string.Empty);
        }

        /// <summary>
        /// 重置字符流以处理新的源代码，复用内部数组避免GC压力
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset(string source)
        {
            // 防御式编程：允许传入null
            source ??= string.Empty;

            // 更新源字符缓冲
            _sourceChars = source.ToCharArray();
            _sourceLength = _sourceChars.Length;

            // 重置位置与位置信息
            _position = 0;
            _line = 1;
            _column = 1;

            // 重置字符缓存
            _currentChar = '\0';
            _nextChar = '\0';
            _prevChar = '\0';
            UpdateCharacterCache();
        }

        /// <summary>
        /// 恢复流到之前保存的位置
        /// </summary>
        /// <param name="location">要恢复的位置</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RestoreLocation(SourceLocation location)
        {
            _position = location.Position;
            _line = location.Line;
            _column = location.Column;
            UpdateCharacterCache();
        }

        /// <summary>
        /// 获取当前位置的SourceLocation
        /// </summary>
        /// <returns>当前位置的SourceLocation</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SourceLocation GetCurrentLocation()
        {
            return new SourceLocation(_position, _line, _column);
        }

        /// <summary>
        /// 更新字符缓存：一次性更新三个字符，最大化缓存效率
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateCharacterCache()
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
        /// 高性能前进操作：更新位置和缓存
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance()
        {
            _position++;
            _column++;

            // 优化的缓存更新：利用已有的_nextChar
            _prevChar = _currentChar;
            _currentChar = _nextChar;
            _nextChar = _position + 1 < _sourceLength ? _sourceChars[_position + 1] : '\0';

            // 处理换行符的行列更新
            if (_prevChar is '\n' or '\r')
            {
                _line++;
                _column = 1;
            }
        }

        /// <summary>
        /// 预览指定偏移位置的字符
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public char PeekAhead(int offset = 1)
        {
            // 优化常见情况：访问下一个字符
            if (offset == 1) return _nextChar;

            int targetPos = _position + offset;
            return targetPos < _sourceLength ? _sourceChars[targetPos] : '\0';
        }

        /// <summary>
        /// 高性能字符访问：直接返回缓存的字符或快速访问数组
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public char GetCharAt(int position)
        {
            // 优化常见情况：访问当前位置或相邻位置
            if (position == _position) return _currentChar;
            if (position == _position + 1) return _nextChar;
            if (position == _position - 1) return _prevChar;

            // 非缓存位置的快速访问
            return position < _sourceLength && position >= 0 ? _sourceChars[position] : '\0';
        }

        /// <summary>
        /// 高性能字符串范围获取：使用Span避免不必要的分配
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string GetRange(int start, int end)
        {
            int length = end - start;
            if (length <= 0) return string.Empty;

            // 边界检查
            if (start < 0 || end > _sourceLength || start > end)
            {
                MLogger.Error($"[词法错误] 无效范围: start={start}, end={end}, sourceLength={_sourceLength}");
                return null;
            }

            // 使用Span进行高效的字符串创建
            return new string(_sourceChars.AsSpan(start, length));
        }

        public void Clear()
        {
            // 更新源字符缓冲
            _sourceChars = null;
            _sourceLength = 0;

            // 重置位置与位置信息
            _position = 0;
            _line = 0;
            _column = 0;

            // 重置字符缓存
            _currentChar = '\0';
            _nextChar = '\0';
            _prevChar = '\0';
        }

        public void Dispose()
        {
            Clear();
        }
    }

    public static class CharacterStreamExtension
    {
        /// <summary>
        /// 检查是否为注释标记
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsCommentMark(this CharStream stream)
        {
            return stream.CurrentChar == '/' && stream.NextChar == '/';
        }

        /// <summary>
        /// 检查是否为引号标记
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsQuoteMark(this CharStream stream)
        {
            return stream.CurrentChar is '\'' or '"';
        }

        /// <summary>
        /// 检查是否为CRLF标记
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsCRLFMark(this CharStream stream)
        {
            return stream.CurrentChar == '\r' && stream.NextChar == '\n';
        }

        /// <summary>
        /// 换行符检查
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNewlineMark(this CharStream stream)
        {
            return stream.CurrentChar is '\n' or '\r';
        }

        /// <summary>
        /// 换行符或文件末尾检查
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNewlineOrEOFMark(this CharStream stream)
        {
            return stream.CurrentChar is '\n' or '\r' or '\0';
        }

        /// <summary>
        /// 检查是否为文件末尾标记
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsEOFMark(this CharStream stream)
        {
            return stream.CurrentChar is '\0';
        }

        /// <summary>
        /// 空格或缩进字符检查
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSpaceOrIndentMark(this CharStream stream)
        {
            return stream.CurrentChar is ' ' or '\t';
        }

        /// <summary>
        /// 检查是否为命令开始标记
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsCommandStart(this CharStream stream)
        {
            return stream.CurrentChar == '<' && stream.NextChar == '<';
        }

        /// <summary>
        /// 检查是否为命令结束标记 >>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsCommandEnd(this CharStream stream)
        {
            return stream.CurrentChar == '>' && stream.NextChar == '>';
        }

        /// <summary>
        /// 检查是否为节点开始标记 ---
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsOptionMark(this CharStream stream)
        {
            return stream.CurrentChar == '-' && stream.NextChar == '>';
        }

        /// <summary>
        /// 检查是否为节点开始标记
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNodeStartMark(this CharStream stream)
        {
            return stream.CurrentChar == '-' &&
                   stream.NextChar == '-' &&
                   stream.PeekAhead(2) == '-';
        }

        /// <summary>
        /// 检查是否为节点结束标记 ===
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNodeEndMark(this CharStream stream)
        {
            return stream.CurrentChar == '=' &&
                   stream.NextChar == '=' &&
                   stream.PeekAhead(2) == '=';
        }
    }
}
