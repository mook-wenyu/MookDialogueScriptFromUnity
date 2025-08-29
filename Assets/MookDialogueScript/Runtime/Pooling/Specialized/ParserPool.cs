using MookDialogueScript.Parsing;
using System.Collections.Generic;

namespace MookDialogueScript.Pooling
{
    /// <summary>
    /// Parser对象池工厂类
    /// 提供针对Parser优化的静态便捷方法
    /// 消除了不必要的包装层，直接使用通用对象池
    /// </summary>
    public static class ParserPoolFactory
    {
        /// <summary>
        /// 默认Parser池名称
        /// </summary>
        private const string DefaultPoolName = "DefaultParser";

        #region 池创建和获取
        /// <summary>
        /// 获取或创建默认Parser池
        /// </summary>
        /// <param name="options">池配置选项，为空时使用默认配置</param>
        /// <returns>Parser对象池实例</returns>
        public static IObjectPool<IParser> GetOrCreatePool(PoolOptions options = null)
        {
            return GlobalPoolManager.Instance.GetOrCreatePool<IParser>(
                DefaultPoolName,
                CreateParser,
                ResetParser,
                options ?? PoolOptions.Default
            );
        }

        /// <summary>
        /// 获取或创建命名Parser池
        /// </summary>
        /// <param name="poolName">池名称</param>
        /// <param name="options">池配置选项</param>
        /// <returns>Parser对象池实例</returns>
        public static IObjectPool<IParser> GetOrCreatePool(string poolName, PoolOptions options = null)
        {
            return GlobalPoolManager.Instance.GetOrCreatePool<IParser>(
                poolName,
                CreateParser,
                ResetParser,
                options ?? PoolOptions.Default
            );
        }

        /// <summary>
        /// 获取默认Parser池
        /// </summary>
        /// <returns>Parser对象池实例，不存在时返回null</returns>
        public static IObjectPool<IParser> GetPool()
        {
            return GlobalPoolManager.Instance.GetPool<IParser>(DefaultPoolName);
        }
        #endregion

        #region 便捷操作方法
        /// <summary>
        /// 直接租借Parser实例
        /// </summary>
        /// <returns>Parser实例</returns>
        public static IParser Rent()
        {
            return GetOrCreatePool().Rent();
        }

        /// <summary>
        /// 归还Parser实例
        /// </summary>
        /// <param name="parser">要归还的Parser实例</param>
        public static void Return(IParser parser)
        {
            GetPool()?.Return(parser);
        }

        /// <summary>
        /// 创建自动归还的作用域Parser（使用通用包装器）
        /// </summary>
        /// <returns>作用域Parser包装器</returns>
        public static ScopedPoolable<IParser> RentScoped()
        {
            var pool = GetOrCreatePool();
            var scopeHandler = pool.RentScoped(out var parser);
            return new ScopedPoolable<IParser>(parser, scopeHandler);
        }

        /// <summary>
        /// 直接解析Token列表并返回AST
        /// 自动管理Parser生命周期
        /// </summary>
        /// <param name="tokens">要解析的Token列表</param>
        /// <returns>解析结果的AST节点</returns>
        public static ScriptNode Parse(List<Token> tokens)
        {
            if (tokens == null || tokens.Count == 0)
                return null;

            using var scopedParser = RentScoped();
            return scopedParser.Item.Parse(tokens);
        }

        /// <summary>
        /// 批量解析多个Token列表
        /// 高效利用对象池进行批量语法分析
        /// </summary>
        /// <param name="tokenLists">Token列表的数组</param>
        /// <returns>AST节点列表</returns>
        public static List<ScriptNode> ParseBatch(List<Token>[] tokenLists)
        {
            if (tokenLists == null || tokenLists.Length == 0)
                return new List<ScriptNode>();

            var results = new List<ScriptNode>(tokenLists.Length);
            var pool = GetOrCreatePool();
            var parsers = pool.RentBatch(tokenLists.Length);

            try
            {
                for (int i = 0; i < tokenLists.Length; i++)
                {
                    results.Add(parsers[i].Parse(tokenLists[i]));
                }
            }
            finally
            {
                pool.ReturnBatch(parsers);
            }

            return results;
        }
        #endregion

        #region 监控和诊断
        /// <summary>
        /// 获取默认Parser池的统计信息
        /// </summary>
        /// <returns>池统计信息</returns>
        public static PoolStatistics GetStatistics()
        {
            var pool = GetPool();
            return pool?.GetStatistics() ?? default;
        }

        /// <summary>
        /// 获取指定Parser池的统计信息
        /// </summary>
        /// <param name="poolName">池名称</param>
        /// <returns>池统计信息</returns>
        public static PoolStatistics GetStatistics(string poolName)
        {
            var pool = GlobalPoolManager.Instance.GetPool<IParser>(poolName);
            return pool?.GetStatistics() ?? default;
        }

        /// <summary>
        /// 调整默认池大小
        /// </summary>
        public static void TrimPool()
        {
            GetPool()?.Trim();
        }

        /// <summary>
        /// 清空默认池
        /// </summary>
        public static void ClearPool()
        {
            GetPool()?.Clear();
        }

        /// <summary>
        /// 获取所有Parser的缓存统计信息
        /// 注意：由于对象池的特性，此方法返回的是静态统计信息
        /// </summary>
        /// <returns>缓存统计信息字典</returns>
        public static Dictionary<string, Dictionary<string, object>> GetAllCacheStatistics()
        {
            var allStats = new Dictionary<string, Dictionary<string, object>>();
            
            // 由于对象是池化的，无法直接获取所有活跃Parser的缓存统计
            // 这里返回空字典，实际应用中可以考虑其他方式收集统计信息
            
            return allStats;
        }
        #endregion

        #region 私有工厂方法
        /// <summary>
        /// 创建Parser实例的工厂方法
        /// 使用重构后的Parser实现
        /// </summary>
        /// <returns>新的Parser实例</returns>
        private static IParser CreateParser()
        {
            // 创建重构后的Parser实例
            // 注意：这里假设RefactoredParser类存在，实际应根据项目情况调整
            return new Parsing.Parser();
        }

        /// <summary>
        /// 重置Parser实例的方法
        /// 确保归还的Parser处于干净状态
        /// </summary>
        /// <param name="parser">要重置的Parser实例</param>
        private static void ResetParser(IParser parser)
        {
            parser?.Reset();
        }
        #endregion
    }
}