using System;

namespace MookDialogueScript.Semantic.TypeSystem
{
    /// <summary>
    /// 语义分析器类型系统
    /// 定义了脚本语言中所有支持的类型种类
    /// </summary>
    public enum TypeKind
    {
        /// <summary>数字类型（包含所有数值类型）</summary>
        Number,
        
        /// <summary>字符串类型</summary>
        String,
        
        /// <summary>布尔类型</summary> 
        Boolean,
        
        /// <summary>空值类型</summary>
        Null,
        
        /// <summary>对象类型（映射到CLR对象）</summary>
        Object,
        
        /// <summary>数组类型</summary>
        Array,
        
        /// <summary>字典类型</summary>
        Dictionary,
        
        /// <summary>函数类型</summary>
        Function,
        
        /// <summary>任意类型（未知类型的占位符）</summary>
        Any,
        
        /// <summary>错误类型（类型推断失败时使用）</summary>
        Error
    }

    /// <summary>
    /// 类型信息类
    /// 使用值类型语义和不可变设计，支持复合类型的完整描述
    /// 采用静态工厂方法和单例模式优化性能
    /// </summary>
    public sealed class TypeInfo : IEquatable<TypeInfo>
    {
        /// <summary>
        /// 类型种类
        /// </summary>
        public TypeKind Kind { get; }
        
        /// <summary>
        /// CLR 类型（用于 Object 类型的具体类型信息）
        /// </summary>
        public Type ClrType { get; }
        
        /// <summary>
        /// 元素类型（用于 Array 类型）
        /// </summary>
        public TypeInfo ElementType { get; }
        
        /// <summary>
        /// 键类型（用于 Dictionary 类型）
        /// </summary>
        public TypeInfo KeyType { get; }
        
        /// <summary>
        /// 值类型（用于 Dictionary 类型）
        /// </summary>
        public TypeInfo ValueType { get; }

        /// <summary>
        /// 私有构造函数，确保只能通过工厂方法创建实例
        /// </summary>
        /// <param name="kind">类型种类</param>
        /// <param name="clrType">CLR类型</param>
        /// <param name="elementType">元素类型</param>
        /// <param name="keyType">键类型</param>
        /// <param name="valueType">值类型</param>
        private TypeInfo(TypeKind kind, Type clrType = null, TypeInfo elementType = null, TypeInfo keyType = null, TypeInfo valueType = null)
        {
            Kind = kind;
            ClrType = clrType;
            ElementType = elementType;
            KeyType = keyType;
            ValueType = valueType;
        }

        #region 预定义单例类型

        /// <summary>数字类型单例</summary>
        public static readonly TypeInfo Number = new TypeInfo(TypeKind.Number, typeof(double));
        
        /// <summary>字符串类型单例</summary>
        public static readonly TypeInfo String = new TypeInfo(TypeKind.String, typeof(string));
        
        /// <summary>布尔类型单例</summary>
        public static readonly TypeInfo Boolean = new TypeInfo(TypeKind.Boolean, typeof(bool));
        
        /// <summary>空值类型单例</summary>
        public static readonly TypeInfo Null = new TypeInfo(TypeKind.Null);
        
        /// <summary>函数类型单例</summary>
        public static readonly TypeInfo Function = new TypeInfo(TypeKind.Function);
        
        /// <summary>任意类型单例</summary>
        public static readonly TypeInfo Any = new TypeInfo(TypeKind.Any);
        
        /// <summary>错误类型单例</summary>
        public static readonly TypeInfo Error = new TypeInfo(TypeKind.Error);

        #endregion

        #region 工厂方法

        /// <summary>
        /// 创建对象类型
        /// </summary>
        /// <param name="clrType">CLR类型</param>
        /// <returns>对象类型实例</returns>
        public static TypeInfo Object(Type clrType) => new TypeInfo(TypeKind.Object, clrType);
        
