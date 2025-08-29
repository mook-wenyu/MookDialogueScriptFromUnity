using System;
using System.Collections.Generic;

namespace MookDialogueScript.Semantic.Symbols
{
    /// <summary>
    /// 符号表接口
    /// 定义符号存储和查找的基本契约
    /// </summary>
    public interface ISymbolTable
    {
        /// <summary>
        /// 获取变量类型
        /// </summary>
        /// <param name="name">变量名</param>
        /// <returns>变量类型，未找到时返回Any类型</returns>
        TypeSystem.TypeInfo GetVariableType(string name);
        
        /// <summary>
        /// 获取标识符类型（可能是变量或函数）
        /// </summary>
        /// <param name="name">标识符名称</param>
        /// <returns>标识符类型</returns>
        TypeSystem.TypeInfo GetIdentifierType(string name);
        
        /// <summary>
        /// 获取函数信息
        /// </summary>
        /// <param name="name">函数名</param>
        /// <returns>函数信息，未找到时返回null</returns>
        FunctionInfo GetFunctionInfo(string name);
        
        /// <summary>
        /// 检查符号是否已定义
        /// </summary>
        /// <param name="name">符号名</param>
        /// <returns>如果已定义返回true</returns>
        bool IsDefined(string name);
        
        /// <summary>
        /// 定义变量
        /// </summary>
        /// <param name="name">变量名</param>
        /// <param name="type">变量类型</param>
        void DefineVariable(string name, TypeSystem.TypeInfo type);
        
        /// <summary>
        /// 定义函数
        /// </summary>
        /// <param name="name">函数名</param>
        /// <param name="info">函数信息</param>
        void DefineFunction(string name, FunctionInfo info);
    }

    /// <summary>
    /// 局部符号表接口
    /// 支持作用域内的符号管理
    /// </summary>
    public interface ILocalSymbolTable : ISymbolTable
    {
        /// <summary>
        /// 获取本地定义的变量名
        /// </summary>
        /// <returns>变量名集合</returns>
        IEnumerable<string> GetLocalVariableNames();
        
        /// <summary>
        /// 获取本地定义的函数名
        /// </summary>
        /// <returns>函数名集合</returns>
        IEnumerable<string> GetLocalFunctionNames();
        
        /// <summary>
        /// 检查符号是否在本地作用域定义
        /// </summary>
        /// <param name="name">符号名</param>
        /// <returns>如果在本地定义返回true</returns>
        bool IsLocallyDefined(string name);
        
        /// <summary>
        /// 父符号表
        /// </summary>
        ISymbolTable Parent { get; }
    }

    /// <summary>
    /// 全局符号表接口
    /// 支持节点名管理和全局符号解析
    /// </summary>
    public interface IGlobalSymbolTable : ILocalSymbolTable
    {
        /// <summary>
        /// 添加节点名
        /// </summary>
        /// <param name="nodeName">节点名</param>
        void AddNodeName(string nodeName);
        
        /// <summary>
        /// 检查节点是否存在
        /// </summary>
        /// <param name="nodeName">节点名</param>
        /// <returns>如果节点存在返回true</returns>
        bool NodeExists(string nodeName);
        
        /// <summary>
        /// 获取相似节点名建议
        /// </summary>
        /// <param name="nodeName">查询的节点名</param>
        /// <returns>最相似的节点名，未找到返回null</returns>
        string GetSimilarNodeName(string nodeName);
        
        /// <summary>
        /// 获取所有节点名
        /// </summary>
        /// <returns>节点名集合</returns>
        IEnumerable<string> GetAllNodeNames();
        
        /// <summary>
        /// 初始化内置符号
        /// </summary>
        /// <param name="variableManager">变量管理器</param>
        /// <param name="functionManager">函数管理器</param>
        void InitializeBuiltInSymbols(VariableManager variableManager, FunctionManager functionManager);
    }

    /// <summary>
    /// 符号表工厂接口
    /// 用于创建不同类型的符号表实例
    /// </summary>
    public interface ISymbolTableFactory
    {
        /// <summary>
        /// 创建全局符号表
        /// </summary>
        /// <param name="variableManager">变量管理器</param>
        /// <param name="functionManager">函数管理器</param>
        /// <returns>全局符号表实例</returns>
        IGlobalSymbolTable CreateGlobalSymbolTable(VariableManager variableManager, FunctionManager functionManager);
        
        /// <summary>
        /// 创建局部符号表
        /// </summary>
        /// <param name="parent">父符号表</param>
        /// <returns>局部符号表实例</returns>
        ILocalSymbolTable CreateLocalSymbolTable(ISymbolTable parent);
        
        /// <summary>
        /// 创建符号解析器
        /// </summary>
        /// <param name="globalSymbolTable">全局符号表</param>
        /// <returns>符号解析器实例</returns>
        Contracts.ISymbolResolver CreateSymbolResolver(IGlobalSymbolTable globalSymbolTable);
    }
}