using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MookDialogueScript.Incremental
{
    /// <summary>
    /// 变量声明缓存实现
    /// 基于内存的高性能变量声明缓存，支持作用域查询、模糊搜索和使用统计
    /// </summary>
    public sealed class VariableDeclarationCache
    {
        // 主缓存：文件路径 -> 变量声明列表
        private readonly ConcurrentDictionary<string, List<VariableDeclaration>> _fileDeclarations;

        private readonly ReaderWriterLockSlim _rwLock;
        private volatile bool _disposed;

        /// <summary>
        /// 初始化变量声明缓存
        /// </summary>
        public VariableDeclarationCache()
        {
            _fileDeclarations = new ConcurrentDictionary<string, List<VariableDeclaration>>(StringComparer.OrdinalIgnoreCase);
            _rwLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        }

        /// <summary>
        /// 获取指定文件中的所有变量声明
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>变量声明集合</returns>
        public async Task<IEnumerable<VariableDeclaration>> GetVariableDeclarationsAsync(string filePath, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(filePath))
            {
                return null;
            }

            return await Task.Run(() =>
            {
                var cacheKey = GenerateCacheKey(filePath);

                if (_fileDeclarations.TryGetValue(cacheKey, out var vars))
                {
                    return vars.AsEnumerable();
                }

                return null;
            }, cancellationToken);
        }

        /// <summary>
        /// 存储变量声明信息
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="declarations">变量声明集合</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>存储任务</returns>
        public async Task SetVariableDeclarationsAsync(string filePath, IEnumerable<VariableDeclaration> declarations, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(filePath) || declarations == null)
                return;

            await Task.Run(() =>
            {
                var declarationList = declarations.ToList();
                var cacheKey = GenerateCacheKey(filePath);

                _rwLock.EnterWriteLock();
                try
                {
                    // 添加新的声明
                    _fileDeclarations[cacheKey] = declarationList;
                }
                finally
                {
                    _rwLock.ExitWriteLock();
                }

            }, cancellationToken);
        }

        /// <summary>
        /// 添加或更新单个变量声明
        /// </summary>
        /// <param name="declaration">变量声明</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>存储任务</returns>
        public async Task AddOrUpdateDeclarationAsync(VariableDeclaration declaration, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (declaration == null)
                return;

            await Task.Run(() =>
            {
                var filePath = declaration.DeclarationFilePath;
                var cacheKey = GenerateCacheKey(filePath);

                _rwLock.EnterWriteLock();
                try
                {
                    // 获取或创建文件的声明列表
                    if (!_fileDeclarations.TryGetValue(cacheKey, out var vars))
                    {
                        vars = new List<VariableDeclaration>();
                        _fileDeclarations[cacheKey] = vars;
                    }

                    // 查找现有声明
                    var existingIndex = vars.FindIndex(d =>
                        d.Name.Equals(declaration.Name, StringComparison.OrdinalIgnoreCase));

                    if (existingIndex >= 0)
                    {
                        // 更新现有声明
                        vars[existingIndex] = declaration;
                    }
                    else
                    {
                        // 添加新声明
                        vars.Add(declaration);
                    }
                }
                finally
                {
                    _rwLock.ExitWriteLock();
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 移除指定文件的所有变量声明
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否成功移除</returns>
        public async Task<bool> RemoveFileDeclarationsAsync(string filePath, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(filePath))
                return false;

            return await Task.Run(() =>
            {
                var cacheKey = GenerateCacheKey(filePath);

                _rwLock.EnterWriteLock();
                try
                {
                    if (_fileDeclarations.TryRemove(cacheKey, out _))
                    {
                        return true;
                    }

                    return false;
                }
                finally
                {
                    _rwLock.ExitWriteLock();
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 清空所有变量声明缓存
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>清空任务</returns>
        public async Task ClearAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            await Task.Run(() =>
            {
                _rwLock.EnterWriteLock();
                try
                {
                    _fileDeclarations.Clear();
                }
                finally
                {
                    _rwLock.ExitWriteLock();
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 生成缓存键
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>缓存键</returns>
        private string GenerateCacheKey(string filePath)
        {
            return $"{filePath.GetHashCode()}";
        }

        /// <summary>
        /// 检查是否已释放资源
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(VariableDeclarationCache));
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _rwLock.EnterWriteLock();
            try
            {
                _fileDeclarations.Clear();
            }
            finally
            {
                _rwLock.ExitWriteLock();
                _rwLock.Dispose();
            }
        }

    }
}
