using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using MookDialogueScript.Semantic.Contracts;

namespace MookDialogueScript.Semantic.Diagnostics
{
    /// <summary>
    /// 诊断信息收集器实现
    /// 支持并发安全的诊断信息收集和管理
    /// </summary>
    public class DiagnosticCollector : IConcurrentDiagnosticCollector
    {
        private readonly ConcurrentBag<Diagnostic> _diagnostics = new ConcurrentBag<Diagnostic>();
        private readonly object _lockObject = new object();

        /// <summary>
        /// 添加诊断信息
        /// </summary>
        public void AddDiagnostic(Diagnostic diagnostic)
        {
            if (diagnostic != null)
            {
                _diagnostics.Add(diagnostic);
            }
        }

        /// <summary>
        /// 添加错误诊断
        /// </summary>
        public void AddError(string code, string message, int line = 0, int column = 0, string suggestion = null)
        {
            AddDiagnostic(new Diagnostic(code, DiagnosticSeverity.Error, message, line, column, suggestion));
        }

        /// <summary>
        /// 添加警告诊断
        /// </summary>
        public void AddWarning(string code, string message, int line = 0, int column = 0, string suggestion = null)
        {
            AddDiagnostic(new Diagnostic(code, DiagnosticSeverity.Warning, message, line, column, suggestion));
        }

        /// <summary>
        /// 添加信息诊断
        /// </summary>
        public void AddInfo(string code, string message, int line = 0, int column = 0, string suggestion = null)
        {
            AddDiagnostic(new Diagnostic(code, DiagnosticSeverity.Info, message, line, column, suggestion));
        }

        /// <summary>
        /// 获取所有诊断信息
        /// </summary>
        public IReadOnlyList<Diagnostic> GetDiagnostics()
        {
            return _diagnostics.ToList();
        }

        /// <summary>
        /// 按严重性过滤诊断信息
        /// </summary>
        public IReadOnlyList<Diagnostic> GetDiagnostics(DiagnosticSeverity severity)
        {
            return _diagnostics.Where(d => d.Severity == severity).ToList();
        }

        /// <summary>
        /// 检查是否有错误
        /// </summary>
        public bool HasErrors => _diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);

        /// <summary>
        /// 检查是否有警告
        /// </summary>
        public bool HasWarnings => _diagnostics.Any(d => d.Severity == DiagnosticSeverity.Warning);

        /// <summary>
        /// 错误数量
        /// </summary>
        public int ErrorCount => _diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);

        /// <summary>
        /// 警告数量
        /// </summary>
        public int WarningCount => _diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning);

        /// <summary>
        /// 信息数量
        /// </summary>
        public int InfoCount => _diagnostics.Count(d => d.Severity == DiagnosticSeverity.Info);

        /// <summary>
        /// 清空所有诊断信息
        /// </summary>
        public void Clear()
        {
            lock (_lockObject)
            {
                while (_diagnostics.TryTake(out _))
                {
                    // 清空 ConcurrentBag
                }
            }
        }

        /// <summary>
        /// 生成语义分析报告
        /// </summary>
        public SemanticReport GenerateReport()
        {
            var report = new SemanticReport();
            foreach (var diagnostic in _diagnostics)
            {
                report.AddDiagnostic(diagnostic);
            }
            return report;
        }

        /// <summary>
        /// 合并另一个收集器的诊断信息
        /// </summary>
        public void Merge(IDiagnosticCollector other)
        {
            if (other == null) return;
            
            foreach (var diagnostic in other.GetDiagnostics())
            {
                AddDiagnostic(diagnostic);
            }
        }

        /// <summary>
        /// 创建当前收集器的快照
        /// </summary>
        public IDiagnosticCollector CreateSnapshot()
        {
            var snapshot = new DiagnosticCollector();
            foreach (var diagnostic in _diagnostics)
            {
                snapshot.AddDiagnostic(diagnostic);
            }
            return snapshot;
        }
    }
}