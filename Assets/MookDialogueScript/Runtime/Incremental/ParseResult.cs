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
        /// 源文件元数据
        /// </summary>
        public FileMetadata FileMetadata { get; private set; } = FileMetadata.CreateEmpty(string.Empty);

        /// <summary>
        /// 词法分析结果（Token列表）
        /// </summary>
        public List<Token> Tokens { get; private set; } = new List<Token>();

        /// <summary>
        /// 语法分析结果（AST根节点）
        /// </summary>
        public ASTNode RootNode { get; private set; }

        /// <summary>
        /// 解析成功标志
        /// </summary>
        public bool IsSuccess { get; private set; }

        /// <summary>
        /// 解析错误列表
        /// </summary>
        public List<ParseError> Errors { get; private set; } = new List<ParseError>();

        /// <summary>
        /// 解析警告列表
        /// </summary>
        public List<ParseWarning> Warnings { get; private set; } = new List<ParseWarning>();

        /// <summary>
        /// 解析开始时间
        /// </summary>
        public DateTime ParseStartTime { get; private set; }

        /// <summary>
        /// 解析结束时间
        /// </summary>
        public DateTime ParseEndTime { get; private set; }

        /// <summary>
        /// 解析耗时
        /// </summary>
        public TimeSpan ParseDuration => ParseEndTime - ParseStartTime;

        /// <summary>
        /// 解析器版本信息
        /// </summary>
        public string ParserVersion { get; private set; } = string.Empty;

        /// <summary>
        /// 解析统计信息
        /// </summary>
        public ParseStatistics Statistics { get; private set; } = new ParseStatistics();

        /// <summary>
        /// 扩展属性字典
        /// </summary>
        public Dictionary<string, object> ExtendedProperties { get; private set; } = new Dictionary<string, object>();

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
        /// <param name="fileMetadata">文件元数据</param>
        /// <param name="tokens">Token列表</param>
        /// <param name="rootNode">AST根节点</param>
        /// <param name="parseStartTime">解析开始时间</param>
        /// <param name="parseEndTime">解析结束时间</param>
        /// <param name="parserVersion">解析器版本</param>
        /// <returns>成功的解析结果</returns>
        public static ParseResult CreateSuccess(
            string filePath,
            FileMetadata fileMetadata,
            List<Token> tokens,
            ASTNode rootNode,
            DateTime parseStartTime,
            DateTime parseEndTime,
            string parserVersion)
        {
            return new ParseResult
            {
                FilePath = filePath,
                FileMetadata = fileMetadata,
                Tokens = tokens ?? new List<Token>(),
                RootNode = rootNode,
                IsSuccess = true,
                ParseStartTime = parseStartTime,
                ParseEndTime = parseEndTime,
                ParserVersion = parserVersion,
                Statistics = ParseStatistics.FromTokensAndNode(tokens, rootNode)
            };
        }

        /// <summary>
        /// 创建失败的解析结果
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="fileMetadata">文件元数据</param>
        /// <param name="errors">错误列表</param>
        /// <param name="parseStartTime">解析开始时间</param>
        /// <param name="parseEndTime">解析结束时间</param>
        /// <param name="parserVersion">解析器版本</param>
        /// <returns>失败的解析结果</returns>
        public static ParseResult CreateFailure(
            string filePath,
            FileMetadata fileMetadata,
            List<ParseError> errors,
            DateTime parseStartTime,
            DateTime parseEndTime,
            string parserVersion)
        {
            return new ParseResult
            {
                FilePath = filePath,
                FileMetadata = fileMetadata,
                Errors = errors ?? new List<ParseError>(),
                IsSuccess = false,
                ParseStartTime = parseStartTime,
                ParseEndTime = parseEndTime,
                ParserVersion = parserVersion
            };
        }

        /// <summary>
        /// 检查解析结果是否有效（可用于缓存）
        /// </summary>
        /// <param name="maxAge">最大缓存时间</param>
        /// <returns>是否有效</returns>
        public bool IsValidForCache(TimeSpan maxAge)
        {
            return DateTime.UtcNow - CacheCreatedTime <= maxAge;
        }

        /// <summary>
        /// 检查是否与指定的文件元数据匹配
        /// </summary>
        /// <param name="metadata">要比较的元数据</param>
        /// <returns>是否匹配</returns>
        public bool MatchesFileMetadata(FileMetadata metadata)
        {
            return FileMetadata.IsEquivalent(metadata, true);
        }

        /// <summary>
        /// 更新访问统计
        /// </summary>
        /// <returns>更新后的解析结果</returns>
        public ParseResult WithUpdatedAccess()
        {
            var result = Clone();
            result.AccessCount += 1;
            result.LastAccessTime = DateTime.UtcNow;
            return result;
        }

        /// <summary>
        /// 添加扩展属性
        /// </summary>
        /// <param name="key">属性键</param>
        /// <param name="value">属性值</param>
        /// <returns>更新后的解析结果</returns>
        public ParseResult WithExtendedProperty(string key, object value)
        {
            var result = Clone();
            result.ExtendedProperties = new Dictionary<string, object>(ExtendedProperties);
            result.ExtendedProperties[key] = value;
            return result;
        }

        /// <summary>
        /// 获取扩展属性值
        /// </summary>
        /// <typeparam name="T">属性值类型</typeparam>
        /// <param name="key">属性键</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>属性值</returns>
        public T GetExtendedProperty<T>(string key, T defaultValue = default(T))
        {
            if (ExtendedProperties.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return defaultValue;
        }

        /// <summary>
        /// 获取解析结果摘要
        /// </summary>
        /// <returns>摘要字符串</returns>
        public string GetSummary()
        {
            var status = IsSuccess ? "成功" : "失败";
            var tokenCount = Tokens?.Count ?? 0;
            var errorCount = Errors?.Count ?? 0;
            var warningCount = Warnings?.Count ?? 0;
            var duration = ParseDuration.TotalMilliseconds;

            return $"解析{status}: {tokenCount}个Token, {errorCount}个错误, {warningCount}个警告, 耗时{duration:F2}ms";
        }

        /// <summary>
        /// 计算解析结果的内存使用量估计（字节）
        /// </summary>
        /// <returns>内存使用量</returns>
        public long EstimateMemoryUsage()
        {
            long size = 0;

            // 基础字符串大小
            size += (FilePath?.Length ?? 0) * 2; // UTF-16
            size += (ParserVersion?.Length ?? 0) * 2;

            // Token列表大小估计
            size += (Tokens?.Count ?? 0) * 64; // 每个Token约64字节

            // 错误和警告列表大小估计
            size += (Errors?.Count ?? 0) * 128; // 每个错误约128字节
            size += (Warnings?.Count ?? 0) * 128; // 每个警告约128字节

            // AST节点大小估计（递归计算会很复杂，使用Token数量估算）
            if (RootNode != null)
            {
                size += (Tokens?.Count ?? 0) * 32; // 每个Token对应的AST节点约32字节
            }

            // 扩展属性大小估计
            size += ExtendedProperties.Count * 64;

            return size;
        }

        /// <summary>
        /// 转换为字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString()
        {
            return $"ParseResult: {System.IO.Path.GetFileName(FilePath)} - {GetSummary()}";
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
                FileMetadata = this.FileMetadata,
                Tokens = new List<Token>(this.Tokens),
                RootNode = this.RootNode,
                IsSuccess = this.IsSuccess,
                Errors = new List<ParseError>(this.Errors),
                Warnings = new List<ParseWarning>(this.Warnings),
                ParseStartTime = this.ParseStartTime,
                ParseEndTime = this.ParseEndTime,
                ParserVersion = this.ParserVersion,
                Statistics = this.Statistics,
                ExtendedProperties = new Dictionary<string, object>(this.ExtendedProperties),
                CacheCreatedTime = this.CacheCreatedTime,
                AccessCount = this.AccessCount,
                LastAccessTime = this.LastAccessTime
            };
        }
    }

    /// <summary>
    /// 解析错误信息
    /// </summary>
    public sealed class ParseError
    {
        /// <summary>
        /// 错误代码
        /// </summary>
        public string Code { get; private set; } = string.Empty;

        /// <summary>
        /// 错误消息
        /// </summary>
        public string Message { get; private set; } = string.Empty;

        /// <summary>
        /// 错误行号
        /// </summary>
        public int Line { get; private set; }

        /// <summary>
        /// 错误列号
        /// </summary>
        public int Column { get; private set; }

        /// <summary>
        /// 错误严重程度
        /// </summary>
        public ParseErrorSeverity Severity { get; private set; } = ParseErrorSeverity.Error;

        /// <summary>
        /// 相关的Token（如果有）
        /// </summary>
        public Token RelatedToken { get; private set; }

        /// <summary>
        /// 错误上下文信息
        /// </summary>
        public string Context { get; private set; } = string.Empty;

        /// <summary>
        /// 建议修复方案
        /// </summary>
        public string SuggestedFix { get; private set; }

        public override string ToString()
        {
            return $"{Severity} {Code}: {Message} (行{Line}, 列{Column})";
        }
    }

    /// <summary>
    /// 解析警告信息
    /// </summary>
    public sealed class ParseWarning
    {
        /// <summary>
        /// 警告代码
        /// </summary>
        public string Code { get; private set; } = string.Empty;

        /// <summary>
        /// 警告消息
        /// </summary>
        public string Message { get; private set; } = string.Empty;

        /// <summary>
        /// 警告行号
        /// </summary>
        public int Line { get; private set; }

        /// <summary>
        /// 警告列号
        /// </summary>
        public int Column { get; private set; }

        /// <summary>
        /// 相关的Token（如果有）
        /// </summary>
        public Token RelatedToken { get; private set; }

        /// <summary>
        /// 警告严重程度
        /// </summary>
        public ParseWarningSeverity Severity { get; private set; } = ParseWarningSeverity.Warning;

        public override string ToString()
        {
            return $"{Severity} {Code}: {Message} (行{Line}, 列{Column})";
        }
    }

    /// <summary>
    /// 解析统计信息
    /// </summary>
    public sealed class ParseStatistics
    {
        /// <summary>
        /// Token总数
        /// </summary>
        public int TokenCount { get; private set; }

        /// <summary>
        /// AST节点总数
        /// </summary>
        public int NodeCount { get; private set; }

        /// <summary>
        /// 行数
        /// </summary>
        public int LineCount { get; private set; }

        /// <summary>
        /// 字符数
        /// </summary>
        public int CharacterCount { get; private set; }

        /// <summary>
        /// 节点定义数量
        /// </summary>
        public int NodeDefinitionCount { get; private set; }

        /// <summary>
        /// 变量声明数量
        /// </summary>
        public int VariableDeclarationCount { get; private set; }

        /// <summary>
        /// 函数调用数量
        /// </summary>
        public int FunctionCallCount { get; private set; }

        /// <summary>
        /// 条件语句数量
        /// </summary>
        public int ConditionalCount { get; private set; }

        /// <summary>
        /// 选择语句数量
        /// </summary>
        public int ChoiceCount { get; private set; }

        /// <summary>
        /// 从Token和AST节点创建统计信息
        /// </summary>
        /// <param name="tokens">Token列表</param>
        /// <param name="rootNode">AST根节点</param>
        /// <returns>统计信息</returns>
        public static ParseStatistics FromTokensAndNode(List<Token> tokens, ASTNode rootNode)
        {
            var stats = new ParseStatistics
            {
                TokenCount = tokens?.Count ?? 0
            };

            if (tokens != null && tokens.Count > 0)
            {
                stats.LineCount = tokens[tokens.Count - 1].Line;
                stats.CharacterCount = tokens.Sum(t => t.Value?.Length ?? 0);
            }

            if (rootNode != null)
            {
                var nodeCount = CountNodes(rootNode);
                stats.NodeCount = nodeCount;

                // 计算各种类型的节点数量
                if (rootNode is ScriptNode scriptNode)
                {
                    stats.NodeDefinitionCount = scriptNode.Nodes?.Count ?? 0;
                }
            }

            return stats;
        }

        /// <summary>
        /// 递归计算AST节点数量
        /// </summary>
        /// <param name="node">根节点</param>
        /// <returns>节点总数</returns>
        private static int CountNodes(ASTNode node)
        {
            int count = 1;
            foreach (var child in node.Children)
            {
                count += CountNodes(child);
            }
            return count;
        }
    }

    /// <summary>
    /// 解析错误严重程度
    /// </summary>
    public enum ParseErrorSeverity
    {
        /// <summary>
        /// 信息
        /// </summary>
        Info,

        /// <summary>
        /// 警告
        /// </summary>
        Warning,

        /// <summary>
        /// 错误
        /// </summary>
        Error,

        /// <summary>
        /// 致命错误
        /// </summary>
        Fatal
    }

    /// <summary>
    /// 解析警告严重程度
    /// </summary>
    public enum ParseWarningSeverity
    {
        /// <summary>
        /// 信息
        /// </summary>
        Info,

        /// <summary>
        /// 警告
        /// </summary>
        Warning,

        /// <summary>
        /// 严重警告
        /// </summary>
        Severe
    }
}