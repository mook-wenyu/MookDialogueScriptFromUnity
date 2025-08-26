using System;
using System.Collections.Generic;
using System.Linq;

namespace MookDialogueScript
{
    /// <summary>
    /// 通用工具函数类
    /// </summary>
    internal static class Utils
    {
        /// <summary>
        /// 计算两个字符串之间的Levenshtein编辑距离
        /// </summary>
        /// <param name="source">源字符串</param>
        /// <param name="target">目标字符串</param>
        /// <returns>编辑距离</returns>
        public static int LevenshteinDistance(string source, string target)
        {
            if (string.IsNullOrEmpty(source))
                return string.IsNullOrEmpty(target) ? 0 : target.Length;

            if (string.IsNullOrEmpty(target))
                return source.Length;

            var matrix = new int[source.Length + 1, target.Length + 1];

            // 初始化矩阵第一行和第一列
            for (int i = 0; i <= source.Length; i++)
                matrix[i, 0] = i;

            for (int j = 0; j <= target.Length; j++)
                matrix[0, j] = j;

            // 计算编辑距离
            for (int i = 1; i <= source.Length; i++)
            {
                for (int j = 1; j <= target.Length; j++)
                {
                    int cost = source[i - 1] == target[j - 1] ? 0 : 1;

                    matrix[i, j] = Math.Min(
                        Math.Min(matrix[i - 1, j] + 1,     // 删除
                                matrix[i, j - 1] + 1),     // 插入
                        matrix[i - 1, j - 1] + cost);      // 替换
                }
            }

            return matrix[source.Length, target.Length];
        }

        /// <summary>
        /// 根据编辑距离获取最相似的候选项
        /// </summary>
        /// <param name="target">目标字符串</param>
        /// <param name="candidates">候选项列表</param>
        /// <param name="maxDistance">最大编辑距离（默认为2）</param>
        /// <returns>最相似的候选项，如果没有找到则返回null</returns>
        public static string GetMostSimilarString(string target, IEnumerable<string> candidates, int maxDistance = 2)
        {
            if (string.IsNullOrEmpty(target) || candidates == null)
                return null;

            return candidates
                .Where(name => LevenshteinDistance(name.ToLowerInvariant(), target.ToLowerInvariant()) <= maxDistance)
                .OrderBy(name => LevenshteinDistance(name.ToLowerInvariant(), target.ToLowerInvariant()))
                .FirstOrDefault();
        }

        /// <summary>
        /// 获取多个相似的候选项建议
        /// </summary>
        /// <param name="target">目标字符串</param>
        /// <param name="candidates">候选项列表</param>
        /// <param name="maxSuggestions">最大建议数量</param>
        /// <param name="maxDistance">最大编辑距离</param>
        /// <returns>相似候选项列表</returns>
        public static List<string> GetSimilarStrings(string target, IEnumerable<string> candidates, 
            int maxSuggestions = 3, int? maxDistance = null)
        {
            if (string.IsNullOrEmpty(target) || candidates == null)
                return new List<string>();

            // 动态计算最大编辑距离：较长的字符串允许更大的编辑距离
            int effectiveMaxDistance = maxDistance ?? Math.Max(2, target.Length / 2);

            return candidates
                .Select(c => new { Name = c, Distance = LevenshteinDistance(target.ToLowerInvariant(), c.ToLowerInvariant()) })
                .Where(s => s.Distance <= effectiveMaxDistance)
                .OrderBy(s => s.Distance)
                .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .Take(maxSuggestions)
                .Select(s => s.Name)
                .ToList();
        }

        /// <summary>
        /// 生成建议消息文本
        /// </summary>
        /// <param name="target">目标字符串</param>
        /// <param name="candidates">候选项列表</param>
        /// <param name="maxSuggestions">最大建议数量</param>
        /// <returns>格式化的建议消息，如果没有找到建议则返回null</returns>
        public static string GenerateSuggestionMessage(string target, IEnumerable<string> candidates, int maxSuggestions = 3)
        {
            var suggestions = GetSimilarStrings(target, candidates, maxSuggestions);
            
            if (!suggestions.Any())
                return null;

            return $"你是否想要: {string.Join(", ", suggestions)}";
        }

        /// <summary>
        /// 检查两个字符串是否仅大小写不同
        /// </summary>
        /// <param name="str1">字符串1</param>
        /// <param name="str2">字符串2</param>
        /// <returns>如果仅大小写不同返回true</returns>
        public static bool IsOnlyCaseDifferent(string str1, string str2)
        {
            return !string.Equals(str1, str2, StringComparison.Ordinal) &&
                   string.Equals(str1, str2, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 在候选项中查找大小写不一致的匹配
        /// </summary>
        /// <param name="target">目标字符串</param>
        /// <param name="candidates">候选项列表</param>
        /// <returns>找到的大小写不一致项，如果没有则返回null</returns>
        public static string FindCaseInsensitiveMatch(string target, IEnumerable<string> candidates)
        {
            if (string.IsNullOrEmpty(target) || candidates == null)
                return null;

            return candidates.FirstOrDefault(c => IsOnlyCaseDifferent(target, c));
        }
    }
}