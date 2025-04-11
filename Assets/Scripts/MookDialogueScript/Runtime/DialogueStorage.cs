using System;
using System.Collections.Generic;

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
        private Dictionary<string, int> _visitedNodes = new Dictionary<string, int>();

        /// <summary>
        /// 脚本变量存储
        /// </summary>
        private Dictionary<string, RuntimeValue> _variables = new Dictionary<string, RuntimeValue>();

        /// <summary>
        /// 获取已访问节点的记录（节点名和访问次数）
        /// </summary>
        public Dictionary<string, int> VisitedNodes => _visitedNodes;

        /// <summary>
        /// 获取所有已保存的脚本变量
        /// </summary>
        public Dictionary<string, RuntimeValue> Variables => _variables;

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
                _variables = new Dictionary<string, RuntimeValue>(context.GetScriptVariables());
            }
        }

        /// <summary>
        /// 记录节点访问
        /// </summary>
        /// <param name="nodeName">节点名称</param>
        public void RecordNodeVisit(string nodeName)
        {
            if (string.IsNullOrEmpty(nodeName))
                return;

            if (_visitedNodes.ContainsKey(nodeName))
            {
                _visitedNodes[nodeName]++;
            }
            else
            {
                _visitedNodes[nodeName] = 1;
            }
        }

        /// <summary>
        /// 检查节点是否已被访问
        /// </summary>
        /// <param name="nodeName">节点名称</param>
        /// <returns>节点是否已被访问</returns>
        public bool HasVisitedNode(string nodeName)
        {
            return _visitedNodes.ContainsKey(nodeName) && _visitedNodes[nodeName] > 0;
        }

        /// <summary>
        /// 获取节点的访问次数
        /// </summary>
        /// <param name="nodeName">节点名称</param>
        /// <returns>节点访问次数，未访问过则返回0</returns>
        public int GetNodeVisitCount(string nodeName)
        {
            return _visitedNodes.TryGetValue(nodeName, out int count) ? count : 0;
        }

        /// <summary>
        /// 检查节点是否是首次访问
        /// </summary>
        /// <param name="nodeName">节点名称</param>
        /// <returns>是否是首次访问</returns>
        public bool IsFirstVisit(string nodeName)
        {
            return GetNodeVisitCount(nodeName) == 1;
        }

        /// <summary>
        /// 清除特定节点的访问记录
        /// </summary>
        /// <param name="nodeName">节点名称</param>
        public void ClearNodeVisit(string nodeName)
        {
            if (_visitedNodes.ContainsKey(nodeName))
            {
                _visitedNodes.Remove(nodeName);
            }
        }

        /// <summary>
        /// 清除所有节点的访问记录
        /// </summary>
        public void ClearAllNodeVisits()
        {
            _visitedNodes.Clear();
        }

        /// <summary>
        /// 保存变量值
        /// </summary>
        /// <param name="name">变量名</param>
        /// <param name="value">变量值</param>
        public void SaveVariable(string name, RuntimeValue value)
        {
            _variables[name] = value;
        }

        /// <summary>
        /// 将存储的状态应用到对话上下文中
        /// </summary>
        /// <param name="context">对话上下文</param>
        public void ApplyToContext(DialogueContext context)
        {
            if (context == null)
                return;

            // 加载存储的变量到上下文
            context.LoadScriptVariables(_variables);
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
            _variables = context.GetScriptVariables();
        }

    }
} 