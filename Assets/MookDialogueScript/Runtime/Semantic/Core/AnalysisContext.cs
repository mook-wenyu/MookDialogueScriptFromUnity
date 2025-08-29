using System.Collections.Generic;
using MookDialogueScript.Semantic.Contracts;
using MookDialogueScript.Semantic.TypeSystem;

namespace MookDialogueScript.Semantic.Core
{
    /// <summary>
    /// 分析上下文实现
    /// 为分析规则提供执行上下文和共享状态
    /// </summary>
    public class AnalysisContext : IAnalysisContext
    {
        /// <summary>
        /// 符号解析器
        /// </summary>
        public ISymbolResolver SymbolResolver { get; private set; }
        
        /// <summary>
        /// 分析选项
        /// </summary>
        public AnalysisOptions Options { get; private set; }
        
        /// <summary>
        /// 全局节点提供者
        /// </summary>
        public IGlobalNodeProvider NodeProvider { get; private set; }
        
        /// <summary>
        /// 函数管理器
        /// </summary>
        public FunctionManager FunctionManager { get; private set; }
        
        /// <summary>
        /// 变量管理器
        /// </summary>
        public VariableManager VariableManager { get; private set; }
        
        /// <summary>
        /// 当前正在分析的节点
        /// </summary>
        public ASTNode CurrentNode { get; set; }
        
        /// <summary>
        /// 当前节点名称
        /// </summary>
        public string CurrentNodeName { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public AnalysisContext(
            ISymbolResolver symbolResolver,
            AnalysisOptions options,
            IGlobalNodeProvider nodeProvider = null,
            FunctionManager functionManager = null,
            VariableManager variableManager = null)
        {
            SymbolResolver = symbolResolver ?? throw new System.ArgumentNullException(nameof(symbolResolver));
            Options = options ?? new AnalysisOptions();
            NodeProvider = nodeProvider;
            FunctionManager = functionManager;
            VariableManager = variableManager;
        }

        /// <summary>
        /// 推断表达式类型
        /// </summary>
        public TypeInfo InferExpressionType(ExpressionNode expression)
        {
            if (expression == null)
                return TypeInfo.Error;
                
            return TypeInference.InferType(expression, SymbolResolver);
        }

        /// <summary>
        /// 检查类型兼容性
        /// </summary>
        public bool IsTypeCompatible(TypeInfo actualType, TypeInfo expectedType)
        {
            return TypeCompatibility.IsCompatible(actualType, expectedType);
        }

        /// <summary>
        /// 获取相似名称建议
        /// </summary>
        public string GetSimilarName(string name, IEnumerable<string> candidates)
        {
            if (string.IsNullOrEmpty(name) || candidates == null)
                return null;
                
            return Utils.GetMostSimilarString(name, candidates);
        }

        /// <summary>
        /// 创建子上下文
        /// </summary>
        public IAnalysisContext CreateChildContext()
        {
            // 创建一个新的上下文，共享大部分状态但可以独立修改
            var childContext = new AnalysisContext(
                SymbolResolver,
                Options,
                NodeProvider,
                FunctionManager,
                VariableManager)
            {
                CurrentNode = CurrentNode,
                CurrentNodeName = CurrentNodeName
            };
            
            return childContext;
        }

        /// <summary>
        /// 释放子上下文
        /// </summary>
        public void ReleaseChildContext(IAnalysisContext childContext)
        {
            // 简化实现，实际可以在这里做清理工作
            if (childContext is AnalysisContext child)
            {
                child.CurrentNode = null;
                child.CurrentNodeName = null;
            }
        }
    }
}