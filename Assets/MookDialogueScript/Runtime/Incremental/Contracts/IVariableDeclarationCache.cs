using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MookDialogueScript.Incremental.Contracts
{
    /// <summary>
    /// 变量声明缓存接口
    /// 负责缓存对话脚本中的变量声明信息，支持快速变量查询和类型推断
    /// </summary>
    public interface IVariableDeclarationCache : IDisposable
    {
        /// <summary>
        /// 获取指定文件中的所有变量声明
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>变量声明集合</returns>
        Task<IEnumerable<VariableDeclaration>> GetVariableDeclarationsAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取指定变量的声明信息
        /// </summary>
        /// <param name="variableName">变量名</param>
        /// <param name="scope">作用域（文件路径或节点名）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>变量声明信息</returns>
        Task<VariableDeclaration> GetVariableDeclarationAsync(string variableName, string scope = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 存储变量声明信息
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="declarations">变量声明集合</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>存储任务</returns>
        Task SetVariableDeclarationsAsync(string filePath, IEnumerable<VariableDeclaration> declarations, CancellationToken cancellationToken = default);

        /// <summary>
        /// 添加或更新单个变量声明
        /// </summary>
        /// <param name="declaration">变量声明</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>存储任务</returns>
        Task AddOrUpdateDeclarationAsync(VariableDeclaration declaration, CancellationToken cancellationToken = default);

        /// <summary>
        /// 移除指定文件的所有变量声明
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否成功移除</returns>
        Task<bool> RemoveFileDeclarationsAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// 移除指定变量的声明
        /// </summary>
        /// <param name="variableName">变量名</param>
        /// <param name="scope">作用域</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否成功移除</returns>
        Task<bool> RemoveVariableDeclarationAsync(string variableName, string scope = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 查询变量声明（支持模糊搜索）
        /// </summary>
        /// <param name="namePattern">变量名模式（支持通配符）</param>
        /// <param name="variableType">变量类型筛选</param>
        /// <param name="scope">作用域筛选</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>匹配的变量声明</returns>
        Task<IEnumerable<VariableDeclaration>> QueryDeclarationsAsync(
            string namePattern = null, 
            Type variableType = null,
            string scope = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取所有已缓存的变量名
        /// </summary>
        /// <param name="scope">作用域筛选</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>变量名集合</returns>
        Task<IEnumerable<string>> GetAllVariableNamesAsync(string scope = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取所有作用域
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>作用域集合</returns>
        Task<IEnumerable<string>> GetAllScopesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 批量获取变量声明
        /// </summary>
        /// <param name="requests">批量请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>变量声明字典</returns>
        Task<Dictionary<string, VariableDeclaration>> BatchGetDeclarationsAsync(
            IEnumerable<(string variableName, string scope)> requests,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 批量存储变量声明
        /// </summary>
        /// <param name="declarations">声明字典</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>存储任务</returns>
        Task BatchSetDeclarationsAsync(Dictionary<string, IEnumerable<VariableDeclaration>> declarations, CancellationToken cancellationToken = default);

        /// <summary>
        /// 检查变量是否存在声明
        /// </summary>
        /// <param name="variableName">变量名</param>
        /// <param name="scope">作用域</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否存在</returns>
        Task<bool> ContainsVariableAsync(string variableName, string scope = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 清空所有变量声明缓存
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>清空任务</returns>
        Task ClearAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 清理过期的变量声明缓存
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
        /// 验证缓存内容的完整性
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>验证结果</returns>
        Task<bool> ValidateIntegrityAsync(CancellationToken cancellationToken = default);

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