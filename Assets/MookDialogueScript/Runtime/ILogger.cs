using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace MookDialogueScript
{
    /// <summary>
    /// 通用日志接口
    /// </summary>
    public interface ILogger
    {
        void Log(string message, LogLevel level = LogLevel.Info,
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string memberName = "");
    }

    /// <summary>
    /// 日志级别枚举
    /// </summary>
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// 通用日志工具类
    /// </summary>
    public static class MLogger
    {
        private static ILogger _logger = new ConsoleLogger();

        /// <summary>
        /// 初始化日志系统
        /// </summary>
        public static void Initialize(ILogger logger)
        {
            _logger = logger;
        }

        [Conditional("DEBUG")]
        public static void Debug(string message,
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string memberName = "")
        {
            _logger?.Log(message, LogLevel.Debug, filePath, lineNumber, memberName);
        }

        public static void Info(string message,
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string memberName = "")
        {
            _logger?.Log(message, LogLevel.Info, filePath, lineNumber, memberName);
        }

        public static void Warning(string message,
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string memberName = "")
        {
            _logger?.Log(message, LogLevel.Warning, filePath, lineNumber, memberName);
        }

        public static void Error(string message,
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string memberName = "")
        {
            _logger?.Log(message, LogLevel.Error, filePath, lineNumber, memberName);
        }
    }

    /// <summary>
    /// 控制台日志实现
    /// </summary>
    public class ConsoleLogger : ILogger
    {
        public void Log(string message, LogLevel level = LogLevel.Info,
            string filePath = "", int lineNumber = 0, string memberName = "")
        {
            string formattedMessage = $"[{level}][{memberName}] {message}\n在 {filePath}:行{lineNumber}";

            switch (level)
            {
                case LogLevel.Debug:
                case LogLevel.Info:
                    Console.WriteLine(formattedMessage);
                    break;
                case LogLevel.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(formattedMessage);
                    Console.ResetColor();
                    break;
                case LogLevel.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(formattedMessage);
                    Console.ResetColor();
                    break;
                default:
                    Console.WriteLine(formattedMessage);
                    break;
            }
        }
    }

#if GODOT
/// <summary>
/// Godot引擎的日志实现
/// </summary>
public class GodotLogger : ILogger
{
    public void Log(string message, LogLevel level = LogLevel.Info, 
        string filePath = "", int lineNumber = 0, string memberName = "")
    {
        string formattedMessage = $"[{memberName}] {message}\n在 {filePath}:行{lineNumber}";
        
        switch (level)
        {
            case LogLevel.Debug:
                GD.PrintDebug(formattedMessage);
                break;
            case LogLevel.Info:
                GD.Print(formattedMessage);
                break;
            case LogLevel.Warning:
                GD.PrintWarning(formattedMessage);
                break;
            case LogLevel.Error:
                GD.PrintErr(formattedMessage);
                break;
        }
    }
}
#endif

}
