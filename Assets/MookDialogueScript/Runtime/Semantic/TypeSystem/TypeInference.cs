using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MookDialogueScript.Semantic.TypeSystem
{
    /// <summary>
    /// 类型推断器
    /// 负责从各种来源推断类型信息，包括CLR类型映射和AST节点类型推断
    /// </summary>
    public static class TypeInference
    {
        /// <summary>
        /// 从 CLR 类型映射到 TypeInfo
        /// 支持基础类型、集合类型、委托类型和自定义对象类型
        /// </summary>
        /// <param name="clrType">CLR类型</param>
        /// <returns>对应的TypeInfo</returns>
        public static TypeInfo FromClrType(Type clrType)
        {
            if (clrType == null)
                return TypeInfo.Null;

            // 基础类型映射 - 使用模式匹配提高性能
            if (IsNumericType(clrType))
                return TypeInfo.Number;

            if (clrType == typeof(string))
                return TypeInfo.String;

            if (clrType == typeof(bool))
                return TypeInfo.Boolean;

            // 数组类型
            if (clrType.IsArray)
            {
                var elementType = FromClrType(clrType.GetElementType());
                return TypeInfo.Array(elementType);
            }

            // 泛型集合类型
            if (clrType.IsGenericType)
            {
                var genericDef = clrType.GetGenericTypeDefinition();
                var genericArgs = clrType.GetGenericArguments();

                // 列表类型
                if (IsListType(genericDef))
                {
                    var elementType = FromClrType(genericArgs[0]);
                    return TypeInfo.Array(elementType);
                }

                // 字典类型
                if (IsDictionaryType(genericDef))
                {
                    var keyType = FromClrType(genericArgs[0]);
                    var valueType = FromClrType(genericArgs[1]);
                    return TypeInfo.Dictionary(keyType, valueType);
                }
            }

            // 委托类型
            if (typeof(Delegate).IsAssignableFrom(clrType))
                return TypeInfo.Function;

            // 其他对象类型
            return TypeInfo.Object(clrType);
        }

        /// <summary>
        /// 从 AST 节点推断类型
        /// 使用访问者模式处理不同类型的AST节点
        /// </summary>
        /// <param name="node">AST节点</param>
        /// <param name="symbolResolver">符号解析器</param>
        /// <returns>推断出的类型信息</returns>
        public static TypeInfo InferType(ASTNode node, Contracts.ISymbolResolver symbolResolver)
        {
            return node switch
            {
                NumberNode => TypeInfo.Number,
                BooleanNode => TypeInfo.Boolean,
                StringInterpolationExpressionNode => TypeInfo.String,
                VariableNode varNode => symbolResolver?.ResolveVariableType(varNode.Name) ?? TypeInfo.Any,
                IdentifierNode idNode => symbolResolver?.ResolveIdentifierType(idNode.Name) ?? TypeInfo.Any,
                BinaryOpNode binaryNode => InferBinaryOpType(binaryNode, symbolResolver),
                UnaryOpNode unaryNode => InferUnaryOpType(unaryNode, symbolResolver),
                CallExpressionNode callNode => InferCallType(callNode, symbolResolver),
                MemberAccessNode memberNode => InferMemberAccessType(memberNode, symbolResolver),
                IndexAccessNode indexNode => InferIndexAccessType(indexNode, symbolResolver),
                _ => TypeInfo.Any
            };
        }

        /// <summary>
        /// 推断二元运算的结果类型
        /// </summary>
        private static TypeInfo InferBinaryOpType(BinaryOpNode node, Contracts.ISymbolResolver symbolResolver)
        {
            var leftType = InferType(node.Left, symbolResolver);
            var rightType = InferType(node.Right, symbolResolver);

            return node.Operator switch
            {
                "+" or "-" or "*" or "/" or "%" => TypeInfo.Number,
                ">" or "<" or ">=" or "<=" or "==" or "!=" => TypeInfo.Boolean,
                "&&" or "||" or "xor" => TypeInfo.Boolean,
                _ => TypeInfo.Any
            };
        }

        /// <summary>
        /// 推断一元运算的结果类型
        /// </summary>
        private static TypeInfo InferUnaryOpType(UnaryOpNode node, Contracts.ISymbolResolver symbolResolver)
        {
            var operandType = InferType(node.Operand, symbolResolver);

            return node.Operator switch
            {
                "-" => TypeInfo.Number,
                "not" => TypeInfo.Boolean,
                _ => operandType
            };
        }

        /// <summary>
        /// 推断函数调用的返回类型
        /// </summary>
        private static TypeInfo InferCallType(CallExpressionNode node, Contracts.ISymbolResolver symbolResolver)
        {
            var calleeType = InferType(node.Callee, symbolResolver);
            
            if (calleeType.Kind == TypeKind.Function)
            {
                // 函数调用返回类型默认为 Any，可通过签名推断优化
                return TypeInfo.Any;
            }

            // 通过标识符名称查找函数信息
            if (node.Callee is IdentifierNode identifierNode)
            {
                var functionInfo = symbolResolver?.ResolveFunctionInfo(identifierNode.Name);
                return functionInfo?.ReturnType ?? TypeInfo.Any;
            }

            return TypeInfo.Error;
        }

        /// <summary>
        /// 推断成员访问的类型
        /// </summary>
        private static TypeInfo InferMemberAccessType(MemberAccessNode node, Contracts.ISymbolResolver symbolResolver)
        {
            var targetType = InferType(node.Target, symbolResolver);
            
            if (targetType.Kind == TypeKind.Object && targetType.ClrType != null)
            {
                var memberInfo = ResolveMember(targetType.ClrType, node.MemberName);
                if (memberInfo != null)
                {
                    return FromClrType(memberInfo.MemberType);
                }
            }

            return targetType.Kind == TypeKind.Any ? TypeInfo.Any : TypeInfo.Error;
        }

        /// <summary>
        /// 推断索引访问的类型
        /// </summary>
        private static TypeInfo InferIndexAccessType(IndexAccessNode node, Contracts.ISymbolResolver symbolResolver)
        {
            var targetType = InferType(node.Target, symbolResolver);
            var indexType = InferType(node.Index, symbolResolver);

            return targetType.Kind switch
            {
                TypeKind.String => TypeInfo.String,
                TypeKind.Array => targetType.ElementType ?? TypeInfo.Any,
                TypeKind.Dictionary => targetType.ValueType ?? TypeInfo.Any,
                TypeKind.Any => TypeInfo.Any,
                _ => TypeInfo.Error
            };
        }

        /// <summary>
        /// 解析CLR类型的成员信息
        /// </summary>
        /// <param name="type">目标类型</param>
        /// <param name="memberName">成员名称</param>
        /// <returns>解析的成员信息，未找到时返回null</returns>
        public static ResolvedMemberInfo ResolveMember(Type type, string memberName)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.IgnoreCase;
            
            // 查找属性
            var property = type.GetProperty(memberName, flags);
            if (property != null)
                return new ResolvedMemberInfo(property.PropertyType, MemberKind.Property, property.CanWrite);

            // 查找字段
            var field = type.GetField(memberName, flags);
            if (field != null)
                return new ResolvedMemberInfo(field.FieldType, MemberKind.Field, !field.IsInitOnly);

            // 查找方法
            var methods = type.GetMethods(flags)
                .Where(m => string.Equals(m.Name, memberName, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (methods.Length > 0)
                return new ResolvedMemberInfo(typeof(Delegate), MemberKind.Method, false);

            return null;
        }

        #region 类型检查辅助方法

        /// <summary>
        /// 检查是否为数值类型
        /// </summary>
        private static bool IsNumericType(Type type)
        {
            return type == typeof(double) || type == typeof(float) || type == typeof(int) || 
                   type == typeof(long) || type == typeof(short) || type == typeof(byte) ||
                   type == typeof(decimal) || type == typeof(uint) || type == typeof(ulong) ||
                   type == typeof(ushort) || type == typeof(sbyte);
        }

        /// <summary>
        /// 检查是否为列表类型
        /// </summary>
        private static bool IsListType(Type genericDef)
        {
            return genericDef == typeof(List<>) || 
                   genericDef == typeof(IList<>) || 
                   genericDef == typeof(ICollection<>) || 
                   genericDef == typeof(IEnumerable<>);
        }

        /// <summary>
        /// 检查是否为字典类型
        /// </summary>
        private static bool IsDictionaryType(Type genericDef)
        {
            return genericDef == typeof(Dictionary<,>) || 
                   genericDef == typeof(IDictionary<,>);
        }

        #endregion
    }

    /// <summary>
    /// 已解析的成员信息
    /// 包含成员的类型、种类和可写性信息
    /// </summary>
    public class ResolvedMemberInfo
    {
        /// <summary>成员的类型</summary>
        public Type MemberType { get; }
        
        /// <summary>成员的种类（属性、字段、方法）</summary>
        public MemberKind Kind { get; }
        
        /// <summary>是否可写</summary>
        public bool CanWrite { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="memberType">成员类型</param>
        /// <param name="kind">成员种类</param>
        /// <param name="canWrite">是否可写</param>
        public ResolvedMemberInfo(Type memberType, MemberKind kind, bool canWrite)
        {
            MemberType = memberType ?? throw new ArgumentNullException(nameof(memberType));
            Kind = kind;
            CanWrite = canWrite;
        }
    }

    /// <summary>
    /// 成员类型枚举
    /// </summary>
    public enum MemberKind
    {
        /// <summary>属性成员</summary>
        Property,
        
        /// <summary>字段成员</summary>
        Field,
        
        /// <summary>方法成员</summary>
        Method
    }
}