using System;
using System.Collections.Generic;
using System.Linq;

namespace MookDialogueScript
{
    /// <summary>
    /// 统一错误码枚举，用于标识不同类型的脚本错误
    /// </summary>
    public enum ErrorCode
    {
        // 语义分析错误
        SA_CALL_UNVERIFIED,         // 函数调用未验证
        SA_FUNC_NOT_FOUND,          // 语义分析：函数未找到
        SA_MEMBER_UNKNOWN,          // 成员未知
        
        // 运行时错误
        FUNC_NOT_FOUND,             // 函数未找到
        FUNC_EXPECTED,              // 期望函数类型
        ARG_MISMATCH,               // 参数不匹配
        FUNC_INVOKE_FAIL,           // 函数调用失败
        CALLABLE_NOT_SUPPORTED,     // 不支持的可调用对象
        
        // 绑定/序列化错误
        FUNC_MEMBER_NOT_BOUND,      // 函数成员未绑定
        SER_REBIND_FAIL             // 序列化重绑定失败
    }

    /// <summary>
    /// 脚本异常基类，包含行号和列号信息用于错误定位
    /// 所有脚本相关的异常都应继承此类以提供统一的位置信息
    /// </summary>
    public abstract class ScriptException : Exception
    {
        /// <summary>
        /// 获取异常的错误码
        /// </summary>
        public ErrorCode? ErrorCode { get; }
        
        /// <summary>
        /// 获取异常发生的行号（从1开始）
        /// </summary>
        public int Line { get; }

        /// <summary>
        /// 获取异常发生的列号（从1开始）
        /// </summary>
        public int Column { get; }

        /// <summary>
        /// 获取相关建议信息
        /// </summary>
        public string Suggestion { get; }

        /// <summary>
        /// 初始化 ScriptException 类的新实例
        /// </summary>
        protected ScriptException()
        {
            Line = 0;
            Column = 0;
            Suggestion = null;
        }

        /// <summary>
        /// 使用指定的错误消息初始化 ScriptException 类的新实例
        /// </summary>
        /// <param name="message">描述错误的消息</param>
        protected ScriptException(string message) : base(message)
        {
            Line = 0;
            Column = 0;
            Suggestion = null;
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
            Suggestion = null;
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
            Suggestion = null;
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
            Suggestion = null;
        }

        /// <summary>
        /// 使用指定的错误码、消息、位置信息和建议初始化 ScriptException 类的新实例
        /// </summary>
        /// <param name="errorCode">错误码</param>
        /// <param name="message">描述错误的消息</param>
        /// <param name="line">异常发生的行号</param>
        /// <param name="column">异常发生的列号</param>
        /// <param name="suggestion">建议信息</param>
        protected ScriptException(ErrorCode errorCode, string message, int line, int column, string suggestion = null) : base(message)
        {
            ErrorCode = errorCode;
            Line = line;
            Column = column;
            Suggestion = suggestion;
        }

        /// <summary>
        /// 使用指定的错误码、消息、位置信息、建议和内部异常初始化 ScriptException 类的新实例
        /// </summary>
        /// <param name="errorCode">错误码</param>
        /// <param name="message">描述错误的消息</param>
        /// <param name="line">异常发生的行号</param>
        /// <param name="column">异常发生的列号</param>
        /// <param name="suggestion">建议信息</param>
        /// <param name="innerException">导致当前异常的异常</param>
        protected ScriptException(ErrorCode errorCode, string message, int line, int column, string suggestion, Exception innerException) : base(message, innerException)
        {
            ErrorCode = errorCode;
            Line = line;
            Column = column;
            Suggestion = suggestion;
        }

