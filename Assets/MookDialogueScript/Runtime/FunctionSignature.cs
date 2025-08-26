using System;
using System.Collections.Generic;
using System.Linq;

namespace MookDialogueScript
{
    /// <summary>
    /// 函数参数定义
    /// </summary>
    public class FunctionParameter 
    {
        /// <summary>
        /// 参数名称
        /// </summary>
        public string Name { get; }
        
        /// <summary>
        /// 参数类型名（脚本类型名：Number/String/Boolean/Function/Object等）
        /// </summary>
        public string TypeName { get; }
        
        /// <summary>
        /// 是否可选参数
        /// </summary>
        public bool IsOptional { get; }
        
        /// <summary>
        /// 默认值（用于语义检查与诊断）
        /// </summary>
        public object DefaultValue { get; }
        
        public FunctionParameter(string name, string typeName, bool isOptional = false, object defaultValue = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
            IsOptional = isOptional;
            DefaultValue = defaultValue;
        }

        public override string ToString()
        {
            var result = $"{TypeName} {Name}";
            if (IsOptional)
            {
                result += DefaultValue != null ? $" = {DefaultValue}" : " = null";
            }
            return result;
        }
    }

    /// <summary>
    /// 函数签名定义 - 用于语义分析和严格的参数校验
    /// </summary>
    public class FunctionSignature
    {
        /// <summary>
        /// 函数名称
        /// </summary>
        public string Name { get; }
        
        /// <summary>
        /// 返回类型名（脚本类型名）
        /// </summary>
        public string ReturnTypeName { get; }
        
        /// <summary>
        /// 参数列表
        /// </summary>
        public List<FunctionParameter> Parameters { get; }
        
        /// <summary>
        /// 来源类型：静态函数、对象方法
        /// </summary>
        public string SourceType { get; }
        
        /// <summary>
        /// 所属类型名
        /// </summary>
        public string SourceClassName { get; }
        
        /// <summary>
        /// 原始方法名
        /// </summary>
        public string MethodName { get; }
        
        public FunctionSignature(string name, string returnTypeName, List<FunctionParameter> parameters, 
            string sourceType, string sourceClassName = null, string methodName = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            ReturnTypeName = returnTypeName ?? throw new ArgumentNullException(nameof(returnTypeName));
            Parameters = parameters ?? new List<FunctionParameter>();
            SourceType = sourceType ?? throw new ArgumentNullException(nameof(sourceType));
            SourceClassName = sourceClassName;
            MethodName = methodName;
        }
        
        /// <summary>
        /// 最少必需参数数量
        /// </summary>
        public int MinRequiredParameters => Parameters.Count(p => !p.IsOptional);
        
        /// <summary>
        /// 最多参数数量
        /// </summary>
        public int MaxParameters => Parameters.Count;

        /// <summary>
        /// 格式化签名为字符串
        /// </summary>
        public string FormatSignature()
        {
            var paramStr = string.Join(", ", Parameters.Select(p => p.ToString()));
            return $"{ReturnTypeName} {Name}({paramStr})";
        }

        public override string ToString()
        {
            return FormatSignature();
        }

        /// <summary>
        /// 检查参数数量是否匹配
        /// </summary>
        public bool IsParameterCountValid(int argumentCount)
        {
            return argumentCount >= MinRequiredParameters && argumentCount <= MaxParameters;
        }
    }
}