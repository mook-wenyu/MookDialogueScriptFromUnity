using MookDialogueScript.Lexers;
using System.Collections.Generic;

namespace MookDialogueScript.Pooling
{
    /// <summary>
    /// Lexer对象池工厂类
    /// 提供针对Lexer优化的静态便捷方法
    /// 消除了不必要的包装层，直接使用通用对象池
    /// </summary>
    public static class LexerPoolFactory
    {
        /// <summary>
        /// 默认Lexer池名称
        /// </summary>
        private const string DefaultPoolName = "DefaultLexer";

        #region 池创建和获取
        /// <summary>
        /// 获取或创建默认Lexer池
        /// </summary>
        /// <param name="options">池配置选项，为空时使用默认配置</param>
        /// <returns>Lexer对象池实例</returns>
        public static IObjectPool<Lexer> GetOrCreatePool(PoolOptions options = null)
        {
            return GlobalPoolManager.Instance.GetOrCreatePool<Lexer>(
                DefaultPoolName,
                CreateLexer,
                ResetLexer,
                options ?? PoolOptions.Default
            );
        }

        /// <summary>
        /// 获取或创建命名Lexer池
        /// </summary>
        /// <param name="poolName">池名称</param>
        /// <param name="options">池配置选项</param>
        /// <returns>Lexer对象池实例</returns>
        public static IObjectPool<Lexer> GetOrCreatePool(string poolName, PoolOptions options = null)
        {
            return GlobalPoolManager.Instance.GetOrCreatePool<Lexer>(
                poolName,
                CreateLexer,
                ResetLexer,
                options ?? PoolOptions.Default
            );
        }

        /// <summary>
        /// 获取默认Lexer池
        /// </summary>
        /// <returns>Lexer对象池实例，不存在时返回null</returns>
        public static IObjectPool<Lexer> GetPool()
        {
            return GlobalPoolManager.Instance.GetPool<Lexer>(DefaultPoolName);
        }
        #endregion

        #region 便捷操作方法
        /// <summary>
        /// 直接租借Lexer实例
        /// </summary>
        /// <returns>Lexer实例</returns>
        public static Lexer Rent()
        {
            return GetOrCreatePool().Rent();
        }

        /// <summary>
        /// 归还Lexer实例
        /// </summary>
        /// <param name="lexer">要归还的Lexer实例</param>
        public static void Return(Lexer lexer)
        {
            GetPool()?.Return(lexer);
        }

        /// <summary>
        /// 创建自动归还的作用域Lexer（使用通用包装器）
        /// </summary>
        /// <param name="source">可选的源代码，为Lexer提供初始输入</param>
        /// <returns>作用域Lexer包装器</returns>
        public static ScopedPoolable<Lexer> RentScoped(string source = null)
        {
            var pool = GetOrCreatePool();
            var scopeHandler = pool.RentScoped(out var lexer);
            
            if (!string.IsNullOrEmpty(source))
            {
                lexer.Reset(source);
            }
            
            return new ScopedPoolable<Lexer>(lexer, scopeHandler);
        }

        /// <summary>
        /// 直接处理源代码并返回Token列表
        /// 自动管理Lexer生命周期
        /// </summary>
        /// <param name="source">要分析的源代码</param>
        /// <returns>Token列表</returns>
        public static List<Token> Tokenize(string source)
        {
            if (string.IsNullOrEmpty(source))
                return new List<Token>();

            using var scopedLexer = RentScoped(source);
            return scopedLexer.Item.Tokenize();
        }

        /// <summary>
        /// 批量处理多个源文件
        /// 高效利用对象池进行批量词法分析
        /// </summary>
        /// <param name="sources">源代码数组</param>
        /// <returns>Token列表的集合</returns>
        public static List<List<Token>> TokenizeBatch(string[] sources)
        {
            if (sources == null || sources.Length == 0)
                return new List<List<Token>>();

            var results = new List<List<Token>>(sources.Length);
            var pool = GetOrCreatePool();
            var lexers = pool.RentBatch(sources.Length);

            try
            {
                for (int i = 0; i < sources.Length; i++)
                {
                    lexers[i].Reset(sources[i]);
                    results.Add(lexers[i].Tokenize());
                }
            }
            finally
            {
                pool.ReturnBatch(lexers);
            }

            return results;
        }
        #endregion

        #region 监控和诊断
        /// <summary>
        /// 获取默认Lexer池的统计信息
        /// </summary>
        /// <returns>池统计信息</returns>
        public static PoolStatistics GetStatistics()
        {
            var pool = GetPool();
            return pool?.GetStatistics() ?? default;
        }

        /// <summary>
        /// 获取指定Lexer池的统计信息
        /// </summary>
        /// <param name="poolName">池名称</param>
        /// <returns>池统计信息</returns>
        public static PoolStatistics GetStatistics(string poolName)
        {
            var pool = GlobalPoolManager.Instance.GetPool<Lexer>(poolName);
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
        #endregion

        #region 私有工厂方法
        /// <summary>
        /// 创建Lexer实例的工厂方法
        /// 可以在此处添加特定的初始化逻辑
        /// </summary>
        /// <returns>新的Lexer实例</returns>
        private static Lexer CreateLexer()
        {
            return new Lexer();
        }

        /// <summary>
        /// 重置Lexer实例的方法
        /// 确保归还的Lexer处于干净状态
        /// </summary>
        /// <param name="lexer">要重置的Lexer实例</param>
        private static void ResetLexer(Lexer lexer)
        {
            lexer?.Reset(string.Empty);
        }
        #endregion
    }
}