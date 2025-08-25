using System;
using System.Collections.Generic;
using UnityEngine.Scripting;

namespace MookDialogueScript
{
    /// <summary>
    /// 对话状态存储类，用于记录节点进入和脚本变量，支持保存和加载游戏状态
    /// </summary>
    [Serializable]
    public class DialogueStorage
    {
        /// <summary>
        /// 已访问的节点记录
        /// </summary>
        public Dictionary<string, int> visitedNodes = new();

        /// <summary>
        /// 脚本变量存储
        /// </summary>
        public Dictionary<string, RuntimeValue> variables = new();

        /// <summary>
        /// 是否正在对话中
        /// </summary>
        public bool isInDialogue = false;

        /// <summary>
        /// 对话的初始节点名称
        /// </summary>
        [Preserve]
        public string initialNodeName = string.Empty;

        /// <summary>
        /// 创建一个新的对话存储实例
        /// </summary>
        public DialogueStorage()
        {
        }

        /// <summary>
        /// 使用已有的对话上下文初始化存储
        /// </summary>
        /// <param name="context">对话上下文</param>
        public DialogueStorage(DialogueContext context)
        {
            // 从DialogueContext获取所有当前的脚本变量
            if (context != null)
            {
                variables = context.GetScriptVariables();
            }
        }

        /// <summary>
        /// 记录初始节点
        /// </summary>
        /// <param name="nodeName">初始节点名称</param>
        public void RecordInitialNode(string nodeName)
        {
            if (string.IsNullOrEmpty(nodeName)) return;
            initialNodeName = nodeName;
            isInDialogue = true;
        }

        /// <summary>
        /// 清除对话状态
        /// </summary>
        public void ClearDialogueState()
        {
            isInDialogue = false;
            initialNodeName = string.Empty;
        }

        /// <summary>
        /// 记录节点访问
        /// </summary>
        /// <param name="nodeName">节点名称</param>
        public void RecordNodeVisit(string nodeName)
        {
            if (string.IsNullOrEmpty(nodeName))
                return;

            if (!visitedNodes.TryAdd(nodeName, 1))
            {
                visitedNodes[nodeName]++;
            }
        }

        /// <summary>
        /// 检查节点是否已被访问
        /// </summary>
        /// <param name="nodeName">节点名称</param>
        /// <returns>节点是否已被访问</returns>
        public bool HasVisitedNode(string nodeName)
        {
            return visitedNodes.ContainsKey(nodeName) && visitedNodes[nodeName] > 0;
        }

        /// <summary>
        /// 获取节点的访问次数
        /// </summary>
        /// <param name="nodeName">节点名称</param>
        /// <returns>节点访问次数，未访问过则返回0</returns>
        public int GetNodeVisitCount(string nodeName)
        {
            return visitedNodes.GetValueOrDefault(nodeName, 0);
        }

        /// <summary>
        /// 清除特定节点的访问记录
        /// </summary>
        /// <param name="nodeName">节点名称</param>
        public void ClearNodeVisit(string nodeName)
        {
            visitedNodes.Remove(nodeName);
        }

        /// <summary>
        /// 清除所有节点的访问记录
        /// </summary>
        public void ClearAllNodeVisits()
        {
            visitedNodes.Clear();
        }

        /// <summary>
        /// 保存变量值
        /// </summary>
        /// <param name="name">变量名</param>
        /// <param name="value">变量值</param>
        public void SaveVariable(string name, RuntimeValue value)
        {
            variables[name] = value;
        }

        /// <summary>
        /// 将存储的状态应用到对话上下文中
        /// </summary>
        /// <param name="context">对话上下文</param>
        public void ApplyToContext(DialogueContext context)
        {
            // 加载存储的变量到上下文
            context?.LoadScriptVariables(variables);
        }

        /// <summary>
        /// 从对话上下文更新存储状态
        /// </summary>
        /// <param name="context">对话上下文</param>
        public void UpdateFromContext(DialogueContext context)
        {
            if (context == null)
                return;

            // 更新变量存储
            variables = context.GetScriptVariables();
        }
    }
}
