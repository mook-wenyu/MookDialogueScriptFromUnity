using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace MookDialogueScript
{
    /// <summary>
    /// 表示注册对象的方法引用
    /// </summary>
    public class MethodReference
    {
        /// <summary>
        /// 对象名称
        /// </summary>
        public string ObjectName { get; }

        /// <summary>
        /// 方法名称
        /// </summary>
        public string MethodName { get; }

        /// <summary>
        /// 函数键（用于在FunctionManager中查找）
        /// </summary>
        public string FunctionKey { get; }

        /// <summary>
        /// 创建方法引用
        /// </summary>
        /// <param name="objectName">对象名称</param>
        /// <param name="methodName">方法名称</param>
        /// <param name="functionKey">函数键</param>
        public MethodReference(string objectName, string methodName, string functionKey)
        {
            ObjectName = objectName ?? throw new ArgumentNullException(nameof(objectName));
            MethodName = methodName ?? throw new ArgumentNullException(nameof(methodName));
            FunctionKey = functionKey ?? throw new ArgumentNullException(nameof(functionKey));
        }

        /// <summary>
        /// 返回方法引用的字符串表示
        /// </summary>
        public override string ToString()
        {
            return $"{ObjectName}.{MethodName}";
        }
    }
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
                // 使用点号格式注册变量
                var varName = $"{objectName}.{property.Name}";
                RegisterInstancePropertyAsVariable(property, instance, varName);
            }

            // 注册字段
            var fields = instance.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                // 使用点号格式注册变量
                var varName = $"{objectName}.{field.Name}";
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
        /// 获取可序列化的脚本变量（排除Function类型）
        /// </summary>
        /// <returns>可序列化的脚本变量字典</returns>
        public Dictionary<string, RuntimeValue> GetSerializableScriptVariables()
        {
            var result = new Dictionary<string, RuntimeValue>();
            
            foreach (var kvp in _scriptVariables)
            {
                // 跳过Function类型的变量，因为它们不能被序列化
                if (kvp.Value.Type == RuntimeValue.ValueType.Function)
                {
                    continue;
                }
                
                // 对于包含MethodReference的Object类型，创建序列化标记
                if (kvp.Value.Type == RuntimeValue.ValueType.Object && kvp.Value.Value is MethodReference methodRef)
                {
                    // 创建一个特殊的序列化标记，用于重绑定
                    var rebindInfo = new Dictionary<string, object>
                    {
                        ["__type"] = "MethodReference",
                        ["objectName"] = methodRef.ObjectName,
                        ["methodName"] = methodRef.MethodName,
                        ["functionKey"] = methodRef.FunctionKey
                    };
                    result[kvp.Key] = new RuntimeValue(rebindInfo);
                }
                else
                {
                    result[kvp.Key] = kvp.Value;
                }
            }
            
            return result;
        }

        /// <summary>
        /// 加载脚本变量（用于恢复状态），包括函数重绑定
        /// </summary>
        /// <param name="variables">要加载的脚本变量字典</param>
        /// <param name="context">对话上下文，用于重绑定函数</param>
        public void LoadScriptVariables(Dictionary<string, RuntimeValue> variables, DialogueContext context = null)
        {
            _scriptVariables = new Dictionary<string, RuntimeValue>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var kvp in variables)
            {
                // 检查是否为MethodReference的序列化标记
                if (kvp.Value.Type == RuntimeValue.ValueType.Object && 
                    kvp.Value.Value is Dictionary<string, object> dict &&
                    dict.ContainsKey("__type") &&
                    dict["__type"].ToString() == "MethodReference")
                {
                    // 尝试重绑定MethodReference
                    if (TryRebindMethodReference(dict, context, out var reboundValue))
                    {
                        _scriptVariables[kvp.Key] = reboundValue;
                    }
                    else
                    {
                        // 重绑定失败，记录统一错误码
                        string errorMsg = $"序列化重绑定失败：变量 '{kvp.Key}' 的方法引用无法重绑定";
                        if (context != null)
                        {
                            // 如果有context，可以创建更详细的错误
                            MLogger.Error($"[{ErrorCode.SER_REBIND_FAIL}] {errorMsg}");
                        }
                        else
                        {
                            MLogger.Warning(errorMsg + "（未提供DialogueContext）");
                        }
                        // 可以选择保留原始信息或跳过
                        // _scriptVariables[kvp.Key] = kvp.Value;
                    }
                }
                else
                {
                    _scriptVariables[kvp.Key] = kvp.Value;
                }
            }
        }

        /// <summary>
        /// 尝试重绑定方法引用
        /// </summary>
        /// <param name="rebindInfo">重绑定信息</param>
        /// <param name="context">对话上下文</param>
        /// <param name="reboundValue">重绑定后的值</param>
        /// <returns>是否成功重绑定</returns>
        private bool TryRebindMethodReference(Dictionary<string, object> rebindInfo, DialogueContext context, out RuntimeValue reboundValue)
        {
            reboundValue = RuntimeValue.Null;
            
            try
            {
                if (!rebindInfo.ContainsKey("objectName") || 
                    !rebindInfo.ContainsKey("methodName") || 
                    !rebindInfo.ContainsKey("functionKey"))
                {
                    return false;
                }

                string objectName = rebindInfo["objectName"].ToString();
                string methodName = rebindInfo["methodName"].ToString();
                string functionKey = rebindInfo["functionKey"].ToString();

                // 检查函数管理器中是否存在对应的函数
                if (context?.HasFunction(functionKey) == true)
                {
                    // 重新创建MethodReference
                    var methodRef = new MethodReference(objectName, methodName, functionKey);
                    reboundValue = new RuntimeValue(methodRef);
                    return true;
                }
                
                MLogger.Warning($"重绑定失败：函数键 '{functionKey}' 在当前函数管理器中不存在");
                return false;
            }
            catch (Exception ex)
            {
                MLogger.Error($"重绑定MethodReference时发生异常: {ex.Message}");
                return false;
            }
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
        public RuntimeValue GetObjectMember(RuntimeValue target, string memberName, DialogueContext context = null)
        {
            // 如果提供了对话上下文，优先使用高级成员解析
            if (context != null)
            {
                var advancedResult = TryAdvancedMemberResolution(target, memberName, context);
                if (advancedResult.HasValue)
                {
                    return advancedResult.Value;
                }
            }

            // 回退到基础成员访问逻辑
            return GetBasicObjectMember(target, memberName, context);
        }

        /// <summary>
        /// 尝试高级成员解析（利用对话上下文）
        /// </summary>
        /// <param name="target">目标对象</param>
        /// <param name="memberName">成员名称</param>
        /// <param name="context">对话上下文</param>
        /// <returns>解析结果，如果无法解析则返回null</returns>
        private RuntimeValue? TryAdvancedMemberResolution(RuntimeValue target, string memberName, DialogueContext context)
        {
            if (target.Type != RuntimeValue.ValueType.Object || target.Value == null)
            {
                return null;
            }

            // 1. 优先检查注册对象的预编译方法委托
            if (context.TryGetObjectName(target.Value, out var objectName))
            {
                // 尝试获取预编译的方法委托
                string functionKey = $"{objectName}.{memberName}";
                if (context.HasFunction(functionKey))
                {
                    MLogger.Debug($"找到注册对象 '{objectName}' 的预编译方法: {memberName}");
                    // 创建一个方法引用对象，包含必要的调用信息
                    var methodRef = new MethodReference(objectName, memberName, functionKey);
                    return new RuntimeValue(methodRef);
                }

                // 2. 尝试通过点号格式查找注册的变量
                string variableKey = $"{objectName}.{memberName}";
                if (context.HasVariable(variableKey))
                {
                    MLogger.Debug($"找到注册对象 '{objectName}' 的变量: {memberName}");
                    return context.GetVariable(variableKey);
                }

                // 3. 尝试通过上下文获取更详细的成员信息
                var contextualResult = TryContextualMemberAccess(target, memberName, context, objectName);
                if (contextualResult.HasValue)
                {
                    return contextualResult.Value;
                }

                MLogger.Debug($"注册对象 '{objectName}' 中未找到成员 '{memberName}'，回退到反射访问");
            }

            return null; // 无法通过高级解析处理
        }

        /// <summary>
        /// 尝试上下文相关的成员访问
        /// </summary>
        /// <param name="target">目标对象</param>
        /// <param name="memberName">成员名称</param>
        /// <param name="context">对话上下文</param>
        /// <param name="objectName">对象名称</param>
        /// <returns>解析结果</returns>
        private RuntimeValue? TryContextualMemberAccess(RuntimeValue target, string memberName, DialogueContext context, string objectName)
        {
            try
            {
                // 基于对象实例检查成员类型，提供更精确的方法引用
                var instance = target.Value;
                var type = instance.GetType();

                // 使用Helper缓存的反射访问器
                var accessor = Helper.GetMemberAccessor(type, memberName);
                if (accessor != null)
                {
                    switch (accessor.Type)
                    {
                        case MemberAccessor.AccessorType.Method:
                            // 确保MethodReference的FunctionKey与FunctionManager注册键一致
                            string functionKey = $"{objectName}.{memberName}";
                            if (context.HasFunction(functionKey))
                            {
                                MLogger.Debug($"找到上下文方法引用: {functionKey}");
                                var methodRef = new MethodReference(objectName, memberName, functionKey);
                                return new RuntimeValue(methodRef);
                            }
                            else
                            {
                                MLogger.Warning($"方法 {functionKey} 在FunctionManager中未注册，但在反射中可见");
                                // 创建运行时绑定方法
                                return Helper.CreateBoundMethod(instance, memberName);
                            }

                        case MemberAccessor.AccessorType.Property:
                        case MemberAccessor.AccessorType.Field:
                            // 属性和字段直接通过Helper转换
                            if (accessor.Getter != null)
                            {
                                var value = accessor.Getter(instance);
                                return Helper.ConvertToRuntimeValue(value);
                            }
                            break;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                MLogger.Warning($"上下文成员访问时出错: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 检测并报告命名冲突
        /// </summary>
        /// <param name="objectName">对象名称</param>
        /// <param name="memberName">成员名称</param>
        /// <param name="context">对话上下文</param>
        private void DetectNamingConflicts(string objectName, string memberName, DialogueContext context)
        {
            string key = $"{objectName}.{memberName}";

            bool hasFunction = context.HasFunction(key);
            bool hasVariable = context.HasVariable(key);

            if (hasFunction && hasVariable)
            {
                MLogger.Warning($"检测到命名冲突: '{key}' 同时存在方法和变量。" +
                                $"访问时将优先使用方法，如需访问变量请考虑重命名。");
            }
        }

        /// <summary>
        /// 基础对象成员访问（原有逻辑）
        /// </summary>
        /// <param name="target">目标对象</param>
        /// <param name="memberName">成员名称</param>
        /// <param name="context">对话上下文</param>
        /// <returns>成员值</returns>
        private RuntimeValue GetBasicObjectMember(RuntimeValue target, string memberName, DialogueContext context = null)
        {
            switch (target.Type)
            {
                case RuntimeValue.ValueType.Object when target.Value is Dictionary<string, object> dict:
                    if (dict.TryGetValue(memberName, out var value))
                    {
                        return Helper.ConvertToRuntimeValue(value);
                    }
                    MLogger.Warning($"字典中不存在键: {memberName}");
                    return RuntimeValue.Null;

                case RuntimeValue.ValueType.Object when target.Value != null:
                    // 尝试通过反射访问成员（受控方式）
                    return GetMemberThroughReflection(target.Value, memberName, context);

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
        /// <param name="context">对话上下文</param>
        /// <returns>成员值</returns>
        private RuntimeValue GetMemberThroughReflection(object target, string memberName, DialogueContext context)
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
                            // 统一返回MethodReference，而不是直接返回绑定方法
                            var method = accessor.Method;
                            
                            // 尝试通过context获取对象名
                            if (context.TryGetObjectName(target, out var objectName))
                            {
                                string functionKey = $"{objectName}.{memberName}";
                                return new RuntimeValue(new MethodReference(objectName, memberName, functionKey));
                            }
                            else
                            {
                                // 如果对象未注册，尝试创建绑定委托作为回退
                                var boundMethod = Helper.GetBoundMethod(target, memberName);
                                if (boundMethod != null)
                                {
                                    return new RuntimeValue(boundMethod);
                                }
                                else
                                {
                                    MLogger.Warning($"对象方法 {memberName} 未绑定到函数管理器且无法创建委托");
                                    return RuntimeValue.Null;
                                }
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
