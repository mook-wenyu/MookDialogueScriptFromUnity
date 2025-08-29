using System.Collections.Generic;

namespace MookDialogueScript.Parsing
{
    /// <summary>
    /// 表达式解析器接口
    /// 专门负责表达式的解析和优化
    /// </summary>
    public interface IExpressionParser
    {
        /// <summary>
        /// 解析表达式
        /// </summary>
        /// <param name="tokens">Token列表</param>
        /// <param name="startIndex">起始位置</param>
        /// <param name="endIndex">结束位置</param>
        /// <returns>表达式节点和消费的Token数量</returns>
        (ExpressionNode expression, int tokensConsumed) ParseExpression(
            List<Token> tokens, int startIndex, int endIndex = -1);
            
        /// <summary>
        /// 解析带优先级的表达式
        /// </summary>
        (ExpressionNode expression, int tokensConsumed) ParseExpressionWithPrecedence(
            List<Token> tokens, int startIndex, int minPrecedence, int endIndex = -1);
            
        /// <summary>
        /// 解析主表达式（字面量、标识符等）
        /// </summary>
        (ExpressionNode expression, int tokensConsumed) ParsePrimary(
            List<Token> tokens, int startIndex);
            
        /// <summary>
        /// 解析后缀链（函数调用、成员访问等）
        /// </summary>
        (ExpressionNode expression, int tokensConsumed) ParsePostfixChain(
            ExpressionNode baseExpr, List<Token> tokens, int startIndex, int endIndex = -1);
    }
}