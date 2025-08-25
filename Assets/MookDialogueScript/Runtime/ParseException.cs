using System;

namespace MookDialogueScript
{
    /// <summary>
    /// 脚本异常基类，包含行号和列号信息用于错误定位
    /// 所有脚本相关的异常都应继承此类以提供统一的位置信息
    /// </summary>
    public abstract class ScriptException : Exception
    {
        /// <summary>
        /// 获取异常发生的行号（从1开始）
        /// </summary>
        public int Line { get; }

        /// <summary>
        /// 获取异常发生的列号（从1开始）
        /// </summary>
        public int Column { get; }

        /// <summary>
        /// 初始化 ScriptException 类的新实例
        /// </summary>
        protected ScriptException()
        {
            Line = 0;
            Column = 0;
        }

        /// <summary>
        /// 使用指定的错误消息初始化 ScriptException 类的新实例
        /// </summary>
        /// <param name="message">描述错误的消息</param>
        protected ScriptException(string message) : base(message)
        {
            Line = 0;
            Column = 0;
        }

        /// <summary>
        /// 使用指定的错误消息和对作为此异常原因的内部异常的引用来初始化 ScriptException 类的新实例
        /// </summary>
        /// <param name="message">描述错误的消息</param>
        /// <param name="innerException">导致当前异常的异常</param>
        protected ScriptException(string message, Exception innerException) : base(message, innerException)
        {
            Line = 0;
            Column = 0;
        }

        /// <summary>
        /// 使用指定的错误消息和位置信息初始化 ScriptException 类的新实例
        /// </summary>
        /// <param name="message">描述错误的消息</param>
        /// <param name="line">异常发生的行号</param>
        /// <param name="column">异常发生的列号</param>
        protected ScriptException(string message, int line, int column) : base(message)
        {
            Line = line;
            Column = column;
        }

        /// <summary>
        /// 使用指定的错误消息、位置信息和对作为此异常原因的内部异常的引用来初始化 ScriptException 类的新实例
        /// </summary>
        /// <param name="message">描述错误的消息</param>
        /// <param name="line">异常发生的行号</param>
        /// <param name="column">异常发生的列号</param>
        /// <param name="innerException">导致当前异常的异常</param>
        protected ScriptException(string message, int line, int column, Exception innerException) : base(message, innerException)
        {
            Line = line;
            Column = column;
        }

        /// <summary>
        /// 返回包含位置信息的异常消息
        /// </summary>
        /// <returns>格式化的异常消息</returns>
        public override string ToString()
        {
            string baseMessage = base.ToString();
            if (Line > 0 && Column > 0)
            {
                return $"第{Line}行，第{Column}列: {baseMessage}";
            }
            return baseMessage;
        }
    }

    /// <summary>
    /// 词法分析异常 - 在词法分析阶段发生的错误
    /// 包括无效字符、未闭合的字符串、数字格式错误等
    /// </summary>
    public class LexerException : ScriptException
    {
        /// <summary>
        /// 初始化 LexerException 类的新实例
        /// </summary>
        public LexerException()
        {
        }

        /// <summary>
        /// 使用指定的错误消息初始化 LexerException 类的新实例
        /// </summary>
        /// <param name="message">描述词法分析错误的消息</param>
        public LexerException(string message) : base(message) { }

        /// <summary>
        /// 使用指定的错误消息和内部异常初始化 LexerException 类的新实例
        /// </summary>
        /// <param name="message">描述词法分析错误的消息</param>
        /// <param name="innerException">导致当前异常的异常</param>
        public LexerException(string message, Exception innerException) : base(message, innerException) { }

        /// <summary>
        /// 使用指定的错误消息和位置信息初始化 LexerException 类的新实例
        /// </summary>
        /// <param name="message">描述词法分析错误的消息</param>
        /// <param name="line">错误发生的行号</param>
        /// <param name="column">错误发生的列号</param>
        public LexerException(string message, int line, int column) : base(message, line, column) { }

        /// <summary>
        /// 使用指定的错误消息、位置信息和内部异常初始化 LexerException 类的新实例
        /// </summary>
        /// <param name="message">描述词法分析错误的消息</param>
        /// <param name="line">错误发生的行号</param>
        /// <param name="column">错误发生的列号</param>
        /// <param name="innerException">导致当前异常的异常</param>
        public LexerException(string message, int line, int column, Exception innerException) : base(message, line, column, innerException) { }
    }

    /// <summary>
    /// 语法分析异常 - 在语法分析阶段发生的错误
    /// 包括语法结构错误、缺少必需的标记、意外的标记等
    /// </summary>
    public class ParseException : ScriptException
    {
        /// <summary>
        /// 初始化 ParseException 类的新实例
        /// </summary>
        public ParseException()
        {
        }

