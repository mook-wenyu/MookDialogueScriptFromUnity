using System;
using System.Collections.Generic;
using MookDialogueScript.Semantic.Contracts;
using MookDialogueScript.Semantic.TypeSystem;
using MookDialogueScript.Semantic.Symbols;
using MookDialogueScript.Semantic.Diagnostics;

namespace MookDialogueScript.Semantic.Core
{
    /// <summary>
    /// 组合式语义分析器
    /// 使用组合模式实现的可扩展语义分析器
    /// </summary>
    public class CompositeSemanticAnalyzer : ISemanticAnalyzer
    {
        private readonly Dictionary<int, SemanticReport> _reportCache = new Dictionary<int, SemanticReport>();
        private readonly ISymbolTableFactory _symbolTableFactory;
        private readonly List<IAnalysisRule> _rules;
        
        /// <summary>
        /// 配置分析选项
        /// </summary>
        public AnalysisOptions Options { get; set; }
        
        /// <summary>
        /// 全局节点提供者
        /// </summary>
        public IGlobalNodeProvider NodeProvider { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public CompositeSemanticAnalyzer(
            AnalysisOptions options = null, 
            IGlobalNodeProvider nodeProvider = null,
            ISymbolTableFactory symbolTableFactory = null)
        {
            Options = options ?? new AnalysisOptions();
            NodeProvider = nodeProvider;
            _symbolTableFactory = symbolTableFactory ?? new DefaultSymbolTableFactory();
            _rules = new List<IAnalysisRule>();
            InitializeDefaultRules();
        }

        /// <summary>
        /// 初始化默认规则
        /// </summary>
        private void InitializeDefaultRules()
        {
            // 这里可以添加默认的分析规则
            // 由于篇幅限制，这里留空，实际分析在Analyze方法中实现
        }

        /// <summary>
        /// 分析脚本的语义结构
        /// </summary>
        public SemanticReport Analyze(ScriptNode script, VariableManager variableManager = null, FunctionManager functionManager = null)
        {
            if (script == null)
            {
                var errorReport = new SemanticReport();
                errorReport.AddError("SEM000", "脚本为空");
                return errorReport;
            }

            // 计算缓存键
            var cacheKey = ComputeCacheKey(script, variableManager, functionManager);
            if (_reportCache.TryGetValue(cacheKey, out var cachedReport))
            {
                return cachedReport;
            }

            // 创建分析上下文
            var context = CreateAnalysisContext(variableManager, functionManager);
            var diagnosticCollector = new DiagnosticCollector();
            
            try
            {
                // 执行分析
                PerformAnalysis(script, context, diagnosticCollector);
                
                // 生成报告
                var report = diagnosticCollector.GenerateReport();
                
                // 缓存结果
                _reportCache[cacheKey] = report;
                
                return report;
            }
            catch (Exception ex)
            {
                diagnosticCollector.AddError("SEM999", $"分析过程中出错: {ex.Message}");
                return diagnosticCollector.GenerateReport();
            }
        }

        /// <summary>
        /// 执行分析逻辑（简化实现）
        /// </summary>
        private void PerformAnalysis(ScriptNode script, IAnalysisContext context, IDiagnosticCollector diagnosticCollector)
        {
            // 简化的分析实现，主要作为架构演示
            // 实际项目中可以将原有的分析逻辑拆分为多个规则
            
            if (context.SymbolResolver is SymbolResolver symbolResolver)
            {
                var scopeManager = symbolResolver.GetScopeManager();
                
                // 第一遍：收集节点名
                foreach (var node in script.Nodes)
                {
                    if (!string.IsNullOrEmpty(node.Name))
                    {
                        if (scopeManager.GlobalScope.NodeExists(node.Name))
                        {
                            diagnosticCollector.AddWarning("SEM070", $"节点名 '{node.Name}' 重复定义", node.Line, node.Column);
                        }
                        else
                        {
                            scopeManager.GlobalScope.AddNodeName(node.Name);
                        }
                    }
                }
                
                // 第二遍：分析节点内容
                foreach (var node in script.Nodes)
                {
                    context.CurrentNode = node;
                    context.CurrentNodeName = node.Name;
                    AnalyzeNode(node, context, diagnosticCollector);
                }
            }
        }

        /// <summary>
        /// 分析单个节点（简化实现）
        /// </summary>
        private void AnalyzeNode(ASTNode node, IAnalysisContext context, IDiagnosticCollector diagnosticCollector)
        {
            // 简化实现，只做基本的类型检查
            // 实际项目中可以使用规则引擎来处理不同类型的节点
            
            if (node is NodeDefinitionNode nodeDefNode)
            {
                if (context.SymbolResolver is SymbolResolver symbolResolver)
                {
                    symbolResolver.PushScope();
                    try
                    {
                        foreach (var content in nodeDefNode.Content)
                        {
                            AnalyzeContentNode(content, context, diagnosticCollector);
                        }
                    }
                    finally
                    {
                        symbolResolver.PopScope();
                    }
                }
            }
        }

        /// <summary>
        /// 分析内容节点（简化实现）
        /// </summary>
        private void AnalyzeContentNode(ContentNode content, IAnalysisContext context, IDiagnosticCollector diagnosticCollector)
        {
            // 简化实现，只处理基本的节点类型
            switch (content)
            {
                case VarCommandNode varCmd:
                    AnalyzeVarCommand(varCmd, context, diagnosticCollector);
                    break;
                case JumpCommandNode jumpCmd:
                    AnalyzeJumpCommand(jumpCmd, context, diagnosticCollector);
                    break;
                // 可以继续添加其他节点类型的处理
            }
        }

        private void AnalyzeVarCommand(VarCommandNode varCmd, IAnalysisContext context, IDiagnosticCollector diagnosticCollector)
        {
            if (varCmd.Value != null)
            {
                var valueType = context.InferExpressionType(varCmd.Value);
                context.SymbolResolver.DefineVariable(varCmd.VariableName, valueType);
            }
        }

        private void AnalyzeJumpCommand(JumpCommandNode jumpCmd, IAnalysisContext context, IDiagnosticCollector diagnosticCollector)
        {
            if (!context.SymbolResolver.NodeExists(jumpCmd.TargetNode))
            {
                var suggestion = context.GetSimilarName(jumpCmd.TargetNode, 
                    context.SymbolResolver.GetDefinedSymbols(Contracts.SymbolType.Node));
                var suggestionText = suggestion != null ? $"，你是否想要跳转到 '{suggestion}'？" : "";
                diagnosticCollector.AddError("SEM060", 
                    $"跳转目标节点 '{jumpCmd.TargetNode}' 不存在{suggestionText}", 
                    jumpCmd.Line, jumpCmd.Column, suggestion);
            }
        }

        /// <summary>
        /// 创建分析上下文
        /// </summary>
        private IAnalysisContext CreateAnalysisContext(VariableManager variableManager, FunctionManager functionManager)
        {
            var globalSymbolTable = _symbolTableFactory.CreateGlobalSymbolTable(variableManager, functionManager);
            var symbolResolver = _symbolTableFactory.CreateSymbolResolver(globalSymbolTable);
            
            return new AnalysisContext(symbolResolver, Options, NodeProvider, functionManager, variableManager);
        }

        /// <summary>
        /// 计算缓存键
        /// </summary>
        private int ComputeCacheKey(ScriptNode script, VariableManager variableManager, FunctionManager functionManager)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (script?.GetHashCode() ?? 0);
                hash = hash * 31 + (variableManager?.GetHashCode() ?? 0);
                hash = hash * 31 + (functionManager?.GetHashCode() ?? 0);
                return hash;
            }
        }

        /// <summary>
        /// 清空内部缓存
        /// </summary>
        public void ClearCache()
        {
            _reportCache.Clear();
        }

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        public (int CachedReports, int TotalMemoryUsage) GetCacheStats()
        {
            var memoryUsage = _reportCache.Count * 1000; // 粗略估算
            return (_reportCache.Count, memoryUsage);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            ClearCache();
        }
    }
}