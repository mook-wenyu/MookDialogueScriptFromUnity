using System.Collections.Generic;
using System.Linq;
using MookDialogueScript.Semantic.TypeSystem;
using MookDialogueScript;

namespace MookDialogueScript.Semantic.Symbols
{
    /// <summary>
    /// 符号解析器实现
    /// 基于作用域管理器实现符号的查找和解析
    /// </summary>
    public class SymbolResolver : Contracts.IScopedSymbolResolver
    {
        private ScopeManager _scopeManager;
        private readonly IGlobalSymbolTable _globalSymbolTable;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="globalSymbolTable">全局符号表</param>
        public SymbolResolver(IGlobalSymbolTable globalSymbolTable)
        {
            _globalSymbolTable = globalSymbolTable ?? throw new System.ArgumentNullException(nameof(globalSymbolTable));
            _scopeManager = new ScopeManager(_globalSymbolTable);
        }

        /// <summary>
        /// 构造函数（使用现有的作用域管理器）
        /// </summary>
        /// <param name="scopeManager">作用域管理器</param>
        public SymbolResolver(ScopeManager scopeManager)
        {
            _scopeManager = scopeManager ?? throw new System.ArgumentNullException(nameof(scopeManager));
            _globalSymbolTable = scopeManager.GlobalScope;
        }

        /// <summary>
        /// 解析变量类型
        /// </summary>
        public TypeInfo ResolveVariableType(string variableName)
        {
            if (string.IsNullOrEmpty(variableName))
                return TypeInfo.Error;

            return _scopeManager.CurrentScope.GetVariableType(variableName);
        }

        /// <summary>
        /// 解析标识符类型
        /// </summary>
        public TypeInfo ResolveIdentifierType(string identifierName)
        {
            if (string.IsNullOrEmpty(identifierName))
                return TypeInfo.Error;

            return _scopeManager.CurrentScope.GetIdentifierType(identifierName);
        }

        /// <summary>
        /// 解析函数信息
        /// </summary>
        public FunctionInfo ResolveFunctionInfo(string functionName)
        {
            if (string.IsNullOrEmpty(functionName))
                return null;

            return _scopeManager.CurrentScope.GetFunctionInfo(functionName);
        }

        /// <summary>
        /// 检查符号是否已定义
        /// </summary>
        public bool IsSymbolDefined(string symbolName)
        {
            if (string.IsNullOrEmpty(symbolName))
                return false;

            return _scopeManager.CurrentScope.IsDefined(symbolName);
        }

        /// <summary>
        /// 定义变量符号
        /// </summary>
        public void DefineVariable(string variableName, TypeInfo type)
        {
            _scopeManager.DefineVariable(variableName, type);
        }

        /// <summary>
        /// 定义函数符号
        /// </summary>
        public void DefineFunction(string functionName, FunctionInfo functionInfo)
        {
            _scopeManager.DefineFunction(functionName, functionInfo);
        }

        /// <summary>
        /// 检查节点是否存在
        /// </summary>
        public bool NodeExists(string nodeName)
        {
            return _globalSymbolTable.NodeExists(nodeName);
        }

        /// <summary>
        /// 获取相似符号名建议
        /// </summary>
        public string GetSimilarSymbolName(string symbolName, Contracts.SymbolType symbolType = Contracts.SymbolType.Any)
        {
            if (string.IsNullOrEmpty(symbolName))
                return null;

            var candidates = new List<string>();

            // 根据符号类型收集候选名称
            switch (symbolType)
            {
                case Contracts.SymbolType.Variable:
                    candidates.AddRange(_scopeManager.GetAllVariableNames());
                    break;
                case Contracts.SymbolType.Function:
                    candidates.AddRange(_scopeManager.GetAllFunctionNames());
                    break;
                case Contracts.SymbolType.Node:
                    candidates.AddRange(_globalSymbolTable.GetAllNodeNames());
                    break;
                case Contracts.SymbolType.Any:
                default:
                    candidates.AddRange(_scopeManager.GetAllVariableNames());
                    candidates.AddRange(_scopeManager.GetAllFunctionNames());
                    candidates.AddRange(_globalSymbolTable.GetAllNodeNames());
                    break;
            }

            return candidates.Any() ? Utils.GetMostSimilarString(symbolName, candidates) : null;
        }

