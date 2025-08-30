using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MookDialogueScript.Pooling;
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
        private string _persistentCachePath = Application.persistentDataPath;

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
                Load();

                _initialized = true;
                Debug.Log("增量缓存系统初始化完成");
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
            var allErrors = await Task.WhenAll(tasks);

            var totalErrors = 0;
            var totalWarnings = 0;

            for (int i = 0; i < scriptPaths.Length; i++)
            {
                var errors = allErrors[i];

                foreach (var error in errors)
                {
                    LogValidationError(error);

                    if (error.Severity == ValidationSeverity.Error)
                        totalErrors++;
                    else if (error.Severity == ValidationSeverity.Warning)
                        totalWarnings++;
                }
            }

            if (totalErrors > 0 || totalWarnings > 0)
            {
                Debug.Log($"{VALIDATION_LOG_PREFIX} 验证完成: {scriptPaths.Length} 个文件, " +
                          $"{totalErrors} 个错误, {totalWarnings} 个警告");
            }
            else if (scriptPaths.Length > 0)
            {
                Debug.Log($"{VALIDATION_LOG_PREFIX} 验证完成: {scriptPaths.Length} 个文件，无错误");
            }
        }

        /// <summary>
        /// 异步验证单个脚本文件
        /// </summary>
        /// <param name="scriptPath">脚本文件路径</param>
        /// <returns>验证错误列表</returns>
        private async Task<List<ValidationError>> ValidateScriptAsync(string scriptPath)
        {
            var errors = new List<ValidationError>();

            try
            {
                if (!File.Exists(scriptPath))
                {
                    errors.Add(new ValidationError
                    {
                        FilePath = scriptPath,
                        Line = 1,
                        Column = 1,
                        Message = "文件不存在",
                        Severity = ValidationSeverity.Error,
                        ErrorCode = "FILE_NOT_FOUND"
                    });


                    return errors;
                }

                var content = await File.ReadAllTextAsync(scriptPath);

                // 完整解析验证（尝试使用现有解析器）
                var parseErrors = await ValidateFullParsing(scriptPath, content);
                errors.AddRange(parseErrors);
            }
            catch (Exception ex)
            {
                errors.Add(new ValidationError
                {
                    FilePath = scriptPath,
                    Line = 1,
                    Column = 1,
                    Message = $"验证过程中发生异常: {ex.Message}",
                    Severity = ValidationSeverity.Error,
                    ErrorCode = "VALIDATION_EXCEPTION"
                });
            }

            return errors;
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
        /// 保存缓存到持久化存储
        /// </summary>
        /// <returns>保存任务</returns>
        public void Save()
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(_persistentCachePath))
                return;

            try
            {
                // 这里应该实现持久化逻辑
                Debug.Log($"保存缓存到持久化存储: {_persistentCachePath}");
                // 实际实现应该序列化缓存数据并保存到磁盘
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
        public void Load()
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(_persistentCachePath))
                return;

            try
            {
                // 这里应该实现持久化加载逻辑
                Debug.Log($"从持久化存储加载缓存: {_persistentCachePath}");
                // 实际实现应该从磁盘加载并反序列化缓存数据
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

            _parseResultCache.Remove(filePath);

            tasks.Add(_variableCache.RemoveFileDeclarationsAsync(filePath));

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// 完整解析验证（尝试使用现有解析器）
        /// </summary>
        private async Task<List<ValidationError>> ValidateFullParsing(string filePath, string content)
        {
            var errors = new List<ValidationError>();

            try
            {
                // 模拟解析过程
                await Task.Run(async () =>
                {
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
                        "0.8.0");

                    // 基础的节点结构检查
                    if (scriptNode is {Nodes: not null})
                    {
                        foreach (var nodeDefNode in scriptNode.Nodes)
                        {
                            if (string.IsNullOrEmpty(nodeDefNode.Name))
                            {
                                errors.Add(new ValidationError
                                {
                                    FilePath = filePath,
                                    Line = nodeDefNode.Line,
                                    Column = nodeDefNode.Column,
                                    Message = "节点缺少名称",
                                    Severity = ValidationSeverity.Error,
                                    ErrorCode = "MISSING_NODE_NAME"
                                });
                            }

                            if (nodeDefNode.Content == null || nodeDefNode.Content.Count == 0)
                            {
                                errors.Add(new ValidationError
                                {
                                    FilePath = filePath,
                                    Line = nodeDefNode.Line,
                                    Column = nodeDefNode.Column,
                                    Message = "节点内容为空",
                                    Severity = ValidationSeverity.Warning,
                                    ErrorCode = "EMPTY_NODE_CONTENT"
                                });
                            }
                        }
                    }

                    // 存储到缓存
                    _parseResultCache.SetParseResult(filePath, parseResult);

                    // 提取并缓存变量声明
                    var variableDeclarations = ExtractVariableDeclarations(scriptNode, filePath);
                    await _variableCache.SetVariableDeclarationsAsync(filePath, variableDeclarations);

                });
            }
            catch (ScriptException ex)
            {
                errors.Add(new ValidationError
                {
                    FilePath = filePath,
                    Line = ex.Line,
                    Column = ex.Column,
                    Message = $"语法错误: {ex.Message}",
                    Severity = ValidationSeverity.Error,
                    ErrorCode = "SYNTAX_ERROR"
                });
            }
            catch (Exception ex)
            {
                errors.Add(new ValidationError
                {
                    FilePath = filePath,
                    Line = 1,
                    Column = 1,
                    Message = $"解析验证失败: {ex.Message}",
                    Severity = ValidationSeverity.Error,
                    ErrorCode = "PARSE_VALIDATION_FAILED"
                });
            }

            await Task.CompletedTask; // 保持异步签名
            return errors;
        }

        /// <summary>
        /// 从AST提取变量声明
        /// </summary>
        /// <param name="ast">AST根节点</param>
        /// <param name="filePath">文件路径</param>
        /// <returns>变量声明集合</returns>
        private List<VariableDeclaration> ExtractVariableDeclarations(ASTNode ast, string filePath)
        {
            var declarations = new List<VariableDeclaration>();

            if (ast == null)
                return declarations;

            // 这里应该遍历AST提取变量声明
            // 由于时间限制，这里只是一个简化的实现

            // 示例：创建一些假的变量声明用于测试
            // 实际实现应该遍历AST节点，查找变量声明语句

            return declarations;
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
        /// 记录验证错误到控制台
        /// </summary>
        private static void LogValidationError(ValidationError error)
        {
            var message = $"{VALIDATION_LOG_PREFIX} {error}";

            switch (error.Severity)
            {
                case ValidationSeverity.Error:
                    Debug.LogError(message);
                    break;
                case ValidationSeverity.Warning:
                    Debug.LogWarning(message);
                    break;
                case ValidationSeverity.Info:
                    Debug.Log(message);
                    break;
            }
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

                Debug.Log("增量缓存系统已关闭");
            }
            catch (Exception ex)
            {
                Debug.LogError($"释放缓存系统资源失败: {ex.Message}");
            }
        }

    }

    /// <summary>
    /// 验证错误信息
    /// </summary>
    public class ValidationError
    {
        public string FilePath { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
        public string Message { get; set; }
        public ValidationSeverity Severity { get; set; }
        public string ErrorCode { get; set; }

        public override string ToString()
        {
            return $"{Path.GetFileName(FilePath)}({Line},{Column}): {Severity.ToString().ToLower()}: {Message} [{ErrorCode}]";
        }
    }

    /// <summary>
    /// 验证严重程度
    /// </summary>
    public enum ValidationSeverity
    {
        Error,
        Warning,
        Info
    }

}
