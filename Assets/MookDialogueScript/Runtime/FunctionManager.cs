using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Scripting;

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
    public class FunctionManager : IDisposable
    {
        #region 字段和构造函数
        // 编译后的函数字典：函数名 -> 函数实现
        private readonly Dictionary<string, Func<List<RuntimeValue>, Task<RuntimeValue>>> _compiledFunctions = new(StringComparer.OrdinalIgnoreCase);

        // 函数签名字典：函数名 -> 函数签名
        private readonly Dictionary<string, FunctionSignature> _functionSignatures = new(StringComparer.OrdinalIgnoreCase);

        // 读写锁，用于线程安全
        private readonly ReaderWriterLockSlim _functionsLock = new(LockRecursionPolicy.NoRecursion);

        // 任务处理器缓存：Task类型 -> 处理函数
        private readonly Dictionary<Type, Func<Task, Task<object>>> _taskResultHandlers = new();

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
                    // 修复命名bug：ScriptFuncAttribute构造函数默认name=""，需要用IsNullOrEmpty判断
                    string funcName = string.IsNullOrEmpty(attribute.Name) ? method.Name : attribute.Name;

                    try
                    {
                        _functionsLock.EnterWriteLock();
                        try
                        {
                            // 严格禁止重名：检查是否已存在同名函数（忽略大小写）
                            if (_compiledFunctions.ContainsKey(funcName))
                            {
                                var existingKey = _compiledFunctions.Keys.FirstOrDefault(k =>
                                    string.Equals(k, funcName, StringComparison.OrdinalIgnoreCase));

                                // 检查是否为大小写冲突
                                bool isCaseConflict = !string.Equals(existingKey, funcName, StringComparison.Ordinal);
                                string conflictType = isCaseConflict ? "大小写冲突" : "重名";

                                string errorMsg = $"脚本函数名 '{funcName}' 重复定义（{conflictType}）。" +
                                                  $"已存在函数：'{existingKey}'，尝试注册：'{funcName}' " +
                                                  $"（类型：{method.DeclaringType?.Name}，方法：{method.Name}）。" +
                                                  $"系统不支持重名/重载，请重命名。";

                                MLogger.Error(errorMsg);
                                continue; // 跳过此函数，继续处理其他函数
                            }

                            _compiledFunctions[funcName] = CompileMethod(method, null);
                        }
                        finally
                        {
                            _functionsLock.ExitWriteLock();
                        }
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
        /// 编译方法为高性能委托（增强版）
        /// </summary>
        private Func<List<RuntimeValue>, Task<RuntimeValue>> CompileMethod(MethodInfo method, object instance)
        {
            try
            {
                // 优先使用 Helper 的高性能编译委托
                return Helper.CompileMethodDelegate(method, instance);
            }
            catch (Exception ex)
            {
                MLogger.Warning($"无法编译方法 {method.Name}，使用传统实现: {ex.Message}");
                // 回退到传统实现
                return CompileMethodFallback(method, instance);
            }
        }

        /// <summary>
        /// 传统的方法编译实现（回退方案）
        /// </summary>
        private Func<List<RuntimeValue>, Task<RuntimeValue>> CompileMethodFallback(MethodInfo method, object instance)
        {
            var parameters = method.GetParameters();
            var returnType = method.ReturnType;

            return async (args) =>
            {
                try
                {
                    // 准备参数（CompileMethod 阶段无法获取位置信息，传递默认值）
                    object[] nativeArgs = PrepareArguments(args, parameters, 0, 0);

                    // 调用并处理结果
                    object result = null;

                    if (returnType == typeof(Task))
                    {
                        // 无返回值的异步方法
                        var task = (Task)method.Invoke(instance, nativeArgs);
                        await task.ConfigureAwait(false);
                        result = null;
                    }
                    else if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
                    {
                        // 有返回值的异步方法
                        var task = (Task)method.Invoke(instance, nativeArgs);
                        result = await GetTaskResultAsync(task);
                    }
                    else
                    {
                        // 同步方法
                        result = method.Invoke(instance, nativeArgs);
                    }

                    return Helper.ConvertToRuntimeValue(result);
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
            _functionsLock.EnterWriteLock();
            try
            {
                // 严格禁止重名：检查是否已存在同名函数（忽略大小写）
                if (_compiledFunctions.ContainsKey(name))
                {
                    var existingKey = _compiledFunctions.Keys.FirstOrDefault(k =>
                        string.Equals(k, name, StringComparison.OrdinalIgnoreCase));

                    // 检查是否为大小写冲突
                    bool isCaseConflict = !string.Equals(existingKey, name, StringComparison.Ordinal);
                    string conflictType = isCaseConflict ? "大小写冲突" : "重名";

                    string errorMsg = $"脚本函数名 '{name}' 重复定义（{conflictType}）。" +
                                      $"已存在函数：'{existingKey}'，尝试注册：'{name}' " +
                                      $"（委托类型：{function.Method.DeclaringType?.Name}，方法：{function.Method.Name}）。" +
                                      $"系统不支持重名/重载，请重命名。";

                    MLogger.Error(errorMsg);
                    return; // 拒绝覆盖，直接返回
                }

                _compiledFunctions[name] = CompileMethod(function.Method, function.Target);
                
                // 构建并注册函数签名
                var signature = CreateFunctionSignature(name, function.Method, function.Target);
                _functionSignatures[name] = signature;

                MLogger.Info($"成功注册函数：{signature.FormatSignature()}（来源：{signature.SourceType}）");
            }
            finally
            {
                _functionsLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// 从 MethodInfo 创建函数签名
        /// </summary>
        /// <param name="name">函数名</param>
        /// <param name="method">方法信息</param>
        /// <param name="instance">实例对象（静态方法为null）</param>
        /// <returns>函数签名</returns>
        private FunctionSignature CreateFunctionSignature(string name, MethodInfo method, object instance)
        {
            // 构建参数签名
            var parameters = method.GetParameters().Select(p => new FunctionParameter(
                p.Name ?? $"param{p.Position}",
                MapClrTypeToScriptType(p.ParameterType),
                p.HasDefaultValue,
                p.DefaultValue
            )).ToList();

            var signature = new FunctionSignature(
                name,
                MapClrTypeToScriptType(method.ReturnType),
                parameters,
                instance == null ? "静态函数" : "对象方法",
                method.DeclaringType?.Name,
                method.Name
            );

            return signature;
        }

        /// <summary>
        /// 将 CLR 类型映射到脚本类型名
        /// </summary>
        /// <param name="clrType">CLR 类型</param>
        /// <returns>脚本类型名</returns>
        private string MapClrTypeToScriptType(Type clrType)
        {
            if (clrType == null) return "Object";

            // 处理 Task 返回类型
            if (clrType == typeof(Task))
                return "Object";
            
            if (clrType.IsGenericType && clrType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var innerType = clrType.GetGenericArguments()[0];
                return MapClrTypeToScriptType(innerType);
            }

            return clrType switch
            {
                Type t when t == typeof(double) || t == typeof(float) || t == typeof(int) || 
                           t == typeof(long) || t == typeof(short) || t == typeof(byte) ||
                           t == typeof(decimal) || t == typeof(uint) || t == typeof(ulong) || 
                           t == typeof(ushort) || t == typeof(sbyte) => "Number",
                Type t when t == typeof(string) => "String",
                Type t when t == typeof(bool) => "Boolean",
                Type t when typeof(Delegate).IsAssignableFrom(t) => "Function",
                Type t when t == typeof(void) => "Object",
                _ => "Object"
            };
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

            // 获取所有非Object基类的公共实例方法，排除属性、事件等特殊方法
            var methods = instance.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.DeclaringType != typeof(object) && !m.IsSpecialName);

            var methodGroups = methods.GroupBy(m => m.Name);

            foreach (var methodGroup in methodGroups)
            {
                var methodArray = methodGroup.ToArray();
                if (methodArray.Length > 1)
                {
                    // 处理方法重载：警告并选择第一个
                    MLogger.Warning($"对象 '{objectName}' 的方法 '{methodGroup.Key}' 存在重载，将使用第一个重载");
                }

                var method = methodArray[0]; // 使用第一个重载
                var funcName = $"{objectName}.{method.Name}";

                try
                {
                    _functionsLock.EnterWriteLock();
                    try
                    {
                        // 严格禁止重名：检查是否已存在同名函数（忽略大小写）
                        if (_compiledFunctions.ContainsKey(funcName))
                        {
                            var existingKey = _compiledFunctions.Keys.FirstOrDefault(k =>
                                string.Equals(k, funcName, StringComparison.OrdinalIgnoreCase));

                            // 检查是否为大小写冲突
                            bool isCaseConflict = !string.Equals(existingKey, funcName, StringComparison.Ordinal);
                            string conflictType = isCaseConflict ? "大小写冲突" : "重名";

                            string errorMsg = $"脚本函数名 '{funcName}' 重复定义（{conflictType}）。" +
                                              $"已存在函数：'{existingKey}'，尝试注册：'{funcName}' " +
                                              $"（对象类型：{instance.GetType().Name}，方法：{method.Name}）。" +
                                              $"系统不支持重名/重载，请重命名。";

                            MLogger.Error(errorMsg);
                            continue; // 跳过此函数，继续处理其他函数
                        }

                        _compiledFunctions[funcName] = CompileMethod(method, instance);
                    }
                    finally
                    {
                        _functionsLock.ExitWriteLock();
                    }
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
            _functionsLock.EnterReadLock();
            try
            {
                return _compiledFunctions.ContainsKey(name);
            }
            finally
            {
                _functionsLock.ExitReadLock();
            }
        }

        /// <summary>
        /// 获取所有已注册的脚本函数信息
        /// </summary>
        public Dictionary<string, string> GetRegisteredScriptFunctions()
        {
            var result = new Dictionary<string, string>();
            _functionsLock.EnterReadLock();
            try
            {
                foreach (string key in _compiledFunctions.Keys)
                {
                    result[key] = "已注册的函数";
                }
            }
            finally
            {
                _functionsLock.ExitReadLock();
            }
            return result;
        }

        /// <summary>
        /// 尝试获取已注册的函数
        /// </summary>
        /// <param name="name">函数名</param>
        /// <param name="func">函数委托</param>
        /// <returns>是否找到函数</returns>
        public bool TryGet(string name, out Func<List<RuntimeValue>, Task<RuntimeValue>> func)
        {
            _functionsLock.EnterReadLock();
            try
            {
                return _compiledFunctions.TryGetValue(name, out func);
            }
            finally
            {
                _functionsLock.ExitReadLock();
            }
        }

        /// <summary>
        /// 获取所有已注册的函数名
        /// </summary>
        /// <returns>函数名列表</returns>
        public IEnumerable<string> GetAllFunctionNames()
        {
            _functionsLock.EnterReadLock();
            try
            {
                return _compiledFunctions.Keys.ToArray(); // 返回快照，避免锁外使用
            }
            finally
            {
                _functionsLock.ExitReadLock();
            }
        }

        /// <summary>
        /// 获取函数值（用于一等函数支持）
        /// </summary>
        /// <param name="name">函数名</param>
        /// <param name="line">行号（用于错误报告）</param>
        /// <param name="column">列号（用于错误报告）</param>
        /// <returns>函数值</returns>
        /// <exception cref="InterpreterException">当函数未找到时抛出异常</exception>
        public RuntimeValue GetFunctionValue(string name, int line = 0, int column = 0)
        {
            _functionsLock.EnterReadLock();
            try
            {
                if (_compiledFunctions.TryGetValue(name, out var func))
                {
                    return new RuntimeValue(func);
                }

                // 使用异常工厂创建带建议的异常
                throw ExceptionFactory.CreateFunctionNotFoundException(name, GetAllFunctionNames(), line, column);
            }
            finally
            {
                _functionsLock.ExitReadLock();
            }
        }

        /// <summary>
        /// 尝试获取函数值
        /// </summary>
        /// <param name="name">函数名</param>
        /// <param name="functionValue">输出的函数值</param>
        /// <returns>是否找到函数</returns>
        public bool TryGetFunctionValue(string name, out RuntimeValue functionValue)
        {
            _functionsLock.EnterReadLock();
            try
            {
                if (_compiledFunctions.TryGetValue(name, out var func))
                {
                    functionValue = new RuntimeValue(func);
                    return true;
                }

                functionValue = RuntimeValue.Null;
                return false;
            }
            finally
            {
                _functionsLock.ExitReadLock();
            }
        }

        /// <summary>
        /// 注册函数值作为变量（支持一等函数）
        /// </summary>
        /// <param name="name">变量名</param>
        /// <param name="functionValue">函数值</param>
        public void RegisterFunctionVariable(string name, Func<List<RuntimeValue>, Task<RuntimeValue>> functionValue)
        {
            // 这个方法主要用于在变量管理器中注册函数值
            // 实际存储由 VariableManager 处理
        }

        /// <summary>
        /// 调用函数值（用于一等函数调用）
        /// </summary>
        /// <param name="functionValue">函数值</param>
        /// <param name="args">参数列表</param>
        /// <param name="line">行号</param>
        /// <param name="column">列号</param>
        /// <returns>调用结果</returns>
        /// <exception cref="InterpreterException">当函数值无效或调用失败时抛出异常</exception>
        public async Task<RuntimeValue> CallFunctionValue(RuntimeValue functionValue, List<RuntimeValue> args, int line = 0, int column = 0)
        {
            // 检查函数类型
            if (functionValue.Type != ValueType.Function)
            {
                throw ExceptionFactory.CreateFunctionExpectedException(functionValue.Type.ToString(), line, column);
            }

            if (functionValue.Value is not Func<List<RuntimeValue>, Task<RuntimeValue>> func)
            {
                throw ExceptionFactory.CreateFunctionExpectedException("无效的函数值类型", line, column);
            }

            try
            {
                return await func(args);
            }
            catch (Exception ex)
            {
                throw ExceptionFactory.CreateFunctionInvokeFailException("函数值", ex, line, column);
            }
        }

        /// <summary>
        /// 调用已注册的函数
        /// </summary>
        /// <param name="name">函数名</param>
        /// <param name="args">参数列表</param>
        /// <param name="line">行号</param>
        /// <param name="column">列号</param>
        /// <returns>调用结果</returns>
        /// <exception cref="InterpreterException">当函数未找到或调用失败时抛出异常</exception>
        public async Task<RuntimeValue> CallFunction(string name, List<RuntimeValue> args, int line = 0, int column = 0)
        {
            Func<List<RuntimeValue>, Task<RuntimeValue>> func;
            
            _functionsLock.EnterReadLock();
            try
            {
                if (!_compiledFunctions.TryGetValue(name, out func))
                {
                    throw ExceptionFactory.CreateFunctionNotFoundException(name, GetAllFunctionNames(), line, column);
                }
            }
            finally
            {
                _functionsLock.ExitReadLock();
            }

            try
            {
                return await func(args);
            }
            catch (Exception ex)
            {
                throw ExceptionFactory.CreateFunctionInvokeFailException(name, ex, line, column);
            }
        }

        /// <summary>
        /// 调用已注册的函数（向后兼容的重载）
        /// </summary>
        public async Task<RuntimeValue> CallFunction(string name, List<RuntimeValue> args)
        {
            return await CallFunction(name, args, 0, 0);
        }
        #endregion

        /// <summary>
        /// 准备函数调用参数，包含严格的参数验证
        /// </summary>
        /// <param name="scriptArgs">脚本参数</param>
        /// <param name="parameters">方法参数信息</param>
        /// <param name="line">行号（用于错误报告）</param>
        /// <param name="column">列号（用于错误报告）</param>
        /// <returns>准备好的参数数组</returns>
        /// <exception cref="InterpreterException">当参数不匹配时抛出异常</exception>
        private object[] PrepareArguments(List<RuntimeValue> scriptArgs, ParameterInfo[] parameters, int line = 0, int column = 0)
        {
            // 检查必需参数数量
            int requiredParamCount = parameters.Count(p => !p.HasDefaultValue);
            if (scriptArgs.Count < requiredParamCount)
            {
                throw ExceptionFactory.CreateArgumentMismatchException(requiredParamCount, scriptArgs.Count, line, column);
            }

            // 检查最大参数数量
            if (scriptArgs.Count > parameters.Length)
            {
                throw ExceptionFactory.CreateArgumentMismatchException(parameters.Length, scriptArgs.Count, line, column);
            }

            var nativeArgs = new object[parameters.Length];

            for (var i = 0; i < parameters.Length; i++)
            {
                if (i < scriptArgs.Count)
                {
                    // 提供了参数值，进行类型转换
                    try
                    {
                        nativeArgs[i] = Helper.ConvertToNativeType(scriptArgs[i], parameters[i].ParameterType);
                    }
                    catch (Exception ex)
                    {
                        string message = $"参数 {i + 1} 类型转换失败：无法将 {scriptArgs[i].Type} 转换为 {parameters[i].ParameterType.Name}";
                        throw new InterpreterException(ErrorCode.ARG_MISMATCH, message, line, column, "检查参数类型是否匹配", ex);
                    }
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

        #region 内置函数
        // 单例随机数生成器，避免重复种子问题
        private static readonly Random _sharedRandom = new Random();
        private static readonly object _randomLock = new object();

        /// <summary>
        /// 输出日志
        /// </summary>
        [Preserve]
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
        [Preserve]
        [ScriptFunc("concat")]
        public static string Concat(string str1, string str2 = "", string str3 = "", string str4 = "",
            string str5 = "", string str6 = "", string str7 = "", string str8 = "",
            string str9 = "", string str10 = "", string str11 = "", string str12 = "",
            string str13 = "", string str14 = "", string str15 = "", string str16 = "")
        {
            var sb = new System.Text.StringBuilder();
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
        [Preserve]
        [ScriptFunc("random")]
        public static double Random(int digits = 2)
        {
            lock (_randomLock)
            {
                return Math.Round(_sharedRandom.NextDouble(), digits);
            }
        }

        /// <summary>
        /// 返回一个介于 min 和 max 之间的随机数
        /// </summary>
        [Preserve]
        [ScriptFunc("random_range")]
        public static double Random_Range(float min, float max, int digits = 2)
        {
            lock (_randomLock)
            {
                return Math.Round(_sharedRandom.NextDouble() * (max - min) + min, digits);
            }
        }

        /// <summary>
        /// 介于 1 和 sides 之间（含 1 和 sides ）的随机整数
        /// </summary>
        [Preserve]
        [ScriptFunc("dice")]
        public static int Dice(int sides, int count = 1)
        {
            lock (_randomLock)
            {
                var total = 0;
                for (var i = 0; i < count; i++)
                {
                    total += _sharedRandom.Next(1, sides + 1);
                }
                return total;
            }
        }
        /// <summary>
        /// 获取所有已注册的函数名
        /// </summary>
        /// <returns>函数名列表</returns>
        public IEnumerable<string> GetFunctionNames()
        {
            _functionsLock.EnterReadLock();
            try
            {
                return _compiledFunctions.Keys.ToList();
            }
            finally
            {
                _functionsLock.ExitReadLock();
            }
        }

        /// <summary>
        /// 获取函数签名
        /// </summary>
        /// <param name="name">函数名</param>
        /// <param name="line">错误行号（用于异常报告）</param>
        /// <param name="column">错误列号（用于异常报告）</param>
        /// <returns>函数签名，如果不存在或有重载冲突则返回null</returns>
        public FunctionSignature GetFunctionSignature(string name, int line = 0, int column = 0)
        {
            _functionsLock.EnterReadLock();
            try
            {
                if (_functionSignatures.TryGetValue(name, out var signature))
                    return signature;
                    
                // 检查是否有同名多个签名（重载冲突）
                var overloadCount = GetOverloadCount(name);
                if (overloadCount > 1)
                {
                    throw new SemanticException($"SEM016: 不支持重载：'{name}'，找到 {overloadCount} 个签名", 
                        line, column);
                }
                    
                return null;
            }
            finally
            {
                _functionsLock.ExitReadLock();
            }
        }

        /// <summary>
        /// 尝试获取函数签名
        /// </summary>
        /// <param name="name">函数名</param>
        /// <param name="signature">输出的函数签名</param>
        /// <returns>是否成功获取</returns>
        public bool TryGetFunctionSignature(string name, out FunctionSignature signature)
        {
            signature = GetFunctionSignature(name);
            return signature != null;
        }

        /// <summary>
        /// 获取所有函数签名
        /// </summary>
        /// <returns>函数名和签名的键值对集合</returns>
        public IEnumerable<KeyValuePair<string, FunctionSignature>> GetAllFunctionSignatures()
        {
            _functionsLock.EnterReadLock();
            try
            {
                return _functionSignatures.ToList();
            }
            finally
            {
                _functionsLock.ExitReadLock();
            }
        }

        /// <summary>
        /// 获取重载数量（在我们的系统中始终为0或1，不支持重载）
        /// </summary>
        /// <param name="name">函数名</param>
        /// <returns>重载数量</returns>
        private int GetOverloadCount(string name)
        {
            // 在当前实现中，我们不支持重载，每个函数名只能有一个签名
            return _functionSignatures.ContainsKey(name) ? 1 : 0;
        }

        /// <summary>
        /// 检测重载函数（返回是否存在同名函数）
        /// </summary>
        /// <param name="name">函数名</param>
        /// <returns>是否存在同名函数（重载）</returns>
        public bool HasOverloads(string name)
        {
            // 在我们的系统中，不支持重载，所以总是返回 false
            return false;
        }

        #endregion

        #region 资源管理
        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _functionsLock?.Dispose();
        }
        #endregion
    }
}
