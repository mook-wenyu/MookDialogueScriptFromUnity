using System;
using System.Collections.Generic;
using System.IO;

namespace MookDialogueScript.Incremental
{
    /// <summary>
    /// 文件元数据结构
    /// 存储文件的基本信息，用于变更检测和缓存验证
    /// </summary>
    public sealed class FileMetadata
    {
        /// <summary>
        /// 文件完整路径
        /// </summary>
        public string FilePath { get; private set; } = string.Empty;

        /// <summary>
        /// 文件名
        /// </summary>
        public string FileName { get; private set; } = string.Empty;

        /// <summary>
        /// 文件扩展名
        /// </summary>
        public string FileExtension { get; private set; } = string.Empty;

        /// <summary>
        /// 文件大小（字节）
        /// </summary>
        public long FileSize { get; private set; }

        /// <summary>
        /// 最后修改时间
        /// </summary>
        public DateTime LastModified { get; private set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedTime { get; private set; }

        /// <summary>
        /// 文件内容哈希值
        /// </summary>
        public string ContentHash { get; private set; } = string.Empty;

        /// <summary>
        /// 文件属性
        /// </summary>
        public FileAttributes Attributes { get; private set; }

        /// <summary>
        /// 是否存在
        /// </summary>
        public bool Exists { get; private set; }

        /// <summary>
        /// 是否为只读
        /// </summary>
        public bool IsReadOnly { get; private set; }

        /// <summary>
        /// 自定义元数据字典
        /// </summary>
        public Dictionary<string, object> CustomMetadata { get; private set; } = new Dictionary<string, object>();

        /// <summary>
        /// 元数据创建时间
        /// </summary>
        public DateTime MetadataCreatedTime { get; private set; } = DateTime.UtcNow;

        /// <summary>
        /// 从文件信息创建元数据
        /// </summary>
        /// <param name="fileInfo">文件信息</param>
        /// <param name="contentHash">内容哈希（可选）</param>
        /// <returns>文件元数据</returns>
        public static FileMetadata FromFileInfo(FileInfo fileInfo, string contentHash = null)
        {
            if (fileInfo == null)
                throw new ArgumentNullException(nameof(fileInfo));

            return new FileMetadata
            {
                FilePath = fileInfo.FullName,
                FileName = fileInfo.Name,
                FileExtension = fileInfo.Extension,
                FileSize = fileInfo.Exists ? fileInfo.Length : 0,
                LastModified = fileInfo.Exists ? fileInfo.LastWriteTimeUtc : DateTime.MinValue,
                CreatedTime = fileInfo.Exists ? fileInfo.CreationTimeUtc : DateTime.MinValue,
                ContentHash = contentHash ?? string.Empty,
                Attributes = fileInfo.Exists ? fileInfo.Attributes : FileAttributes.Normal,
                Exists = fileInfo.Exists,
                IsReadOnly = fileInfo.Exists && fileInfo.IsReadOnly
            };
        }

        /// <summary>
        /// 从文件路径创建元数据
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="contentHash">内容哈希（可选）</param>
        /// <returns>文件元数据</returns>
        public static FileMetadata FromFilePath(string filePath, string contentHash = null)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("文件路径不能为空", nameof(filePath));

            var fileInfo = new FileInfo(filePath);
            return FromFileInfo(fileInfo, contentHash);
        }

        /// <summary>
        /// 创建空的元数据（文件不存在）
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>空的文件元数据</returns>
        public static FileMetadata CreateEmpty(string filePath)
        {
            return new FileMetadata
            {
                FilePath = filePath,
                FileName = Path.GetFileName(filePath),
                FileExtension = Path.GetExtension(filePath),
                Exists = false
            };
        }

