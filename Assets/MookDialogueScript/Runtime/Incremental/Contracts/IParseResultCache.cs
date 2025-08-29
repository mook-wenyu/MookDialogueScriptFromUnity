using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MookDialogueScript.Incremental.Contracts
{
    /// <summary>
    /// 解析结果缓存接口
    /// 负责缓存对话脚本的词法分析和语法解析结果
    /// </summary>
    public interface IParseResultCache : IDisposable
    {
        /// <summary>
        /// 尝试从缓存获取解析结果
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="fileMetadata">文件元数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>解析结果，如果缓存未命中则返回null</returns>
        Task<ParseResult> GetAsync(string filePath, FileMetadata fileMetadata, CancellationToken cancellationToken = default);

        /// <summary>
        /// 将解析结果存储到缓存
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="parseResult">解析结果</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>存储任务</returns>
        Task SetAsync(string filePath, ParseResult parseResult, CancellationToken cancellationToken = default);

        /// <summary>
        /// 从缓存中移除指定文件的解析结果
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否成功移除</returns>
        Task<bool> RemoveAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// 批量获取解析结果
        /// </summary>
        /// <param name="requests">批量请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>解析结果字典</returns>
        Task<Dictionary<string, ParseResult>> BatchGetAsync(
            IEnumerable<(string filePath, FileMetadata metadata)> requests,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 批量存储解析结果
        /// </summary>
        /// <param name="results">解析结果字典</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>存储任务</returns>
        Task BatchSetAsync(Dictionary<string, ParseResult> results, CancellationToken cancellationToken = default);

        /// <summary>
        /// 检查缓存中是否存在指定文件的有效解析结果
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="fileMetadata">文件元数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否存在有效缓存</returns>
        Task<bool> ContainsValidAsync(string filePath, FileMetadata fileMetadata, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取缓存中的所有文件路径
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>文件路径集合</returns>
        Task<IEnumerable<string>> GetAllKeysAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 清空所有解析结果缓存
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>清空任务</returns>
        Task ClearAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 清理过期的解析结果缓存
        /// </summary>
        /// <param name="maxAge">最大缓存时间</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>清理的项数</returns>
        Task<int> CleanupExpiredAsync(TimeSpan maxAge, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>缓存统计信息</returns>
        Task<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 预热缓存（预加载解析结果）
        /// </summary>
        /// <param name="filePaths">要预热的文件路径集合</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>预热任务</returns>
        Task WarmupAsync(IEnumerable<string> filePaths, CancellationToken cancellationToken = default);

        /// <summary>
        /// 验证缓存内容的完整性
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>验证结果</returns>
        Task<bool> ValidateIntegrityAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 计算缓存命中率
        /// </summary>
        /// <returns>命中率（0.0-1.0）</returns>
        double GetHitRatio();

        /// <summary>
        /// 缓存大小（项数）
        /// </summary>
        int Count { get; }

        /// <summary>
        /// 缓存使用的内存大小（字节）
        /// </summary>
        long MemoryUsage { get; }
    }
}