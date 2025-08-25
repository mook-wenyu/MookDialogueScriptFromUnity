using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MookDialogueScript
{
    /// <summary>
    /// 脚本运行时值
    /// </summary>
    [Serializable]
    public readonly struct RuntimeValue : IEquatable<RuntimeValue>
    {
        /// <summary>
        /// 脚本运行时值类型
        /// </summary>
        public enum ValueType
        {
            Null,
            Number,
            Boolean,
            String,
            Object
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

        public RuntimeValue(object value)
        {
            Type = ValueType.Object;
            Value = value;
        }

        /// <summary>
        /// 创建一个空值的静态方法
        /// </summary>
        public static RuntimeValue Null => default;

        public override string ToString()
        {
            if (Value == null)
                return "null";
            return Type == ValueType.Boolean ? Value.ToString().ToLower() : Value.ToString();
        }

        /// <summary>
        /// 按“类型一致 + 值一致”的值语义比较。
        /// 注意：
        /// 1) 只有 Type 相同才比较具体值；
        /// 2) Number 使用 double.Equals 进行精确相等（不引入容差）；
        /// 3) String 使用 Ordinal 比较（区分大小写，文化无关）；
        /// 4) Null 与 Null 相等；
        /// 该实现需与 GetHashCode 保持一致：若 Equals(lhs, rhs) 为 true，则两者哈希必须相等。
        /// </summary>
        public bool Equals(RuntimeValue other)
        {
            // 类型不一致直接不相等
            if (Type != other.Type) return false;

            switch (Type)
            {
                case ValueType.Null:
                    return true; // 两个都是 Null，视为相等
                case ValueType.Number:
                    return ((double)Value).Equals((double)other.Value);
                case ValueType.Boolean:
                    return (bool)Value == (bool)other.Value;
                case ValueType.String:
                    return string.Equals((string)Value, (string)other.Value, StringComparison.Ordinal);
                default:
                    return Equals(Value, other.Value);
            }
        }

        public override bool Equals(object obj)
        {
            return obj is RuntimeValue other && Equals(other);
        }

        /// <summary>
        /// 生成与 Equals 一致的哈希值。
        /// 规则：先混入 Type 的哈希，再根据具体类型混入对应值的哈希。
        /// 这样保证：若两个实例 Equals 返回 true，则其哈希必然相等（满足字典/集合要求）。
        /// 采用经典“乘以质数再相加”的方式扩散位分布，降低碰撞概率。
        /// 特别说明：
        /// - Number 使用 double.GetHashCode()
        /// - String 使用其自身哈希（与 Ordinal 相等语义兼容）
        /// - Null 仅使用类型哈希
        /// </summary>
        public override int GetHashCode()
        {
            // 组合 Type 与具体值的哈希
            // 使用 unchecked 允许哈希混合中整数乘加的自然溢出（环绕），避免异常与不必要的溢出检查开销
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + Type.GetHashCode();
                switch (Type)
                {
                    case ValueType.Null:
                        return hash;
                    case ValueType.Number:
                        return hash * 31 + ((double)Value).GetHashCode();
                    case ValueType.Boolean:
                        return hash * 31 + ((bool)Value).GetHashCode();
                    case ValueType.String:
                    {
                        var s = (string)Value;
                        return hash * 31 + (s?.GetHashCode() ?? 0);
                    }
                    default:
                        return hash * 31 + (Value?.GetHashCode() ?? 0);
                }
            }
        }

        /// <summary>
        /// 运算符重载：与 Equals 一致的值语义相等/不等。
        /// 注意必须与 Equals/GetHashCode 语义保持一致，避免集合/字典行为异常。
        /// </summary>
        public static bool operator ==(RuntimeValue left, RuntimeValue right) => left.Equals(right);
        public static bool operator !=(RuntimeValue left, RuntimeValue right) => !left.Equals(right);
    }

    /// <summary>
    /// 对话上下文，管理变量、函数和节点
    /// </summary>
    public class DialogueContext
    {
        private readonly VariableManager _variableManager = new();
        private readonly FunctionManager _functionManager = new();
        private readonly Dictionary<string, NodeDefinitionNode> _nodes = new();

        // 对象注册映射
        private readonly Dictionary<string, object> _nameToObject = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<object, string> _objectToName = new();

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
            if (_nodes.TryGetValue(name, out var value))
                return value;

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
        public void RegisterObjectOnlyPropertiesAndFields(string objectName, object instance)
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
        /// 注册脚本变量
        /// </summary>
        /// <param name="name">变量名</param>
        /// <param name="value">变量值</param>
        public void RegisterScriptVariable(string name, RuntimeValue value)
        {
            _variableManager.RegisterScriptVariable(name, value);
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
        public void RegisterObjectOnlyFunctions(string objectName, object instance)
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
        /// 尝试获取函数委托
        /// </summary>
        /// <param name="name">函数名</param>
        /// <param name="func">函数委托</param>
        /// <returns>是否找到函数</returns>
        public bool TryGetFunction(string name, out Func<List<RuntimeValue>, Task<RuntimeValue>> func)
        {
            return _functionManager.TryGet(name, out func);
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
                if (_nodes.TryGetValue(nodeName, out var node))
                    return node.Metadata.GetValueOrDefault(key, null);

                MLogger.Warning($"节点 {nodeName} 不存在");
                return null;
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
                if (_nodes.TryGetValue(nodeName, out var node))
                    return node.Metadata;

                MLogger.Warning($"节点 {nodeName} 不存在");
                return null;
            }
            catch (Exception ex)
            {
                MLogger.Error($"获取元数据时出错: {ex}");
                return null;
            }
        }

        /// <summary>
        /// 获取对象成员值
        /// </summary>
        /// <param name="target">目标对象</param>
        /// <param name="memberName">成员名称</param>
        /// <returns>成员值</returns>
        public async Task<RuntimeValue> GetObjectMember(RuntimeValue target, string memberName)
        {
            // 直接委托给VariableManager处理，它会利用传入的context进行高级解析
            var result = _variableManager.GetObjectMember(target, memberName, this);

            // 如果返回的是MethodReference，需要转换为实际的函数委托
            if (result.Type == RuntimeValue.ValueType.Object && result.Value is MethodReference methodRef)
            {
                if (TryGetFunction(methodRef.FunctionKey, out var func))
                {
                    MLogger.Debug($"将方法引用 '{methodRef}' 转换为可调用委托");
                    return new RuntimeValue(func);
                }
                else
                {
                    MLogger.Warning($"无法找到方法引用 '{methodRef}' 对应的函数委托");
                    return RuntimeValue.Null;
                }
            }

            return await Task.FromResult(result);
        }

        /// <summary>
        /// 注册对象实例，使其可以通过名称访问
        /// 会同时注册对象的方法（函数）、属性和字段
        /// </summary>
        /// <param name="name">对象名称</param>
        /// <param name="instance">对象实例</param>
        public void RegisterObject(string name, object instance)
        {
            if (instance == null)
            {
                MLogger.Warning($"试图注册空对象: {name}");
                return;
            }

            // 移除旧的映射
            if (_nameToObject.TryGetValue(name, out var oldInstance))
            {
                _objectToName.Remove(oldInstance);
            }
            if (_objectToName.TryGetValue(instance, out var oldName))
            {
                _nameToObject.Remove(oldName);
            }

            // 添加新的映射
            _nameToObject[name] = instance;
            _objectToName[instance] = name;

            // 注册对象的所有成员
            RegisterObjectOnlyFunctions(name, instance);           // 注册方法（函数）
            RegisterObjectOnlyPropertiesAndFields(name, instance); // 注册属性和字段
        }

        /// <summary>
        /// 尝试通过名称获取对象实例
        /// </summary>
        /// <param name="name">对象名称</param>
        /// <param name="instance">对象实例</param>
        /// <returns>是否找到对象</returns>
        public bool TryGetObjectByName(string name, out object instance)
        {
            return _nameToObject.TryGetValue(name, out instance);
        }

        /// <summary>
        /// 尝试通过对象实例获取名称
        /// </summary>
        /// <param name="instance">对象实例</param>
        /// <param name="name">对象名称</param>
        /// <returns>是否找到名称</returns>
        public bool TryGetObjectName(object instance, out string name)
        {
            return _objectToName.TryGetValue(instance, out name);
        }

        /// <summary>
        /// 获取所有已注册的对象信息
        /// </summary>
        /// <returns>对象名称到类型名称的映射</returns>
        public Dictionary<string, string> GetRegisteredObjects()
        {
            var result = new Dictionary<string, string>();
            foreach (var kvp in _nameToObject)
            {
                result[kvp.Key] = kvp.Value.GetType().Name;
            }
            return result;
        }

        /// <summary>
        /// 清理所有缓存（包括Helper缓存和本地缓存）
        /// </summary>
        public void ClearAllCaches()
        {
            Helper.ClearCache();
        }

        /// <summary>
        /// 获取系统性能统计信息
        /// </summary>
        /// <returns>性能统计字典</returns>
        public Dictionary<string, object> GetPerformanceStatistics()
        {
            var stats = new Dictionary<string, object>
            {
                ["RegisteredNodes"] = _nodes.Count,
                ["RegisteredObjects"] = _nameToObject.Count,
                ["RegisteredFunctions"] = _functionManager.GetRegisteredScriptFunctions().Count,
                ["AllVariables"] = GetAllVariables().Count
            };

            // 添加Helper缓存统计
            var cacheStats = Helper.GetCacheStatistics();
            foreach (var kvp in cacheStats)
            {
                stats[$"Cache_{kvp.Key}"] = kvp.Value;
            }

            return stats;
        }
    }
}
