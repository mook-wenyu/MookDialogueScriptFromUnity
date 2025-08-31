using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MookDialogueScript.Pooling;
using Newtonsoft.Json;
using UnityEngine;

namespace MookDialogueScript.Incremental
{
    /// <summary>
    /// 增量缓存管理器实现
    /// 统一管理文件变更检测、解析结果缓存、变量声明缓存等子系统
    /// </summary>
    public sealed class IncrementalCacheManager : IDisposable
    {
        private readonly ParseResultCache _parseResultCache;
        private readonly VariableDeclarationCache _variableCache;
        private readonly string _persistentCachePath = Application.persistentDataPath;

        private volatile bool _disposed;
        private volatile bool _initialized;

        // 验证配置
        private const string VALIDATION_LOG_PREFIX = "[对话脚本验证]";

        // 统计信息
        private long _totalRefreshOperations;
        private long _totalWarmupOperations;
        private long _totalCleanupOperations;

        /// <summary>
        /// 解析结果缓存
        /// </summary>
        public ParseResultCache ParseResultCache => _parseResultCache;

        /// <summary>
        /// 变量声明缓存
        /// </summary>
        public VariableDeclarationCache VariableCache => _variableCache;

        /// <summary>
        /// 初始化增量缓存管理器
        /// </summary>
        public IncrementalCacheManager()
        {
            // 初始化子系统
            _parseResultCache = new ParseResultCache();
            _variableCache = new VariableDeclarationCache();
        }