        /// <summary>
        /// 返回包含错误码、位置信息和建议的异常消息
        /// </summary>
        /// <returns>格式化的异常消息</returns>
        public override string ToString()
        {
            string baseMessage = base.ToString();
            string formattedMessage = "";

            // 添加错误码前缀
            if (ErrorCode.HasValue)
            {
                formattedMessage = $"[{ErrorCode.Value}] ";
            }

            // 添加位置信息
            if (Line > 0 && Column > 0)
            {
                formattedMessage += $"{baseMessage} (第{Line}行，第{Column}列)";
            }
            else
            {
                formattedMessage += baseMessage;
            }

            // 添加建议信息
            if (!string.IsNullOrEmpty(Suggestion))
            {
                formattedMessage += $"\n建议: {Suggestion}";
            }

            return formattedMessage;
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

        /// <summary>
        /// 使用指定的错误码、消息、位置信息和建议初始化 SemanticException 类的新实例
        /// </summary>
        /// <param name="errorCode">错误码</param>
        /// <param name="message">描述语义分析错误的消息</param>
        /// <param name="line">错误发生的行号</param>
        /// <param name="column">错误发生的列号</param>
        /// <param name="suggestion">建议信息</param>
        public SemanticException(ErrorCode errorCode, string message, int line, int column, string suggestion = null) : base(errorCode, message, line, column, suggestion) { }

        /// <summary>
        /// 使用指定的错误码、消息、位置信息、建议和内部异常初始化 SemanticException 类的新实例
        /// </summary>
        /// <param name="errorCode">错误码</param>
        /// <param name="message">描述语义分析错误的消息</param>
        /// <param name="line">错误发生的行号</param>
        /// <param name="column">错误发生的列号</param>
        /// <param name="suggestion">建议信息</param>
        /// <param name="innerException">导致当前异常的异常</param>
        public SemanticException(ErrorCode errorCode, string message, int line, int column, string suggestion, Exception innerException) : base(errorCode, message, line, column, suggestion, innerException) { }
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

        /// <summary>
        /// 使用指定的错误码、消息、位置信息和建议初始化 InterpreterException 类的新实例
        /// </summary>
        /// <param name="errorCode">错误码</param>
        /// <param name="message">描述解释器运行时错误的消息</param>
        /// <param name="line">错误发生的行号</param>
        /// <param name="column">错误发生的列号</param>
        /// <param name="suggestion">建议信息</param>
        public InterpreterException(ErrorCode errorCode, string message, int line, int column, string suggestion = null) : base(errorCode, message, line, column, suggestion) { }

        /// <summary>
        /// 使用指定的错误码、消息、位置信息、建议和内部异常初始化 InterpreterException 类的新实例
        /// </summary>
        /// <param name="errorCode">错误码</param>
        /// <param name="message">描述解释器运行时错误的消息</param>
        /// <param name="line">错误发生的行号</param>
        /// <param name="column">错误发生的列号</param>
        /// <param name="suggestion">建议信息</param>
        /// <param name="innerException">导致当前异常的异常</param>
        public InterpreterException(ErrorCode errorCode, string message, int line, int column, string suggestion, Exception innerException) : base(errorCode, message, line, column, suggestion, innerException) { }
    }

    /// <summary>
    /// 异常工厂类，提供统一的异常创建和相似名建议功能
    /// </summary>
    public static class ExceptionFactory
    {
        /// <summary>
        /// 创建函数未找到异常，包含相似名建议
        /// </summary>
        /// <param name="functionName">未找到的函数名</param>
        /// <param name="availableFunctions">可用函数名列表</param>
        /// <param name="line">行号</param>
        /// <param name="column">列号</param>
        /// <returns>包含建议的异常</returns>
        public static InterpreterException CreateFunctionNotFoundException(string functionName, IEnumerable<string> availableFunctions, int line = 0, int column = 0)
        {
            string suggestion = GetSimilarNameSuggestion(functionName, availableFunctions);
            string message = $"未找到函数 '{functionName}'";
            return new InterpreterException(ErrorCode.FUNC_NOT_FOUND, message, line, column, suggestion);
        }

        /// <summary>
        /// 创建函数类型期望异常
        /// </summary>
        /// <param name="actualType">实际类型</param>
        /// <param name="line">行号</param>
        /// <param name="column">列号</param>
        /// <returns>异常实例</returns>
        public static InterpreterException CreateFunctionExpectedException(string actualType, int line = 0, int column = 0)
        {
            string message = $"期望函数类型，但得到 '{actualType}'";
            string suggestion = "确保变量包含函数值或调用有效的函数名";
            return new InterpreterException(ErrorCode.FUNC_EXPECTED, message, line, column, suggestion);
        }

