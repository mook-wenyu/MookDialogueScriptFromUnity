using System;
using System.Collections.Generic;
using System.Linq;

namespace MookDialogueScript.Incremental
{
    /// <summary>
    /// 解析结果数据结构
    /// 包含对话脚本的词法分析和语法解析完整结果
    /// </summary>
    public sealed class ParseResult
    {
        /// <summary>
        /// 源文件路径
        /// </summary>
        public string FilePath { get; private set; } = string.Empty;

        /// <summary>
        /// 词法分析结果（Token列表）
        /// </summary>
        public List<Token> Tokens { get; private set; } = new();

        /// <summary>
        /// 语法分析结果（AST根节点）
        /// </summary>
        public ScriptNode ScriptNode { get; private set; }

        /// <summary>
        /// 解析成功标志
        /// </summary>
        public bool IsSuccess { get; private set; }

        /// <summary>
        /// 解析器版本信息
        /// </summary>
        public string ParserVersion { get; private set; } = string.Empty;

        /// <summary>
        /// 缓存创建时间
        /// </summary>
        public DateTime CacheCreatedTime { get; private set; } = DateTime.UtcNow;

        /// <summary>
        /// 缓存访问次数
        /// </summary>
        public int AccessCount { get; private set; }

        /// <summary>
        /// 最后访问时间
        /// </summary>
        public DateTime LastAccessTime { get; private set; } = DateTime.UtcNow;

        /// <summary>
        /// 创建成功的解析结果
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="tokens">Token列表</param>
        /// <param name="scriptNode">脚本节点</param>
        /// <param name="parserVersion">解析器版本</param>
        /// <returns>成功的解析结果</returns>
        public static ParseResult CreateSuccess(
            string filePath,
            List<Token> tokens,
            ScriptNode scriptNode,
            string parserVersion)
        {
            return new ParseResult
            {
                FilePath = filePath,
                Tokens = tokens ?? new List<Token>(),
                ScriptNode = scriptNode,
                IsSuccess = true,
                ParserVersion = parserVersion,
            };
        }

        /// <summary>
        /// 克隆当前解析结果
        /// </summary>
        /// <returns>克隆的解析结果实例</returns>
        private ParseResult Clone()
        {
            return new ParseResult
            {
                FilePath = this.FilePath,
                Tokens = new List<Token>(this.Tokens),
                ScriptNode = this.ScriptNode,
                IsSuccess = this.IsSuccess,
                ParserVersion = this.ParserVersion,
                CacheCreatedTime = this.CacheCreatedTime,
                AccessCount = this.AccessCount,
                LastAccessTime = this.LastAccessTime
            };
        }
    }
}
