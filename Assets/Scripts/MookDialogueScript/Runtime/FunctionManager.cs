using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace MookDialogueScript
{
    /// <summary>
    /// 标记可在脚本中调用的函数
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ScriptFuncAttribute : Attribute
    {
        /// <summary>
        /// 在脚本中使用的函数名
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 创建一个新的脚本函数特性
        /// </summary>
        /// <param name="name">在脚本中使用的函数名，如果为空则使用方法名</param>
        public ScriptFuncAttribute(string name = "")
        {
            Name = name;
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
            new Dictionary<string, Func<List<RuntimeValue>, Task<RuntimeValue>>>(StringComparer.OrdinalIgnoreCase);

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
            RegisterTaskHandler<long>();
            RegisterTaskHandler<float>();
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
                if (resultProperty != null) return resultProperty.GetValue(task);
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
                if (value.Type != RuntimeValue.ValueType.Number)
                {
                    MLogger.Error($"期望数字类型用于 '{targetType.Name}'，但得到了{GetTypeName(value.Type)}");
                }
                return Convert.ChangeType(value.Value, targetType);
            }
            // 字符串类型转换
            else if (targetType == typeof(string))
            {
                if (value.Type != RuntimeValue.ValueType.String)
                {
                    MLogger.Warning($"期望字符串类型用于 '{targetType.Name}'，但得到了{GetTypeName(value.Type)}");
                }
                return value.Value.ToString();
            }
            // 布尔类型转换
            else if (targetType == typeof(bool))
            {
                if (value.Type != RuntimeValue.ValueType.Boolean)
                {
                    MLogger.Error($"期望布尔类型用于 '{targetType.Name}'，但得到了{GetTypeName(value.Type)}");
                }
                return Convert.ChangeType(value.Value, targetType);
            }
            // 引用类型和可空值类型
            else if (targetType.IsClass || (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) != null))
            {
                return null;
            }

            // 不支持的类型
            MLogger.Error($"不支持的参数类型转换: {targetType.Name}");
            // 返回类型的默认值而不是抛出异常
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
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
                    MLogger.Error($"不支持的返回值类型: {value.GetType().Name}，将返回空值");
                    return RuntimeValue.Null;
            }

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

                    try
                    {
                        _compiledFunctions[funcName] = CompileMethod(method, null);
                    }
                    catch (Exception ex)
                    {
                        MLogger.Error($"无法为函数 '{funcName}' 创建适配器: {ex}");
                    }
                }
            }
            catch (Exception ex)
            {
                MLogger.Error($"扫描程序集 {assembly.FullName} 时出错: {ex}");
                // 继续尝试扫描其他程序集
            }
        }

        /// <summary>
        /// 编译方法为可执行函数
        /// </summary>
        private Func<List<RuntimeValue>, Task<RuntimeValue>> CompileMethod(MethodInfo method, object instance)
        {
            var parameters = method.GetParameters();
            var returnType = method.ReturnType;

            return async (args) =>
            {
                try
                {
                    // 准备参数
                    object[] nativeArgs = PrepareArguments(args, parameters);

                    // 调用并处理结果
                    object result = null;

                    if (returnType.IsAssignableFrom(typeof(Task<object>)) ||
                        returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
                    {
                        var task = (Task)method.Invoke(instance, nativeArgs);
                        result = await GetTaskResultAsync(task);
                    }
                    else if (returnType == typeof(Task))
                    {
                        var task = (Task)method.Invoke(instance, nativeArgs);
                        await task.ConfigureAwait(false);
                    }
                    else
                    {
                        result = method.Invoke(instance, nativeArgs);
                    }

                    return ConvertToRuntimeValue(result);
                }
                catch (Exception ex)
                {
                    MLogger.Error($"调用函数 '{method.Name}' 时出错: {ex}");
                    return RuntimeValue.Null;
                }
            };
        }

        /// <summary>
        /// 注册内置函数
        /// </summary>
        public void RegisterFunction(string name, Delegate function)
        {
            _compiledFunctions[name] = CompileMethod(function.Method, function.Target);
        }

        /// <summary>
        /// 注册对象实例的方法作为脚本函数
        /// </summary>
        public void RegisterObjectFunctions(string objectName, object instance)
        {
            if (instance == null)
            {
                MLogger.Error("注册对象实例的方法作为脚本函数时，对象实例不能为null");
                return;
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
                    _compiledFunctions[funcName] = CompileMethod(method, instance);
                }
                catch (Exception ex)
                {
                    MLogger.Error($"注册 {funcName} 时出错: {ex}");
                    // 继续处理其他方法
                }
            }
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
            foreach (string key in _compiledFunctions.Keys)
            {
                result[key] = "已注册的函数";
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
                    MLogger.Error($"调用函数 '{name}' 时出错: {ex}");
                    return RuntimeValue.Null; // 返回空值
                }
            }

            MLogger.Error($"函数 '{name}' 未找到");
            return RuntimeValue.Null; // 返回空值而不是抛出异常
        }

        #endregion

        #region 内置函数

        /// <summary>
        /// 输出日志
        /// </summary>
        [ScriptFunc("log")]
        public static void Log(string message, string type = "info")
        {
            switch (type)
            {
                case "info":
                    MLogger.Info(message);
                    break;
                case "warn":
                    MLogger.Warning(message);
                    break;
                case "error":
                    MLogger.Error(message);
                    break;
                default:
                    MLogger.Info(message);
                    break;
            }
        }

        /// <summary>
        /// 连接字符串
        /// </summary>
        [ScriptFunc("concat")]
        public static string Concat(string str1, string str2 = "", string str3 = "", string str4 = "",
            string str5 = "", string str6 = "", string str7 = "", string str8 = "",
            string str9 = "", string str10 = "", string str11 = "", string str12 = "",
            string str13 = "", string str14 = "", string str15 = "", string str16 = "")
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(str1)) sb.Append(str1);
            if (!string.IsNullOrEmpty(str2)) sb.Append(str2);
            if (!string.IsNullOrEmpty(str3)) sb.Append(str3);
            if (!string.IsNullOrEmpty(str4)) sb.Append(str4);
            if (!string.IsNullOrEmpty(str5)) sb.Append(str5);
            if (!string.IsNullOrEmpty(str6)) sb.Append(str6);
            if (!string.IsNullOrEmpty(str7)) sb.Append(str7);
            if (!string.IsNullOrEmpty(str8)) sb.Append(str8);
            if (!string.IsNullOrEmpty(str9)) sb.Append(str9);
            if (!string.IsNullOrEmpty(str10)) sb.Append(str10);
            if (!string.IsNullOrEmpty(str11)) sb.Append(str11);
            if (!string.IsNullOrEmpty(str12)) sb.Append(str12);
            if (!string.IsNullOrEmpty(str13)) sb.Append(str13);
            if (!string.IsNullOrEmpty(str14)) sb.Append(str14);
            if (!string.IsNullOrEmpty(str15)) sb.Append(str15);
            if (!string.IsNullOrEmpty(str16)) sb.Append(str16);
            return sb.ToString();
        }

        /// <summary>
        /// 返回一个介于 0 和 1 之间的随机数
        /// </summary>
        [ScriptFunc("random")]
        public static double Random(int digits = 2)
        {
            return Math.Round(new System.Random().NextDouble(), digits);
        }

        /// <summary>
        /// 返回一个介于 min 和 max 之间的随机数
        /// </summary>
        [ScriptFunc("random_range")]
        public static double Random_Range(float min, float max, int digits = 2)
        {
            return Math.Round(new System.Random().NextDouble() * (max - min) + min, digits);
        }

        /// <summary>
        /// 介于 1 和 sides 之间（含 1 和 sides ）的随机整数
        /// </summary>
        [ScriptFunc("dice")]
        public static int Dice(int sides)
        {
            return new System.Random().Next(1, sides + 1);
        }

        #endregion
    }
}