        /// <summary>
        /// 创建参数不匹配异常
        /// </summary>
        /// <param name="expectedCount">期望参数数量</param>
        /// <param name="actualCount">实际参数数量</param>
        /// <param name="line">行号</param>
        /// <param name="column">列号</param>
        /// <returns>异常实例</returns>
        public static InterpreterException CreateArgumentMismatchException(int expectedCount, int actualCount, int line = 0, int column = 0)
        {
            string message = $"参数数量不匹配：期望 {expectedCount} 个，实际 {actualCount} 个";
            string suggestion = "检查函数调用的参数数量是否正确";
            return new InterpreterException(ErrorCode.ARG_MISMATCH, message, line, column, suggestion);
        }

        /// <summary>
        /// 创建函数调用失败异常
        /// </summary>
        /// <param name="functionName">函数名</param>
        /// <param name="innerException">内部异常</param>
        /// <param name="line">行号</param>
        /// <param name="column">列号</param>
        /// <returns>异常实例</returns>
        public static InterpreterException CreateFunctionInvokeFailException(string functionName, Exception innerException, int line = 0, int column = 0)
        {
            string message = $"函数 '{functionName}' 调用失败";
            string suggestion = "检查函数实现是否正确，参数类型是否匹配";
            return new InterpreterException(ErrorCode.FUNC_INVOKE_FAIL, message, line, column, suggestion, innerException);
        }

        /// <summary>
        /// 创建不支持可调用对象异常
        /// </summary>
        /// <param name="objectType">对象类型</param>
        /// <param name="line">行号</param>
        /// <param name="column">列号</param>
        /// <returns>异常实例</returns>
        public static InterpreterException CreateCallableNotSupportedException(string objectType, int line = 0, int column = 0)
        {
            string message = $"类型 '{objectType}' 不支持调用操作";
            string suggestion = "确保对象是函数、委托或具有 Invoke 方法的类型";
            return new InterpreterException(ErrorCode.CALLABLE_NOT_SUPPORTED, message, line, column, suggestion);
        }

        /// <summary>
        /// 创建语义分析函数未找到异常
        /// </summary>
        /// <param name="functionName">函数名</param>
        /// <param name="availableFunctions">可用函数名列表</param>
        /// <param name="line">行号</param>
        /// <param name="column">列号</param>
        /// <returns>异常实例</returns>
        public static SemanticException CreateSemanticFunctionNotFoundException(string functionName, IEnumerable<string> availableFunctions, int line = 0, int column = 0)
        {
            string suggestion = GetSimilarNameSuggestion(functionName, availableFunctions);
            string message = $"语义分析：未找到函数 '{functionName}'";
            return new SemanticException(ErrorCode.SA_FUNC_NOT_FOUND, message, line, column, suggestion);
        }

        /// <summary>
        /// 创建函数成员未绑定异常
        /// </summary>
        /// <param name="objectName">对象名</param>
        /// <param name="memberName">成员名</param>
        /// <param name="line">行号</param>
        /// <param name="column">列号</param>
        /// <returns>异常实例</returns>
        public static InterpreterException CreateFunctionMemberNotBoundException(string objectName, string memberName, int line = 0, int column = 0)
        {
            string message = $"对象 '{objectName}' 的成员方法 '{memberName}' 未绑定到函数管理器";
            string suggestion = "确保对象已注册且方法标注了 [ScriptFunc] 特性";
            return new InterpreterException(ErrorCode.FUNC_MEMBER_NOT_BOUND, message, line, column, suggestion);
        }

        /// <summary>
        /// 获取相似名称建议（基于编辑距离）
        /// </summary>
        /// <param name="target">目标名称</param>
        /// <param name="candidates">候选名称集合</param>
        /// <param name="maxSuggestions">最大建议数量</param>
        /// <returns>建议字符串，如果没有相似项则返回 null</returns>
        private static string GetSimilarNameSuggestion(string target, IEnumerable<string> candidates, int maxSuggestions = 3)
        {
            return Utils.GenerateSuggestionMessage(target, candidates, maxSuggestions);
        }

    }
}
