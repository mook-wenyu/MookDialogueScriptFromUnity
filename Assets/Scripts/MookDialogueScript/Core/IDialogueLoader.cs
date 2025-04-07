using System;
using System.IO;
using System.Linq;
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
        /// <param name="dialogueManager">对话管理器</param>
        /// <returns>异步任务</returns>
        public void LoadScripts(Runner dialogueManager);
    }

    /// <summary>
    /// 默认对话脚本加载器
    /// </summary>
    public class DefaultDialogueLoader : IDialogueLoader
    {
        private readonly string _rootDir;
        private readonly string[] _extensions;

        public DefaultDialogueLoader() : this(string.Empty)
        {
        }

        /// <summary>
        /// 创建一个默认对话脚本加载器
        /// </summary>
        /// <param name="rootDir">脚本文件根目录</param>
        /// <param name="extensions">脚本文件扩展名数组（包含点号，如 .txt, .mds）</param>
        public DefaultDialogueLoader(string rootDir, string[] extensions = null)
        {
            if (string.IsNullOrEmpty(rootDir))
            {
                _rootDir = "DialogueScripts";
            }
            else
            {
                _rootDir = rootDir;
            }
            _extensions = extensions ?? new[] { ".txt", ".mds" };
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
                try
                {
                    var textAsset = asset as TextAsset;
                    if (textAsset != null)
                    {
                        LoadScriptContent(textAsset.text, runner, asset.name);
                        Debug.Log($"加载脚本文件 {asset.name} 成功");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"加载脚本文件 {asset.name} 时出错: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 加载脚本内容
        /// </summary>
        private void LoadScriptContent(string scriptContent,
            Runner runner, string filePath = "")
        {
            try
            {
                // 创建词法分析器
                var lexer = new Lexer(scriptContent);

                // 创建语法分析器
                var parser = new Parser(lexer.Tokenize());
                var scriptNode = parser.Parse();

                // 注册脚本节点
                runner.RegisterScript(scriptNode);
            }
            catch (Exception ex)
            {
                string fileInfo = string.IsNullOrEmpty(filePath) ? "" : $" (文件: {filePath})";
                Debug.LogError($"解析脚本内容时出错{fileInfo}: {ex.Message}");
                Debug.LogError(ex.StackTrace);
            }
        }
    }

}