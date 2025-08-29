using System;

namespace MookDialogueScript.Semantic.TypeSystem
{
    /// <summary>
    /// 类型兼容性检查器
    /// 负责判断类型之间的兼容性和转换规则
    /// </summary>
    public static class TypeCompatibility
    {
        /// <summary>
        /// 检查两个类型是否兼容
        /// 支持隐式类型转换和继承关系检查
        /// </summary>
        /// <param name="actualType">实际类型</param>
        /// <param name="expectedType">期望类型</param>
        /// <returns>如果类型兼容返回true，否则返回false</returns>
        public static bool IsCompatible(TypeInfo actualType, TypeInfo expectedType)
        {
            // 空值检查
            if (actualType == null || expectedType == null)
                return false;

            // Any 类型与任何类型兼容
            if (actualType.Kind == TypeKind.Any || expectedType.Kind == TypeKind.Any)
                return true;

            // 相同类型直接兼容
            if (actualType.Equals(expectedType))
                return true;

            // 特殊兼容性规则
            return CheckSpecialCompatibility(actualType, expectedType);
        }

        /// <summary>
        /// 检查特殊的类型兼容性规则
        /// </summary>
        /// <param name="actualType">实际类型</param>
        /// <param name="expectedType">期望类型</param>
        /// <returns>是否兼容</returns>
        private static bool CheckSpecialCompatibility(TypeInfo actualType, TypeInfo expectedType)
        {
            return expectedType.Kind switch
            {
                TypeKind.Number => CheckNumericCompatibility(actualType),
                TypeKind.String => CheckStringCompatibility(actualType),
                TypeKind.Boolean => CheckBooleanCompatibility(actualType),
                TypeKind.Object => CheckObjectCompatibility(actualType, expectedType),
                TypeKind.Array => CheckArrayCompatibility(actualType, expectedType),
                TypeKind.Dictionary => CheckDictionaryCompatibility(actualType, expectedType),
                TypeKind.Function => CheckFunctionCompatibility(actualType),
                _ => false
            };
        }

        /// <summary>
        /// 检查数字类型兼容性
        /// </summary>
        private static bool CheckNumericCompatibility(TypeInfo actualType)
        {
            // 数字类型之间可以相互转换
            return actualType.Kind == TypeKind.Number;
        }

        /// <summary>
        /// 检查字符串类型兼容性
        /// </summary>
        private static bool CheckStringCompatibility(TypeInfo actualType)
        {
            // 基本类型可以转换为字符串（通过ToString）
            return actualType.Kind is TypeKind.Number or TypeKind.Boolean or TypeKind.String;
        }

        /// <summary>
        /// 检查布尔类型兼容性
        /// </summary>
        private static bool CheckBooleanCompatibility(TypeInfo actualType)
        {
            // 数字可以转换为布尔（0为false，非0为true）
            // 但这通常会产生警告
            return actualType.Kind == TypeKind.Boolean || actualType.Kind == TypeKind.Number;
        }

        /// <summary>
        /// 检查对象类型兼容性
        /// </summary>
        private static bool CheckObjectCompatibility(TypeInfo actualType, TypeInfo expectedType)
        {
            if (actualType.Kind != TypeKind.Object)
                return false;

            // 如果两者都有CLR类型信息，检查继承关系
            if (actualType.ClrType != null && expectedType.ClrType != null)
            {
                return expectedType.ClrType.IsAssignableFrom(actualType.ClrType);
            }

            // 缺少CLR类型信息时，保守地认为兼容
            return true;
        }

        /// <summary>
        /// 检查数组类型兼容性
        /// </summary>
        private static bool CheckArrayCompatibility(TypeInfo actualType, TypeInfo expectedType)
        {
            if (actualType.Kind != TypeKind.Array)
                return false;

            // 检查元素类型兼容性
            if (actualType.ElementType != null && expectedType.ElementType != null)
            {
                return IsCompatible(actualType.ElementType, expectedType.ElementType);
            }

            // 元素类型未知时认为兼容
            return true;
        }

        /// <summary>
        /// 检查字典类型兼容性
        /// </summary>
        private static bool CheckDictionaryCompatibility(TypeInfo actualType, TypeInfo expectedType)
        {
            if (actualType.Kind != TypeKind.Dictionary)
                return false;

            // 检查键和值类型兼容性
            bool keyCompatible = actualType.KeyType == null || expectedType.KeyType == null ||
                                IsCompatible(actualType.KeyType, expectedType.KeyType);
            
            bool valueCompatible = actualType.ValueType == null || expectedType.ValueType == null ||
                                 IsCompatible(actualType.ValueType, expectedType.ValueType);

            return keyCompatible && valueCompatible;
        }

