using System;

namespace MookDialogueScript.Semantic.Core
{
    /// <summary>
    /// 默认符号表工厂实现
    /// 作为内部使用的工厂实现，支持创建各种符号表和解析器
    /// </summary>
    internal class DefaultSymbolTableFactory : Symbols.ISymbolTableFactory
    {
        /// <summary>
        /// 创建全局符号表
        /// </summary>
        public Symbols.IGlobalSymbolTable CreateGlobalSymbolTable(VariableManager variableManager, FunctionManager functionManager)
        {
            var globalTable = new Symbols.GlobalSymbolTable();
            globalTable.InitializeBuiltInSymbols(variableManager, functionManager);
            return globalTable;
        }

        /// <summary>
        /// 创建局部符号表
        /// </summary>
        public Symbols.ILocalSymbolTable CreateLocalSymbolTable(Symbols.ISymbolTable parent)
        {
            return new Symbols.SymbolTable(parent);
        }

        /// <summary>
        /// 创建符号解析器
        /// </summary>
        public Contracts.ISymbolResolver CreateSymbolResolver(Symbols.IGlobalSymbolTable globalSymbolTable)
        {
            return new Symbols.SymbolResolver(globalSymbolTable);
        }
    }
}