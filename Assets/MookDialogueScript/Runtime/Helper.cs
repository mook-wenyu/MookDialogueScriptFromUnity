using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

        // 类型转换缓存：缓存常用类型转换器
        private static readonly Dictionary<(Type, Type), Func<object, object>> _conversionCache =
            new Dictionary<(Type, Type), Func<object, object>>();

        // 大小写不敏感的字符串比较器
        private static readonly StringComparer _stringComparer = StringComparer.OrdinalIgnoreCase;
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
            lock (_conversionCache)
            {
                _conversionCache.Clear();
            }
        }

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        public static Dictionary<string, int> GetCacheStatistics()
        {
            return new Dictionary<string, int>
            {
                ["MemberCache"] = _memberCache.Count,
                ["BoundMethodCache"] = _boundMethodCache.Count,
                ["ConversionCache"] = _conversionCache.Count
            };
        }
        #endregion

        #region 高性能成员访问
        /// <summary>
        /// 获取类型的成员访问器（带缓存）
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
                        typeMembers = BuildMemberCache(type);
                        _memberCache[type] = typeMembers;
                    }
                }
            }

            // 使用大小写不敏感查找
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
        /// 获取绑定方法委托（带缓存）
        /// </summary>
        public static Delegate GetBoundMethod(object instance, string methodName)
        {
            if (instance == null) return null;

            var type = instance.GetType();
            var cacheKey = (type, methodName);

            // 尝试从缓存获取
            if (_boundMethodCache.TryGetValue(cacheKey, out var cachedDelegate))
            {
                return cachedDelegate;
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
        /// 将 RuntimeValue 转换为原生 C# 类型（带缓存优化）
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

            // 缓存查找类型转换器
            var conversionKey = (value.Value?.GetType() ?? typeof(object), targetType);
            if (_conversionCache.TryGetValue(conversionKey, out var converter))
            {
                return converter(value.Value);
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

    }
}
