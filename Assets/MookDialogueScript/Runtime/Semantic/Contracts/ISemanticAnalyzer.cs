using System;

namespace MookDialogueScript.Semantic.Contracts
{
    /// <summary>
    /// 语义分析器接口
    /// 定义语义分析的核心契约，支持并发分析和增量分析
    /// </summary>
    public interface ISemanticAnalyzer : IDisposable
    {
        /// <summary>
        /// 分析脚本的语义结构
        /// </summary>
        /// <param name="script">要分析的脚本节点</param>
        /// <param name="variableManager">变量管理器（可选）</param>
        /// <param name="functionManager">函数管理器（可选）</param>
        /// <returns>语义分析报告</returns>
        SemanticReport Analyze(ScriptNode script, VariableManager variableManager = null, FunctionManager functionManager = null);
        
        /// <summary>
        /// 清空内部缓存
        /// </summary>
        void ClearCache();
        
        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        /// <returns>缓存项数量和内存使用量估算</returns>
        (int CachedReports, int TotalMemoryUsage) GetCacheStats();
        
        /// <summary>
        /// 配置分析选项
        /// </summary>
        AnalysisOptions Options { get; set; }
        
        /// <summary>
        /// 全局节点提供者（可选）
        /// 用于跨文件的节点引用检查
        /// </summary>
        IGlobalNodeProvider NodeProvider { get; set; }
    }
}