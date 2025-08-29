using System.Collections.Generic;

namespace MookDialogueScript.Parsing
{
    /// <summary>
    /// 语法解析器主接口
    /// 定义解析器的核心契约，支持依赖注入和测试
    /// </summary>
    public interface IParser
    {
        /// <summary>
        /// 解析Token列表生成AST
        /// </summary>
        /// <param name="tokens">输入Token列表</param>
        /// <returns>语法树根节点</returns>
        ScriptNode Parse(List<Token> tokens);
        
        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        Dictionary<string, object> GetCacheStatistics();
        
        /// <summary>
        /// 清理缓存
        /// </summary>
        void ClearCache();
        
        /// <summary>
        /// 重置解析器状态
        /// </summary>
        void Reset();
    }
}