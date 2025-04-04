using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace MookDialogueScript
{
    /// <summary>
    /// 标记可在脚本中调用的函数
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class ScriptFuncAttribute : Attribute
    {
        /// <summary>
        /// 在脚本中使用的函数名
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 函数描述
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// 创建一个新的脚本函数特性
        /// </summary>
        /// <param name="name">在脚本中使用的函数名，如果为空则使用方法名</param>
        /// <param name="description">函数描述</param>
        public ScriptFuncAttribute(string name = "", string description = "")
        {
            Name = name;
            Description = description;
        }
    }

    /// <summary>
    /// 函数管理器，负责管理、注册和调用脚本中的函数
    /// </summary>
    public class FunctionManager
    {
        #region 字段和构造函数

        // 编译后的函数字典：函数名 -> 函数实现
        private readonly Dictionary<string, Func<List<RuntimeValue>, Task<RuntimeValue>>> _compiledFunctions =
            new Dictionary<string, Func<List<RuntimeValue>, Task<RuntimeValue>>>();

        // 函数元数据字典：函数名 -> (方法信息, 描述)
        private readonly Dictionary<string, (MethodInfo Method, string Description)> _functionMetadata =
            new Dictionary<string, (MethodInfo, string)>();

        // 任务处理器缓存：Task类型 -> 处理函数
        private readonly Dictionary<Type, Func<Task, Task<object>>> _taskResultHandlers =
            new Dictionary<Type, Func<Task, Task<object>>>();

        /// <summary>
        /// 初始化函数管理器
        /// </summary>
        public FunctionManager()
        {
            // 初始化任务处理器
            InitializeTaskHandlers();
        }

        #endregion

        #region 任务处理

        /// <summary>
        /// 初始化常用任务类型的处理器
        /// </summary>
        private void InitializeTaskHandlers()
        {
            // 注册基本类型的处理器
            RegisterTaskHandler<string>();
            RegisterTaskHandler<int>();
            RegisterTaskHandler<double>();
            RegisterTaskHandler<bool>();
            RegisterTaskHandler<object>();
            // 可以根据需要添加更多类型
        }

        /// <summary>
        /// 注册特定类型的任务处理器
        /// </summary>
        private void RegisterTaskHandler<T>()
        {
            _taskResultHandlers[typeof(Task<T>)] = async (task) =>
            {
                var typedTask = (Task<T>)task;
                await typedTask.ConfigureAwait(false);
                return typedTask.Result;
            };
        }

        /// <summary>
        /// 安全地获取任务的结果
        /// </summary>
        private async Task<object> GetTaskResultAsync(Task task)
        {
            if (task == null)
                return null;

            var taskType = task.GetType();

            // 使用预注册的处理器（性能更好）
            if (_taskResultHandlers.TryGetValue(taskType, out var handler))
            {
                return await handler(task);
            }

            // 回退到反射处理（兼容任意类型）
            await task.ConfigureAwait(false);

            // 获取泛型任务的结果
            if (taskType.IsGenericType && taskType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var resultProperty = taskType.GetProperty("Result");
                return resultProperty.GetValue(task);
            }

            return null; // Task 没有结果
        }

        #endregion

        #region 参数和类型处理

        /// <summary>
        /// 准备函数调用参数
        /// </summary>
        private object[] PrepareArguments(List<RuntimeValue> scriptArgs, ParameterInfo[] parameters)
        {
            object[] nativeArgs = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                if (i < scriptArgs.Count)
                {
                    // 提供了参数值，进行类型转换
                    nativeArgs[i] = ConvertToNativeType(scriptArgs[i], parameters[i].ParameterType);
                }
                else if (parameters[i].HasDefaultValue)
                {
                    // 使用参数的默认值
                    nativeArgs[i] = parameters[i].DefaultValue;
                }
                else
                {
                    // 使用类型的默认值
                    nativeArgs[i] = parameters[i].ParameterType.IsValueType
                        ? Activator.CreateInstance(parameters[i].ParameterType)
                        : null;
                }
            }

            return nativeArgs;
        }

        /// <summary>
        /// 将运行时值转换为原生类型
        /// </summary>
        private object ConvertToNativeType(RuntimeValue value, Type targetType)
        {
            if (value.Type == RuntimeValue.ValueType.Null)
            {
                return null;
            }

            // 数值类型转换
            if (targetType == typeof(double) || targetType == typeof(float) ||
                targetType == typeof(int) || targetType == typeof(long))
            {
                ValidateType(value, RuntimeValue.ValueType.Number, targetType.Name);
                return Convert.ChangeType(value.Value, targetType);
            }
            // 字符串类型转换
            else if (targetType == typeof(string))
            {
                ValidateType(value, RuntimeValue.ValueType.String, targetType.Name);
                return value.Value;
            }
            // 布尔类型转换
            else if (targetType == typeof(bool))
            {
                ValidateType(value, RuntimeValue.ValueType.Boolean, targetType.Name);
                return value.Value;
            }
            // 引用类型和可空值类型
            else if (targetType.IsClass || (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) != null))
            {
                return null;
            }

            // 不支持的类型
            Debug.LogError($"不支持的参数类型转换: {targetType.Name}");
            // 返回类型的默认值而不是抛出异常
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }

        /// <summary>
        /// 验证值类型
        /// </summary>
        private bool ValidateType(RuntimeValue value, RuntimeValue.ValueType expectedType, string typeName)
        {
            if (value.Type == expectedType) return true;
            Debug.LogError($"期望{GetTypeName(expectedType)}类型用于 '{typeName}'，但得到了{GetTypeName(value.Type)}");
            return false;
        }

        /// <summary>
        /// 获取类型名称的可读表示
        /// </summary>
        private string GetTypeName(RuntimeValue.ValueType type)
        {
            switch (type)
            {
                case RuntimeValue.ValueType.Number: return "数字";
                case RuntimeValue.ValueType.String: return "字符串";
                case RuntimeValue.ValueType.Boolean: return "布尔";
                case RuntimeValue.ValueType.Null: return "空";
                default: return type.ToString();
            }
        }

        /// <summary>
        /// 将对象转换为运行时值
        /// </summary>
        private RuntimeValue ConvertToRuntimeValue(object value)
        {
            if (value == null)
                return RuntimeValue.Null;

            if (value is double || value is int || value is float)
                return new RuntimeValue(Convert.ToDouble(value));
            else if (value is string strValue)
                return new RuntimeValue(strValue);
            else if (value is bool boolValue)
                return new RuntimeValue(boolValue);

            Debug.LogError($"不支持的返回值类型: {value.GetType().Name}，将返回空值");
            return RuntimeValue.Null; // 返回空值而不是抛出异常
        }

        #endregion

        #region 函数注册和调用

        /// <summary>
        /// 扫描并注册所有标记了ScriptFunc特性的方法
        /// </summary>
        public void ScanAndRegisterScriptFunctions()
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
                ScanAssemblyForScriptFunctions(assembly);
            }
        }

        /// <summary>
        /// 扫描程序集中的脚本函数
        /// </summary>
        private void ScanAssemblyForScriptFunctions(Assembly assembly)
        {
            try
            {
                // 获取所有公共静态方法
                var staticMethods = assembly.GetTypes()
                    .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                    .Where(m => m.GetCustomAttribute<ScriptFuncAttribute>() != null);

                foreach (var method in staticMethods)
                {
                    var attribute = method.GetCustomAttribute<ScriptFuncAttribute>();
                    string funcName = attribute.Name ?? method.Name;

                    // 创建并注册适配器
                    var adapter = CreateFunctionAdapter(method);
                    if (adapter != null)
                    {
                        _compiledFunctions[funcName] = adapter;
                        _functionMetadata[funcName] = (method, attribute.Description);
                    }
                    else
                    {
                        Debug.LogError($"无法为函数 '{funcName}' 创建适配器");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"扫描程序集 {assembly.FullName} 时出错: {ex.Message}");
                // 继续尝试扫描其他程序集
            }
        }

        /// <summary>
        /// 为方法创建适配器函数
        /// </summary>
        private Func<List<RuntimeValue>, Task<RuntimeValue>> CreateFunctionAdapter(MethodInfo method)
        {
            try
            {
                var parameters = method.GetParameters();
                var returnType = method.ReturnType;

                return async (args) =>
                {
                    try
                    {
                        object[] nativeArgs = PrepareArguments(args, parameters);
                        object result = await InvokeMethodAsync(method, null, nativeArgs, returnType);
                        return ConvertToRuntimeValue(result);
                    }
                    catch (TargetInvocationException ex)
                    {
                        Debug.LogError($"调用函数时出错: {ex.InnerException?.Message}");
                        return RuntimeValue.Null; // 返回空值而不是抛出异常
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"调用函数时出错: {ex.Message}");
                        return RuntimeValue.Null; // 返回空值而不是抛出异常
                    }
                };
            }
            catch (Exception ex)
            {
                Debug.LogError($"创建函数适配器时出错: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 通用的方法调用逻辑
        /// </summary>
        private async Task<object> InvokeMethodAsync(MethodInfo method, object instance, object[] args, Type returnType)
        {
            // 处理异步方法 - 有返回值
            if (returnType.IsAssignableFrom(typeof(Task<object>)) ||
                (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>)))
            {
                var task = (Task)method.Invoke(instance, args);
                return await GetTaskResultAsync(task);
            }
            // 处理异步方法 - 无返回值
            else if (returnType == typeof(Task))
            {
                var task = (Task)method.Invoke(instance, args);
                await task.ConfigureAwait(false);
                return null;
            }
            // 处理同步方法
            else
            {
                return method.Invoke(instance, args);
            }
        }

        /// <summary>
        /// 注册对象实例的方法作为脚本函数
        /// </summary>
        public void RegisterObjectFunctions(string objectName, object instance)
        {
            if (instance == null)
            {
                Debug.LogError("注册对象实例的方法作为脚本函数时，对象实例不能为null");
                return; // 直接返回而不是抛出异常
            }

            // 获取所有非Object基类的公共实例方法
            var methods = instance.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.DeclaringType != typeof(object));

            foreach (var method in methods)
            {
                string funcName = $"{objectName}__{method.Name}";
                try
                {
                    var adapter = CreateObjectMethodAdapter(method, instance, funcName);
                    if (adapter != null)
                    {
                        _compiledFunctions[funcName] = adapter;
                        _functionMetadata[funcName] = (method, $"{objectName}对象的{method.Name}方法");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"注册 {funcName} 时出错: {ex.Message}");
                    // 继续处理其他方法，不是抛出异常
                }
            }
        }

        /// <summary>
        /// 为对象方法创建适配器
        /// </summary>
        private Func<List<RuntimeValue>, Task<RuntimeValue>> CreateObjectMethodAdapter(
            MethodInfo method, object instance, string funcName)
        {
            var parameters = method.GetParameters();
            var returnType = method.ReturnType;

            return async (args) =>
            {
                try
                {
                    object[] nativeArgs = PrepareArguments(args, parameters);
                    object result = await InvokeMethodAsync(method, instance, nativeArgs, returnType);
                    return ConvertToRuntimeValue(result);
                }
                catch (TargetInvocationException ex)
                {
                    Debug.LogError($"调用函数 '{funcName}' 时出错: {ex.InnerException?.Message}");
                    return RuntimeValue.Null; // 返回空值而不是抛出异常
                }
                catch (Exception ex)
                {
                    Debug.LogError($"调用函数 '{funcName}' 时出错: {ex.Message}");
                    return RuntimeValue.Null; // 返回空值而不是抛出异常
                }
            };
        }

        /// <summary>
        /// 注册内置函数
        /// </summary>
        public void RegisterFunction(string name, Delegate function)
        {
            _compiledFunctions[name] = CompileFunction(function);

            // 添加到函数元数据
            var methodInfo = function.Method;
            string description = "内置函数";

            // 尝试获取函数的描述信息
            var attr = methodInfo.GetCustomAttribute<ScriptFuncAttribute>();
            if (attr != null && !string.IsNullOrEmpty(attr.Description))
            {
                description = attr.Description;
            }

            _functionMetadata[name] = (methodInfo, description);
        }

        /// <summary>
        /// 编译函数委托
        /// </summary>
        private Func<List<RuntimeValue>, Task<RuntimeValue>> CompileFunction(Delegate function)
        {
            var methodInfo = function.Method;
            var parameters = methodInfo.GetParameters();
            var returnType = methodInfo.ReturnType;

            return async (args) =>
            {
                // 准备参数
                object[] nativeArgs = PrepareArguments(args, parameters);

                // 调用并处理结果
                object result = null;

                if (returnType.IsAssignableFrom(typeof(Task<object>)) ||
                    returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    var task = (Task)function.DynamicInvoke(nativeArgs);
                    result = await GetTaskResultAsync(task);
                }
                else if (returnType == typeof(Task))
                {
                    var task = (Task)function.DynamicInvoke(nativeArgs);
                    await task.ConfigureAwait(false);
                }
                else
                {
                    result = function.DynamicInvoke(nativeArgs);
                }

                return ConvertToRuntimeValue(result);
            };
        }

        /// <summary>
        /// 注册脚本函数
        /// </summary>
        public void RegisterScriptFunction(string name, Func<List<RuntimeValue>, Task<RuntimeValue>> function)
        {
            _compiledFunctions[name] = function;
            _functionMetadata[name] = (null, $"脚本函数 {name}");
        }

        /// <summary>
        /// 检查函数是否存在
        /// </summary>
        /// <param name="name">函数名</param>
        /// <returns>是否存在</returns>
        public bool HasFunction(string name)
        {
            return _compiledFunctions.ContainsKey(name);
        }

        /// <summary>
        /// 获取所有已注册的脚本函数信息
        /// </summary>
        public Dictionary<string, string> GetRegisteredScriptFunctions()
        {
            var result = new Dictionary<string, string>();

            // 添加函数元数据
            foreach (var pair in _functionMetadata)
            {
                result[pair.Key] = pair.Value.Description;
            }

            // 添加手动注册的函数
            foreach (string key in _compiledFunctions.Keys)
            {
                result.TryAdd(key, "手动注册的函数");
            }

            return result;
        }

        /// <summary>
        /// 调用已注册的函数
        /// </summary>
        public async Task<RuntimeValue> CallFunction(string name, List<RuntimeValue> args)
        {
            if (_compiledFunctions.TryGetValue(name, out var func))
            {
                try
                {
                    return await func(args);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"调用函数 '{name}' 时出错: {ex.Message}");
                    return RuntimeValue.Null; // 返回空值
                }
            }

            Debug.LogError($"函数 '{name}' 未找到");
            return RuntimeValue.Null; // 返回空值而不是抛出异常
        }

        #endregion

        #region 内置函数

        /// <summary>
        /// 输出日志
        /// </summary>
        [ScriptFunc("cs_log", "输出日志消息")]
        public static void CsLog(string message)
        {
            Debug.Log($"[LOG] {message}");
        }

        /// <summary>
        /// 输出日志
        /// </summary>
        [ScriptFunc("log", "输出日志消息")]
        public static void Log(string message, string type = "log")
        {
            if (type == "log")
            {
                Debug.Log(message);
            }
            else if (type == "warn")
            {
                Debug.LogWarning(message);
            }
            else if (type == "error")
            {
                Debug.LogError(message);
            }
        }

        /// <summary>
        /// 连接字符串
        /// </summary>
        [ScriptFunc("concat", "连接字符串")]
        public static string Concat(string str1, string str2 = "", string str3 = "", string str4 = "",
            string str5 = "", string str6 = "", string str7 = "", string str8 = "")
        {
            return str1 + str2 + str3 + str4 + str5 + str6 + str7 + str8;
        }

        /// <summary>
        /// 返回一个介于 0 和 1 之间的随机数
        /// </summary>
        [ScriptFunc("random")]
        public static double Random_Float(int digits = 2)
        {
            return Math.Round(new System.Random().NextDouble(), digits);
        }

        /// <summary>
        /// 返回一个介于 min 和 max 之间的随机数
        /// </summary>
        [ScriptFunc("random_range")]
        public static double Random_Float_Range(float min, float max, int digits = 2)
        {
            return Math.Round(new System.Random().NextDouble() * (max - min) + min, digits);
        }

        /// <summary>
        /// 介于 1 和 sides 之间（含 1 和 sides ）的随机整数
        /// </summary>
        [ScriptFunc("dice")]
        public static int Random_Dice(int sides)
        {
            return new System.Random().Next(1, sides + 1);
        }

        #endregion
    }
}