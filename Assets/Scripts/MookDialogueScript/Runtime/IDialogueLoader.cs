using System;
using UnityEngine;

namespace MookDialogueScript
{
    /// <summary>
    /// 对话脚本加载器接口
    /// </summary>
    public interface IDialogueLoader
    {
        /// <summary>
        /// 加载脚本
        /// </summary>
        /// <param name="runner">对话引擎运行器</param>
        public void LoadScripts(Runner runner);
    }

    /// <summary>
    /// Unity对话脚本加载器
    /// </summary>
    public class UnityDialogueLoader : IDialogueLoader
    {
        private readonly string _rootDir;
        private readonly string[] _extensions;

        public UnityDialogueLoader() : this(string.Empty)
        {
        }

        /// <summary>
        /// 创建一个Unity对话脚本加载器
        /// </summary>
        /// <param name="rootDir">脚本文件根目录</param>
        /// <param name="extensions">脚本文件扩展名数组（包含点号，如 .txt, .mds）</param>
        public UnityDialogueLoader(string rootDir, string[] extensions = null)
        {
            _rootDir = string.IsNullOrEmpty(rootDir) ? "DialogueScripts" : rootDir;
            _extensions = extensions ?? new[] {".txt", ".mds"};
        }

        /// <summary>
        /// 加载脚本
        /// </summary>
        public void LoadScripts(Runner runner)
        {
            // 加载所有对话脚本
            var assets = Resources.LoadAll(_rootDir);

            foreach (var asset in assets)
            {
                var textAsset = asset as TextAsset;
                if (textAsset != null)
                {
                    LoadScriptContent(textAsset.text, runner, asset.name);
                }
            }
        }

        /// <summary>
        /// 加载脚本内容
        /// </summary>
        private void LoadScriptContent(string scriptContent,
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
                Debug.LogError($"解析脚本内容时出错 (文件: {filePath}): {ex}");
            }
        }
    }

}
