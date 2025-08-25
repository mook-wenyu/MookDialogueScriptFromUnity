namespace MookDialogueScript
{
    /// <summary>
    /// 对话脚本加载器接口
    /// </summary>
    public interface IDialogueLoader
    {
        /// <summary>
        /// 脚本文件根目录
        /// </summary>
        public string RootDir { get; }

        /// <summary>
        /// 脚本文件扩展名
        /// </summary>
        public string Extension { get; }

        /// <summary>
        /// 加载脚本
        /// </summary>
        /// <param name="runner">对话引擎运行器</param>
        public void LoadScripts(Runner runner);
    }

}
