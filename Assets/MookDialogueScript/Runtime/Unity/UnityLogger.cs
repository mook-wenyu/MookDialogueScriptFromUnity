using UnityEngine;

namespace MookDialogueScript
{
    /// <summary>
    /// Unity引擎的日志实现
    /// </summary>
    public class UnityLogger : ILogger
    {
        public void Log(string message, LogLevel level = LogLevel.Info,
            string filePath = "", int lineNumber = 0, string memberName = "")
        {
            string formattedMessage = $"[{memberName}] {message}\n在 {filePath}:行{lineNumber}";

            switch (level)
            {
                case LogLevel.Debug:
                case LogLevel.Info:
                    Debug.Log(formattedMessage);
                    break;
                case LogLevel.Warning:
                    Debug.LogWarning(formattedMessage);
                    break;
                case LogLevel.Error:
                    Debug.LogError(formattedMessage);
                    break;
                default:
                    Debug.Log(formattedMessage);
                    break;
            }
        }
    }
}