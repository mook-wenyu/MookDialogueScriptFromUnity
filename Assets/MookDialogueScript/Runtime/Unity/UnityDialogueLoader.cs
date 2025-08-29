using System.Threading.Tasks;
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
            // 创建多个任务并发使用Lexer池
            var tasks = new Task[assets.Length];
            for (int i = 0; i < tasks.Length; i++)
            {
                int taskId = i;
                tasks[i] = Task.Run(() =>
                {
                    var asset = assets[taskId];

                    // 创建词法分析器
                    var lexer = new Lexer();
                    lexer.Reset(asset.text);
                    // 创建语法分析器
                    var parser = new Parser(lexer.Tokenize());
                    // 注册脚本节点
                    runner.RegisterScript(parser.Parse());

                    Debug.Log($"任务 {taskId} 完成！");
                });
            }

            // 等待所有任务完成
            Task.WaitAll(tasks);
            Debug.Log("所有多线程任务完成");
        }


        /// <summary>
        /// 多线程安全使用示例
        /// </summary>
        /*private void ThreadSafeUsage()
        {
            Debug.Log("=== 多线程安全使用示例 ===");

            // 创建多个任务并发使用Lexer池
            var tasks = new System.Threading.Tasks.Task[3];

            for (int i = 0; i < tasks.Length; i++)
            {
                int taskId = i;
                tasks[i] = System.Threading.Tasks.Task.Run(() =>
                {
                    string source = $"--- task{taskId}\n:任务 {taskId} 的对话文本\nvar $task_id {taskId}\n===\n";

                    // 每个任务使用自己的池化Lexer
                    using var pooledLexer = LexerPoolManager.Instance.RentScoped(source);
                    var tokens = (pooledLexer as Lexer)?.Tokenize();

                    // 模拟处理
                    System.Threading.Thread.Sleep(10);

                    Debug.Log($"任务 {taskId} 完成，Token数量: {tokens.Count}");
                });
            }

            // 等待所有任务完成
            System.Threading.Tasks.Task.WaitAll(tasks);
            Debug.Log("所有多线程任务完成");
        }*/

    }
}