        /// <summary>
        /// 创建数组类型
        /// </summary>
        /// <param name="elementType">元素类型</param>
        /// <returns>数组类型实例</returns>
        public static TypeInfo Array(TypeInfo elementType) => new TypeInfo(TypeKind.Array, elementType: elementType);
        
        /// <summary>
        /// 创建字典类型
        /// </summary>
        /// <param name="keyType">键类型</param>
        /// <param name="valueType">值类型</param>
        /// <returns>字典类型实例</returns>
        public static TypeInfo Dictionary(TypeInfo keyType, TypeInfo valueType) => 
            new TypeInfo(TypeKind.Dictionary, keyType: keyType, valueType: valueType);

        #endregion

        #region 相等性和哈希

        /// <summary>
        /// 值语义判等：比较所有字段是否相等
        /// </summary>
        /// <param name="other">要比较的其他TypeInfo实例</param>
        /// <returns>如果相等返回true，否则返回false</returns>
        public bool Equals(TypeInfo other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            
            return Kind == other.Kind &&
                   Equals(ClrType, other.ClrType) &&
                   Equals(ElementType, other.ElementType) &&
                   Equals(KeyType, other.KeyType) &&
                   Equals(ValueType, other.ValueType);
        }

        /// <summary>
        /// 重写Object.Equals方法
        /// </summary>
        /// <param name="obj">要比较的对象</param>
        /// <returns>如果相等返回true，否则返回false</returns>
        public override bool Equals(object obj)
        {
            return obj is TypeInfo other && Equals(other);
        }

        /// <summary>
        /// 与 Equals 一致的哈希值计算
        /// 使用质数组合减少哈希冲突
        /// </summary>
        /// <returns>类型信息的哈希值</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + Kind.GetHashCode();
                hash = hash * 31 + (ClrType?.GetHashCode() ?? 0);
                hash = hash * 31 + (ElementType?.GetHashCode() ?? 0);
                hash = hash * 31 + (KeyType?.GetHashCode() ?? 0);
                hash = hash * 31 + (ValueType?.GetHashCode() ?? 0);
                return hash;
            }
        }

        /// <summary>相等操作符重载</summary>
        public static bool operator ==(TypeInfo left, TypeInfo right) => Equals(left, right);
        
        /// <summary>不等操作符重载</summary>
        public static bool operator !=(TypeInfo left, TypeInfo right) => !Equals(left, right);

        #endregion

        #region 字符串表示

        /// <summary>
        /// 生成类型的字符串表示
        /// </summary>
        /// <returns>类型的字符串描述</returns>
        public override string ToString()
        {
            return Kind switch
            {
                TypeKind.Object => $"Object({ClrType?.Name ?? "unknown"})",
                TypeKind.Array => $"Array({ElementType})",
                TypeKind.Dictionary => $"Dictionary({KeyType}, {ValueType})",
                _ => Kind.ToString()
            };
        }

        #endregion

        #region 类型检查辅助方法

        /// <summary>
        /// 检查是否为基本类型（Number, String, Boolean, Null）
        /// </summary>
        public bool IsPrimitive => Kind is TypeKind.Number or TypeKind.String or TypeKind.Boolean or TypeKind.Null;
        
        /// <summary>
        /// 检查是否为复合类型（Object, Array, Dictionary）
        /// </summary>
        public bool IsComposite => Kind is TypeKind.Object or TypeKind.Array or TypeKind.Dictionary;
        
        /// <summary>
        /// 检查是否为集合类型（Array, Dictionary）
        /// </summary>
        public bool IsCollection => Kind is TypeKind.Array or TypeKind.Dictionary;
        
        /// <summary>
        /// 检查是否为错误或未知类型
        /// </summary>
        public bool IsErrorOrUnknown => Kind is TypeKind.Error or TypeKind.Any;
        
        /// <summary>
        /// 检查是否可以进行数值运算
        /// </summary>
        public bool IsNumeric => Kind == TypeKind.Number;
        
        /// <summary>
        /// 检查是否可以进行逻辑运算
        /// </summary>
        public bool IsLogical => Kind == TypeKind.Boolean;

        #endregion
    }
}