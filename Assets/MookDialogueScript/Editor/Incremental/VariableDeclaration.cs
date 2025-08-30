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
        public Type VariableType { get; private set; } = typeof(object);

        /// <summary>
        /// 类型名称（字符串表示）
        /// </summary>
        public string TypeName => VariableType.Name;

        /// <summary>
        /// 声明位置（文件路径）
        /// </summary>
        public string DeclarationFilePath { get; private set; } = string.Empty;

        /// <summary>
        /// 声明行号
        /// </summary>
        public int DeclarationLine { get; private set; }

        /// <summary>
        /// 声明列号
        /// </summary>
        public int DeclarationColumn { get; private set; }

        /// <summary>
        /// 作用域（全局、节点名等）
        /// </summary>
        public string Scope { get; private set; } = string.Empty;

        /// <summary>
        /// 变量的初始值（如果有）
        /// </summary>
        public object DefaultValue { get; private set; }

        /// <summary>
        /// 初始值的字符串表示
        /// </summary>
        public string DefaultValueText { get; private set; }

        /// <summary>
        /// 是否为全局变量
        /// </summary>
        public bool IsGlobal { get; private set; }

        /// <summary>
        /// 是否为内置变量
        /// </summary>
        public bool IsBuiltIn { get; private set; }

        /// <summary>
        /// 变量描述或注释
        /// </summary>
        public string Description { get; private set; }

        /// <summary>
        /// 变量使用统计
        /// </summary>
        public VariableUsageStats UsageStats { get; private set; } = new VariableUsageStats();

        /// <summary>
        /// 创建变量声明
        /// </summary>
        /// <param name="name">变量名</param>
        /// <param name="type">变量类型</param>
        /// <param name="filePath">声明文件路径</param>
        /// <param name="line">声明行号</param>
        /// <param name="column">声明列号</param>
        /// <param name="scope">作用域</param>
        /// <returns>变量声明实例</returns>
        public static VariableDeclaration Create(
            string name,
            Type type,
            string filePath,
            int line,
            int column,
            string scope = "")
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("变量名不能为空", nameof(name));

            return new VariableDeclaration
            {
                Name = name,
                VariableType = type ?? typeof(object),
                DeclarationFilePath = filePath,
                DeclarationLine = line,
                DeclarationColumn = column,
                Scope = scope,
                IsGlobal = string.IsNullOrEmpty(scope) || scope.Equals("global", StringComparison.OrdinalIgnoreCase)
            };
        }

        /// <summary>
        /// 创建全局变量声明
        /// </summary>
        /// <param name="name">变量名</param>
        /// <param name="type">变量类型</param>
        /// <param name="defaultValue">默认值</param>
        /// <param name="filePath">声明文件路径</param>
        /// <returns>全局变量声明实例</returns>
        public static VariableDeclaration CreateGlobal(
            string name,
            Type type,
            object defaultValue = null,
            string filePath = "")
        {
            var declaration = Create(name, type, filePath, 0, 0, "global");
            declaration.DefaultValue = defaultValue;
            declaration.DefaultValueText = defaultValue?.ToString();
            declaration.IsGlobal = true;
            return declaration;
        }

        /// <summary>
        /// 创建内置变量声明
        /// </summary>
        /// <param name="name">变量名</param>
        /// <param name="type">变量类型</param>
        /// <param name="description">描述</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>内置变量声明实例</returns>
        public static VariableDeclaration CreateBuiltIn(
            string name,
            Type type,
            string description = null,
            object defaultValue = null)
        {
            return new VariableDeclaration
            {
                Name = name,
                VariableType = type,
                IsBuiltIn = true,
                IsGlobal = true,
                Scope = "builtin",
                Description = description,
                DefaultValue = defaultValue,
                DefaultValueText = defaultValue?.ToString(),
                DeclarationFilePath = "<built-in>"
            };
        }

        /// <summary>
        /// 检查类型是否兼容
        /// </summary>
        /// <param name="targetType">目标类型</param>
        /// <returns>是否兼容</returns>
        public bool IsTypeCompatible(Type targetType)
        {
            if (VariableType == targetType)
                return true;

            return targetType.IsAssignableFrom(VariableType);
        }

        /// <summary>
        /// 更新使用统计
        /// </summary>
        /// <param name="usage">使用类型</param>
        /// <param name="filePath">使用文件路径</param>
        /// <param name="line">使用行号</param>
        /// <returns>更新后的声明</returns>
        public VariableDeclaration WithUsage(VariableUsageType usage, string filePath, int line)
        {
            var newDeclaration = Clone();
            newDeclaration.UsageStats = UsageStats.AddUsage(usage, filePath, line);
            return newDeclaration;
        }

        /// <summary>
        /// 获取变量的完全限定名
        /// </summary>
        /// <returns>完全限定名</returns>
        public string GetQualifiedName()
        {
            if (IsGlobal || string.IsNullOrEmpty(Scope))
                return Name;

            return $"{Scope}.{Name}";
        }

        /// <summary>
        /// 获取变量签名字符串
        /// </summary>
        /// <returns>签名字符串</returns>
        public string GetSignature()
        {
            var signature = $"{TypeName} {Name}";

            if (DefaultValue != null)
            {
                signature += $" = {DefaultValueText}";
            }

            if (!IsGlobal && !string.IsNullOrEmpty(Scope))
                signature += $" (in {Scope})";

            return signature;
        }

        /// <summary>
        /// 转换为字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString()
        {
            var scope = IsGlobal ? "全局" : Scope;
            const string type = "变量";
            var location = $"{System.IO.Path.GetFileName(DeclarationFilePath)}:{DeclarationLine}";

            return $"{type} {Name} ({TypeName}) - 作用域: {scope}, 位置: {location}";
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
                VariableType = this.VariableType,
                DeclarationFilePath = this.DeclarationFilePath,
                DeclarationLine = this.DeclarationLine,
                DeclarationColumn = this.DeclarationColumn,
                Scope = this.Scope,
                DefaultValue = this.DefaultValue,
                DefaultValueText = this.DefaultValueText,
                IsGlobal = this.IsGlobal,
                IsBuiltIn = this.IsBuiltIn,
                Description = this.Description,
                UsageStats = this.UsageStats,
            };
        }
    }

    /// <summary>
    /// 变量使用统计信息
    /// </summary>
    public sealed class VariableUsageStats
    {
        /// <summary>
        /// 读取次数
        /// </summary>
        public int ReadCount { get; private set; }

        /// <summary>
        /// 写入次数
        /// </summary>
        public int WriteCount { get; private set; }

        /// <summary>
        /// 总使用次数
        /// </summary>
        public int TotalUsageCount => ReadCount + WriteCount;

        /// <summary>
        /// 使用记录列表
        /// </summary>
        public List<VariableUsageRecord> UsageRecords { get; private set; } = new List<VariableUsageRecord>();

        /// <summary>
        /// 第一次使用时间
        /// </summary>
        public DateTime? FirstUsed { get; private set; }

        /// <summary>
        /// 最后使用时间
        /// </summary>
        public DateTime? LastUsed { get; private set; }

        /// <summary>
        /// 使用的文件数量
        /// </summary>
        public int UsedFileCount => UsageRecords.Select(r => r.FilePath).Distinct().Count();

        /// <summary>
        /// 添加使用记录
        /// </summary>
        /// <param name="usageType">使用类型</param>
        /// <param name="filePath">文件路径</param>
        /// <param name="line">行号</param>
        /// <returns>更新后的统计信息</returns>
        public VariableUsageStats AddUsage(VariableUsageType usageType, string filePath, int line)
        {
            var now = DateTime.UtcNow;
            var newRecord = new VariableUsageRecord
            {
                UsageType = usageType,
                FilePath = filePath,
                Line = line,
                Timestamp = now
            };

            var newStats = Clone();
            newStats.UsageRecords.Add(newRecord);

            if (usageType == VariableUsageType.Read)
                newStats.ReadCount++;
            else if (usageType == VariableUsageType.Write)
                newStats.WriteCount++;

            newStats.FirstUsed = FirstUsed ?? now;
            newStats.LastUsed = now;

            return newStats;
        }

        /// <summary>
        /// 获取在指定文件中的使用次数
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>使用次数</returns>
        public int GetUsageCountInFile(string filePath)
        {
            return UsageRecords.Count(r => r.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 获取使用该变量的所有文件
        /// </summary>
        /// <returns>文件路径集合</returns>
        public IEnumerable<string> GetUsedFiles()
        {
            return UsageRecords.Select(r => r.FilePath).Distinct();
        }

        /// <summary>
        /// 克隆当前使用统计
        /// </summary>
        /// <returns>克隆的使用统计实例</returns>
        private VariableUsageStats Clone()
        {
            return new VariableUsageStats
            {
                ReadCount = this.ReadCount,
                WriteCount = this.WriteCount,
                UsageRecords = new List<VariableUsageRecord>(this.UsageRecords),
                FirstUsed = this.FirstUsed,
                LastUsed = this.LastUsed
            };
        }
    }

    /// <summary>
    /// 变量使用记录
    /// </summary>
    public sealed class VariableUsageRecord
    {
        /// <summary>
        /// 使用类型
        /// </summary>
        public VariableUsageType UsageType { get; set; }

        /// <summary>
        /// 使用文件路径
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// 使用行号
        /// </summary>
        public int Line { get; set; }

        /// <summary>
        /// 使用时间戳
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 上下文信息
        /// </summary>
        public string Context { get; set; }
    }

    /// <summary>
    /// 变量使用类型
    /// </summary>
    public enum VariableUsageType
    {
        /// <summary>
        /// 读取
        /// </summary>
        Read,

        /// <summary>
        /// 写入
        /// </summary>
        Write,

        /// <summary>
        /// 声明
        /// </summary>
        Declaration
    }
}
