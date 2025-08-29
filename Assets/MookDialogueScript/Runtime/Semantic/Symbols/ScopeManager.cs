using System.Collections.Generic;
using System.Linq;
using MookDialogueScript.Semantic.Core;

namespace MookDialogueScript.Semantic.Symbols
{
    /// <summary>
    /// 作用域管理器
    /// 负责管理符号表的层次结构和作用域生命周期
    /// </summary>
    public class ScopeManager
    {
        private readonly Stack<ILocalSymbolTable> _scopes = new Stack<ILocalSymbolTable>();
        private readonly IGlobalSymbolTable _globalScope;
        private readonly ISymbolTableFactory _symbolTableFactory;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="globalScope">全局符号表</param>
        /// <param name="symbolTableFactory">符号表工厂（可选）</param>
        public ScopeManager(IGlobalSymbolTable globalScope, ISymbolTableFactory symbolTableFactory = null)
        {
            _globalScope = globalScope ?? throw new System.ArgumentNullException(nameof(globalScope));
            _symbolTableFactory = symbolTableFactory ?? new DefaultSymbolTableFactory();
            _scopes.Push(globalScope);
        }

        /// <summary>
        /// 当前符号表
        /// </summary>
        public ISymbolTable CurrentScope => _scopes.Peek();

        /// <summary>
        /// 全局符号表
        /// </summary>
        public IGlobalSymbolTable GlobalScope => _globalScope;

        /// <summary>
        /// 作用域深度（0为全局作用域）
        /// </summary>
        public int ScopeDepth => _scopes.Count - 1;

        /// <summary>
        /// 进入新作用域
        /// </summary>
        public void EnterScope()
        {
            var currentScope = _scopes.Peek();
            var newScope = _symbolTableFactory.CreateLocalSymbolTable(currentScope);
            _scopes.Push(newScope);
        }

        /// <summary>
        /// 退出当前作用域
        /// </summary>
        /// <returns>如果成功退出作用域返回true，如果已在全局作用域返回false</returns>
        public bool ExitScope()
        {
            if (_scopes.Count > 1) // 保留全局作用域
            {
                _scopes.Pop();
                return true;
            }
            return false;
        }

        /// <summary>
        /// 定义变量到当前作用域
        /// </summary>
        /// <param name="name">变量名</param>
        /// <param name="type">变量类型</param>
        public void DefineVariable(string name, TypeSystem.TypeInfo type)
        {
            if (_scopes.Peek() is ILocalSymbolTable symbolTable)
            {
                symbolTable.DefineVariable(name, type);
            }
        }

        /// <summary>
        /// 定义函数到全局作用域
        /// </summary>
        /// <param name="name">函数名</param>
        /// <param name="info">函数信息</param>
        public void DefineFunction(string name, FunctionInfo info)
        {
            _globalScope.DefineFunction(name, info);
        }

        /// <summary>
        /// 检查当前作用域是否已定义变量
        /// </summary>
        /// <param name="name">变量名</param>
        /// <returns>如果在当前作用域定义返回true</returns>
        public bool IsLocallyDefined(string name)
        {
            return _scopes.Peek() is ILocalSymbolTable symbolTable && symbolTable.IsLocallyDefined(name);
        }

        /// <summary>
        /// 获取指定深度的作用域
        /// </summary>
        /// <param name="depth">作用域深度（0为全局作用域）</param>
        /// <returns>指定深度的符号表，如果深度无效返回null</returns>
        public ISymbolTable GetScopeAtDepth(int depth)
        {
            if (depth < 0 || depth >= _scopes.Count)
                return null;

            var scopeArray = _scopes.ToArray();
            return scopeArray[_scopes.Count - 1 - depth]; // 栈是倒序的
        }

        /// <summary>
        /// 获取所有作用域的变量名（从当前向上查找）
        /// </summary>
        /// <returns>变量名集合</returns>
        public IEnumerable<string> GetAllVariableNames()
        {
            var result = new HashSet<string>();
            foreach (var scope in _scopes)
            {
                if (scope is ILocalSymbolTable localScope)
                {
                    foreach (var name in localScope.GetLocalVariableNames())
                    {
                        result.Add(name);
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// 获取所有作用域的函数名（从当前向上查找）
        /// </summary>
        /// <returns>函数名集合</returns>
        public IEnumerable<string> GetAllFunctionNames()
        {
            var result = new HashSet<string>();
            foreach (var scope in _scopes)
            {
                if (scope is ILocalSymbolTable localScope)
                {
                    foreach (var name in localScope.GetLocalFunctionNames())
                    {
                        result.Add(name);
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// 重置到全局作用域
        /// </summary>
        public void ResetToGlobalScope()
        {
            while (_scopes.Count > 1)
            {
                _scopes.Pop();
            }
        }

        /// <summary>
        /// 创建作用域快照
        /// 用于保存当前的作用域状态
        /// </summary>
        /// <returns>作用域快照</returns>
        public ScopeSnapshot CreateSnapshot()
        {
            return new ScopeSnapshot(_scopes.ToArray());
        }

        /// <summary>
        /// 恢复作用域快照
        /// </summary>
        /// <param name="snapshot">要恢复的快照</param>
        public void RestoreSnapshot(ScopeSnapshot snapshot)
        {
            if (snapshot?.Scopes == null)
                return;

            _scopes.Clear();
            foreach (var scope in snapshot.Scopes.Reverse())
            {
                _scopes.Push(scope);
            }
        }
    }

    /// <summary>
    /// 作用域快照
    /// 用于保存和恢复作用域状态
    /// </summary>
    public class ScopeSnapshot
    {
        /// <summary>
        /// 作用域数组（从全局到当前）
        /// </summary>
        public ILocalSymbolTable[] Scopes { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="scopes">作用域数组</param>
        internal ScopeSnapshot(ILocalSymbolTable[] scopes)
        {
            Scopes = scopes;
        }
    }
}