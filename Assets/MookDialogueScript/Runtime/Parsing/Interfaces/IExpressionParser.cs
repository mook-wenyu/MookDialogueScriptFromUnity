namespace MookDialogueScript.Parsing
{
    /// <summary>
    /// 表达式解析器接口
    /// 专门负责表达式的解析和优化
    /// 完全依赖注入的TokenBuffer，实现单一数据源原则
    /// </summary>
    public interface IExpressionParser
    {
        /// <summary>
        /// 解析表达式
        /// </summary>
        /// <returns>表达式节点</returns>
        ExpressionNode ParseExpression();

        /// <summary>
        /// 解析带优先级的表达式
        /// </summary>
        /// <param name="minPrecedence">最小优先级</param>
        /// <returns>表达式节点</returns>
        ExpressionNode ParseExpressionWithPrecedence(int minPrecedence);

        /// <summary>
        /// 解析主表达式（字面量、标识符等）
        /// </summary>
        /// <returns>表达式节点</returns>
        ExpressionNode ParsePrimary();

        /// <summary>
        /// 解析后缀链（函数调用、成员访问等）
        /// </summary>
        /// <param name="baseExpr">基础表达式</param>
        /// <returns>表达式节点</returns>
        ExpressionNode ParsePostfixChain(ExpressionNode baseExpr);
    }
}
