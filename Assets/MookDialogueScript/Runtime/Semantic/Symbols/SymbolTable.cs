using System;
using System.Collections.Generic;
using System.Linq;

namespace MookDialogueScript.Semantic.Symbols
{
    /// <summary>
    /// 符号表实现
    /// 提供基础的符号存储和查找功能
    /// </summary>
    public class SymbolTable : ILocalSymbolTable
    {
        private readonly Dictionary<string, TypeSystem.TypeInfo> _variables;
        private readonly Dictionary<string, FunctionInfo> _functions;

        /// <summary>
        /// 父符号表
        /// </summary>
        public ISymbolTable Parent { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="parent">父符号表</param>
        /// <param name="comparer">字符串比较器，默认忽略大小写</param>
        public SymbolTable(ISymbolTable parent = null, StringComparer comparer = null)
        {
            Parent = parent;
            var actualComparer = comparer ?? StringComparer.OrdinalIgnoreCase;
            _variables = new Dictionary<string, TypeSystem.TypeInfo>(actualComparer);
            _functions = new Dictionary<string, FunctionInfo>(actualComparer);
        }

        /// <summary>
        /// 获取变量类型
        /// </summary>
        public virtual TypeSystem.TypeInfo GetVariableType(string name)
        {
            if (string.IsNullOrEmpty(name))
                return TypeSystem.TypeInfo.Error;

            // 先查找本地作用域
            if (_variables.TryGetValue(name, out var type))
                return type;
            
            // 查找父作用域
            return Parent?.GetVariableType(name) ?? TypeSystem.TypeInfo.Any;
        }

        /// <summary>
        /// 获取标识符类型
        /// </summary>
        public virtual TypeSystem.TypeInfo GetIdentifierType(string name)
        {
            if (string.IsNullOrEmpty(name))
                return TypeSystem.TypeInfo.Error;

            // 先尝试作为变量查找
            var varType = GetVariableType(name);
            if (varType.Kind != TypeSystem.TypeKind.Error && varType.Kind != TypeSystem.TypeKind.Any)
                return varType;

            // 再尝试作为函数查找
            var funcInfo = GetFunctionInfo(name);
            if (funcInfo != null)
                return TypeSystem.TypeInfo.Function;

            return TypeSystem.TypeInfo.Any;
        }

        /// <summary>
        /// 获取函数信息
        /// </summary>
        public virtual FunctionInfo GetFunctionInfo(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            // 先查找本地作用域
            if (_functions.TryGetValue(name, out var info))
                return info;
            
            // 查找父作用域
            return Parent?.GetFunctionInfo(name);
        }

        /// <summary>
        /// 检查符号是否已定义
        /// </summary>
        public virtual bool IsDefined(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            return _variables.ContainsKey(name) || 
                   _functions.ContainsKey(name) || 
                   (Parent?.IsDefined(name) ?? false);
        }

        /// <summary>
        /// 定义变量
        /// </summary>
        public virtual void DefineVariable(string name, TypeSystem.TypeInfo type)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("变量名不能为空", nameof(name));
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            _variables[name] = type;
        }

        /// <summary>
        /// 定义函数
        /// </summary>
        public virtual void DefineFunction(string name, FunctionInfo info)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("函数名不能为空", nameof(name));
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            _functions[name] = info;
        }

        /// <summary>
        /// 获取本地定义的变量名
        /// </summary>
        public IEnumerable<string> GetLocalVariableNames()
        {
            return _variables.Keys.ToList();
        }

        /// <summary>
        /// 获取本地定义的函数名
        /// </summary>
        public IEnumerable<string> GetLocalFunctionNames()
        {
            return _functions.Keys.ToList();
        }

        /// <summary>
        /// 检查符号是否在本地作用域定义
        /// </summary>
        public bool IsLocallyDefined(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            return _variables.ContainsKey(name) || _functions.ContainsKey(name);
        }

        /// <summary>
        /// 获取所有变量（包括父作用域）
        /// </summary>
        /// <returns>变量名和类型的映射</returns>
        public Dictionary<string, TypeSystem.TypeInfo> GetAllVariables()
        {
            var result = new Dictionary<string, TypeSystem.TypeInfo>(StringComparer.OrdinalIgnoreCase);
            
            // 先添加父作用域的变量
            if (Parent is SymbolTable parentSymbolTable)
            {
                var parentVars = parentSymbolTable.GetAllVariables();
                foreach (var kvp in parentVars)
                {
                    result[kvp.Key] = kvp.Value;
                }
            }
            
            // 本地变量会覆盖父作用域的同名变量
            foreach (var kvp in _variables)
            {
                result[kvp.Key] = kvp.Value;
            }
            
            return result;
        }

        /// <summary>
        /// 获取所有函数（包括父作用域）
        /// </summary>
        /// <returns>函数名和信息的映射</returns>
        public Dictionary<string, FunctionInfo> GetAllFunctions()
        {
            var result = new Dictionary<string, FunctionInfo>(StringComparer.OrdinalIgnoreCase);
            
            // 先添加父作用域的函数
            if (Parent is SymbolTable parentSymbolTable)
            {
                var parentFuncs = parentSymbolTable.GetAllFunctions();
                foreach (var kvp in parentFuncs)
                {
                    result[kvp.Key] = kvp.Value;
                }
            }
            
            // 本地函数会覆盖父作用域的同名函数
            foreach (var kvp in _functions)
            {
                result[kvp.Key] = kvp.Value;
            }
            
            return result;
        }

        /// <summary>
        /// 清空本地符号表
        /// </summary>
        public void Clear()
        {
            _variables.Clear();
            _functions.Clear();
        }

        /// <summary>
        /// 获取符号表的深度
        /// </summary>
        public int GetDepth()
        {
            int depth = 0;
            var current = Parent;
            while (current != null)
            {
                depth++;
                current = current is ILocalSymbolTable localTable ? localTable.Parent : null;
            }
            return depth;
        }
    }
}