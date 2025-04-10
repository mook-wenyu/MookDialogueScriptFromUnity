using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace MookDialogueScript
{
    /// <summary>
    /// 脚本运行时值
    /// </summary>
    public class RuntimeValue
    {
        /// <summary>
        /// 脚本运行时值类型
        /// </summary>
        public enum ValueType
        {
            Null,
            Number,
            String,
            Boolean
        }

        public ValueType Type { get; }
        public object Value { get; }

        public RuntimeValue(double value)
        {
            Type = ValueType.Number;
            Value = value;
        }

        public RuntimeValue(string value)
        {
            Type = ValueType.String;
            Value = value;
        }

        public RuntimeValue(bool value)
        {
            Type = ValueType.Boolean;
            Value = value;
        }

        /// <summary>
        /// 创建一个空值
        /// </summary>
        public RuntimeValue()
        {
            Type = ValueType.Null;
            Value = null;
        }

        /// <summary>
        /// 创建一个空值的静态方法
        /// </summary>
        public static RuntimeValue Null { get; } = new RuntimeValue();

        public override string ToString()
        {
            if (Value == null)
                return "null";
            return Type == ValueType.Boolean ? Value.ToString().ToLower() : Value.ToString();
        }
    }

    /// <summary>
    /// 对话上下文，管理变量、函数和节点
    /// </summary>
    public class DialogueContext
    {
        private VariableManager _variableManager = new VariableManager();
        private FunctionManager _functionManager = new FunctionManager();
        private Dictionary<string, NodeDefinitionNode> _nodes = new Dictionary<string, NodeDefinitionNode>();

        /// <summary>
        /// 创建一个新的对话上下文
        /// </summary>
        public DialogueContext()
        {
            // 扫描并注册所有标记了ScriptVar特性的变量
            _variableManager.ScanAndRegisterScriptVariables();

            // 扫描并注册所有标记了ScriptFunc特性的方法
            _functionManager.ScanAndRegisterScriptFunctions();
        }

        /// <summary>
        /// 注册对话节点
        /// </summary>
        /// <param name="name">节点名</param>
        /// <param name="node">节点</param>
        public void RegisterNode(string name, NodeDefinitionNode node)
        {
            _nodes[name] = node;
        }

        /// <summary>
        /// 获取对话节点
        /// </summary>
        /// <param name="name">节点名</param>
        /// <returns>节点</returns>
        public NodeDefinitionNode GetNode(string name)
        {
            if (_nodes.TryGetValue(name, out var value)) return value;
            throw new KeyNotFoundException($"找不到节点 '{name}'");
        }

        /// <summary>
        /// 获取所有节点
        /// </summary>
        /// <returns>所有节点</returns>
        public Dictionary<string, NodeDefinitionNode> GetNodes()
        {
            return _nodes;
        }

        /// <summary>
        /// 注册对象实例的所有属性和字段作为脚本变量
        /// </summary>
        /// <param name="objectName">对象名称（用作变量名称的前缀）</param>
        /// <param name="instance">对象实例</param>
        public void RegisterObjectPropertiesAndFields(string objectName, object instance)
        {
            _variableManager.RegisterObjectPropertiesAndFields(objectName, instance);
        }

        /// <summary>
        /// 注册内置变量
        /// </summary>
        /// <param name="name">变量名</param>
        /// <param name="getter">获取变量值的委托</param>
        /// <param name="setter">设置变量值的委托</param>
        public void RegisterBuiltinVariable(string name, Func<object> getter, Action<object> setter)
        {
            _variableManager.RegisterBuiltinVariable(name, getter, setter);
        }

        /// <summary>
        /// 获取所有内置变量
        /// </summary>
        /// <returns>内置变量字典</returns>
        public Dictionary<string, RuntimeValue> GetBuiltinVariables()
        {
            return _variableManager.GetBuiltinVariables();
        }

        /// <summary>
        /// 获取所有变量（包括内置变量和脚本变量）
        /// </summary>
        /// <returns>所有变量字典</returns>
        public Dictionary<string, RuntimeValue> GetAllVariables()
        {
            return _variableManager.GetAllVariables();
        }

        /// <summary>
        /// 获取所有脚本变量（用于保存状态）
        /// </summary>
        /// <returns>脚本变量的字典</returns>
        public Dictionary<string, RuntimeValue> GetScriptVariables()
        {
            return _variableManager.GetScriptVariables();
        }

        /// <summary>
        /// 设置变量值
        /// </summary>
        /// <param name="name">变量名</param>
        /// <param name="value">变量值</param>
        public void SetVariable(string name, RuntimeValue value)
        {
            _variableManager.SetVariable(name, value);
        }

        /// <summary>
        /// 获取变量值
        /// </summary>
        /// <param name="name">变量名</param>
        /// <returns>变量值</returns>
        public RuntimeValue GetVariable(string name)
        {
            return _variableManager.GetVariable(name);
        }

        /// <summary>
        /// 检查变量是否存在
        /// </summary>
        /// <param name="name">变量名</param>
        /// <returns>是否存在</returns>
        public bool HasVariable(string name)
        {
            return _variableManager.HasVariable(name);
        }

        /// <summary>
        /// 加载脚本变量（用于恢复状态）
        /// </summary>
        /// <param name="variables">要加载的脚本变量字典</param>
        public void LoadScriptVariables(Dictionary<string, RuntimeValue> variables)
        {
            _variableManager.LoadScriptVariables(variables);
        }

        /// <summary>
        /// 注册对象实例的方法作为脚本函数
        /// </summary>
        /// <param name="objectName">对象名称（用作函数名称的前缀）</param>
        /// <param name="instance">对象实例</param>
        public void RegisterObjectFunctions(string objectName, object instance)
        {
            _functionManager.RegisterObjectFunctions(objectName, instance);
        }

        /// <summary>
        /// 注册内置函数
        /// </summary>
        /// <param name="name">函数名</param>
        /// <param name="function">函数</param>
        public void RegisterFunction(string name, Delegate function)
        {
            _functionManager.RegisterFunction(name, function);
        }

        /// <summary>
        /// 获取所有已注册的脚本函数信息
        /// </summary>
        /// <returns>函数名和描述的字典</returns>
        public Dictionary<string, string> GetRegisteredFunctions()
        {
            return _functionManager.GetRegisteredScriptFunctions();
        }

        /// <summary>
        /// 调用函数
        /// </summary>
        /// <param name="name">函数名</param>
        /// <param name="args">参数</param>
        /// <returns>返回值</returns>
        public async Task<RuntimeValue> CallFunction(string name, List<RuntimeValue> args)
        {
            return await _functionManager.CallFunction(name, args);
        }

        /// <summary>
        /// 检查函数是否存在
        /// </summary>
        /// <param name="name">函数名</param>
        /// <returns>是否存在</returns>
        public bool HasFunction(string name)
        {
            return _functionManager.HasFunction(name);
        }

        /// <summary>
        /// 获取节点的元数据
        /// </summary>
        /// <param name="nodeName">节点名称</param>
        /// <param name="key">元数据键，为null则返回所有元数据</param>
        /// <returns>元数据值，节点不存在或键不存在时返回null</returns>
        public string GetMetadata(string nodeName, string key)
        {
            try
            {
                // 获取指定节点
                if (_nodes.TryGetValue(nodeName, out var node)) return node.Metadata.GetValueOrDefault(key, null);
                MLogger.Warning($"节点 {nodeName} 不存在");
                return null;

                // 如果元数据中不包含指定键，返回null
            }
            catch (Exception ex)
            {
                MLogger.Error($"获取元数据时出错: {ex}");
                return null;
            }
        }

        /// <summary>
        /// 获取节点的所有元数据
        /// </summary>
        /// <param name="nodeName">节点名称</param>
        /// <returns>节点的所有元数据，如果节点不存在则返回null</returns>
        public Dictionary<string, string> GetAllMetadata(string nodeName)
        {
            try
            {
                // 获取指定节点
                if (_nodes.TryGetValue(nodeName, out var node)) return node.Metadata;
                MLogger.Warning($"节点 {nodeName} 不存在");
                return null;

            }
            catch (Exception ex)
            {
                MLogger.Error($"获取元数据时出错: {ex}");
                return null;
            }
        }
    }
}