using System;

namespace MookDialogueScript.Lexers
{
    /// <summary>
    /// 表示源代码中的一个位置，用于错误报告和位置标记
    /// </summary>
    public readonly struct SourceLocation : IEquatable<SourceLocation>
    {
        /// <summary>
        /// 表示无效位置
        /// </summary>
        public static readonly SourceLocation None = new(-1, -1, -1);

        /// <summary>
        /// 字符位置（从0开始）
        /// </summary>
        public int Position { get; }

        /// <summary>
        /// 行号（从1开始）
        /// </summary>
        public int Line { get; }

        /// <summary>
        /// 列号（从1开始）
        /// </summary>
        public int Column { get; }

        public SourceLocation(int position, int line, int column)
        {
            Position = position;
            Line = line;
            Column = column;
        }

        /// <summary>
        /// 从字符流创建SourceLocation
        /// </summary>
        /// <param name="stream">字符流</param>
        /// <returns>表示当前位置的SourceLocation</returns>
        public static SourceLocation FromStream(CharStream stream)
        {
            if (stream == null)
            {
                // MLogger.Error();
                return None;
            }

            return new SourceLocation(stream.Position, stream.Line, stream.Column);
        }

        /// <summary>
        /// 是否为有效位置
        /// </summary>
        public bool IsValid => Position >= 0 && Line > 0 && Column > 0;

        public override string ToString() =>
            IsValid ? $"({Line},{Column})" : "(None)";

        public bool Equals(SourceLocation other) =>
            Position == other.Position && Line == other.Line && Column == other.Column;

        public override bool Equals(object obj) =>
            obj is SourceLocation other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Position, Line, Column);

        public static bool operator ==(SourceLocation left, SourceLocation right) =>
            left.Equals(right);

        public static bool operator !=(SourceLocation left, SourceLocation right) =>
            !left.Equals(right);

    }
}
