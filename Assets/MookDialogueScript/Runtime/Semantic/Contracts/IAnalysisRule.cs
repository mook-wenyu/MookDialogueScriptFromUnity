using System.Collections.Generic;

namespace MookDialogueScript.Semantic.Contracts
{
    /// <summary>
    /// 分析规则接口
    /// 定义可插拔的语义分析规则，遵循开闭原则
    /// </summary>
    public interface IAnalysisRule
    {
        /// <summary>
        /// 规则名称，用于标识和调试
        /// </summary>
        string RuleName { get; }
        
        /// <summary>
        /// 规则优先级，数值越小优先级越高
        /// </summary>
        int Priority { get; }
        
        /// <summary>
        /// 检查规则是否适用于指定的AST节点
        /// </summary>
        /// <param name="node">要检查的AST节点</param>
        /// <returns>如果规则适用返回true，否则返回false</returns>
        bool IsApplicable(ASTNode node);
        
        /// <summary>
        /// 执行规则分析
        /// </summary>
        /// <param name="node">要分析的AST节点</param>
        /// <param name="context">分析上下文</param>
        /// <param name="diagnosticCollector">诊断信息收集器</param>
        void Analyze(ASTNode node, IAnalysisContext context, IDiagnosticCollector diagnosticCollector);
    }
    
    /// <summary>
    /// 复合分析规则接口
    /// 支持包含子规则的复杂规则系统
    /// </summary>
    public interface ICompositeAnalysisRule : IAnalysisRule
    {
        /// <summary>
        /// 子规则列表
        /// </summary>
        IReadOnlyList<IAnalysisRule> SubRules { get; }
        
        /// <summary>
        /// 添加子规则
        /// </summary>
        /// <param name="rule">要添加的子规则</param>
        void AddSubRule(IAnalysisRule rule);
        
        /// <summary>
        /// 移除子规则
        /// </summary>
        /// <param name="rule">要移除的子规则</param>
        /// <returns>如果成功移除返回true，否则返回false</returns>
        bool RemoveSubRule(IAnalysisRule rule);
    }
    
    /// <summary>
    /// 异步分析规则接口
    /// 支持异步分析操作，用于复杂的长时间运行规则
    /// </summary>
    public interface IAsyncAnalysisRule : IAnalysisRule
    {
        /// <summary>
        /// 异步执行规则分析
        /// </summary>
        /// <param name="node">要分析的AST节点</param>
        /// <param name="context">分析上下文</param>
        /// <param name="diagnosticCollector">诊断信息收集器</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>分析任务</returns>
        System.Threading.Tasks.Task AnalyzeAsync(ASTNode node, IAnalysisContext context, IDiagnosticCollector diagnosticCollector, 
            System.Threading.CancellationToken cancellationToken = default);
    }
}