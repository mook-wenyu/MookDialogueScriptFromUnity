using UnityEngine;

namespace MookDialogueScript
{
    /// <summary>
    /// Unity对话脚本加载器
    /// </summary>
    public class UnityDialogueLoader : IDialogueLoader
    {
        public string RootDir { get; }
        public string Extension { get; }

        /// <summary>
        /// 创建一个Unity对话脚本加载器，使用默认根目录和扩展名
        /// </summary>
        public UnityDialogueLoader() : this(string.Empty)
        {
        }

        /// <summary>
        /// 创建一个Unity对话脚本加载器
        /// </summary>
        /// <param name="rootDir">脚本文件根目录</param>
        /// <param name="extension">脚本文件扩展名</param>
        public UnityDialogueLoader(string rootDir, string extension = ".mds")
        {
            RootDir = rootDir;
            Extension = extension;
        }

        /// <summary>
        /// 加载脚本
        /// </summary>
        public void LoadScripts(Runner runner)
        {
            // 加载所有对话脚本
            var assets = Resources.LoadAll<TextAsset>(RootDir);

            LoadScriptContent(assets, runner);
        }

        /// <summary>
        /// 加载脚本内容
        /// </summary>
        public void LoadScriptContent(TextAsset[] assets,
            Runner runner)
        {
            foreach (var asset in assets)
            {
                // 创建词法分析器
                var lexer = new Lexers.Lexer();
                // 创建语法分析器
                var parser = new Parsing.Parser();

                var tokens = lexer.Tokenize(asset.text);
                var nodes = parser.Parse(tokens);

                // 注册脚本节点
                runner.RegisterScript(nodes);
            }
        }

    }
}