        /// <summary>
        /// 初始化缓存系统
        /// </summary>
        public void Initialize()
        {
            ThrowIfDisposed();

            if (_initialized)
                return;

            try
            {
                // 加载持久化缓存
                LoadCache();

                _initialized = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"初始化缓存系统失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 检查并刷新指定文件的缓存
        /// </summary>
        /// <param name="scriptPaths">文件路径数组</param>
        /// <returns>刷新任务</returns>
        public async Task ValidateScriptsAsync(string[] scriptPaths)
        {
            ThrowIfDisposed();

            var tasks = scriptPaths.Select(ValidateScriptAsync).ToArray();
            await Task.WhenAll(tasks);

            SaveCache();
            Debug.Log($"{VALIDATION_LOG_PREFIX} 验证完成: {scriptPaths.Length} 个文件");
        }

        /// <summary>
        /// 异步验证单个脚本文件
        /// </summary>
        /// <param name="scriptPath">脚本文件路径</param>
        /// <returns>验证错误列表</returns>
        private async Task ValidateScriptAsync(string scriptPath)
        {
            if (!File.Exists(scriptPath))
            {
                MLogger.Error("文件不存在");
                return;
            }

            var content = await File.ReadAllTextAsync(scriptPath);
            // 完整解析验证（尝试使用现有解析器）
            await ValidateFullParsing(scriptPath, content);
        }

        /// <summary>
        /// 保存缓存到持久化存储
        /// </summary>
        /// <returns>保存任务</returns>
        public void SaveCache()
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(_persistentCachePath))
                return;

            try
            {
                var cacheData = _parseResultCache.Cache;
                var str = JsonConvert.SerializeObject(cacheData);
                File.WriteAllText(Path.Combine(_persistentCachePath, "parse_cache.json"), str);
            }
            catch (Exception ex)
            {
                Debug.LogError($"保存持久化缓存失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 从持久化存储加载缓存
        /// </summary>
        /// <returns>加载任务</returns>
        public void LoadCache()
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(_persistentCachePath))
                return;

            try
            {
                var filePath = Path.Combine(_persistentCachePath, "parse_cache.json");
                if (!File.Exists(filePath))
                    return;

                var str = File.ReadAllText(filePath);
                var cacheData = JsonConvert.DeserializeObject<ConcurrentDictionary<string, ParseResult>>(str);
                if (cacheData != null)
                {
                    _parseResultCache.LoadCache(cacheData);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"加载持久化缓存失败: {ex.Message}");
                // 加载失败不应该阻止系统启动
            }
        }

        /// <summary>
        /// 清理文件缓存
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>清理任务</returns>
        public async Task ClearCacheAsync(string filePath)
        {
            var tasks = new List<Task>();

            _parseResultCache.RemoveParseResult(filePath);

            tasks.Add(_variableCache.RemoveFileDeclarationsAsync(filePath));

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// 完整解析验证（尝试使用现有解析器）
        /// </summary>
        private async Task ValidateFullParsing(string filePath, string content)
        {
            // 模拟解析过程
            await Task.Run(async () =>
            {
                // 计算内容哈希
                var contentHash = content.GetHashCode().ToString();

                // 使用对象池获取词法分析器
                using var lexerPooled = GlobalPoolManager.Instance.RentScopedPoolable<Lexers.Lexer>();
                var lexer = lexerPooled.Item;

                // 使用对象池获取解析器  
                using var parserPooled = GlobalPoolManager.Instance.RentScopedPoolable<Parsing.Parser>();
                var parser = parserPooled.Item;

                // 执行词法分析
                var tokens = lexer.Tokenize(content);

                // 执行语法分析
                var scriptNode = parser.Parse(tokens);

                // 创建解析结果
                var parseResult = ParseResult.CreateSuccess(
                    filePath,
                    tokens,
                    scriptNode,
                    contentHash);

                // 基础的节点结构检查
                if (scriptNode is {Nodes: not null})
                {
                    foreach (var nodeDefNode in scriptNode.Nodes)
                    {
                        if (string.IsNullOrEmpty(nodeDefNode.Name))
                        {
                            MLogger.Error($"节点缺少名称: {filePath} Line: {nodeDefNode.Line}");
                        }

                        if (nodeDefNode.Content == null || nodeDefNode.Content.Count == 0)
                        {
                            MLogger.Warning($"节点内容为空: {filePath} Line: {nodeDefNode.Line}");
                        }
                    }
                }

                // 存储到缓存
                _parseResultCache.SetParseResult(filePath, parseResult);

                // 提取并缓存变量声明
                var variableDeclarations = ExtractVariableDeclarations(scriptNode, filePath);
                await _variableCache.SetVariableDeclarationsAsync(filePath, variableDeclarations);

            });

            await Task.CompletedTask; // 保持异步签名
        }

        /// <summary>
        /// 从AST提取变量声明
        /// </summary>
        /// <param name="ast">AST根节点</param>
        /// <param name="filePath">文件路径</param>
        /// <returns>变量声明集合</returns>
        private List<VariableDeclaration> ExtractVariableDeclarations(ASTNode ast, string filePath)
        {
            var rootNode = ast as ScriptNode;
            var declarations = new List<VariableDeclaration>();

            if (rootNode == null)
                return declarations;

            foreach (var node in rootNode.Nodes)
            {
                foreach (var content in node.Content)
                {
                    switch (content)
                    {
                        case VarCommandNode varCommandNode:
                            if (varCommandNode.Operation == "var")
                            {
                                var declaration = VariableDeclaration.Create(
                                    varCommandNode.VariableName,
                                    ValueType.Object,
                                    string.Empty,
                                    filePath);
                                declarations.Add(declaration);
                            }
                            break;
                        case DialogueNode dialogueNode:
                            var cs = ExtractVar<DialogueNode>(dialogueNode, filePath);
                            if (cs is {Count: > 0})
                                declarations.AddRange(cs);
                            break;
                        case ChoiceNode choiceNode:
                            var cs2 = ExtractVar<ChoiceNode>(choiceNode, filePath);
                            if (cs2 is {Count: > 0})
                                declarations.AddRange(cs2);
                            break;
                        case ConditionNode conditionNode:
                            var cs3 = ExtractVar<ConditionNode>(conditionNode, filePath);
                            if (cs3 is {Count: > 0})
                                declarations.AddRange(cs3);
                            break;
                    }
                }
            }
            return declarations;
        }

        private List<VariableDeclaration> ExtractVar<T>(ContentNode content, string filePath)
        {
            var declarations = new List<VariableDeclaration>();
            if (content is not T rootContent)
                return declarations;
            switch (rootContent)
            {
                case DialogueNode dialogueNode:
                {
                    foreach (var cmd in dialogueNode.Content)
                    {
                        switch (cmd)
                        {
                            case VarCommandNode {Operation: "var"} varCmd:
                            {
                                var declaration = VariableDeclaration.Create(
                                    varCmd.VariableName,
                                    ValueType.Object,
                                    string.Empty,
                                    filePath);
                                declarations.Add(declaration);
                                break;
                            }
                            case DialogueNode d:
                                ExtractVar<DialogueNode>(d, filePath);
                                break;
                            case ChoiceNode c:
                                ExtractVar<ChoiceNode>(c, filePath);
                                break;
                            case ConditionNode cond:
                                ExtractVar<ConditionNode>(cond, filePath);
                                break;
                        }
                    }
                    break;
                }
                case ChoiceNode choiceNode:
                {
                    foreach (var cmd in choiceNode.Content)
                    {
                        switch (cmd)
                        {
                            case VarCommandNode {Operation: "var"} varCmd:
                            {
                                var declaration = VariableDeclaration.Create(
                                    varCmd.VariableName,
                                    ValueType.Object,
                                    string.Empty,
                                    filePath);
                                declarations.Add(declaration);
                                break;
                            }
                            case DialogueNode d:
                                ExtractVar<DialogueNode>(d, filePath);
                                break;
                            case ChoiceNode c:
                                ExtractVar<ChoiceNode>(c, filePath);
                                break;
                            case ConditionNode cond:
                                ExtractVar<ConditionNode>(cond, filePath);
                                break;
                        }
                    }
                    break;
                }
                case ConditionNode conditionNode:
                {
                    foreach (var cmd in conditionNode.ThenContent)
                    {
                        switch (cmd)
                        {
                            case VarCommandNode {Operation: "var"} varCmd:
                            {
                                var declaration = VariableDeclaration.Create(
                                    varCmd.VariableName,
                                    ValueType.Object,
                                    string.Empty,
                                    filePath);
                                declarations.Add(declaration);
                                break;
                            }
                            case DialogueNode d:
                                ExtractVar<DialogueNode>(d, filePath);
                                break;
                            case ChoiceNode c:
                                ExtractVar<ChoiceNode>(c, filePath);
                                break;
                            case ConditionNode cond:
                                ExtractVar<ConditionNode>(cond, filePath);
                                break;
                        }
                    }
                    foreach (var cmd in conditionNode.ElseContent)
                    {
                        switch (cmd)
                        {
                            case VarCommandNode {Operation: "var"} varCmd:
                            {
                                var declaration = VariableDeclaration.Create(
                                    varCmd.VariableName,
                                    ValueType.Object,
                                    string.Empty,
                                    filePath);
                                declarations.Add(declaration);
                                break;
                            }
                            case DialogueNode d:
                                ExtractVar<DialogueNode>(d, filePath);
                                break;
                            case ChoiceNode c:
                                ExtractVar<ChoiceNode>(c, filePath);
                                break;
                            case ConditionNode cond:
                                ExtractVar<ConditionNode>(cond, filePath);
                                break;
                        }
                    }
                    foreach (var conds in conditionNode.ElifContents)
                    {
                        foreach (var c in conds.Content)
                        {
                            switch (c)
                            {
                                case VarCommandNode {Operation: "var"} varCmd:
                                {
                                    var declaration = VariableDeclaration.Create(
                                        varCmd.VariableName,
                                        ValueType.Object,
                                        string.Empty,
                                        filePath);
                                    declarations.Add(declaration);
                                    break;
                                }
                                case DialogueNode d:
                                    ExtractVar<DialogueNode>(d, filePath);
                                    break;
                                case ChoiceNode c2:
                                    ExtractVar<ChoiceNode>(c2, filePath);
                                    break;
                                case ConditionNode cond:
                                    ExtractVar<ConditionNode>(cond, filePath);
                                    break;
                            }
                        }
                    }
                    break;
                }
            }

            return declarations;
        }

        /// <summary>
        /// 清空所有缓存
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>清空任务</returns>
        public async Task ClearAllAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            try
            {
                // 清空解析结果缓存
                _parseResultCache.Clear();

                // 清空变量声明缓存
                await _variableCache.ClearAsync(cancellationToken);

                Debug.Log("已清空所有缓存");
            }
            catch (Exception ex)
            {
                Debug.LogError($"清空缓存失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 检查是否已释放资源
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(IncrementalCacheManager));
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                // 释放缓存
                _parseResultCache?.Dispose();
                _variableCache?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.LogError($"释放缓存系统资源失败: {ex.Message}");
            }
        }

    }
}
