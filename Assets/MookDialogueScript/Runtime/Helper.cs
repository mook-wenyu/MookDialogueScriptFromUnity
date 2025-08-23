using System;

namespace MookDialogueScript
{
    public static class Helper
    {

        /// <summary>
        /// 将运行时值转换为原生类型
        /// </summary>
        public static object ConvertToNativeType(RuntimeValue value, Type targetType)
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
                case RuntimeValue.ValueType.Null: return "null";
                default: return type.ToString();
            }
        }

        /// <summary>
        /// 将C#对象转换为脚本运行时值
        /// </summary>
        public static RuntimeValue ConvertToRuntimeValue(object value)
        {
            switch (value)
            {
                case null:
                    return RuntimeValue.Null;
                case double or int or float or long:
                    return new RuntimeValue(Convert.ToDouble(value));
                case bool boolValue:
                    return new RuntimeValue(boolValue);
                case string strValue:
                    return new RuntimeValue(strValue);
                default:
                    MLogger.Error($"不支持的返回值类型: {value.GetType().Name}，将返回空值");
                    return RuntimeValue.Null;
            }
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