        /// <summary>
        /// 检查函数类型兼容性
        /// </summary>
        private static bool CheckFunctionCompatibility(TypeInfo actualType)
        {
            // 函数类型和委托对象都可以作为函数使用
            return actualType.Kind == TypeKind.Function ||
                   (actualType.Kind == TypeKind.Object && actualType.ClrType != null &&
                    typeof(Delegate).IsAssignableFrom(actualType.ClrType));
        }

        /// <summary>
        /// 检查数值到布尔转换的可接受性
        /// </summary>
        /// <param name="actualType">实际类型</param>
        /// <param name="expectedType">期望类型</param>
        /// <returns>转换级别</returns>
        public static ConversionLevel GetConversionLevel(TypeInfo actualType, TypeInfo expectedType)
        {
            if (IsCompatible(actualType, expectedType))
            {
                // 完全兼容
                if (actualType.Equals(expectedType))
                    return ConversionLevel.Identity;

                // Any类型转换
                if (actualType.Kind == TypeKind.Any || expectedType.Kind == TypeKind.Any)
                    return ConversionLevel.Unknown;

                // 特殊转换检查
                return GetSpecialConversionLevel(actualType, expectedType);
            }

            return ConversionLevel.None;
        }

        /// <summary>
        /// 检查特殊转换级别
        /// </summary>
        private static ConversionLevel GetSpecialConversionLevel(TypeInfo actualType, TypeInfo expectedType)
        {
            return (actualType.Kind, expectedType.Kind) switch
            {
                (TypeKind.Number, TypeKind.Boolean) => ConversionLevel.Warning,
                (TypeKind.Number, TypeKind.String) => ConversionLevel.Implicit,
                (TypeKind.Boolean, TypeKind.String) => ConversionLevel.Implicit,
                (TypeKind.Object, TypeKind.Object) => ConversionLevel.Implicit,
                _ => ConversionLevel.Implicit
            };
        }

        /// <summary>
        /// 检查是否可以进行算术运算
        /// </summary>
        /// <param name="leftType">左操作数类型</param>
        /// <param name="rightType">右操作数类型</param>
        /// <returns>如果可以进行算术运算返回true</returns>
        public static bool CanPerformArithmetic(TypeInfo leftType, TypeInfo rightType)
        {
            return (leftType.Kind == TypeKind.Number || leftType.Kind == TypeKind.Any) &&
                   (rightType.Kind == TypeKind.Number || rightType.Kind == TypeKind.Any);
        }

        /// <summary>
        /// 检查是否可以进行逻辑运算
        /// </summary>
        /// <param name="leftType">左操作数类型</param>
        /// <param name="rightType">右操作数类型</param>
        /// <returns>如果可以进行逻辑运算返回true</returns>
        public static bool CanPerformLogical(TypeInfo leftType, TypeInfo rightType)
        {
            return (leftType.Kind == TypeKind.Boolean || leftType.Kind == TypeKind.Any) &&
                   (rightType.Kind == TypeKind.Boolean || rightType.Kind == TypeKind.Any);
        }

        /// <summary>
        /// 检查是否可以进行比较运算
        /// </summary>
        /// <param name="leftType">左操作数类型</param>
        /// <param name="rightType">右操作数类型</param>
        /// <returns>如果可以进行比较运算返回true</returns>
        public static bool CanPerformComparison(TypeInfo leftType, TypeInfo rightType)
        {
            // 相同类型之间可以比较
            if (leftType.Kind == rightType.Kind)
                return true;

            // Any类型可以与任何类型比较
            if (leftType.Kind == TypeKind.Any || rightType.Kind == TypeKind.Any)
                return true;

            // 数字类型之间可以比较
            return leftType.Kind == TypeKind.Number && rightType.Kind == TypeKind.Number;
        }

        /// <summary>
        /// 检查类型是否支持索引访问
        /// </summary>
        /// <param name="targetType">目标类型</param>
        /// <param name="indexType">索引类型</param>
        /// <returns>如果支持索引访问返回true</returns>
        public static bool SupportsIndexAccess(TypeInfo targetType, TypeInfo indexType)
        {
            return targetType.Kind switch
            {
                TypeKind.String => indexType.Kind == TypeKind.Number || indexType.Kind == TypeKind.Any,
                TypeKind.Array => indexType.Kind == TypeKind.Number || indexType.Kind == TypeKind.Any,
                TypeKind.Dictionary => IsCompatible(indexType, targetType.KeyType ?? TypeInfo.Any),
                TypeKind.Any => true,
                _ => false
            };
        }
    }

    /// <summary>
    /// 类型转换级别
    /// 用于描述类型转换的安全程度和建议级别
    /// </summary>
    public enum ConversionLevel
    {
        /// <summary>无法转换</summary>
        None,
        
        /// <summary>完全相同的类型</summary>
        Identity,
        
        /// <summary>隐式转换（安全）</summary>
        Implicit,
        
        /// <summary>需要警告的转换</summary>
        Warning,
        
        /// <summary>未知类型转换</summary>
        Unknown
    }
}