        /// <summary>
        /// 获取已定义的符号
        /// </summary>
        public IEnumerable<string> GetDefinedSymbols(Contracts.SymbolType symbolType = Contracts.SymbolType.Any)
        {
            var result = new List<string>();

            switch (symbolType)
            {
                case Contracts.SymbolType.Variable:
                    result.AddRange(_scopeManager.GetAllVariableNames());
                    break;
                case Contracts.SymbolType.Function:
                    result.AddRange(_scopeManager.GetAllFunctionNames());
                    break;
                case Contracts.SymbolType.Node:
                    result.AddRange(_globalSymbolTable.GetAllNodeNames());
                    break;
                case Contracts.SymbolType.Any:
                default:
                    result.AddRange(_scopeManager.GetAllVariableNames());
                    result.AddRange(_scopeManager.GetAllFunctionNames());
                    result.AddRange(_globalSymbolTable.GetAllNodeNames());
                    break;
            }

            return result.Distinct().ToList();
        }

        /// <summary>
        /// 推入新的作用域
        /// </summary>
        public void PushScope()
        {
            _scopeManager.EnterScope();
        }

        /// <summary>
        /// 弹出当前作用域
        /// </summary>
        public void PopScope()
        {
            _scopeManager.ExitScope();
        }

        /// <summary>
        /// 当前作用域深度
        /// </summary>
        public int ScopeDepth => _scopeManager.ScopeDepth;

        /// <summary>
        /// 检查符号是否在当前作用域中定义
        /// </summary>
        public bool IsLocallyDefined(string symbolName)
        {
            return _scopeManager.IsLocallyDefined(symbolName);
        }

        /// <summary>
        /// 获取当前作用域中定义的所有符号
        /// </summary>
        public IEnumerable<string> GetLocalSymbols(Contracts.SymbolType symbolType = Contracts.SymbolType.Any)
        {
            var result = new List<string>();
            var currentScope = _scopeManager.CurrentScope;

            if (currentScope is ILocalSymbolTable localTable)
            {
                switch (symbolType)
                {
                    case Contracts.SymbolType.Variable:
                        result.AddRange(localTable.GetLocalVariableNames());
                        break;
                    case Contracts.SymbolType.Function:
                        result.AddRange(localTable.GetLocalFunctionNames());
                        break;
                    case Contracts.SymbolType.Any:
                    default:
                        result.AddRange(localTable.GetLocalVariableNames());
                        result.AddRange(localTable.GetLocalFunctionNames());
                        break;
                }
            }

            return result;
        }

        /// <summary>
        /// 获取指定作用域深度的符号解析器
        /// </summary>
        public Contracts.ISymbolResolver GetScopeResolver(int scopeDepth)
        {
            var targetScope = _scopeManager.GetScopeAtDepth(scopeDepth);
            if (targetScope == null)
                return null;

            // 创建一个临时的作用域管理器和解析器
            var tempGlobal = _globalSymbolTable;
            var tempScopeManager = new ScopeManager(tempGlobal);
            
            // 恢复到指定深度
            for (int i = 0; i < scopeDepth; i++)
            {
                tempScopeManager.EnterScope();
            }

            return new SymbolResolver(tempScopeManager);
        }

        /// <summary>
        /// 设置作用域管理器（用于外部管理作用域）
        /// </summary>
        /// <param name="scopeManager">新的作用域管理器</param>
        public void SetScopeManager(ScopeManager scopeManager)
        {
            _scopeManager = scopeManager ?? throw new System.ArgumentNullException(nameof(scopeManager));
        }

        /// <summary>
        /// 获取当前的作用域管理器
        /// </summary>
        /// <returns>当前作用域管理器</returns>
        public ScopeManager GetScopeManager()
        {
            return _scopeManager;
        }
    }
}