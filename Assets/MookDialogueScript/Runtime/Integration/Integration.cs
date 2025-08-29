using System;
using System.Collections.Generic;
using System.Linq;
using MookDialogueScript.Parsing;
using MookDialogueScript.Pooling;
using MookDialogueScript.Lexers;

namespace MookDialogueScript
{
    /// <summary>
    /// 重构架构集成器
    /// 提供统一的API来集成重构后的Parser和池系统
    /// 保持与原有代码的兼容性
    /// </summary>
    public static class Integration
    {
        #region 单例池实例
        private static ConcurrentLexerPool _lexerPool;
        private static readonly object _lexerPoolLock = new object();

        /// <summary>
        /// 全局Lexer池 - 使用新的高性能并发池
        /// </summary>
        public static ConcurrentLexerPool LexerPool
        {
            get
            {
                if (_lexerPool == null)
                {
                    lock (_lexerPoolLock)
                    {
                        if (_lexerPool == null)
                            _lexerPool = new ConcurrentLexerPool();
                    }
                }
                return _lexerPool;
            }
        }

        /// <summary>
        /// 创建Parser实例 - 使用重构后的解析器架构
        /// </summary>
        public static Parsing.Parser CreateParser()
        {
            return new Parsing.Parser();
        }
        #endregion

        #region 高级API
        /// <summary>
        /// 完整的脚本解析流程
        /// 从源代码到AST的一站式处理
        /// </summary>
        /// <param name="source">源代码</param>
        /// <returns>解析后的AST</returns>
        public static ScriptNode ParseScript(string source)
        {
            if (string.IsNullOrEmpty(source))
                return new ScriptNode(new List<NodeDefinitionNode>());

            // 词法分析 - 使用新的并发池
            using var pooledLexer = LexerPool.RentScoped(source);
            var tokens = pooledLexer.Lexer.Tokenize();
            
            // 语法分析 - 使用重构后的解析器
            using var parser = CreateParser();
            return parser.Parse(tokens);
        }

        /// <summary>
        /// 批量处理多个脚本
        /// </summary>
        /// <param name="sources">源代码数组</param>
        /// <returns>解析后的AST数组</returns>
        public static ScriptNode[] ParseScripts(string[] sources)
        {
            if (sources == null || sources.Length == 0)
                return new ScriptNode[0];

            var results = new ScriptNode[sources.Length];
            
            // 批量处理 - 并发处理多个脚本
            System.Threading.Tasks.Parallel.For(0, sources.Length, i =>
            {
                results[i] = ParseScript(sources[i]);
            });
            
            return results;
        }

        /// <summary>
        /// 异步处理脚本（如果需要）
        /// </summary>
        public static System.Threading.Tasks.Task<ScriptNode> ParseScriptAsync(string source)
        {
            return System.Threading.Tasks.Task.Run(() => ParseScript(source));
        }

        /// <summary>
        /// 获取性能统计信息
        /// </summary>
        public static IntegrationStatistics GetPerformanceStatistics()
        {
            return new IntegrationStatistics
            {
                LexerPoolStats = ConvertToConcurrentPoolStatistics(LexerPool.GetStatistics()),
                ParserPoolStats = CreateEmptyParserStats(), // Parser现在不使用池模式
                Timestamp = System.DateTime.UtcNow
            };
        }

        /// <summary>
        /// 将并发池统计转换为通用统计格式
        /// </summary>
        private static PoolStatistics ConvertToConcurrentPoolStatistics(ConcurrentLexerPoolStatistics concurrentStats)
        {
            return new PoolStatistics
            {
                TypeName = "ConcurrentLexerPool",
                PoolName = "Global",
                PoolSize = concurrentStats.PoolSize,
                ActiveCount = concurrentStats.ActiveCount,
                PeakActiveCount = concurrentStats.PeakActiveCount,
                TotalCreated = concurrentStats.TotalCreated,
                TotalBorrowed = concurrentStats.TotalBorrowed,
                TotalReturned = concurrentStats.TotalReturned,
                TotalRecycled = concurrentStats.TotalRecycled,
                HitRate = concurrentStats.HitRate,
                CreatedAt = System.DateTime.UtcNow,
                LastAccessAt = System.DateTime.UtcNow
            };
        }

