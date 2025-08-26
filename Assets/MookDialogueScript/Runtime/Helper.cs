using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine.Scripting;

namespace MookDialogueScript
{
    /// <summary>
    /// 成员访问器，用于缓存反射操作
    /// </summary>
    public class MemberAccessor
    {
        public enum AccessorType
        {
            Property,
            Field,
            Method
        }

        public AccessorType Type { get; }
        public string Name { get; }
        public Func<object, object> Getter { get; }
        public Action<object, object> Setter { get; }
        public MethodInfo Method { get; } // 单一方法，不支持重载

        public MemberAccessor(PropertyInfo property)
        {
            Type = AccessorType.Property;
            Name = property.Name;
            Getter = property.CanRead ? (obj) => property.GetValue(obj) : null;
            Setter = property.CanWrite ? (obj, value) => property.SetValue(obj, value) : null;
        }

        public MemberAccessor(FieldInfo field)
        {
            Type = AccessorType.Field;
            Name = field.Name;
            Getter = (obj) => field.GetValue(obj);
            Setter = field.IsInitOnly ? null : (obj, value) => field.SetValue(obj, value);
        }

        public MemberAccessor(MethodInfo method)
        {
            Type = AccessorType.Method;
            Name = method.Name;
            Method = method;
        }
    }

    /// <summary>
    /// 辅助类，提供类型转换和性能缓存功能
    /// </summary>
    [Preserve]
    public static class Helper
    {
        #region 性能缓存
        // 类型成员访问缓存：避免重复反射查找
        private static readonly Dictionary<Type, Dictionary<string, MemberAccessor>> _memberCache =
            new Dictionary<Type, Dictionary<string, MemberAccessor>>();

        // 绑定方法缓存：缓存已编译的方法委托
        private static readonly Dictionary<(Type, string), Delegate> _boundMethodCache =
            new Dictionary<(Type, string), Delegate>();

        // 函数值缓存：缓存创建的函数值
        private static readonly Dictionary<(object, string), Func<List<RuntimeValue>, Task<RuntimeValue>>> _functionCache =
            new Dictionary<(object, string), Func<List<RuntimeValue>, Task<RuntimeValue>>>();

        // 编译委托缓存：高性能委托编译缓存
        private static readonly ConcurrentDictionary<string, Func<List<RuntimeValue>, Task<RuntimeValue>>> 
            _compiledFunctions = new();

        // 类型转换缓存：缓存常用类型转换器
        private static readonly Dictionary<(Type, Type), Func<object, object>> _conversionCache =
            new Dictionary<(Type, Type), Func<object, object>>();

        // 大小写不敏感的字符串比较器
        private static readonly StringComparer _stringComparer = StringComparer.OrdinalIgnoreCase;
        
        // 缓存性能统计
        private static long _memberCacheHits = 0;
        private static long _memberCacheMisses = 0;
        private static long _methodCacheHits = 0;
        private static long _methodCacheMisses = 0;
        private static long _functionCacheHits = 0;
        private static long _functionCacheMisses = 0;
        private static long _conversionCacheHits = 0;
        private static long _conversionCacheMisses = 0;
        
        // 缓存大小限制
        private const int MAX_MEMBER_CACHE_SIZE = 1000;
        private const int MAX_METHOD_CACHE_SIZE = 500;
        private const int MAX_FUNCTION_CACHE_SIZE = 200;
        private const int MAX_CONVERSION_CACHE_SIZE = 100;
        #endregion

        #region 缓存管理
        /// <summary>
        /// 清理所有缓存
        /// </summary>
        public static void ClearCache()
        {
            lock (_memberCache)
            {
                _memberCache.Clear();
            }
            lock (_boundMethodCache)
            {
                _boundMethodCache.Clear();
            }
            lock (_functionCache)
            {
                _functionCache.Clear();
            }
            lock (_conversionCache)
            {
                _conversionCache.Clear();
            }
            lock (_compiledFunctions)
            {
                _compiledFunctions.Clear();
            }
        }

        /// <summary>
        /// 获取缓存统计信息（增强版）
        /// </summary>
        public static Dictionary<string, object> GetCacheStatistics()
        {
            var totalMembers = _memberCache.Values.Sum(dict => dict.Count);
            var estimatedMemory = EstimateCacheMemoryUsage();
            
            return new Dictionary<string, object>
            {
                ["MemberTypes"] = _memberCache.Count,
                ["TotalMembers"] = totalMembers,
                ["BoundMethods"] = _boundMethodCache.Count,
                ["Functions"] = _functionCache.Count,
                ["CompiledFunctions"] = _compiledFunctions.Count,
                ["Conversions"] = _conversionCache.Count,
                ["MemberHitRate"] = CalculateHitRate(_memberCacheHits, _memberCacheMisses),
                ["MethodHitRate"] = CalculateHitRate(_methodCacheHits, _methodCacheMisses),
                ["FunctionHitRate"] = CalculateHitRate(_functionCacheHits, _functionCacheMisses),
                ["ConversionHitRate"] = CalculateHitRate(_conversionCacheHits, _conversionCacheMisses),
                ["EstimatedMemoryKB"] = estimatedMemory
            };
        }

