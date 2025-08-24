using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

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
        private Dictionary<string, RuntimeValue> _scriptVariables = new(StringComparer.OrdinalIgnoreCase);

        // 内置变量字典：变量名 -> (getter, setter)
        private readonly Dictionary<string, (Func<object> getter, Action<object> setter)> _builtinVariables = new(StringComparer.OrdinalIgnoreCase);
        #endregion

        #region 变量注册
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
                                MLogger.Error($"扫描属性 {property.Name} 时出错: {ex}");
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
                                MLogger.Error($"扫描字段 {field.Name} 时出错: {ex}");
                                // 继续处理其他字段
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MLogger.Error($"扫描类型 {type.FullName} 时出错: {ex}");
                        // 继续处理其他类型
                    }
                }
            }
            catch (Exception ex)
            {
                MLogger.Error($"扫描程序集 {assembly.FullName} 时出错: {ex}");
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
                ? (obj) => { MLogger.Error($"变量 '{varName}' 是只读的"); }
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
                ? (obj) => { MLogger.Error($"变量 '{varName}' 是只读的"); }
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
                MLogger.Error("注册对象实例的成员时，对象实例不能为null");
                return;
            }

            // 注册属性
            var properties = instance.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var property in properties)
            {
                var varName = $"{objectName}__{property.Name}";
                RegisterInstancePropertyAsVariable(property, instance, varName);
            }

            // 注册字段
            var fields = instance.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                var varName = $"{objectName}__{field.Name}";
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
                ? (obj) => { MLogger.Error($"字段 '{varName}' 是只读的"); }
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
                ? (obj) => { MLogger.Error($"属性 '{varName}' 是只读的"); }
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
                result[pair.Key] = Helper.ConvertToRuntimeValue(pair.Value.getter());
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
                result[pair.Key] = Helper.ConvertToRuntimeValue(pair.Value.getter());
            }

            // 添加脚本变量
            foreach (var pair in _scriptVariables)
            {
                result[pair.Key] = pair.Value;
            }

            return result;
        }

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
        /// 注册脚本变量
        /// </summary>
        /// <param name="name">变量名</param>
        /// <param name="value">变量值</param>
        public void RegisterScriptVariable(string name, RuntimeValue value)
        {
            _scriptVariables[name] = value;
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
                handlers.setter(Helper.ConvertToNativeType(value));
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
                return Helper.ConvertToRuntimeValue(handlers.getter());
            }

            if (_scriptVariables.TryGetValue(name, out var value))
            {
                return value;
            }

            MLogger.Error($"变量 '{name}' 未找到");
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

        /// <summary>
        /// 获取对象成员值
        /// </summary>
        /// <param name="target">目标对象</param>
        /// <param name="memberName">成员名称</param>
        /// <param name="context">对话上下文（可选，用于高级成员解析）</param>
        /// <returns>成员值</returns>
        public RuntimeValue GetObjectMember(RuntimeValue target, string memberName, object context = null)
        {
            // 在这里可以实现对象成员访问逻辑
            // 例如：访问字典键、对象属性等
            switch (target.Type)
            {
                case RuntimeValue.ValueType.Object when target.Value is System.Collections.Generic.Dictionary<string, object> dict:
                    if (dict.TryGetValue(memberName, out var value))
                    {
                        return Helper.ConvertToRuntimeValue(value);
                    }
                    MLogger.Warning($"字典中不存在键: {memberName}");
                    return RuntimeValue.Null;

                case RuntimeValue.ValueType.Object when target.Value != null:
                    // 尝试通过反射访问成员（受控方式）
                    return GetMemberThroughReflection(target.Value, memberName);

                default:
                    MLogger.Warning($"暂不支持对象成员访问: {target.Type}.{memberName}");
                    return RuntimeValue.Null;
            }
        }

        /// <summary>
        /// 通过高性能缓存反射获取成员值
        /// </summary>
        /// <param name="target">目标对象</param>
        /// <param name="memberName">成员名称</param>
        /// <returns>成员值</returns>
        private RuntimeValue GetMemberThroughReflection(object target, string memberName)
        {
            try
            {
                var type = target.GetType();
                
                // 使用缓存的成员访问器
                var accessor = Helper.GetMemberAccessor(type, memberName);
                if (accessor != null)
                {
                    switch (accessor.Type)
                    {
                        case MemberAccessor.AccessorType.Property:
                        case MemberAccessor.AccessorType.Field:
                            if (accessor.Getter != null)
                            {
                                var value = accessor.Getter(target);
                                return Helper.ConvertToRuntimeValue(value);
                            }
                            break;
                            
                        case MemberAccessor.AccessorType.Method:
                            // 返回方法的绑定委托（用于后续调用）
                            var method = accessor.Method;
                            var boundMethod = Helper.GetBoundMethod(target, memberName);
                            if (boundMethod != null)
                            {
                                return new RuntimeValue(boundMethod);
                            }
                            else
                            {
                                // 如果无法创建绑定委托，返回MethodInfo以供FunctionManager处理
                                return new RuntimeValue(method);
                            }
                    }
                }
                
                MLogger.Warning($"对象 {type.Name} 中不存在可访问的成员: {memberName}");
                return RuntimeValue.Null;
            }
            catch (Exception ex)
            {
                MLogger.Error($"缓存反射访问成员时出错: {ex.Message}");
                return RuntimeValue.Null;
            }
        }
        #endregion

    }
}
