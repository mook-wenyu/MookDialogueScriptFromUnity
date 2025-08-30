using System;

namespace MookDialogueScript.Lexers
{
    /// <summary>
    /// Token生成器接口，定义Token识别和生成的统一规范
    /// 设计原则：开闭原则 - 新Token类型通过实现此接口进行扩展
    /// </summary>
    public interface ITokenizer : IDisposable
    {
        /// <summary>
        /// 快速判断是否可以处理当前字符流状态
        /// 此方法应该尽可能高效，避免复杂的计算
        /// </summary>
        /// <param name="stream">字符流</param>
        /// <param name="state">词法分析器状态</param>
        /// <returns>是否可以处理当前状态</returns>
        bool CanHandle(CharStream stream, LexerState state);

        /// <summary>
        /// 尝试从当前位置生成Token
        /// </summary>
        /// <param name="stream">字符流</param>
        /// <param name="state">词法分析器状态</param>
        /// <returns>成功生成的Token，无法处理返回null</returns>
        Token TryTokenize(CharStream stream, LexerState state);

        /// <summary>
        /// 获取处理器的描述信息（用于调试和日志）
        /// </summary>
        string Description { get; }

        /// <summary>
        /// 获取此处理器可以生成的Token类型列表
        /// </summary>
        TokenType[] SupportedTokenTypes { get; }
    }
}
