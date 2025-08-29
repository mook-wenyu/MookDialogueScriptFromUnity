using System.Collections.Generic;
using MookDialogueScript.Semantic.TypeSystem;
using MookDialogueScript;

namespace MookDialogueScript.Semantic.Contracts
{
    /// <summary>
    /// 符号解析器接口
    /// 负责管理变量、函数和类型符号的解析
    /// </summary>
    public interface ISymbolResolver
    {
        /// <summary>
        /// 解析变量类型
        /// </summary>
        /// <param name="variableName">变量名</param>
        /// <returns>变量的类型信息，如果未找到返回Any类型</returns>
        TypeInfo ResolveVariableType(string variableName);
        
        /// <summary>
        /// 解析标识符类型（可以是变量或函数）
        /// </summary>
        /// <param name="identifierName">标识符名称</param>
        /// <returns>标识符的类型信息</returns>
        TypeInfo ResolveIdentifierType(string identifierName);
        
        /// <summary>
        /// 解析函数信息
        /// </summary>
        /// <param name="functionName">函数名</param>
        /// <returns>函数信息，如果未找到返回null</returns>
        FunctionInfo ResolveFunctionInfo(string functionName);
        
        /// <summary>
        /// 检查符号是否已定义
        /// </summary>
        /// <param name="symbolName">符号名称</param>
        /// <returns>如果已定义返回true，否则返回false</returns>
        bool IsSymbolDefined(string symbolName);
        
        /// <summary>
        /// 定义变量符号
        /// </summary>
        /// <param name="variableName">变量名</param>
        /// <param name="type">变量类型</param>
        void DefineVariable(string variableName, TypeInfo type);
        
        /// <summary>
        /// 定义函数符号
        /// </summary>
        /// <param name="functionName">函数名</param>
        /// <param name="functionInfo">函数信息</param>
        void DefineFunction(string functionName, FunctionInfo functionInfo);
        
        /// <summary>
        /// 检查节点是否存在
        /// </summary>
        /// <param name="nodeName">节点名</param>
        /// <returns>如果节点存在返回true，否则返回false</returns>
        bool NodeExists(string nodeName);
        
        /// <summary>
        /// 获取相似符号名建议
        /// </summary>
        /// <param name="symbolName">查询的符号名</param>
        /// <param name="symbolType">符号类型（变量、函数等）</param>
        /// <returns>最相似的符号名，如果没有找到返回null</returns>
        string GetSimilarSymbolName(string symbolName, SymbolType symbolType = SymbolType.Any);
        
        /// <summary>
        /// 获取所有已定义的符号名
        /// </summary>
        /// <param name="symbolType">要查询的符号类型</param>
        /// <returns>符号名集合</returns>
        IEnumerable<string> GetDefinedSymbols(SymbolType symbolType = SymbolType.Any);
        
        /// <summary>
        /// 推入新的作用域
        /// </summary>
        void PushScope();
        
        /// <summary>
        /// 弹出当前作用域
        /// </summary>
        void PopScope();
        
        /// <summary>
        /// 当前作用域深度
        /// </summary>
        int ScopeDepth { get; }
    }
    
    /// <summary>
    /// 符号类型枚举
    /// </summary>
    public enum SymbolType
    {
        /// <summary>任何类型的符号</summary>
        Any,
        
        /// <summary>变量符号</summary>
        Variable,
        
        /// <summary>函数符号</summary>
        Function,
        
        /// <summary>节点符号</summary>
        Node,
        
        /// <summary>类型符号</summary>
        Type
    }
    
    /// <summary>
    /// 作用域符号解析器接口
    /// 提供作用域感知的符号解析功能
    /// </summary>
    public interface IScopedSymbolResolver : ISymbolResolver
    {
        /// <summary>
        /// 检查符号是否在当前作用域中定义
        /// </summary>
        /// <param name="symbolName">符号名</param>
        /// <returns>如果在当前作用域中定义返回true，否则返回false</returns>
        bool IsLocallyDefined(string symbolName);
        
        /// <summary>
        /// 获取当前作用域中定义的所有符号
        /// </summary>
        /// <param name="symbolType">符号类型过滤</param>
        /// <returns>当前作用域的符号集合</returns>
        IEnumerable<string> GetLocalSymbols(SymbolType symbolType = SymbolType.Any);
        
        /// <summary>
        /// 获取指定作用域深度的符号解析器
        /// </summary>
        /// <param name="scopeDepth">作用域深度（0为全局作用域）</param>
        /// <returns>指定作用域的符号解析器</returns>
        ISymbolResolver GetScopeResolver(int scopeDepth);
    }
}