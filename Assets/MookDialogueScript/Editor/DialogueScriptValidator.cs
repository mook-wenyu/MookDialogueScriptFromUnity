using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MookDialogueScript.Incremental;
using UnityEngine;
using UnityEditor;

namespace MookDialogueScript.Editor
{
    /// <summary>
    /// 对话脚本验证器
    /// 编辑器专用工具，用于自动验证.mds文件的语法正确性
    /// </summary>
    public class DialogueScriptValidator : AssetPostprocessor
    {
        #region 静态字段和配置
        private static IncrementalCacheManager _cacheManager;
        private static bool _isInitialized;

        // 验证配置
        private const string DIALOGUE_SCRIPT_EXTENSION = ".mds";
        #endregion

        #region Unity编辑器初始化
        /// <summary>
        /// 编辑器初始化时设置验证器
        /// </summary>
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            if (_isInitialized) return;

            _cacheManager = new IncrementalCacheManager();

            EditorApplication.delayCall += () =>
            {
                _cacheManager.Initialize();
                _isInitialized = true;

                // 进行初始化验证
                _ = ValidateAllScriptsAsync();
            };
        }
        #endregion

        #region AssetPostprocessor 实现
        /// <summary>
        /// 资源导入完成后的处理
        /// </summary>
        /// <param name="importedAssets">导入的资源</param>
        /// <param name="deletedAssets">删除的资源</param>
        /// <param name="movedAssets">移动的资源</param>
        /// <param name="movedFromAssetPaths">移动前的路径</param>
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (!_isInitialized) return;

            var dialogueScripts = new List<string>();

            // 检查导入的对话脚本
            foreach (var asset in importedAssets)
            {
                if (IsDialogueScript(asset))
                {
                    dialogueScripts.Add(asset);
                }
            }

            // 检查移动的对话脚本
            foreach (var asset in movedAssets)
            {
                if (IsDialogueScript(asset))
                {
                    dialogueScripts.Add(asset);
                }
            }

            // 清理已删除文件的验证结果
            foreach (var asset in deletedAssets)
            {
                if (IsDialogueScript(asset))
                {
                    _ = _cacheManager.ClearCacheAsync(asset);
                }
            }

            // 异步验证修改的脚本
            if (dialogueScripts.Count > 0)
            {
                _ = _cacheManager.ValidateScriptsAsync(dialogueScripts.ToArray());
            }
        }
        #endregion

        #region 批量验证和工具方法
        /// <summary>
        /// 验证所有对话脚本
        /// </summary>
        public static async Task ValidateAllScriptsAsync()
        {
            var scriptPaths = AssetDatabase.FindAssets("t:TextAsset")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(IsDialogueScript)
                .ToArray();

            if (scriptPaths.Length > 0)
            {
                await _cacheManager.ValidateScriptsAsync(scriptPaths);
            }
            else
            {
                Debug.Log("[对话脚本验证] 未找到对话脚本文件");
            }
        }

        /// <summary>
        /// 检查是否为对话脚本文件
        /// </summary>
        private static bool IsDialogueScript(string assetPath)
        {
            return assetPath.EndsWith(DIALOGUE_SCRIPT_EXTENSION, StringComparison.OrdinalIgnoreCase);
        }
        #endregion

        #region 编辑器菜单功能
        /// <summary>
        /// 手动验证所有对话脚本
        /// </summary>
        [MenuItem("Tools/MookDialogue/Validate All Scripts")]
        public static void ValidateAllScriptsMenuItem()
        {
            _ = ValidateAllScriptsAsync();
        }
        #endregion
    }
}
