using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MookDialogueScript
{
    /// <summary>
    /// 语义分析配置选项
    /// </summary>
    public class AnalysisOptions
    {
        /// <summary>
        /// 大小写不一致警告（默认开启）
        /// </summary>
        public bool CaseInconsistencyAsWarning { get; set; } = true;
        
        /// <summary>
        /// 数值当布尔使用的处理方式（默认警告）
        /// </summary>
        public DiagnosticSeverity NumberAsBooleanSeverity { get; set; } = DiagnosticSeverity.Warning;
        
        /// <summary>
        /// 字符串拼接非法类型的处理方式（默认警告）
        /// </summary>
        public DiagnosticSeverity InvalidStringConcatSeverity { get; set; } = DiagnosticSeverity.Warning;
        
        /// <summary>
        /// 未知类型参与运算的处理方式（默认警告）
        /// </summary>
        public DiagnosticSeverity UnknownTypeOperationSeverity { get; set; } = DiagnosticSeverity.Warning;
        
        /// <summary>
        /// 自跳转警告（默认关闭）
        /// </summary>
        public bool SelfJumpAsWarning { get; set; } = false;
        
        /// <summary>
        /// 标签严格性检查（默认关闭）
        /// </summary>
        public bool StrictTagValidation { get; set; } = false;
        
        /// <summary>
        /// 启用函数签名推断（默认开启）
        /// </summary>
        public bool EnableSignatureInference { get; set; } = true;
    }

    /// <summary>
    /// 语义诊断严重性级别
    /// </summary>
    public enum DiagnosticSeverity
    {
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// 语义诊断信息
    /// </summary>
    public class Diagnostic
    {
        /// <summary>
        /// 诊断代码
        /// </summary>
        public string Code { get; set; }
        
        /// <summary>
        /// 严重性级别
        /// </summary>
        public DiagnosticSeverity Severity { get; set; }
        
        /// <summary>
        /// 诊断消息
        /// </summary>
        public string Message { get; set; }
        
        /// <summary>
        /// 行号
        /// </summary>
        public int Line { get; set; }
        
        /// <summary>
        /// 列号
        /// </summary>
        public int Column { get; set; }
        
        /// <summary>
        /// 修复建议
        /// </summary>
        public string Suggestion { get; set; }

        public Diagnostic(string code, DiagnosticSeverity severity, string message, int line = 0, int column = 0, string suggestion = null)
        {
            Code = code;
            Severity = severity;
            Message = message;
            Line = line;
            Column = column;
            Suggestion = suggestion;
        }
    }

    /// <summary>
    /// 语义分析报告
    /// </summary>
    public class SemanticReport
    {
        /// <summary>
        /// 所有诊断信息
        /// </summary>
        public List<Diagnostic> Diagnostics { get; set; } = new List<Diagnostic>();
        
        /// <summary>
        /// 是否存在错误
        /// </summary>
        public bool HasErrors => Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
        
        /// <summary>
        /// 是否存在警告
        /// </summary>
        public bool HasWarnings => Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Warning);
        
        /// <summary>
        /// 错误数量
        /// </summary>
        public int ErrorCount => Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
        
        /// <summary>
        /// 警告数量
        /// </summary>
        public int WarningCount => Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning);
        
        /// <summary>
        /// 信息数量
        /// </summary>
        public int InfoCount => Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Info);

        /// <summary>
        /// 错误列表
        /// </summary>
        public List<Diagnostic> Errors => Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

        /// <summary>
        /// 添加诊断信息
        /// </summary>
        public void AddDiagnostic(Diagnostic diagnostic)
        {
            Diagnostics.Add(diagnostic);
        }

        /// <summary>
        /// 添加错误
        /// </summary>
        public void AddError(string code, string message, int line = 0, int column = 0, string suggestion = null)
        {
            AddDiagnostic(new Diagnostic(code, DiagnosticSeverity.Error, message, line, column, suggestion));
        }

        /// <summary>
        /// 添加警告
        /// </summary>
        public void AddWarning(string code, string message, int line = 0, int column = 0, string suggestion = null)
        {
            AddDiagnostic(new Diagnostic(code, DiagnosticSeverity.Warning, message, line, column, suggestion));
        }

        /// <summary>
        /// 添加信息
        /// </summary>
        public void AddInfo(string code, string message, int line = 0, int column = 0, string suggestion = null)
        {
            AddDiagnostic(new Diagnostic(code, DiagnosticSeverity.Info, message, line, column, suggestion));
        }
    }

    /// <summary>
    /// 全局节点查找提供者接口
    /// </summary>
    public interface IGlobalNodeProvider
    {
        /// <summary>
        /// 查找节点是否存在
        /// </summary>
        bool NodeExists(string nodeName);
        
        /// <summary>
        /// 获取相似节点名建议
        /// </summary>
        string GetSimilarNodeName(string nodeName);
        
        /// <summary>
        /// 获取所有节点名（可选，用于大小写一致性检查等）
        /// </summary>
        /// <returns>所有节点名的枚举，如果不支持则返回null</returns>
        IEnumerable<string> GetAllNodeNames();
    }

    /// <summary>
    /// 函数信息
    /// </summary>
    public class FunctionInfo
    {
        public string Name { get; }
        public ValueType ReturnType { get; }
        public List<ValueType> ParameterTypes { get; }
        public int MinParameters { get; }
        public int MaxParameters { get; }

        public FunctionInfo(string name, ValueType returnType, List<ValueType> parameterTypes, int minParameters = -1, int maxParameters = -1)
        {
            Name = name;
            ReturnType = returnType;
            ParameterTypes = parameterTypes ?? new List<ValueType>();
            MinParameters = minParameters < 0 ? ParameterTypes.Count : minParameters;
            MaxParameters = maxParameters < 0 ? ParameterTypes.Count : maxParameters;
        }
    }
}