        /// <summary>
        /// 创建空的Parser统计信息（因为Parser现在不使用池）
        /// </summary>
        private static PoolStatistics CreateEmptyParserStats()
        {
            return new PoolStatistics
            {
                TypeName = "Parser",
                PoolName = "DirectCreate",
                PoolSize = 0,
                ActiveCount = 0,
                PeakActiveCount = 0,
                TotalCreated = 0,
                TotalBorrowed = 0,
                TotalReturned = 0,
                TotalRecycled = 0,
                HitRate = 1.0f, // 100%，因为都是直接创建
                CreatedAt = System.DateTime.UtcNow,
                LastAccessAt = System.DateTime.UtcNow
            };
        }
        #endregion

        #region 兼容性支持
        /// <summary>
        /// 创建兼容原有API的Parser实例
        /// 内部使用池化管理
        /// </summary>
        public static CompatibilityParser CreateCompatibleParser(List<Token> tokens)
        {
            return new CompatibilityParser(tokens);
        }

        /// <summary>
        /// 创建兼容原有API的Lexer实例
        /// 注意：使用完毕后需要调用ReturnLexer归还到池中
        /// </summary>
        public static MookDialogueScript.Lexers.Lexer CreateCompatibleLexer()
        {
            return LexerPool.Rent();
        }

        /// <summary>
        /// 归还Lexer实例到池中
        /// </summary>
        /// <param name="lexer">要归还的Lexer实例</param>
        public static void ReturnLexer(MookDialogueScript.Lexers.Lexer lexer)
        {
            if (lexer != null)
            {
                LexerPool.Return(lexer);
            }
        }
        #endregion

        #region 配置和管理
        /// <summary>
        /// 配置池参数
        /// </summary>
        public static void ConfigurePools(ConcurrentLexerPoolOptions lexerOptions = null, PoolOptions parserOptions = null)
        {
            lock (_lexerPoolLock)
            {
                // 如果提供了Lexer配置，重新创建池
                if (lexerOptions != null)
                {
                    _lexerPool?.Dispose();
                    _lexerPool = new ConcurrentLexerPool(lexerOptions);
                }
                
                // parserOptions 现在被忽略，因为Parser不再使用池模式
                if (parserOptions != null)
                {
                    MLogger.Info("Parser配置选项被忽略，因为Parser现在使用直接创建模式");
                }
            }
        }

        /// <summary>
        /// 清理所有池
        /// </summary>
        public static void ClearAllPools()
        {
            _lexerPool?.Clear(); // 清理Lexer池
            // Parser不再使用池，无需清理
        }

        /// <summary>
        /// 输出完整的性能报告
        /// </summary>
        public static void PrintPerformanceReport()
        {
            var stats = GetPerformanceStatistics();
            var concurrentStats = LexerPool.GetStatistics();
            
            MLogger.Info("=== 重构架构性能报告 ===");
            MLogger.Info($"统计时间: {stats.Timestamp:yyyy-MM-dd HH:mm:ss}");
            MLogger.Info($"高性能Lexer池: {concurrentStats}");
            MLogger.Info($"Parser模式: 直接创建，无池化");
            MLogger.Info($"并发处理能力: 支持");
            MLogger.Info($"线程本地缓存: {(LexerPool.GetStatistics().ThreadCount > 1 ? "启用" : "禁用")}");
            MLogger.Info("========================");
        }
        #endregion
    }

    /// <summary>
    /// 集成统计信息
    /// </summary>
    public struct IntegrationStatistics
    {
        public PoolStatistics LexerPoolStats { get; set; }
        public PoolStatistics ParserPoolStats { get; set; }
        public System.DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// 兼容性Parser包装器
    /// 提供与原有Parser类相同的API，内部使用重构的架构
    /// </summary>
    public class CompatibilityParser
    {
        private readonly IParser _refactoredParser;
        private readonly List<Token> _tokens;

        public CompatibilityParser(List<Token> tokens)
        {
            _tokens = tokens;
            _refactoredParser = Integration.CreateParser();
        }

        /// <summary>
        /// 解析脚本（兼容原有API）
        /// </summary>
        public ScriptNode Parse()
        {
            return _refactoredParser.Parse(_tokens);
        }

        /// <summary>
        /// 获取缓存统计信息（兼容原有API）
        /// </summary>
        public Dictionary<string, object> GetCacheStatistics()
        {
            return _refactoredParser.GetCacheStatistics();
        }

        /// <summary>
        /// 清理AST缓存（兼容原有API）
        /// </summary>
        public void ClearASTCache()
        {
            _refactoredParser.ClearCache();
        }

        /// <summary>
        /// 释放资源（Parser现在使用直接创建模式，实现IDisposable）
        /// </summary>
        ~CompatibilityParser()
        {
            if (_refactoredParser is IDisposable disposableParser)
            {
                disposableParser.Dispose();
            }
        }
    }
}