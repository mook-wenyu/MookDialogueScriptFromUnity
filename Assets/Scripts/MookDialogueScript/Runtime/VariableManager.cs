using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;

namespace MookDialogueScript
{
    /// <summary>
    /// 标记可在脚本中访问的变量
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class ScriptVarAttribute : Attribute
    {
        /// <summary>
        /// 在脚本中使用的变量名
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 创建一个新的脚本变量特性
        /// </summary>
        /// <param name="name">在脚本中使用的变量名，如果为null则使用属性/字段名</param>
        public ScriptVarAttribute(string name = "")
        {
            Name = name;
        }
    }

    /// <summary>
    /// 变量管理器，负责管理、注册和访问脚本中的变量
    /// </summary>
    public class VariableManager
    {
        #region 字段和属性

        // 脚本定义的变量字典
        private Dictionary<string, RuntimeValue> _scriptVariables = new Dictionary<string, RuntimeValue>(StringComparer.OrdinalIgnoreCase);

        // 内置变量字典：变量名 -> (getter, setter)
        private Dictionary<string, (Func<object> getter, Action<object> setter)> _builtinVariables =
            new Dictionary<string, (Func<object> getter, Action<object> setter)>(StringComparer.OrdinalIgnoreCase);

        #endregion

        #region 变量注册

        /// <summary>
        /// 注册内置变量
        /// </summary>
        /// <param name="name">变量名</param>
        /// <param name="getter">获取变量值的委托</param>
        /// <param name="setter">设置变量值的委托</param>
        public void RegisterBuiltinVariable(string name, Func<object> getter, Action<object> setter)
        {
            _builtinVariables[name] = (getter, setter);
        }

        /// <summary>
        /// 扫描并注册所有标记了ScriptVar特性的静态属性和字段
        /// </summary>
        public void ScanAndRegisterScriptVariables()
        {
            var currentAssembly = Assembly.GetExecutingAssembly();

            // 获取相关程序集
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetReferencedAssemblies().Any(r => r.FullName == currentAssembly.FullName))
                .ToList();

            assemblies.Add(currentAssembly);

            // 扫描所有程序集
            foreach (var assembly in assemblies)
            {
                ScanAssemblyForScriptVariables(assembly);
            }
        }

