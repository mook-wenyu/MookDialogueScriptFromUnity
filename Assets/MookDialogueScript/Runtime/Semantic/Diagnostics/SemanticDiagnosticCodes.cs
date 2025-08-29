namespace MookDialogueScript.Semantic.Diagnostics
{
    /// <summary>
    /// 语义分析诊断代码常量
    /// 定义了所有语义分析过程中可能产生的错误、警告和信息代码
    /// </summary>
    public static class SemanticDiagnosticCodes
    {
        #region 变量相关错误 (SEM001-SEM009)
        
        /// <summary>未定义变量</summary>
        public const string SEM001 = "SEM001";
        
        /// <summary>覆盖内置变量/重复定义</summary>
        public const string SEM002 = "SEM002";
        
        /// <summary>未知变量操作</summary>
        public const string SEM003 = "SEM003";
        
        /// <summary>只读变量修改</summary>
        public const string SEM004 = "SEM004";
        
        /// <summary>变量类型不匹配</summary>
        public const string SEM005 = "SEM005";

        #endregion

        #region 函数调用相关错误 (SEM010-SEM019)
        
        /// <summary>不可调用的值</summary>
        public const string SEM010 = "SEM010";
        
        /// <summary>未知函数名</summary>
        public const string SEM011 = "SEM011";
        
        /// <summary>函数名大小写不一致</summary>
        public const string SEM012 = "SEM012";
        
        /// <summary>参数计数不匹配</summary>
        public const string SEM013 = "SEM013";
        
        /// <summary>参数类型不兼容</summary>
        public const string SEM014 = "SEM014";
        
        /// <summary>不支持重载</summary>
        public const string SEM016 = "SEM016";

        #endregion

        #region 成员访问相关错误 (SEM020-SEM029)
        
        /// <summary>成员不存在</summary>
        public const string SEM020 = "SEM020";
        
        /// <summary>成员大小写不一致</summary>
        public const string SEM021 = "SEM021";

        #endregion

        #region 索引访问相关错误 (SEM030-SEM039)
        
        /// <summary>不可索引类型/键类型不匹配</summary>
        public const string SEM030 = "SEM030";

        #endregion

        #region 条件表达式相关错误 (SEM040-SEM049)
        
        /// <summary>条件非布尔</summary>
        public const string SEM040 = "SEM040";
        
        /// <summary>数值充当布尔</summary>
        public const string SEM041 = "SEM041";

        #endregion

        #region 运算符相关错误 (SEM050-SEM059)
        
        /// <summary>算数类型错误</summary>
        public const string SEM050 = "SEM050";
        
        /// <summary>常量除零/比较不同类型</summary>
        public const string SEM051 = "SEM051";

        #endregion

        #region 跳转相关错误 (SEM060-SEM069)
        
        /// <summary>跳转目标不存在</summary>
        public const string SEM060 = "SEM060";
        
        /// <summary>跳转大小写不一致/字符串插值类型错误</summary>
        public const string SEM061 = "SEM061";
        
        /// <summary>自跳转</summary>
        public const string SEM062 = "SEM062";

        #endregion

        #region 结构相关错误 (SEM070-SEM079)
        
        /// <summary>重复节点名/未知类型操作</summary>
        public const string SEM070 = "SEM070";

        #endregion

        #region 通用错误 (SEM990-SEM999)
        
        /// <summary>其他未处理错误</summary>
        public const string SEM999 = "SEM999";

        #endregion

        /// <summary>
        /// 获取诊断代码的描述
        /// </summary>
        /// <param name="code">诊断代码</param>
        /// <returns>代码描述</returns>
        public static string GetDescription(string code)
        {
            return code switch
            {
                SEM001 => "未定义变量",
                SEM002 => "覆盖内置变量或重复定义",
                SEM003 => "未知变量操作",
                SEM004 => "只读变量修改",
                SEM005 => "变量类型不匹配",
                SEM010 => "不可调用的值",
                SEM011 => "未知函数名",
                SEM012 => "函数名大小写不一致",
                SEM013 => "参数计数不匹配",
                SEM014 => "参数类型不兼容",
                SEM016 => "不支持函数重载",
                SEM020 => "成员不存在",
                SEM021 => "成员大小写不一致",
                SEM030 => "不可索引类型或键类型不匹配",
                SEM040 => "条件表达式非布尔类型",
                SEM041 => "数值用作布尔值",
                SEM050 => "算术运算类型错误",
                SEM051 => "常量除零或比较不同类型",
                SEM060 => "跳转目标不存在",
                SEM061 => "跳转大小写不一致或字符串插值类型错误",
                SEM062 => "自跳转警告",
                SEM070 => "重复节点名或未知类型操作",
                SEM999 => "其他未处理错误",
                _ => "未知诊断代码"
            };
        }

        /// <summary>
        /// 获取诊断代码的类别
        /// </summary>
        /// <param name="code">诊断代码</param>
        /// <returns>代码类别</returns>
        public static string GetCategory(string code)
        {
            if (string.IsNullOrEmpty(code) || !code.StartsWith("SEM"))
                return "Unknown";

            if (code.Length >= 6 && int.TryParse(code.Substring(3, 3), out int codeNumber))
            {
                return codeNumber switch
                {
                    >= 1 and <= 9 => "Variable",
                    >= 10 and <= 19 => "Function",
                    >= 20 and <= 29 => "Member",
                    >= 30 and <= 39 => "Index",
                    >= 40 and <= 49 => "Condition",
                    >= 50 and <= 59 => "Operator",
                    >= 60 and <= 69 => "Jump",
                    >= 70 and <= 79 => "Structure",
                    >= 990 and <= 999 => "General",
                    _ => "Other"
                };
            }

            return "Unknown";
        }
    }
}