        #region 高性能委托编译

        /// <summary>
        /// 编译方法为高性能委托（使用表达式树）
        /// </summary>
        public static Func<List<RuntimeValue>, Task<RuntimeValue>> CompileMethodDelegate(MethodInfo method, object instance = null)
        {
            var key = $"{method.DeclaringType?.FullName}.{method.Name}_{instance?.GetHashCode() ?? 0}";
            
            if (_compiledFunctions.TryGetValue(key, out var cached))
            {
                return cached;
            }

            try
            {
                var compiled = CreateCompiledDelegate(method, instance);
                _compiledFunctions.TryAdd(key, compiled);
                return compiled;
            }
            catch (Exception ex)
            {
                MLogger.Warning($"无法编译方法 {method.Name}，回退到反射调用: {ex.Message}");
                // 回退到传统的反射调用
                return CreateReflectionDelegate(method, instance);
            }
        }

        /// <summary>
        /// 使用表达式树创建编译委托
        /// </summary>
        private static Func<List<RuntimeValue>, Task<RuntimeValue>> CreateCompiledDelegate(MethodInfo method, object instance)
        {
            var argsParam = Expression.Parameter(typeof(List<RuntimeValue>), "args");
            var parameters = method.GetParameters();
            
            // 创建参数转换表达式
            var argExpressions = new Expression[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                var paramType = parameters[i].ParameterType;
                var argAccess = Expression.Call(argsParam, typeof(List<RuntimeValue>).GetMethod("get_Item"), 
                    Expression.Constant(i));
                
                argExpressions[i] = CreateParameterConversionExpression(argAccess, paramType);
            }

            // 创建方法调用表达式
            Expression callExpression;
            if (method.IsStatic)
            {
                callExpression = Expression.Call(method, argExpressions);
            }
            else
            {
                var instanceExpr = Expression.Constant(instance, method.DeclaringType);
                callExpression = Expression.Call(instanceExpr, method, argExpressions);
            }

            // 处理返回类型
            var resultExpression = CreateReturnConversionExpression(callExpression, method.ReturnType);
            
            // 编译表达式
            var lambda = Expression.Lambda<Func<List<RuntimeValue>, Task<RuntimeValue>>>(
                resultExpression, argsParam);
            
            return lambda.Compile();
        }

        /// <summary>
        /// 创建参数转换表达式
        /// </summary>
        private static Expression CreateParameterConversionExpression(Expression argExpression, Type targetType)
        {
            // 获取 RuntimeValue.Value 属性
            var valueProperty = typeof(RuntimeValue).GetProperty("Value");
            var valueExpr = Expression.Property(argExpression, valueProperty);
            
            if (targetType == typeof(double))
            {
                // 调用 Helper.ConvertToDouble
                var convertMethod = typeof(Helper).GetMethod(nameof(ConvertToDouble), 
                    BindingFlags.Public | BindingFlags.Static);
                return Expression.Call(convertMethod, argExpression);
            }
            else if (targetType == typeof(string))
            {
                // 调用 Helper.ConvertToString
                var convertMethod = typeof(Helper).GetMethod(nameof(ConvertToString), 
                    BindingFlags.Public | BindingFlags.Static);
                return Expression.Call(convertMethod, argExpression);
            }
            else if (targetType == typeof(bool))
            {
                // 直接转换布尔值
                return Expression.Convert(valueExpr, targetType);
            }
            else if (targetType == typeof(int))
            {
                // 先转换为double，再转为int
                var doubleValue = CreateParameterConversionExpression(argExpression, typeof(double));
                return Expression.Convert(doubleValue, typeof(int));
            }
            
            // 默认转换
            return Expression.Convert(valueExpr, targetType);
        }

