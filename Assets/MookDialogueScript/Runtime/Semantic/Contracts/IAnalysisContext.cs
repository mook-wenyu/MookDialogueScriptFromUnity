using MookDialogueScript.Semantic.TypeSystem;

namespace MookDialogueScript.Semantic.Contracts
{
    /// <summary>
    /// 分析上下文接口
    /// 为分析规则提供执行上下文和共享状态
    /// </summary>
    public interface IAnalysisContext
    {
        /// <summary>
        /// 符号解析器
        /// 提供符号查找和解析功能
        /// </summary>
        ISymbolResolver SymbolResolver { get; }
        
        /// <summary>
        /// 分析选项
        /// 控制分析器的行为和严格性
        /// </summary>
        AnalysisOptions Options { get; }
        
        /// <summary>
        /// 全局节点提供者（可选）
        /// 用于跨文件的节点引用验证
        /// </summary>
        IGlobalNodeProvider NodeProvider { get; }
        
        /// <summary>
        /// 函数管理器（可选）
        /// 提供函数签名和验证支持
        /// </summary>
        FunctionManager FunctionManager { get; }
        
        /// <summary>
        /// 变量管理器（可选）
        /// 提供变量类型和值信息
        /// </summary>
        VariableManager VariableManager { get; }
        
        /// <summary>
        /// 当前正在分析的节点
        /// </summary>
        ASTNode CurrentNode { get; set; }
        
        /// <summary>
        /// 当前节点名称（用于自跳转检测等）
        /// </summary>
        string CurrentNodeName { get; set; }
        
        /// <summary>
        /// 推断表达式类型
        /// </summary>
        /// <param name="expression">要推断的表达式</param>
        /// <returns>推断出的类型信息</returns>
        TypeInfo InferExpressionType(ExpressionNode expression);
        
        /// <summary>
        /// 检查类型兼容性
        /// </summary>
        /// <param name="actualType">实际类型</param>
        /// <param name="expectedType">期望类型</param>
        /// <returns>如果兼容返回true，否则返回false</returns>
        bool IsTypeCompatible(TypeInfo actualType, TypeInfo expectedType);
        
        /// <summary>
        /// 获取相似名称建议
        /// </summary>
        /// <param name="name">要查找的名称</param>
        /// <param name="candidates">候选名称集合</param>
        /// <returns>最相似的名称，如果没有找到返回null</returns>
        string GetSimilarName(string name, System.Collections.Generic.IEnumerable<string> candidates);
        
        /// <summary>
        /// 创建子上下文
        /// 用于嵌套分析场景，如条件语句内部分析
        /// </summary>
        /// <returns>新的子上下文实例</returns>
        IAnalysisContext CreateChildContext();
        
        /// <summary>
        /// 释放子上下文
        /// </summary>
        /// <param name="childContext">要释放的子上下文</param>
        void ReleaseChildContext(IAnalysisContext childContext);
    }
    
    /// <summary>
    /// 可变分析上下文接口
    /// 支持修改上下文状态的分析场景
    /// </summary>
    public interface IMutableAnalysisContext : IAnalysisContext
    {
        /// <summary>
        /// 设置符号解析器
        /// </summary>
        /// <param name="symbolResolver">新的符号解析器</param>
        void SetSymbolResolver(ISymbolResolver symbolResolver);
        
        /// <summary>
        /// 设置分析选项
        /// </summary>
        /// <param name="options">新的分析选项</param>
        void SetOptions(AnalysisOptions options);
        
        /// <summary>
        /// 设置节点提供者
        /// </summary>
        /// <param name="nodeProvider">新的节点提供者</param>
        void SetNodeProvider(IGlobalNodeProvider nodeProvider);
        
        /// <summary>
        /// 设置函数管理器
        /// </summary>
        /// <param name="functionManager">新的函数管理器</param>
        void SetFunctionManager(FunctionManager functionManager);
        
        /// <summary>
        /// 设置变量管理器
        /// </summary>
        /// <param name="variableManager">新的变量管理器</param>
        void SetVariableManager(VariableManager variableManager);
    }
}