using System.Runtime.CompilerServices;

namespace MookDialogueScript.Lexers
{
    /// <summary>
    /// 高性能字符分类实现，使用预计算查找表加速ASCII字符分类
    /// 设计原则：单一职责 - 专注于字符分类逻辑优化
    /// </summary>
    public class CharacterClassifier
    {
        // 预计算查找表：避免重复的字符分类计算，保持原有性能优化
        private static readonly bool[] _isWhitespaceTable = new bool[128];
        private static readonly bool[] _isLetterTable = new bool[128];
        private static readonly bool[] _isDigitTable = new bool[128];
        private static readonly bool[] _isLetterOrDigitTable = new bool[128];

        static CharacterClassifier()
        {
            // 预计算ASCII字符的分类，避免运行时计算
            for (int i = 0; i < 128; i++)
            {
                char c = (char)i;
                _isWhitespaceTable[i] = char.IsWhiteSpace(c);
                _isLetterTable[i] = char.IsLetter(c);
                _isDigitTable[i] = char.IsDigit(c);
                _isLetterOrDigitTable[i] = char.IsLetterOrDigit(c);
            }
        }

        /// <summary>
        /// 高性能空白字符检查：使用预计算查找表
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsWhitespace(char c)
        {
            return c < 128 ? _isWhitespaceTable[c] : char.IsWhiteSpace(c);
        }

        /// <summary>
        /// 高性能字母检查：使用预计算查找表
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsLetter(char c)
        {
            return c < 128 ? _isLetterTable[c] : char.IsLetter(c);
        }

        /// <summary>
        /// 高性能数字检查：使用预计算查找表
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsDigit(char c)
        {
            return c < 128 ? _isDigitTable[c] : char.IsDigit(c);
        }

        /// <summary>
        /// 高性能字母或数字检查：使用预计算查找表
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsLetterOrDigit(char c)
        {
            return c < 128 ? _isLetterOrDigitTable[c] : char.IsLetterOrDigit(c);
        }

        /// <summary>
        /// 标识符起始字符检查
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsIdentifierStart(char c)
        {
            return IsLetter(c) || c == '_';
        }

        /// <summary>
        /// 标识符组成字符检查
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsIdentifierPart(char c)
        {
            return IsLetterOrDigit(c) || c == '_';
        }

        /// <summary>
        /// 引号检查
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsQuote(char c)
        {
            return c is '\'' or '"';
        }

        /// <summary>
        /// 换行符或文件末尾检查
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsNewlineOrEOF(char c)
        {
            return c is '\n' or '\r' or '\0';
        }
    }
}
