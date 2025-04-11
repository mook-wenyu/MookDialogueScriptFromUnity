using System;
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
        public UnityDialogueLoader() : this(string.Empty, ".mds")
        {
        }

        /// <summary>
        /// 创建一个Unity对话脚本加载器
        /// </summary>
        /// <param name="rootDir">脚本文件根目录</param>
        public UnityDialogueLoader(string rootDir) : this(rootDir, ".mds")
        {
        }

        /// <summary>
        /// 创建一个Unity对话脚本加载器
        /// </summary>
        /// <param name="rootDir">脚本文件根目录</param>
        /// <param name="extension">脚本文件扩展名</param>
        public UnityDialogueLoader(string rootDir, string extension)
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

            foreach (var asset in assets)
            {
                if (asset != null)
                {
                    LoadScriptContent(asset.text, runner, asset.name);
                }
            }
        }

        /// <summary>
        /// 加载脚本内容
        /// </summary>
        public void LoadScriptContent(string scriptContent,
            Runner runner, string filePath)
        {
            try
            {
                // 创建词法分析器
                var lexer = new Lexer(scriptContent);
                // 创建语法分析器
                var parser = new Parser(lexer.Tokenize());
                // 注册脚本节点
                runner.RegisterScript(parser.Parse());
            }
            catch (Exception ex)
            {
                MLogger.Error($"解析脚本内容时出错 (文件: {filePath}): {ex}");
            }
        }
    }
}
