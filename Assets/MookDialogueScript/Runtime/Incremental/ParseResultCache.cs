using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MookDialogueScript.Incremental
{
    /// <summary>
    /// 解析结果缓存实现
    /// 基于内存的高性能解析结果缓存，支持LRU淘汰策略和统计信息收集
    /// </summary>
    public sealed class ParseResultCache
    {
        private ConcurrentDictionary<string, ParseResult> _cache;
        private readonly ReaderWriterLockSlim _rwLock;
        private volatile bool _disposed;

        /// <summary>
        /// 缓存大小（项数）
        /// </summary>
        public int Count => _cache.Count;

        /// <summary>
        /// 获取缓存字典（只读）
        /// </summary>
        public IReadOnlyDictionary<string, ParseResult> Cache => _cache;

        /// <summary>
        /// 初始化解析结果缓存
        /// </summary>
        public ParseResultCache()
        {
            _cache = new ConcurrentDictionary<string, ParseResult>(StringComparer.OrdinalIgnoreCase);
            _rwLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        }

        public void LoadCache(ConcurrentDictionary<string, ParseResult> cache)
        {
            _cache = cache;
        }

        /// <summary>
        /// 尝试从缓存获取解析结果
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>解析结果，如果缓存未命中则返回null</returns>
        public ParseResult GetParseResult(string filePath)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(filePath))
            {
                return null;
            }

            var cacheKey = GenerateCacheKey(filePath);

            if (_cache.TryGetValue(cacheKey, out var result))
            {
                return result;
            }

            return null;
        }

        /// <summary>
        /// 将解析结果存储到缓存
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="parseResult">解析结果</param>
        /// <returns>存储任务</returns>
        public void SetParseResult(string filePath, ParseResult parseResult)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(filePath) || parseResult == null)
                return;

            var cacheKey = GenerateCacheKey(filePath);
            _cache[cacheKey] = parseResult;
        }

        /// <summary>
        /// 从缓存中移除指定文件的解析结果
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>是否成功移除</returns>
        public void RemoveParseResult(string filePath)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(filePath))
                return;

            var cacheKey = GenerateCacheKey(filePath);
            _cache.TryRemove(cacheKey, out _);
        }

        /// <summary>
        /// 获取缓存中的所有文件路径
        /// </summary>
        /// <returns>文件路径集合</returns>
        public IEnumerable<string> GetAllKeys()
        {
            ThrowIfDisposed();

            return _cache.Keys;
        }

        /// <summary>
        /// 清空所有解析结果缓存
        /// </summary>
        /// <returns>清空任务</returns>
        public void Clear()
        {
            ThrowIfDisposed();

            _cache.Clear();
        }

        /// <summary>
        /// 生成缓存键
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>缓存键</returns>
        private string GenerateCacheKey(string filePath)
        {
            return filePath.Replace("\\", "/").Replace("/", "_").Replace(".", "_").ToLowerInvariant();
        }

        /// <summary>
        /// 检查是否已释放资源
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ParseResultCache));
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _cache.Clear();
            _rwLock.Dispose();
        }

    }
}
