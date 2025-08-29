using System;
using System.Collections.Generic;
using System.Linq;

namespace MookDialogueScript.Semantic.Symbols
{
    /// <summary>
    /// 全局符号表实现
    /// 扩展基础符号表功能，添加节点名管理和内置符号初始化
    /// </summary>
    public class GlobalSymbolTable : SymbolTable, IGlobalSymbolTable
    {
        private readonly Dictionary<string, string> _nodeNames;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="comparer">字符串比较器，默认忽略大小写</param>
        public GlobalSymbolTable(StringComparer comparer = null) : base(null, comparer)
        {
            var actualComparer = comparer ?? StringComparer.OrdinalIgnoreCase;
            _nodeNames = new Dictionary<string, string>(actualComparer);
        }

        /// <summary>
        /// 添加节点名
        /// </summary>
        /// <param name="nodeName">节点名</param>
        public void AddNodeName(string nodeName)
        {
            if (string.IsNullOrEmpty(nodeName))
                return;

            _nodeNames[nodeName] = nodeName;
        }

        /// <summary>
        /// 检查节点是否存在
        /// </summary>
        /// <param name="nodeName">节点名</param>
        /// <returns>如果节点存在返回true</returns>
        public bool NodeExists(string nodeName)
        {
            if (string.IsNullOrEmpty(nodeName))
                return false;

            return _nodeNames.ContainsKey(nodeName);
        }

        /// <summary>
        /// 获取相似节点名建议
        /// </summary>
        /// <param name="nodeName">查询的节点名</param>
        /// <returns>最相似的节点名，未找到返回null</returns>
        public string GetSimilarNodeName(string nodeName)
        {
            if (string.IsNullOrEmpty(nodeName) || !_nodeNames.Any())
                return null;

            return Utils.GetMostSimilarString(nodeName, _nodeNames.Keys);
        }

        /// <summary>
        /// 获取所有节点名
        /// </summary>
        /// <returns>节点名集合</returns>
        public IEnumerable<string> GetAllNodeNames()
        {
            return _nodeNames.Keys.ToList();
        }

        /// <summary>
        /// 初始化内置符号
        /// 从VariableManager和FunctionManager加载预定义的符号
        /// </summary>
        /// <param name="variableManager">变量管理器</param>
        /// <param name="functionManager">函数管理器</param>
        public void InitializeBuiltInSymbols(VariableManager variableManager, FunctionManager functionManager)
        {
            InitializeBuiltInVariables(variableManager);
            InitializeBuiltInFunctions(functionManager);
        }

        /// <summary>
        /// 初始化内置变量
        /// </summary>
        /// <param name="variableManager">变量管理器</param>
        private void InitializeBuiltInVariables(VariableManager variableManager)
        {
            if (variableManager == null)
                return;

            try
            {
                var variables = variableManager.GetAllVariables();
                foreach (var kvp in variables)
                {
                    if (!string.IsNullOrEmpty(kvp.Key))
                    {
                        var actualType = kvp.Value.Value?.GetType() ?? typeof(object);
                        var typeInfo = TypeSystem.TypeInference.FromClrType(actualType);
                        DefineVariable(kvp.Key, typeInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                // 记录错误但不中断初始化过程
                UnityEngine.Debug.LogWarning($"初始化内置变量时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 初始化内置函数
        /// </summary>
        /// <param name="functionManager">函数管理器</param>
        private void InitializeBuiltInFunctions(FunctionManager functionManager)
        {
            if (functionManager == null)
                return;

            try
            {
                var functionNames = functionManager.GetFunctionNames();
                foreach (var name in functionNames)
                {
                    if (!string.IsNullOrEmpty(name))
                    {
                        var functionInfo = CreateFunctionInfoFromManager(name, functionManager);
                        if (functionInfo != null)
                        {
                            DefineFunction(name, functionInfo);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 记录错误但不中断初始化过程
                UnityEngine.Debug.LogWarning($"初始化内置函数时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 从函数管理器创建函数信息
        /// </summary>
        /// <param name="name">函数名</param>
        /// <param name="functionManager">函数管理器</param>
        /// <returns>函数信息，创建失败返回null</returns>
        private FunctionInfo CreateFunctionInfoFromManager(string name, FunctionManager functionManager)
        {
            try
            {
                // 尝试获取函数签名信息
                var signature = functionManager.GetFunctionSignature(name, 0, 0);
                if (signature != null)
                {
                    var parameterTypes = new List<TypeSystem.TypeInfo>();
                    foreach (var param in signature.Parameters)
                    {
                        var paramType = MapScriptTypeToTypeInfo(param.TypeName);
                        parameterTypes.Add(paramType);
                    }

                    var returnType = MapScriptTypeToTypeInfo(signature.ReturnTypeName);
                    return new FunctionInfo(name, returnType, parameterTypes, 
                        signature.MinRequiredParameters, signature.MaxParameters);
                }
            }
            catch
            {
                // 如果获取签名失败，创建基本的函数信息
            }

            // 回退：创建基本的函数信息
            return new FunctionInfo(name, TypeSystem.TypeInfo.Any, new List<TypeSystem.TypeInfo>());
        }

        /// <summary>
        /// 将脚本类型名映射到 TypeInfo
        /// </summary>
        /// <param name="typeName">脚本类型名</param>
        /// <returns>对应的TypeInfo</returns>
        private TypeSystem.TypeInfo MapScriptTypeToTypeInfo(string typeName)
        {
            return typeName switch
            {
                "Number" => TypeSystem.TypeInfo.Number,
                "String" => TypeSystem.TypeInfo.String,
                "Boolean" => TypeSystem.TypeInfo.Boolean,
                "Function" => TypeSystem.TypeInfo.Function,
                "Object" => TypeSystem.TypeInfo.Object(typeof(object)),
                _ => TypeSystem.TypeInfo.Any
            };
        }

        /// <summary>
        /// 清空所有符号和节点名
        /// </summary>
        public new void Clear()
        {
            base.Clear();
            _nodeNames.Clear();
        }

        /// <summary>
        /// 获取相似符号名建议（包括变量、函数、节点）
        /// </summary>
        /// <param name="name">查询的名称</param>
        /// <param name="symbolType">符号类型过滤</param>
        /// <returns>最相似的符号名，未找到返回null</returns>
        public string GetSimilarSymbolName(string name, SymbolType symbolType = SymbolType.Any)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            var candidates = new List<string>();

            // 根据符号类型收集候选名称
            if (symbolType == SymbolType.Any || symbolType == SymbolType.Variable)
            {
                candidates.AddRange(GetLocalVariableNames());
            }

            if (symbolType == SymbolType.Any || symbolType == SymbolType.Function)
            {
                candidates.AddRange(GetLocalFunctionNames());
            }

            if (symbolType == SymbolType.Any || symbolType == SymbolType.Node)
            {
                candidates.AddRange(GetAllNodeNames());
            }

            return candidates.Any() ? Utils.GetMostSimilarString(name, candidates) : null;
        }

        /// <summary>
        /// 符号类型枚举（本地定义以避免循环依赖）
        /// </summary>
        public enum SymbolType
        {
            Any,
            Variable,
            Function,
            Node
        }
    }
}