        /// <summary>
        /// 创建返回值转换表达式
        /// </summary>
        private static Expression CreateReturnConversionExpression(Expression callExpression, Type returnType)
        {
            if (returnType == typeof(void))
            {
                // void 方法返回 Null
                var voidBlock = Expression.Block(
                    callExpression,
                    Expression.Constant(Task.FromResult(RuntimeValue.Null))
                );
                return voidBlock;
            }
            else if (returnType == typeof(Task))
            {
                // Task 方法，等待完成后返回 Null
                var awaitTask = Expression.Call(
                    typeof(Helper).GetMethod(nameof(WrapTaskResult), BindingFlags.NonPublic | BindingFlags.Static),
                    callExpression);
                return awaitTask;
            }
            else if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                // Task<T> 方法，等待完成后转换结果
                var awaitTaskGeneric = Expression.Call(
                    typeof(Helper).GetMethod(nameof(WrapTaskGenericResult), BindingFlags.NonPublic | BindingFlags.Static)
                        .MakeGenericMethod(returnType.GetGenericArguments()[0]),
                    callExpression);
                return awaitTaskGeneric;
            }
            else
            {
                // 同步方法，直接转换结果
                var runtimeValueCtor = typeof(RuntimeValue).GetConstructor(new[] { typeof(object) });
                var resultValue = Expression.New(runtimeValueCtor, Expression.Convert(callExpression, typeof(object)));
                var taskResult = Expression.Call(typeof(Task).GetMethod(nameof(Task.FromResult))
                    .MakeGenericMethod(typeof(RuntimeValue)), resultValue);
                return taskResult;
            }
        }

        /// <summary>
        /// Task 包装方法
        /// </summary>
        private static async Task<RuntimeValue> WrapTaskResult(Task task)
        {
            await task.ConfigureAwait(false);
            return RuntimeValue.Null;
        }

        /// <summary>
        /// Task<T> 包装方法
        /// </summary>
        private static async Task<RuntimeValue> WrapTaskGenericResult<T>(Task<T> task)
        {
            var result = await task.ConfigureAwait(false);
            return new RuntimeValue(result);
        }

        /// <summary>
        /// 创建反射委托（回退方案）
        /// </summary>
        private static Func<List<RuntimeValue>, Task<RuntimeValue>> CreateReflectionDelegate(MethodInfo method, object instance)
        {
            return async (args) =>
            {
                try
                {
                    var parameters = method.GetParameters();
                    var nativeArgs = new object[parameters.Length];
                    
                    // 转换参数
                    for (int i = 0; i < Math.Min(args.Count, parameters.Length); i++)
                    {
                        var paramType = parameters[i].ParameterType;
                        nativeArgs[i] = ConvertToNativeType(args[i], paramType);
                    }
                    
                    // 调用方法
                    var result = method.Invoke(instance, nativeArgs);
                    
                    // 处理异步结果
                    if (result is Task task)
                    {
                        await task.ConfigureAwait(false);
                        
                        if (task.GetType().IsGenericType)
                        {
                            var property = task.GetType().GetProperty("Result");
                            var taskResult = property?.GetValue(task);
                            return ConvertToRuntimeValue(taskResult);
                        }
                        
                        return RuntimeValue.Null;
                    }
                    
                    return ConvertToRuntimeValue(result);
                }
                catch (Exception ex)
                {
                    MLogger.Error($"调用编译委托时出错: {ex}");
                    return RuntimeValue.Null;
                }
            };
        }

        #endregion
        
        /// <summary>
        /// 计算命中率
        /// </summary>
        private static double CalculateHitRate(long hits, long misses)
        {
            var total = hits + misses;
            return total == 0 ? 0.0 : (double)hits / total * 100.0;
        }
        
        /// <summary>
        /// 估算缓存内存使用量（KB）
        /// </summary>
        private static int EstimateCacheMemoryUsage()
        {
            var memberMemory = _memberCache.Count * 200; // 粗略估算每个类型200字节
            var methodMemory = _boundMethodCache.Count * 50;
            var functionMemory = _functionCache.Count * 100;
            var conversionMemory = _conversionCache.Count * 30;
            
            return (memberMemory + methodMemory + functionMemory + conversionMemory) / 1024;
        }
        #endregion

        #region 高性能成员访问
        /// <summary>
        /// 获取类型的成员访问器（高性能缓存版）
        /// </summary>
        public static MemberAccessor GetMemberAccessor(Type type, string memberName)
        {
            // 双重检查锁定模式确保线程安全
            if (!_memberCache.TryGetValue(type, out var typeMembers))
            {
                lock (_memberCache)
                {
                    if (!_memberCache.TryGetValue(type, out typeMembers))
                    {
                        // 检查缓存大小限制
                        if (_memberCache.Count >= MAX_MEMBER_CACHE_SIZE)
                        {
                            EvictLRUMemberCache();
                        }
                        
                        typeMembers = BuildMemberCache(type);
                        _memberCache[type] = typeMembers;
                        System.Threading.Interlocked.Increment(ref _memberCacheMisses);
                    }
                    else
                    {
                        System.Threading.Interlocked.Increment(ref _memberCacheHits);
                    }
                }
            }
            else
            {
                System.Threading.Interlocked.Increment(ref _memberCacheHits);
            }

            // 优化的快速查找：直接使用TryGetValue而不是遍历
            if (typeMembers.TryGetValue(memberName, out var accessor))
            {
                return accessor;
            }

            // 回退到大小写不敏感查找（性能较低）
            foreach (var kvp in typeMembers)
            {
                if (_stringComparer.Equals(kvp.Key, memberName))
                {
                    return kvp.Value;
                }
            }

            return null;
        }
        
