using System.Collections.Generic;

namespace MookDialogueScript.Semantic.Contracts
{
    /// <summary>
    /// 诊断信息收集器接口
    /// 负责收集和管理语义分析过程中的诊断信息
    /// </summary>
    public interface IDiagnosticCollector
    {
        /// <summary>
        /// 添加诊断信息
        /// </summary>
        /// <param name="diagnostic">诊断信息</param>
        void AddDiagnostic(Diagnostic diagnostic);
        
        /// <summary>
        /// 添加错误诊断
        /// </summary>
        /// <param name="code">错误代码</param>
        /// <param name="message">错误消息</param>
        /// <param name="line">行号</param>
        /// <param name="column">列号</param>
        /// <param name="suggestion">修复建议</param>
        void AddError(string code, string message, int line = 0, int column = 0, string suggestion = null);
        
        /// <summary>
        /// 添加警告诊断
        /// </summary>
        /// <param name="code">警告代码</param>
        /// <param name="message">警告消息</param>
        /// <param name="line">行号</param>
        /// <param name="column">列号</param>
        /// <param name="suggestion">修复建议</param>
        void AddWarning(string code, string message, int line = 0, int column = 0, string suggestion = null);
        
        /// <summary>
        /// 添加信息诊断
        /// </summary>
        /// <param name="code">信息代码</param>
        /// <param name="message">信息消息</param>
        /// <param name="line">行号</param>
        /// <param name="column">列号</param>
        /// <param name="suggestion">修复建议</param>
        void AddInfo(string code, string message, int line = 0, int column = 0, string suggestion = null);
        
        /// <summary>
        /// 获取所有诊断信息
        /// </summary>
        /// <returns>诊断信息列表</returns>
        IReadOnlyList<Diagnostic> GetDiagnostics();
        
        /// <summary>
        /// 按严重性过滤诊断信息
        /// </summary>
        /// <param name="severity">要过滤的严重性级别</param>
        /// <returns>指定严重性的诊断信息</returns>
        IReadOnlyList<Diagnostic> GetDiagnostics(DiagnosticSeverity severity);
        
        /// <summary>
        /// 检查是否有错误
        /// </summary>
        bool HasErrors { get; }
        
        /// <summary>
        /// 检查是否有警告
        /// </summary>
        bool HasWarnings { get; }
        
        /// <summary>
        /// 错误数量
        /// </summary>
        int ErrorCount { get; }
        
        /// <summary>
        /// 警告数量
        /// </summary>
        int WarningCount { get; }
        
        /// <summary>
        /// 信息数量
        /// </summary>
        int InfoCount { get; }
        
        /// <summary>
        /// 清空所有诊断信息
        /// </summary>
        void Clear();
        
        /// <summary>
        /// 生成语义分析报告
        /// </summary>
        /// <returns>包含所有诊断信息的语义报告</returns>
        SemanticReport GenerateReport();
    }
    
    /// <summary>
    /// 分类诊断收集器接口
    /// 支持按类别收集和管理诊断信息
    /// </summary>
    public interface ICategorizedDiagnosticCollector : IDiagnosticCollector
    {
        /// <summary>
        /// 添加带类别的诊断信息
        /// </summary>
        /// <param name="category">诊断类别</param>
        /// <param name="diagnostic">诊断信息</param>
        void AddDiagnostic(string category, Diagnostic diagnostic);
        
        /// <summary>
        /// 获取指定类别的诊断信息
        /// </summary>
        /// <param name="category">诊断类别</param>
        /// <returns>该类别的诊断信息列表</returns>
        IReadOnlyList<Diagnostic> GetDiagnostics(string category);
        
        /// <summary>
        /// 获取所有诊断类别
        /// </summary>
        /// <returns>类别名称集合</returns>
        IEnumerable<string> GetCategories();
        
        /// <summary>
        /// 清空指定类别的诊断信息
        /// </summary>
        /// <param name="category">要清空的类别</param>
        void Clear(string category);
    }
    
    /// <summary>
    /// 并发安全的诊断收集器接口
    /// 支持多线程环境下的诊断信息收集
    /// </summary>
    public interface IConcurrentDiagnosticCollector : IDiagnosticCollector
    {
        /// <summary>
        /// 合并另一个收集器的诊断信息
        /// </summary>
        /// <param name="other">要合并的诊断收集器</param>
        void Merge(IDiagnosticCollector other);
        
        /// <summary>
        /// 创建当前收集器的快照
        /// </summary>
        /// <returns>包含当前所有诊断信息的快照</returns>
        IDiagnosticCollector CreateSnapshot();
    }
}