        /// <summary>
        /// 扫描程序集中的脚本变量
        /// </summary>
        private void ScanAssemblyForScriptVariables(Assembly assembly)
        {
            try
            {
                // 获取程序集中的所有类型并扫描它们的静态成员
                foreach (var type in assembly.GetTypes())
                {
                    try
                    {
                        // 扫描静态属性
                        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Static))
                        {
                            try
                            {
                                var attribute = property.GetCustomAttribute<ScriptVarAttribute>();
                                if (attribute == null) continue;
                                string varName = attribute.Name ?? property.Name;
                                RegisterPropertyAsVariable(property, varName);
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"扫描属性 {property.Name} 时出错: {ex.Message}");
                                // 继续处理其他属性
                            }
                        }

                        // 扫描静态字段
                        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
                        {
                            try
                            {
                                var attribute = field.GetCustomAttribute<ScriptVarAttribute>();
                                if (attribute == null) continue;
                                string varName = attribute.Name ?? field.Name;
                                bool isReadOnly = field.IsInitOnly;
                                RegisterFieldAsVariable(field, varName, isReadOnly);
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"扫描字段 {field.Name} 时出错: {ex.Message}");
                                // 继续处理其他字段
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"扫描类型 {type.FullName} 时出错: {ex.Message}");
                        // 继续处理其他类型
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"扫描程序集 {assembly.FullName} 时出错: {ex.Message}");
                // 继续处理其他程序集
            }
        }

        /// <summary>
        /// 注册属性作为变量
        /// </summary>
        private void RegisterPropertyAsVariable(PropertyInfo property, string varName)
        {
            // 创建getter
            Func<object> getter = () => property.GetValue(null);

            // 创建setter
            Action<object> setter = !property.CanWrite
                ? (obj) => { Debug.LogError($"变量 '{varName}' 是只读的"); }
            : (obj) => property.SetValue(null, obj);

            _builtinVariables[varName] = (getter, setter);
        }

        /// <summary>
        /// 注册字段作为变量
        /// </summary>
        private void RegisterFieldAsVariable(FieldInfo field, string varName, bool isReadOnly)
        {
            // 创建getter
            Func<object> getter = () => field.GetValue(null);

            // 创建setter
            Action<object> setter = isReadOnly
                ? (obj) => { Debug.LogError($"变量 '{varName}' 是只读的"); throw new InvalidOperationException($"变量 '{varName}' 是只读的"); }
            : (obj) => field.SetValue(null, obj);

            // 注册变量
            RegisterBuiltinVariable(varName, getter, setter);
        }

        /// <summary>
        /// 注册对象实例的所有属性和字段作为脚本变量
        /// </summary>
        /// <param name="objectName">对象名称（用作变量名称的前缀）</param>
        /// <param name="instance">对象实例</param>
        public void RegisterObjectPropertiesAndFields(string objectName, object instance)
        {
            if (instance == null)
            {
                Debug.LogError("注册对象实例的成员时，对象实例不能为null");
                return;
            }

            // 注册属性
            var properties = instance.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var property in properties)
            {
                string varName = $"{objectName}__{property.Name}";
                RegisterInstancePropertyAsVariable(property, instance, varName);
            }

            // 注册字段
            var fields = instance.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                string varName = $"{objectName}__{field.Name}";
                RegisterInstanceFieldAsVariable(field, instance, varName);
            }
        }

        /// <summary>
        /// 注册实例字段作为变量
        /// </summary>
        private void RegisterInstanceFieldAsVariable(FieldInfo field, object instance, string varName)
        {
            // 创建getter
            Func<object> getter = () => field.GetValue(instance);

            // 创建setter
            Action<object> setter = field.IsInitOnly
                ? (obj) => { Debug.LogError($"字段 '{varName}' 是只读的"); }
                : (obj) => field.SetValue(instance, obj);

            // 注册变量
            RegisterBuiltinVariable(varName, getter, setter);
        }

        /// <summary>
        /// 注册实例属性作为变量
        /// </summary>
        private void RegisterInstancePropertyAsVariable(PropertyInfo property, object instance, string varName)
        {
            // 创建getter
            Func<object> getter = () => property.GetValue(instance);

            // 创建setter
            Action<object> setter = !property.CanWrite
                ? (obj) => { Debug.LogError($"属性 '{varName}' 是只读的"); }
            : (obj) => property.SetValue(instance, obj);

            // 注册变量
            RegisterBuiltinVariable(varName, getter, setter);
        }

        #endregion

        #region 变量管理

        /// <summary>
        /// 获取所有脚本变量（用于保存状态）
        /// </summary>
        /// <returns>脚本变量</returns>
        public Dictionary<string, RuntimeValue> GetScriptVariables()
        {
            return _scriptVariables;
        }

        /// <summary>
        /// 加载脚本变量（用于恢复状态）
        /// </summary>
        /// <param name="variables">要加载的脚本变量字典</param>
        public void LoadScriptVariables(Dictionary<string, RuntimeValue> variables)
        {
            _scriptVariables = variables;
        }

        /// <summary>
        /// 获取所有内置变量
        /// </summary>
        /// <returns>内置变量字典</returns>
        public Dictionary<string, RuntimeValue> GetBuiltinVariables()
        {
            var result = new Dictionary<string, RuntimeValue>();
            foreach (var pair in _builtinVariables)
            {
                result[pair.Key] = ConvertToRuntimeValue(pair.Value.getter());
            }
            return result;
        }

        /// <summary>
        /// 获取所有变量（包括内置变量和脚本变量）
        /// </summary>
        /// <returns>所有变量字典</returns>
        public Dictionary<string, RuntimeValue> GetAllVariables()
        {
            var result = new Dictionary<string, RuntimeValue>();

            // 添加内置变量
            foreach (var pair in _builtinVariables)
            {
                result[pair.Key] = ConvertToRuntimeValue(pair.Value.getter());
            }

            // 添加脚本变量
            foreach (var pair in _scriptVariables)
            {
                result[pair.Key] = pair.Value;
            }

            return result;
        }

        /// <summary>
        /// 设置变量值
        /// </summary>
        /// <param name="name">变量名</param>
        /// <param name="value">变量值</param>
        public void SetVariable(string name, RuntimeValue value)
        {
            if (_builtinVariables.TryGetValue(name, out var handlers))
            {
                handlers.setter(ConvertToNativeType(value));
            }
            else
            {
                _scriptVariables[name] = value;
            }
        }

        /// <summary>
        /// 获取变量值
        /// </summary>
        /// <param name="name">变量名</param>
        /// <returns>变量值</returns>
        public RuntimeValue GetVariable(string name)
        {
            if (_builtinVariables.TryGetValue(name, out var handlers))
            {
                return ConvertToRuntimeValue(handlers.getter());
            }

            if (_scriptVariables.TryGetValue(name, out var value))
            {
                return value;
            }

            Debug.LogError($"变量 '{name}' 未找到");
            // 返回空值而不是抛出异常
            return RuntimeValue.Null;
        }

        /// <summary>
        /// 检查变量是否存在
        /// </summary>
        /// <param name="name">变量名</param>
        /// <returns>是否存在</returns>
        public bool HasVariable(string name)
        {
            return _scriptVariables.ContainsKey(name) || _builtinVariables.ContainsKey(name);
        }

        #endregion

        #region 类型转换

        /// <summary>
        /// 将脚本运行时值转换为C#对象
        /// </summary>
        private object ConvertToNativeType(RuntimeValue value)
        {
            switch (value.Type)
            {
                case RuntimeValue.ValueType.Number:
                    return ConvertNumberToNativeType((double)value.Value);

                case RuntimeValue.ValueType.String:
                    return value.Value;

                case RuntimeValue.ValueType.Boolean:
                    return value.Value;

                case RuntimeValue.ValueType.Null:
                    return null;

                default:
                    Debug.LogError($"不支持的运行时值类型: {value.Type}");
                    return null; // 返回空值而不是抛出异常
            }
        }

        /// <summary>
        /// 将数字转换为最合适的原生类型
        /// </summary>
        private object ConvertNumberToNativeType(double number)
        {
            // 检查是否是整数
            if (Math.Abs(number - Math.Round(number)) < double.Epsilon)
            {
                // 如果是整数且在int范围内
                if (number >= int.MinValue && number <= int.MaxValue)
                    return (int)number;
                // 如果是整数但超出int范围
                return (long)number;
            }

            // 如果是小数
            return number;
        }

        /// <summary>
        /// 将C#对象转换为脚本运行时值
        /// </summary>
        private RuntimeValue ConvertToRuntimeValue(object value)
        {
            switch (value)
            {
                case null:
                    return RuntimeValue.Null;
                case double or int or float or long:
                    return new RuntimeValue(Convert.ToDouble(value));
                case string strValue:
                    return new RuntimeValue(strValue);
                case bool boolValue:
                    return new RuntimeValue(boolValue);
                default:
                    Debug.LogError($"不支持的内置变量类型: {value.GetType().Name}");
                    return RuntimeValue.Null; // 返回空值而不是抛出异常
            }

        }

        #endregion
    }
}