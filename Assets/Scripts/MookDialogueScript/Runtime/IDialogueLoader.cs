using System;

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
        private readonly string[] _extensions = new[] {".txt", ".mds"};
        
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
        public UnityDialogueLoader(string rootDir)
        {
            _rootDir = rootDir;
        }

        /// <summary>
        /// 加载脚本
        /// </summary>
        public void LoadScripts(Runner runner)
        {
            // 加载所有对话脚本
            var assets = UnityEngine.Resources.LoadAll(_rootDir);

            foreach (var asset in assets)
            {
                var textAsset = asset as UnityEngine.TextAsset;
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
                MLogger.Error($"解析脚本内容时出错 (文件: {filePath}): {ex}");
            }
        }
    }

}
