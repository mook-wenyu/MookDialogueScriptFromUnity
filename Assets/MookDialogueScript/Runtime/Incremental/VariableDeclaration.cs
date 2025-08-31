using System;
using System.Collections.Generic;
using System.Linq;

namespace MookDialogueScript.Incremental
{
    /// <summary>
    /// 变量声明信息结构
    /// 存储对话脚本中变量的声明和类型信息
    /// </summary>
    public sealed class VariableDeclaration
    {
        /// <summary>
        /// 变量名称
        /// </summary>
        public string Name { get; private set; } = string.Empty;

        /// <summary>
        /// 变量类型
        /// </summary>
        public ValueType Type { get; private set; } = ValueType.Null;

        /// <summary>
        /// 类型名称（字符串表示）
        /// </summary>
        public string TypeName => Enum.GetName(typeof(ValueType), Type);

        /// <summary>
        /// 变量的初始值（如果有）
        /// </summary>
        public string DefaultValue { get; private set; }

        /// <summary>
        /// 变量描述或注释
        /// </summary>
        public string Description { get; private set; }

        /// <summary>
        /// 是否为全局变量
        /// </summary>
        public bool IsGlobal { get; private set; }

        /// <summary>
        /// 声明位置（文件路径）
        /// </summary>
        public string DeclarationFilePath { get; private set; } = string.Empty;

        /// <summary>
        /// 创建变量声明
        /// </summary>
        /// <param name="name">变量名</param>
        /// <param name="type">变量类型</param>
        /// <param name="defaultValue">默认值</param>
        /// <param name="filePath">声明文件路径</param>
        /// <param name="isGlobal">是否为全局变量</param>
        /// <returns>变量声明实例</returns>
        public static VariableDeclaration Create(
            string name,
            ValueType type,
            string defaultValue,
            string filePath,
            bool isGlobal = false)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("变量名不能为空", nameof(name));

            return new VariableDeclaration
            {
                Name = name,
                Type = type,
                DefaultValue = defaultValue,
                DeclarationFilePath = filePath,
                IsGlobal = isGlobal
            };
        }

        /// <summary>
        /// 检查类型是否兼容
        /// </summary>
        /// <param name="targetType">目标类型</param>
        /// <returns>是否兼容</returns>
        public bool IsTypeCompatible(ValueType targetType)
        {
            if (Type == targetType)
                return true;

            return false;
        }

        /// <summary>
        /// 转换为字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString()
        {
            return $"{Name} ({TypeName}), 位置: {DeclarationFilePath}";
        }

        /// <summary>
        /// 克隆当前变量声明
        /// </summary>
        /// <returns>克隆的变量声明实例</returns>
        private VariableDeclaration Clone()
        {
            return new VariableDeclaration
            {
                Name = this.Name,
                Type = this.Type,
                DefaultValue = this.DefaultValue,
                Description = this.Description,
                IsGlobal = this.IsGlobal,
                DeclarationFilePath = this.DeclarationFilePath,
            };
        }
    }
}