        /// <summary>
        /// 清理最久未使用的成员缓存（简单LRU实现）
        /// </summary>
        private static void EvictLRUMemberCache()
        {
            if (_memberCache.Count == 0) return;
            
            // 简单实现：移除前10%的缓存项
            var toRemove = _memberCache.Keys.Take(_memberCache.Count / 10).ToList();
            foreach (var key in toRemove)
            {
                _memberCache.Remove(key);
            }
            
            MLogger.Debug($"已清理 {toRemove.Count} 个成员缓存项以控制内存使用");
        }

        /// <summary>
        /// 构建类型的成员缓存
        /// </summary>
        private static Dictionary<string, MemberAccessor> BuildMemberCache(Type type)
        {
            var members = new Dictionary<string, MemberAccessor>(_stringComparer);

            // 缓存所有公共属性
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var property in properties)
            {
                members[property.Name] = new MemberAccessor(property);
            }

            // 缓存所有公共字段
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                members[field.Name] = new MemberAccessor(field);
            }

            // 缓存所有公共方法（排除Object基类方法和特殊方法），不支持重载
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.DeclaringType != typeof(object) && !m.IsSpecialName)
                .GroupBy(m => m.Name, _stringComparer);

            foreach (var methodGroup in methods)
            {
                var methodList = methodGroup.ToList();
                if (methodList.Count == 1)
                {
                    members[methodGroup.Key] = new MemberAccessor(methodList[0]);
                }
                else
                {
                    // 不支持重载，选择第一个方法并提供警告信息
                    var selectedMethod = methodList[0];
                    members[methodGroup.Key] = new MemberAccessor(selectedMethod);
                    MLogger.Warning($"类型 {type.Name} 的方法 {methodGroup.Key} 有 {methodList.Count} 个重载，不支持重载，已选择第一个：{selectedMethod}。" +
                                    $"若需要访问其他重载，请重命名方法或使用不同的方法名。");
                }
            }

            return members;
        }

        /// <summary>
        /// 获取绑定方法委托（高性能缓存版）
        /// </summary>
        public static Delegate GetBoundMethod(object instance, string methodName)
        {
            if (instance == null) return null;

            var type = instance.GetType();
            var cacheKey = (type, methodName);

            // 尝试从缓存获取
            if (_boundMethodCache.TryGetValue(cacheKey, out var cachedDelegate))
            {
                System.Threading.Interlocked.Increment(ref _methodCacheHits);
                return cachedDelegate;
            }

            System.Threading.Interlocked.Increment(ref _methodCacheMisses);

            // 检查缓存大小限制
            if (_boundMethodCache.Count >= MAX_METHOD_CACHE_SIZE)
            {
                EvictLRUMethodCache();
            }

            // 查找方法并创建委托（处理重载问题：选择第一个匹配的方法）
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                .Where(m => _stringComparer.Equals(m.Name, methodName) && !m.IsSpecialName)
                .ToList();

            if (methods.Count > 1)
            {
                MLogger.Debug($"方法 {type.Name}.{methodName} 有 {methods.Count} 个重载，使用第一个");
            }

            var method = methods.FirstOrDefault();
            if (method != null)
            {
                try
                {
                    // 创建绑定到实例的委托
                    var delegateType = GetDelegateTypeForMethod(method);
                    if (delegateType != null)
                    {
                        var boundDelegate = Delegate.CreateDelegate(delegateType, instance, method, false);
                        if (boundDelegate != null)
                        {
                            lock (_boundMethodCache)
                            {
                                _boundMethodCache[cacheKey] = boundDelegate;
                            }
                            return boundDelegate;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MLogger.Warning($"创建绑定方法委托失败: {type.Name}.{methodName} - {ex.Message}");
                }
            }

            return null;
        }
        
        /// <summary>
        /// 清理最久未使用的方法缓存
        /// </summary>
        private static void EvictLRUMethodCache()
        {
            if (_boundMethodCache.Count == 0) return;
            
            var toRemove = _boundMethodCache.Keys.Take(_boundMethodCache.Count / 10).ToList();
            foreach (var key in toRemove)
            {
                _boundMethodCache.Remove(key);
            }
            
            MLogger.Debug($"已清理 {toRemove.Count} 个方法缓存项以控制内存使用");
        }

        /// <summary>
        /// 为方法获取合适的委托类型
        /// </summary>
        private static Type GetDelegateTypeForMethod(MethodInfo method)
        {
            var parameters = method.GetParameters();

            // 根据返回类型选择委托类型
            if (method.ReturnType == typeof(void))
            {
                return parameters.Length switch
                {
                    0 => typeof(Action),
                    1 => typeof(Action<>).MakeGenericType(parameters[0].ParameterType),
                    2 => typeof(Action<,>).MakeGenericType(parameters[0].ParameterType, parameters[1].ParameterType),
                    _ => null // 暂不支持更多参数的Action
                };
            }
            else
            {
                return parameters.Length switch
                {
                    0 => typeof(Func<>).MakeGenericType(method.ReturnType),
                    1 => typeof(Func<,>).MakeGenericType(parameters[0].ParameterType, method.ReturnType),
                    2 => typeof(Func<,,>).MakeGenericType(parameters[0].ParameterType, parameters[1].ParameterType, method.ReturnType),
                    _ => null // 暂不支持更多参数的Func
                };
            }
        }
        #endregion

        #region 高性能索引访问
        /// <summary>
        /// 高性能索引访问
        /// </summary>
        public static RuntimeValue GetIndexValue(RuntimeValue target, RuntimeValue index, int line, int column)
        {
            switch (target.Type)
            {
                case RuntimeValue.ValueType.String when index.Type == RuntimeValue.ValueType.Number:
                    var str = (string)target.Value;
                    var idx = (int)(double)index.Value;
                    if (idx >= 0 && idx < str.Length)
                    {
                        return new RuntimeValue(str[idx].ToString());
                    }
                    throw new InvalidOperationException($"运行时错误: 第{line}行，第{column}列，字符串索引越界（索引: {idx}，长度: {str.Length}）");

                case RuntimeValue.ValueType.Object when target.Value is IDictionary dict:
                    // 修复字典索引类型错误：将index转换为原生对象而不是强制转换为string
                    var key = ConvertToNativeType(index);
                    if (dict.Contains(key))
                    {
                        return ConvertToRuntimeValue(dict[key]);
                    }

                    // 改进错误信息：包含键类型信息
                    string keyTypeName = key?.GetType().Name ?? "null";
                    string dictKeyTypeName = "未知";

                    // 尝试获取字典的键类型
                    var dictType = dict.GetType();
                    if (dictType.IsGenericType)
                    {
                        var keyType = dictType.GetGenericArguments()[0];
                        dictKeyTypeName = keyType.Name;
                    }

                    throw new InvalidOperationException($"运行时错误: 第{line}行，第{column}列，字典中不存在键: '{key}' " +
                                                        $"（键类型: {keyTypeName}，目标字典键类型: {dictKeyTypeName}）");

                case RuntimeValue.ValueType.Object when target.Value is IList list:
                    if (index.Type == RuntimeValue.ValueType.Number)
                    {
                        var listIdx = (int)(double)index.Value;
                        if (listIdx >= 0 && listIdx < list.Count)
                        {
                            return ConvertToRuntimeValue(list[listIdx]);
                        }
                        throw new InvalidOperationException($"运行时错误: 第{line}行，第{column}列，列表索引越界（索引: {listIdx}，长度: {list.Count}）");
                    }
                    break;

                case RuntimeValue.ValueType.Object when target.Value is Array array:
                    if (index.Type == RuntimeValue.ValueType.Number)
                    {
                        var arrayIdx = (int)(double)index.Value;
                        if (arrayIdx >= 0 && arrayIdx < array.Length)
                        {
                            return ConvertToRuntimeValue(array.GetValue(arrayIdx));
                        }
                        throw new InvalidOperationException($"运行时错误: 第{line}行，第{column}列，数组索引越界（索引: {arrayIdx}，长度: {array.Length}）");
                    }
                    break;
            }

            throw new InvalidOperationException($"运行时错误: 第{line}行，第{column}列，不支持的索引访问: {target.Type}[{index.Type}]");
        }
        #endregion

        #region 类型转换（优化版）
        /// <summary>
        /// 将 RuntimeValue 转换为原生 C# 类型（高性能缓存版）
        /// </summary>
        public static object ConvertToNativeType(RuntimeValue value, Type targetType)
        {
            if (value.Type == RuntimeValue.ValueType.Null)
            {
                return null;
            }

            // 快速路径：类型已经匹配
            if (value.Value != null && targetType.IsAssignableFrom(value.Value.GetType()))
            {
                return value.Value;
            }

            // 热路径优化：常用类型转换
            if (TryHotPathConversion(value, targetType, out var result))
            {
                System.Threading.Interlocked.Increment(ref _conversionCacheHits);
                return result;
            }

            // 缓存查找类型转换器
            var conversionKey = (value.Value?.GetType() ?? typeof(object), targetType);
            if (_conversionCache.TryGetValue(conversionKey, out var converter))
            {
                System.Threading.Interlocked.Increment(ref _conversionCacheHits);
                return converter(value.Value);
            }

            System.Threading.Interlocked.Increment(ref _conversionCacheMisses);

            // 检查缓存大小限制
            if (_conversionCache.Count >= MAX_CONVERSION_CACHE_SIZE)
            {
                EvictLRUConversionCache();
            }

            // 创建并缓存转换器
            converter = CreateTypeConverter(value.Type, targetType);
            if (converter != null)
            {
                lock (_conversionCache)
                {
                    _conversionCache[conversionKey] = converter;
                }
                return converter(value.Value);
            }

            // 回退到原有转换逻辑
            return ConvertToNativeTypeOriginal(value, targetType);
        }

        /// <summary>
        /// 热路径类型转换优化
        /// </summary>
        private static bool TryHotPathConversion(RuntimeValue value, Type targetType, out object result)
        {
            result = null;

            // 最常用的转换：string
            if (targetType == typeof(string))
            {
                result = value.ToString();
                return true;
            }

            // 数值类型转换
            if (value.Type == RuntimeValue.ValueType.Number)
            {
                var doubleValue = (double)value.Value;
                if (targetType == typeof(int))
                {
                    result = (int)doubleValue;
                    return true;
                }
                if (targetType == typeof(float))
                {
                    result = (float)doubleValue;
                    return true;
                }
                if (targetType == typeof(double))
                {
                    result = doubleValue;
                    return true;
                }
            }

            // 布尔类型转换
            if (value.Type == RuntimeValue.ValueType.Boolean && targetType == typeof(bool))
            {
                result = (bool)value.Value;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 清理最久未使用的转换缓存
        /// </summary>
        private static void EvictLRUConversionCache()
        {
            if (_conversionCache.Count == 0) return;
            
            var toRemove = _conversionCache.Keys.Take(_conversionCache.Count / 2).ToList(); // 清理更多项
            foreach (var key in toRemove)
            {
                _conversionCache.Remove(key);
            }
            
            MLogger.Debug($"已清理 {toRemove.Count} 个类型转换缓存项");
        }

        /// <summary>
        /// 创建类型转换器
        /// </summary>
        private static Func<object, object> CreateTypeConverter(RuntimeValue.ValueType sourceType, Type targetType)
        {
            // 基本类型转换
            if (targetType == typeof(string))
            {
                return (obj) => obj?.ToString() ?? string.Empty;
            }

            if (targetType == typeof(int) || targetType == typeof(int?))
            {
                return sourceType switch
                {
                    RuntimeValue.ValueType.Number => (obj) => (int)(double)obj,
                    RuntimeValue.ValueType.String => (obj) => int.TryParse((string)obj, out var result) ? result : 0,
                    _ => (obj) => Convert.ToInt32(obj)
                };
            }

            if (targetType == typeof(double) || targetType == typeof(double?) || targetType == typeof(float) || targetType == typeof(float?))
            {
                return sourceType switch
                {
                    RuntimeValue.ValueType.Number => (obj) => Convert.ChangeType((double)obj, targetType),
                    RuntimeValue.ValueType.String => (obj) => double.TryParse((string)obj, out var result) ? Convert.ChangeType(result, targetType) : Convert.ChangeType(0.0, targetType),
                    _ => (obj) => Convert.ChangeType(obj, targetType)
                };
            }

            if (targetType == typeof(bool) || targetType == typeof(bool?))
            {
                return sourceType switch
                {
                    RuntimeValue.ValueType.Boolean => (obj) => (bool)obj,
                    RuntimeValue.ValueType.String => (obj) => bool.TryParse((string)obj, out var result) ? result : false,
                    RuntimeValue.ValueType.Number => (obj) => (double)obj != 0.0,
                    _ => (obj) => Convert.ToBoolean(obj)
                };
            }

            return null;
        }

        /// <summary>
        /// 原始类型转换方法（保持兼容性）
        /// </summary>
        private static object ConvertToNativeTypeOriginal(RuntimeValue value, Type targetType)
        {
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
            if (targetType == typeof(string))
            {
                if (value.Type != RuntimeValue.ValueType.String)
                {
                    MLogger.Warning($"期望字符串类型用于 '{targetType.Name}'，但得到了{GetTypeName(value.Type)}");
                }
                return value.Value.ToString();
            }
            // 布尔类型转换
            if (targetType == typeof(bool))
            {
                if (value.Type != RuntimeValue.ValueType.Boolean)
                {
                    MLogger.Error($"期望布尔类型用于 '{targetType.Name}'，但得到了{GetTypeName(value.Type)}");
                }
                return Convert.ChangeType(value.Value, targetType);
            }
            // 引用类型和可空值类型
            if (targetType.IsClass || (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) != null))
            {
                return null;
            }

            // 不支持的类型
            MLogger.Error($"不支持的参数类型转换: {targetType.Name}");
            // 返回类型的默认值而不是抛出异常
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }
        #endregion

        /// <summary>
        /// 获取类型名称的可读表示
        /// </summary>
        public static string GetTypeName(RuntimeValue.ValueType type)
        {
            switch (type)
            {
                case RuntimeValue.ValueType.Number: return "number";
                case RuntimeValue.ValueType.String: return "string";
                case RuntimeValue.ValueType.Boolean: return "boolean";
                case RuntimeValue.ValueType.Object: return "object";
                case RuntimeValue.ValueType.Function: return "function";
                case RuntimeValue.ValueType.Null: return "null";
                default: return type.ToString();
            }
        }

        /// <summary>
        /// 将C#对象转换为脚本运行时值（优化版）
        /// </summary>
        public static RuntimeValue ConvertToRuntimeValue(object value)
        {
            return value switch
            {
                null => RuntimeValue.Null,
                double d => new RuntimeValue(d),
                float f => new RuntimeValue((double)f),
                int i => new RuntimeValue((double)i),
                long l => new RuntimeValue((double)l),
                bool b => new RuntimeValue(b),
                string s => new RuntimeValue(s),
                _ => new RuntimeValue(value) // 支持对象类型
            };
        }

        /// <summary>
        /// 将脚本运行时值转换为C#对象
        /// </summary>
        public static object ConvertToNativeType(RuntimeValue value)
        {
            switch (value.Type)
            {
                case RuntimeValue.ValueType.Number:
                    return ConvertNumberToNativeType((double)value.Value);

                case RuntimeValue.ValueType.Boolean:
                    return (bool)value.Value;

                case RuntimeValue.ValueType.String:
                    return value.Value.ToString();

                case RuntimeValue.ValueType.Null:
                    return null;

                case RuntimeValue.ValueType.Function:
                    return value.Value; // 函数值直接返回

                case RuntimeValue.ValueType.Object:
                    return value.Value;

                default:
                    MLogger.Error($"不支持的运行时值类型: {value.Type}");
                    return null; // 返回空值而不是抛出异常
            }
        }

        /// <summary>
        /// 将数字转换为最合适的原生类型
        /// </summary>
        public static object ConvertNumberToNativeType(double number)
        {
            // 检查是否是整数
            if (Math.Abs(number - Math.Round(number)) < double.Epsilon)
            {
                // 如果是整数且在int范围内
                if (number is >= int.MinValue and <= int.MaxValue)
                    return (int)number;
                // 如果是整数但超出int范围
                return (long)number;
            }

            // 如果是小数
            return number;
        }

        /// <summary>
        /// 高频转换：将RuntimeValue转换为double（热点优化）
        /// </summary>
        [Preserve]
        public static double ConvertToDouble(RuntimeValue value)
        {
            switch (value.Type)
            {
                case RuntimeValue.ValueType.Number:
                    return (double)value.Value;
                case RuntimeValue.ValueType.Boolean:
                    return (bool)value.Value ? 1.0 : 0.0;
                case RuntimeValue.ValueType.String:
                    if (double.TryParse(value.Value?.ToString() ?? "", out var result))
                        return result;
                    return 0.0;
                case RuntimeValue.ValueType.Null:
                    return 0.0;
                default:
                    return 0.0;
            }
        }

        /// <summary>
        /// 高频转换：将RuntimeValue转换为string（热点优化）
        /// </summary>
        [Preserve]
        public static string ConvertToString(RuntimeValue value)
        {
            switch (value.Type)
            {
                case RuntimeValue.ValueType.String:
                    return value.Value?.ToString() ?? "";
                case RuntimeValue.ValueType.Number:
                    return ((double)value.Value).ToString("G");
                case RuntimeValue.ValueType.Boolean:
                    return (bool)value.Value ? "true" : "false";
                case RuntimeValue.ValueType.Null:
                    return "";
                case RuntimeValue.ValueType.Function:
                    return "function";
                case RuntimeValue.ValueType.Object:
                    return value.Value?.ToString() ?? "object";
                default:
                    return "";
            }
        }

        #region 一等函数支持

        /// <summary>
        /// 创建绑定方法的函数值（高性能缓存版）
        /// </summary>
        public static RuntimeValue CreateBoundMethod(object instance, string methodName)
        {
            if (instance == null)
                return RuntimeValue.Null;

            var key = (instance, methodName);
            if (_functionCache.TryGetValue(key, out var cachedFunc))
            {
                System.Threading.Interlocked.Increment(ref _functionCacheHits);
                return new RuntimeValue(cachedFunc);
            }

            System.Threading.Interlocked.Increment(ref _functionCacheMisses);

            // 检查缓存大小限制
            if (_functionCache.Count >= MAX_FUNCTION_CACHE_SIZE)
            {
                EvictLRUFunctionCache();
            }

            var type = instance.GetType();
            var accessor = GetMemberAccessor(type, methodName);
            
            if (accessor?.Type != MemberAccessor.AccessorType.Method || accessor.Method == null)
            {
                return RuntimeValue.Null;
            }

            var method = accessor.Method;
            var func = CreateMethodFunction(instance, method);
            
            lock (_functionCache)
            {
                _functionCache[key] = func;
            }
            
            return new RuntimeValue(func);
        }
        
        /// <summary>
        /// 清理最久未使用的函数缓存
        /// </summary>
        private static void EvictLRUFunctionCache()
        {
            if (_functionCache.Count == 0) return;
            
            var toRemove = _functionCache.Keys.Take(_functionCache.Count / 3).ToList();
            foreach (var key in toRemove)
            {
                _functionCache.Remove(key);
            }
            
            MLogger.Debug($"已清理 {toRemove.Count} 个函数缓存项以控制内存使用");
        }

        /// <summary>
        /// 创建静态函数值
        /// </summary>
        public static RuntimeValue CreateStaticFunction(Type type, string methodName)
        {
            if (type == null)
                return RuntimeValue.Null;

            var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);
            if (method == null)
                return RuntimeValue.Null;

            var func = CreateMethodFunction(null, method);
            return new RuntimeValue(func);
        }

        /// <summary>
        /// 从 Delegate 创建函数值
        /// </summary>
        public static RuntimeValue CreateFunctionValue(Delegate del)
        {
            if (del == null)
                return RuntimeValue.Null;

            var func = CreateDelegateFunction(del);
            return new RuntimeValue(func);
        }

        /// <summary>
        /// 创建方法函数包装器
        /// </summary>
        private static Func<List<RuntimeValue>, Task<RuntimeValue>> CreateMethodFunction(object instance, MethodInfo method)
        {
            return async (args) =>
            {
                try
                {
                    var parameters = method.GetParameters();
                    var nativeArgs = new object[parameters.Length];

                    // 转换参数
                    for (int i = 0; i < Math.Min(args.Count, parameters.Length); i++)
                    {
                        var paramType = parameters[i].ParameterType;
                        var arg = ConvertToNativeType(args[i]);
                        nativeArgs[i] = ConvertToNativeType(args[i], paramType);
                    }

                    // 调用方法
                    var result = method.Invoke(instance, nativeArgs);

                    // 处理异步结果
                    if (result is Task task)
                    {
                        await task.ConfigureAwait(false);
                        
                        if (task.GetType().IsGenericType)
                        {
                            var property = task.GetType().GetProperty("Result");
                            var taskResult = property?.GetValue(task);
                            return ConvertToRuntimeValue(taskResult);
                        }
                        
                        return RuntimeValue.Null;
                    }

                    return ConvertToRuntimeValue(result);
                }
                catch (Exception ex)
                {
                    MLogger.Error($"调用绑定方法 '{method.Name}' 时出错: {ex}");
                    return RuntimeValue.Null;
                }
            };
        }

        /// <summary>
        /// 创建委托函数包装器
        /// </summary>
        private static Func<List<RuntimeValue>, Task<RuntimeValue>> CreateDelegateFunction(Delegate del)
        {
            return async (args) =>
            {
                try
                {
                    var method = del.Method;
                    var parameters = method.GetParameters();
                    var nativeArgs = new object[parameters.Length];

                    // 转换参数
                    for (int i = 0; i < Math.Min(args.Count, parameters.Length); i++)
                    {
                        var paramType = parameters[i].ParameterType;
                        var arg = ConvertToNativeType(args[i]);
                        nativeArgs[i] = ConvertToNativeType(args[i], paramType);
                    }

                    // 调用委托
                    var result = del.DynamicInvoke(nativeArgs);

                    // 处理异步结果
                    if (result is Task task)
                    {
                        await task.ConfigureAwait(false);
                        
                        if (task.GetType().IsGenericType)
                        {
                            var property = task.GetType().GetProperty("Result");
                            var taskResult = property?.GetValue(task);
                            return ConvertToRuntimeValue(taskResult);
                        }
                        
                        return RuntimeValue.Null;
                    }

                    return ConvertToRuntimeValue(result);
                }
                catch (Exception ex)
                {
                    MLogger.Error($"调用委托函数时出错: {ex}");
                    return RuntimeValue.Null;
                }
            };
        }

        /// <summary>
        /// 检查值是否为函数
        /// </summary>
        public static bool IsFunction(RuntimeValue value)
        {
            return value.Type == RuntimeValue.ValueType.Function && value.Value != null;
        }

        /// <summary>
        /// 获取函数值
        /// </summary>
        public static Func<List<RuntimeValue>, Task<RuntimeValue>> GetFunction(RuntimeValue value)
        {
            if (IsFunction(value))
                return value.Value as Func<List<RuntimeValue>, Task<RuntimeValue>>;
            return null;
        }

        #endregion

    }
}