        /// <summary>
        /// 使用指定的错误消息初始化 ParseException 类的新实例
        /// </summary>
        /// <param name="message">描述语法分析错误的消息</param>
        public ParseException(string message) : base(message) { }

        /// <summary>
        /// 使用指定的错误消息和内部异常初始化 ParseException 类的新实例
        /// </summary>
        /// <param name="message">描述语法分析错误的消息</param>
        /// <param name="innerException">导致当前异常的异常</param>
        public ParseException(string message, Exception innerException) : base(message, innerException) { }

        /// <summary>
        /// 使用指定的错误消息和位置信息初始化 ParseException 类的新实例
        /// </summary>
        /// <param name="message">描述语法分析错误的消息</param>
        /// <param name="line">错误发生的行号</param>
        /// <param name="column">错误发生的列号</param>
        public ParseException(string message, int line, int column) : base(message, line, column) { }

        /// <summary>
        /// 使用指定的错误消息、位置信息和内部异常初始化 ParseException 类的新实例
        /// </summary>
        /// <param name="message">描述语法分析错误的消息</param>
        /// <param name="line">错误发生的行号</param>
        /// <param name="column">错误发生的列号</param>
        /// <param name="innerException">导致当前异常的异常</param>
        public ParseException(string message, int line, int column, Exception innerException) : base(message, line, column, innerException) { }
    }

    /// <summary>
    /// 语义分析异常 - 在语义分析阶段发生的错误
    /// 包括类型不匹配、未定义的变量或函数、作用域错误等
    /// </summary>
    public class SemanticException : ScriptException
    {
        /// <summary>
        /// 初始化 SemanticException 类的新实例
        /// </summary>
        public SemanticException()
        {
        }

        /// <summary>
        /// 使用指定的错误消息初始化 SemanticException 类的新实例
        /// </summary>
        /// <param name="message">描述语义分析错误的消息</param>
        public SemanticException(string message) : base(message) { }

        /// <summary>
        /// 使用指定的错误消息和内部异常初始化 SemanticException 类的新实例
        /// </summary>
        /// <param name="message">描述语义分析错误的消息</param>
        /// <param name="innerException">导致当前异常的异常</param>
        public SemanticException(string message, Exception innerException) : base(message, innerException) { }

        /// <summary>
        /// 使用指定的错误消息和位置信息初始化 SemanticException 类的新实例
        /// </summary>
        /// <param name="message">描述语义分析错误的消息</param>
        /// <param name="line">错误发生的行号</param>
        /// <param name="column">错误发生的列号</param>
        public SemanticException(string message, int line, int column) : base(message, line, column) { }

        /// <summary>
        /// 使用指定的错误消息、位置信息和内部异常初始化 SemanticException 类的新实例
        /// </summary>
        /// <param name="message">描述语义分析错误的消息</param>
        /// <param name="line">错误发生的行号</param>
        /// <param name="column">错误发生的列号</param>
        /// <param name="innerException">导致当前异常的异常</param>
        public SemanticException(string message, int line, int column, Exception innerException) : base(message, line, column, innerException) { }
    }

    /// <summary>
    /// 解释器运行时异常 - 在脚本执行阶段发生的错误
    /// 包括运行时类型错误、空引用、除零错误、函数调用失败等
    /// </summary>
    public class InterpreterException : ScriptException
    {
        /// <summary>
        /// 初始化 InterpreterException 类的新实例
        /// </summary>
        public InterpreterException()
        {
        }

        /// <summary>
        /// 使用指定的错误消息初始化 InterpreterException 类的新实例
        /// </summary>
        /// <param name="message">描述解释器运行时错误的消息</param>
        public InterpreterException(string message) : base(message) { }

        /// <summary>
        /// 使用指定的错误消息和内部异常初始化 InterpreterException 类的新实例
        /// </summary>
        /// <param name="message">描述解释器运行时错误的消息</param>
        /// <param name="innerException">导致当前异常的异常</param>
        public InterpreterException(string message, Exception innerException) : base(message, innerException) { }

        /// <summary>
        /// 使用指定的错误消息和位置信息初始化 InterpreterException 类的新实例
        /// </summary>
        /// <param name="message">描述解释器运行时错误的消息</param>
        /// <param name="line">错误发生的行号</param>
        /// <param name="column">错误发生的列号</param>
        public InterpreterException(string message, int line, int column) : base(message, line, column) { }

        /// <summary>
        /// 使用指定的错误消息、位置信息和内部异常初始化 InterpreterException 类的新实例
        /// </summary>
        /// <param name="message">描述解释器运行时错误的消息</param>
        /// <param name="line">错误发生的行号</param>
        /// <param name="column">错误发生的列号</param>
        /// <param name="innerException">导致当前异常的异常</param>
        public InterpreterException(string message, int line, int column, Exception innerException) : base(message, line, column, innerException) { }
    }
}