        /// <summary>
        /// 检查是否与其他元数据相等（用于变更检测）
        /// </summary>
        /// <param name="other">其他元数据</param>
        /// <param name="compareContent">是否比较内容哈希</param>
        /// <returns>是否相等</returns>
        public bool IsEquivalent(FileMetadata other, bool compareContent = true)
        {
            if (other == null)
                return false;

            // 基本属性比较
            if (FilePath != other.FilePath ||
                FileSize != other.FileSize ||
                LastModified != other.LastModified ||
                Exists != other.Exists)
            {
                return false;
            }

            // 内容哈希比较
            if (compareContent && !string.IsNullOrEmpty(ContentHash) && !string.IsNullOrEmpty(other.ContentHash))
            {
                return ContentHash.Equals(other.ContentHash, StringComparison.Ordinal);
            }

            return true;
        }

        /// <summary>
        /// 检查文件是否已过期（基于时间）
        /// </summary>
        /// <param name="maxAge">最大存活时间</param>
        /// <returns>是否过期</returns>
        public bool IsExpired(TimeSpan maxAge)
        {
            return DateTime.UtcNow - MetadataCreatedTime > maxAge;
        }

        /// <summary>
        /// 获取元数据摘要字符串
        /// </summary>
        /// <returns>摘要字符串</returns>
        public string GetDigest()
        {
            return $"{Path.GetFileName(FilePath)}|{FileSize}|{LastModified:yyyy-MM-dd HH:mm:ss}|{(string.IsNullOrEmpty(ContentHash) ? "NoHash" : ContentHash.Substring(0, Math.Min(8, ContentHash.Length)))}";
        }

        /// <summary>
        /// 计算元数据的哈希码
        /// </summary>
        /// <returns>哈希码</returns>
        public override int GetHashCode()
        {
            // 使用简单的哈希组合，兼容Unity版本
            int hash = 17;
            hash = hash * 31 + (FilePath?.GetHashCode() ?? 0);
            hash = hash * 31 + FileSize.GetHashCode();
            hash = hash * 31 + LastModified.GetHashCode();
            hash = hash * 31 + (ContentHash?.GetHashCode() ?? 0);
            return hash;
        }

        /// <summary>
        /// 更新内容哈希
        /// </summary>
        /// <param name="newHash">新的内容哈希</param>
        /// <returns>更新后的元数据</returns>
        public FileMetadata WithContentHash(string newHash)
        {
            var newMetadata = Clone();
            newMetadata.ContentHash = newHash;
            return newMetadata;
        }

        /// <summary>
        /// 添加自定义元数据
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        /// <returns>更新后的元数据</returns>
        public FileMetadata WithCustomMetadata(string key, object value)
        {
            var newMetadata = Clone();
            newMetadata.CustomMetadata = new Dictionary<string, object>(CustomMetadata);
            newMetadata.CustomMetadata[key] = value;
            return newMetadata;
        }

        /// <summary>
        /// 获取自定义元数据
        /// </summary>
        /// <typeparam name="T">值类型</typeparam>
        /// <param name="key">键</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>元数据值</returns>
        public T GetCustomMetadata<T>(string key, T defaultValue = default(T))
        {
            if (CustomMetadata.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return defaultValue;
        }

        /// <summary>
        /// 转换为字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString()
        {
            var status = Exists ? "存在" : "不存在";
            var size = Exists ? $"{FileSize:N0} 字节" : "N/A";
            var modified = Exists ? LastModified.ToString("yyyy-MM-dd HH:mm:ss") : "N/A";
            
            return $"文件: {FileName} ({status}, 大小: {size}, 修改: {modified})";
        }

        /// <summary>
        /// 克隆当前元数据
        /// </summary>
        /// <returns>克隆的元数据实例</returns>
        private FileMetadata Clone()
        {
            return new FileMetadata
            {
                FilePath = this.FilePath,
                FileName = this.FileName,
                FileExtension = this.FileExtension,
                FileSize = this.FileSize,
                LastModified = this.LastModified,
                CreatedTime = this.CreatedTime,
                ContentHash = this.ContentHash,
                Attributes = this.Attributes,
                Exists = this.Exists,
                IsReadOnly = this.IsReadOnly,
                CustomMetadata = new Dictionary<string, object>(this.CustomMetadata),
                MetadataCreatedTime = this.MetadataCreatedTime
            };
        }
    }
}