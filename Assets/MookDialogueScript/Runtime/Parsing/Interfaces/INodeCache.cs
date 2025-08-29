using System.Collections.Generic;

namespace MookDialogueScript.Parsing
{
    /// <summary>
    /// AST节点缓存管理接口
    /// 专门负责节点的缓存和优化
    /// </summary>
    public interface INodeCache
    {
        /// <summary>
        /// 获取或创建数值节点
        /// </summary>
        NumberNode GetOrCreateNumberNode(double value, int line, int column);
        
        /// <summary>
        /// 获取或创建布尔节点
        /// </summary>
        BooleanNode GetOrCreateBooleanNode(bool value, int line, int column);
        
        /// <summary>
        /// 获取或创建变量节点
        /// </summary>
        VariableNode GetOrCreateVariableNode(string name, int line, int column);
        
        /// <summary>
        /// 获取或创建标识符节点
        /// </summary>
        IdentifierNode GetOrCreateIdentifierNode(string name, int line, int column);
        
        /// <summary>
        /// 尝试获取缓存的表达式节点
        /// </summary>
        /// <param name="cacheKey">缓存键</param>
        /// <param name="expression">输出表达式</param>
        /// <returns>是否命中缓存</returns>
        bool TryGetCachedExpression(int cacheKey, out ExpressionNode expression);
        
        /// <summary>
        /// 缓存表达式节点
        /// </summary>
        void CacheExpression(int cacheKey, ExpressionNode expression);
        
        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        Dictionary<string, object> GetStatistics();
        
        /// <summary>
        /// 清理所有缓存
        /// </summary>
        void Clear();
        
        /// <summary>
        /// 调整缓存大小
        /// </summary>
        void Trim();
    }
}