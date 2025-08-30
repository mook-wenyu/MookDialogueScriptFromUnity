using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Scripting;

namespace MookDialogueScript
{
    /// <summary>
    /// 运行器，负责运行对话
    /// </summary>
    public class Runner
    {
        private readonly DialogueContext _context;
        private readonly Interpreter _interpreter;

        // 是否正在执行中（等待用户输入等状态）
        private bool _isExecuting;

        // 条件状态
        private readonly Dictionary<ConditionNode, (int branch, int elifIndex, int contentIndex)> _conditionStates =
            new();

        // 当前收集的选项列表
        private readonly List<ChoiceNode> _currentChoices = new();

        // 是否正在收集选项
        private bool _isCollectingChoices;

        // AST节点执行栈，用于跟踪嵌套执行
        private readonly Stack<(ASTNode node, int index)> _executionStack = new();

        // 是否有可选择的选项
        [Preserve]
        public bool HasChoices => _isCollectingChoices && _currentChoices.Count > 0;

        /// <summary>
        /// 获取当前对话存储
        /// </summary>
        [Preserve]
        public DialogueStorage Storage { get; private set; }

        /// <summary>
        /// 获取对话上下文（用于访问性能统计等功能）
        /// </summary>
        [Preserve]
        public DialogueContext Context => _context;

        /// <summary>
        /// 对话开始事件，需要存储对话状态并存档，对话结束前禁止存档
        /// </summary>
        public event Func<Task> OnDialogueStarted;

        /// <summary>
        /// 节点开始事件
        /// </summary>
        [Preserve]
        public event Action<string> OnNodeStarted;

        /// <summary>
        /// 对话显示事件
        /// </summary>
        public event Action<DialogueNode> OnDialogueDisplayed;

        /// <summary>
        /// 选项显示事件
        /// </summary>
        public event Action<List<ChoiceNode>> OnChoicesDisplayed;

        /// <summary>
        /// 选项选择事件
        /// </summary>
        public event Action<ChoiceNode, int> OnOptionSelected;

        /// <summary>
        /// 对话完成事件，存档时机
        /// </summary>
        public event Action OnDialogueCompleted;

        public Runner() : this(new UnityDialogueLoader())
        {
        }

        public Runner(string rootDir) : this(new UnityDialogueLoader(rootDir))
        {
        }

        public Runner(IDialogueLoader loader)
        {
            // 初始化日志系统
            MLogger.Initialize(new UnityLogger());

            // 初始化对话上下文
            _context = new DialogueContext();
            // 初始化表达式解释器
            _interpreter = new Interpreter(_context);
            // 初始化存储
            Storage = new DialogueStorage();

            // 重置状态
            _conditionStates.Clear();
            _currentChoices.Clear();
            _isCollectingChoices = false;
            // 清空执行栈
            _executionStack.Clear();

            // 注册内置函数
            RegisterBuiltinFunctions();

            // 加载脚本
            loader.LoadScripts(this);
        }

        private void RegisterBuiltinFunctions()
        {
            // 注册内置函数
            _context.RegisterFunction("visited", (Func<string, bool>)(nodeName => Storage.HasVisitedNode(nodeName)));
            _context.RegisterFunction("visit_count", (Func<string, int>)(nodeName => Storage.GetNodeVisitCount(nodeName)));
        }

        /// <summary>
        /// 设置对话存储
        /// </summary>
        /// <param name="storage">对话存储实例</param>
        [Preserve]
        public void SetStorage(DialogueStorage storage)
        {
            Storage = storage;
            // 应用存储的变量
            storage.ApplyToContext(_context);
        }

        /// <summary>
        /// 获取当前对话存储
        /// </summary>
        /// <returns>对话存储</returns>
        [Preserve]
        public DialogueStorage GetCurrentStorage()
        {
            // 更新存储中的变量
            Storage.UpdateFromContext(_context);
            return Storage;
        }

        /// <summary>
        /// 注册脚本
        /// </summary>
        /// <param name="script">脚本</param>
        public void RegisterScript(ScriptNode script)
        {
            _interpreter.RegisterNodes(script);
        }

        /// <summary>
        /// 获取节点中的下一个内容
        /// </summary>
        /// <param name="node">当前节点</param>
        /// <param name="currentIndex">当前内容索引</param>
        /// <returns>下一个内容索引，如果没有下一个内容则返回-1</returns>
        [Preserve]
        public int GetNextContentIndex(NodeDefinitionNode node, int currentIndex)
        {
            if (currentIndex < node.Content.Count - 1)
            {
                return currentIndex + 1;
            }
            return -1;
        }

        /// <summary>
        /// 注册脚本变量
        /// </summary>
        /// <param name="name">变量名</param>
        /// <param name="value">变量值</param>
        public void RegisterVariable(string name, RuntimeValue value)
        {
            _context.RegisterScriptVariable(name, value);
        }

        /// <summary>
        /// 注册内置变量
        /// </summary>
        /// <param name="name">变量名</param>
        /// <param name="getter">获取变量值的委托</param>
        /// <param name="setter">设置变量值的委托</param>
        [Preserve]
        public void RegisterVariable(string name, Func<object> getter, Action<object> setter)
        {
            _context.RegisterBuiltinVariable(name, getter, setter);
        }

        /// <summary>
        /// 设置变量值
        /// </summary>
        /// <param name="name">变量名</param>
        /// <param name="value">变量值</param>
        [Preserve]
        public void SetVariable(string name, RuntimeValue value)
        {
            _context.SetVariable(name, value);
        }

        /// <summary>
        /// 获取变量值
        /// </summary>
        /// <param name="name">变量名</param>
        /// <returns>变量值</returns>
        [Preserve]
        public RuntimeValue GetVariable(string name)
        {
            return _context.GetVariable(name);
        }

        /// <summary>
        /// 注册对象实例，将其属性和字段注册为变量，方法注册为函数
        /// </summary>
        /// <param name="name">对象名</param>
        /// <param name="instance">对象实例</param>
        public void RegisterObject(string name, object instance)
        {
            _context.RegisterObject(name, instance);
        }

        /// <summary>
        /// 注册函数
        /// </summary>
        /// <param name="name">函数名</param>
        /// <param name="function">函数</param>
        [Preserve]
        public void RegisterFunction(string name, Delegate function)
        {
            _context.RegisterFunction(name, function);
        }

        /// <summary>
        /// 获取所有已注册的函数
        /// </summary>
        /// <returns>函数名和描述的字典</returns>
        [Preserve]
        public Dictionary<string, string> GetRegisteredFunctions()
        {
            return _context.GetRegisteredFunctions();
        }

        /// <summary>
        /// 获取所有内置变量
        /// </summary>
        /// <returns>内置变量字典</returns>
        [Preserve]
        public Dictionary<string, RuntimeValue> GetBuiltinVariables()
        {
            return _context.GetBuiltinVariables();
        }

        /// <summary>
        /// 获取所有变量（包括内置变量和脚本变量）
        /// </summary>
        /// <returns>所有变量字典</returns>
        [Preserve]
        public Dictionary<string, RuntimeValue> GetAllVariables()
        {
            return _context.GetAllVariables();
        }

        /// <summary>
        /// 开始执行对话
        /// </summary>
        /// <param name="startNodeName">起始节点名称，默认为"start"</param>
        /// <param name="startContentIndex">起始内容索引，默认为0</param>
        /// <param name="force">是否强制开始，忽略当前执行状态</param>
        /// <returns>异步任务</returns>
        public async Task StartDialogue(string startNodeName = "start", int startContentIndex = 0, bool force = false)
        {
            // 如果当前有对话在进行，不允许开始新对话
            if (!force && Storage.isInDialogue)
            {
                MLogger.Warning("当前对话尚未结束，无法开始新的对话");
                return;
            }

            NodeDefinitionNode startNode;
            try
            {
                startNode = _context.GetNode(startNodeName);
            }
            catch (Exception ex)
            {
                MLogger.Error($"找不到起始节点 '{startNodeName}': {ex}");
                return;
            }

            // 记录初始节点到存储中
            Storage.RecordInitialNode(startNodeName);

            // 触发对话开始事件
            if (OnDialogueStarted != null)
            {
                await Task.WhenAll(OnDialogueStarted.GetInvocationList()
                    .Cast<Func<Task>>()
                    .Select(handler => handler()));
            }

            // 重置选项收集状态
            _currentChoices.Clear();
            _isCollectingChoices = false;

            // 清空执行栈
            _executionStack.Clear();

            if (startContentIndex < 0 || startContentIndex >= startNode.Content.Count)
            {
                MLogger.Error($"起始内容索引无效: {startContentIndex}");
                startContentIndex = 0;
            }
            // 将当前节点压入栈
            _executionStack.Push((startNode, startContentIndex));

            // 触发节点开始事件
            OnNodeStarted?.Invoke(startNodeName);

            // 记录节点访问
            Storage.RecordNodeVisit(startNodeName);

            // 显示第一个内容
            await Continue();
        }

        /// <summary>
        /// 继续执行当前节点
        /// </summary>
        /// <returns>异步任务</returns>
        public async Task Continue()
        {
            if (_isExecuting)
            {
                MLogger.Warning("当前正在执行中，请等待执行完成");
                return;
            }

            // 如果执行栈为空，无法继续执行
            if (_executionStack.Count == 0)
            {
                MLogger.Error("没有当前执行上下文");
                return;
            }

            // 如果正在收集选项，并且已经有选项，则处理选项
            if (_isCollectingChoices && _currentChoices.Count > 0)
            {
                MLogger.Warning("当前正在等待选择选项");
                return;
            }

            // 获取当前执行上下文
            (var node, int index) = _executionStack.Peek();

            // 根据当前节点类型执行不同的逻辑
            if (node is NodeDefinitionNode currentNode)
            {
                // 处理单个内容
                await ProcessNextContent(currentNode, index);
            }
            else if (node is DialogueNode dialogueNode)
            {
                // 处理对话节点中的嵌套内容
                await ProcessNextContent(dialogueNode, index);
            }
            else if (node is ChoiceNode choiceNode)
            {
                // 处理选项中的单个内容
                await ProcessNextContent(choiceNode, index);
            }
            else if (node is ConditionNode conditionNode)
            {
                // 处理条件内容
                await ProcessConditionContent(conditionNode, index);
            }
            else
            {
                MLogger.Error($"未知的节点类型: {node.GetType().Name}");
                await EndDialogue(true);
            }
        }

        /// <summary>
        /// 处理下一个内容
        /// </summary>
        /// <param name="contentContainer">内容容器</param>
        /// <param name="index">当前索引</param>
        private async Task ProcessNextContent(ASTNode contentContainer, int index)
        {
            List<ContentNode> contents;

            if (contentContainer is NodeDefinitionNode nodeDef)
            {
                contents = nodeDef.Content;
            }
            else if (contentContainer is ChoiceNode choice)
            {
                contents = choice.Content;
            }
            else if (contentContainer is DialogueNode dialogue)
            {
                contents = dialogue.Content;
            }
            else
            {
                MLogger.Error($"不支持的内容容器类型: {contentContainer.GetType().Name}");
                _executionStack.Pop(); // 弹出当前节点
                if (_executionStack.Count > 0)
                {
                    await Continue(); // 继续执行而不是抛出异常
                }
                else
                {
                    await EndDialogue(true);
                }
                return;
            }

            // 检查索引是否有效
            if (index < 0 || index >= contents.Count)
            {
                // 如果是嵌套内容结束，回到父节点
                if (contentContainer is ChoiceNode or DialogueNode)
                {
                    _executionStack.Pop(); // 弹出当前容器

                    // 继续执行父节点的下一个内容
                    await Continue();
                    return;
                }

                // 如果是主节点结束，结束对话
                await EndDialogue();
                return;
            }

            // 获取当前内容
            var content = contents[index];

            // 处理通用内容逻辑
            await ProcessContentCommon(content, contentContainer, index, contents);
        }

        /// <summary>
        /// 处理条件内容
        /// </summary>
        /// <param name="conditionNode">条件节点</param>
        /// <param name="index">当前索引</param>
        private async Task ProcessConditionContent(ConditionNode conditionNode, int index)
        {
            // 获取或初始化条件状态，如果条件节点不存在，则评估主条件
            if (!_conditionStates.TryGetValue(conditionNode, out var state))
            {
                // 评估主条件
                var condition = await _interpreter.EvaluateExpression(conditionNode.Condition);
                if (condition.Type != ValueType.Boolean)
                {
                    MLogger.Error("条件必须计算为布尔类型");
                    // 默认假设条件为假，继续处理
                    condition = new RuntimeValue(false);
                }

                if ((bool)condition.Value)
                {
                    state = (0, 0, 0); // then分支
                }
                else
                {
                    // 检查elif分支
                    var elifExecuted = false;
                    for (var i = 0; i < conditionNode.ElifContents.Count; i++)
                    {
                        var (elifCondition, _) = conditionNode.ElifContents[i];
                        condition = await _interpreter.EvaluateExpression(elifCondition);
                        if (condition.Type != ValueType.Boolean)
                        {
                            MLogger.Error("Elif 条件必须计算为布尔类型");
                            // 默认假设条件为假，继续处理
                            condition = new RuntimeValue(false);
                        }

                        if ((bool)condition.Value)
                        {
                            state = (1, i, 0); // elif分支
                            elifExecuted = true;
                            break;
                        }
                    }

                    // 如果没有elif分支为真，且有else分支，执行else分支
                    if (!elifExecuted && conditionNode.ElseContent != null)
                    {
                        state = (2, 0, 0); // else分支
                    }
                    else if (!elifExecuted)
                    {
                        // 没有条件匹配，跳过条件节点
                        _executionStack.Pop(); // 弹出条件节点
                        await Continue();
                        return;
                    }
                }
                _conditionStates[conditionNode] = state;
            }

            // 根据分支状态获取内容列表
            List<ContentNode> contents;
            if (state.branch == 0) // then分支
            {
                contents = conditionNode.ThenContent;
            }
            else if (state.branch == 1) // elif分支
            {
                contents = conditionNode.ElifContents[state.elifIndex].Item2;
            }
            else // else分支
            {
                contents = conditionNode.ElseContent;
            }

            // 检查索引是否有效
            if (index < 0 || index >= contents.Count)
            {
                // 条件执行完毕，弹出条件节点
                _executionStack.Pop();
                _conditionStates.Remove(conditionNode);

                // 继续执行
                await Continue();
                return;
            }

            // 获取当前内容
            var content = contents[index];

            // 处理内容并更新条件状态
            await ProcessContentCommon(content, conditionNode, index, contents);

            // 如果是对话或旁白，更新条件状态
            if (content is DialogueNode)
            {
                _conditionStates[conditionNode] = (state.branch, state.elifIndex, index + 1);
            }
        }

        /// <summary>
        /// 处理公共内容逻辑
        /// </summary>
        /// <param name="content">内容节点</param>
        /// <param name="contentContainer">内容容器</param>
        /// <param name="index">内容索引</param>
        /// <param name="contents">内容列表</param>
        private async Task ProcessContentCommon(
            ContentNode content,
            ASTNode contentContainer,
            int index,
            List<ContentNode> contents)
        {
            // 设置执行状态
            _isExecuting = true;

            try
            {
                // 处理命令节点
                if (content is CommandNode commandNode)
                {
                    // PrintCommand(commandNode);
                    // 执行命令
                    string nextNodeName = await _interpreter.ExecuteCommand(commandNode);
                    if (!string.IsNullOrEmpty(nextNodeName))
                    {
                        await JumpToNode(nextNodeName);
                        return;
                    }

                    // 更新执行栈
                    _executionStack.Pop();
                    _executionStack.Push((contentContainer, index + 1));

                    _isExecuting = false;
                    // 继续处理下一个内容
                    await Continue();
                }
                // 处理选项节点
                else if (content is ChoiceNode choiceNode)
                {
                    // 开始收集选项
                    _isCollectingChoices = true;
                    _currentChoices.Clear();

                    // 收集当前选项
                    _currentChoices.Add(choiceNode);

                    // 收集后续选项
                    int nextIndex = index + 1;
                    while (nextIndex < contents.Count && contents[nextIndex] is ChoiceNode nextChoice)
                    {
                        _currentChoices.Add(nextChoice);
                        nextIndex++;
                    }

                    // 更新执行栈
                    _executionStack.Pop();
                    _executionStack.Push((contentContainer, nextIndex));

                    // 触发选项显示事件
                    if (_currentChoices.Count > 0)
                    {
                        OnChoicesDisplayed?.Invoke(_currentChoices);
                    }
                    else
                    {
                        _isCollectingChoices = false;
                        _isExecuting = false;
                        await Continue();
                    }
                }
                // 处理嵌套条件节点
                else if (content is ConditionNode conditionNode)
                {
                    // 更新执行栈
                    _executionStack.Pop();
                    _executionStack.Push((contentContainer, index + 1)); // 更新容器索引
                    _executionStack.Push((conditionNode, 0));            // 压入条件节点

                    _isExecuting = false;
                    // 处理条件内容
                    await ProcessConditionContent(conditionNode, 0);
                }
                // 处理对话或旁白
                else if (content is DialogueNode dialogueNode)
                {
                    // 显示内容
                    OnDialogueDisplayed?.Invoke(dialogueNode);

                    // 更新执行栈
                    _executionStack.Pop();

                    // 检查对话节点是否有嵌套内容
                    if (dialogueNode.Content is {Count: > 0})
                    {
                        // 把下一个内容的索引先压入堆栈
                        _executionStack.Push((contentContainer, index + 1));
                        // 再把对话节点的内容压入栈，从第一个开始处理
                        _executionStack.Push((dialogueNode, 0));

                        // 检查下一个内容是否为选项
                        int nextContentType = await HasNextContent();
                        if (nextContentType == 2) // 2表示选项内容
                        {
                            _isExecuting = false;
                            await Continue();
                        }
                    }
                    else
                    {
                        // 如果没有嵌套内容，继续执行下一个内容
                        _executionStack.Push((contentContainer, index + 1));

                        // 检查下一个内容是否为选项
                        int nextContentType = await HasNextContent();
                        if (nextContentType == 2) // 2表示选项内容
                        {
                            _isExecuting = false;
                            await Continue();
                        }
                    }
                }
                else
                {
                    MLogger.Error($"未知的内容类型 {content.GetType().Name}");
                }
            }
            finally
            {
                _isExecuting = false;
            }
        }

        /// <summary>
        /// 评估选项条件
        /// </summary>
        /// <param name="node">选项节点</param>
        /// <returns>如果选项条件满足，返回true；否则返回false</returns>
        public async Task<bool> EvaluateChoiceCondition(ChoiceNode node)
        {
            // 如果有条件，先检查条件
            if (node.Condition != null)
            {
                var condition = await _interpreter.EvaluateExpression(node.Condition);
                if (condition.Type != ValueType.Boolean)
                {
                    MLogger.Error("选项条件必须计算为布尔类型");
                    return false;
                }
                return (bool)condition.Value;
            }
            return true;
        }

        /// <summary>
        /// 选择选项
        /// </summary>
        /// <param name="index">选项索引</param>
        /// <returns>异步任务</returns>
        public async Task SelectChoice(int index)
        {
            if (_isExecuting)
            {
                MLogger.Warning("当前正在执行中，请等待执行完成");
                return;
            }

            if (!_isCollectingChoices || _currentChoices.Count == 0)
            {
                MLogger.Error("当前没有可选择的选项");
                return;
            }

            if (index < 0 || index >= _currentChoices.Count)
            {
                MLogger.Error($"选项索引无效: {index}");
                if (_currentChoices.Count > 0)
                {
                    index = 0;
                }
                else
                {
                    return;
                }
            }

            var selectedChoice = _currentChoices[index];

            // 评估选项条件
            if (selectedChoice.Condition != null)
            {
                bool conditionMet = await EvaluateChoiceCondition(selectedChoice);
                if (!conditionMet)
                {
                    MLogger.Error($"选项条件不满足，无法选择: {index}");
                    return;
                }
            }

            // 触发选项选择事件
            OnOptionSelected?.Invoke(selectedChoice, index);

            // 重置选项收集状态
            _isCollectingChoices = false;
            _currentChoices.Clear();

            // 如果选项有内容，将其压入执行栈
            if (selectedChoice.Content.Count > 0)
            {
                // 将选项内容压入栈
                _executionStack.Push((selectedChoice, 0));

                // 执行选项的第一个内容
                await Continue();
            }
            else
            {
                // 选项没有内容，继续执行当前节点的下一个内容
                await Continue();
            }
        }

        /// <summary>
        /// 跳转到指定节点
        /// </summary>
        /// <param name="nodeName">节点名称</param>
        /// <returns>异步任务</returns>
        public async Task JumpToNode(string nodeName)
        {
            try
            {
                var node = _context.GetNode(nodeName);
                _currentChoices.Clear();
                _isCollectingChoices = false;
                _isExecuting = false;

                // 清空执行栈
                _executionStack.Clear();

                // 将新节点压入栈
                _executionStack.Push((node, 0));

                // 触发节点开始事件
                OnNodeStarted?.Invoke(nodeName);

                // 记录节点访问
                Storage.RecordNodeVisit(nodeName);

                // 开始执行
                await Continue();
            }
            catch (Exception ex)
            {
                MLogger.Error($"跳转到节点 {nodeName} 失败: {ex.Message}");
                // 尝试恢复执行
                if (_executionStack.Count > 0)
                {
                    await Continue();
                }
                else
                {
                    await EndDialogue(true);
                }
            }
        }

        /// <summary>
        /// 获取当前节点的内容
        /// </summary>
        /// <returns>内容节点列表</returns>
        [Preserve]
        public List<ContentNode> GetCurrentNodeContents()
        {
            if (_executionStack.Count != 0 && _executionStack.Peek().node is NodeDefinitionNode node) return node.Content;
            MLogger.Error("没有当前节点");
            return null;
        }

        /// <summary>
        /// 获取当前选项列表
        /// </summary>
        /// <returns>选项列表</returns>
        [Preserve]
        public List<ChoiceNode> GetCurrentChoices()
        {
            return _currentChoices;
        }

        /// <summary>
        /// 结束对话
        /// </summary>
        /// <param name="force">是否强制结束，忽略当前执行状态</param>
        /// <returns>异步任务</returns>
        public async Task EndDialogue(bool force = false)
        {
            if (!force && _isExecuting)
            {
                MLogger.Warning("当前正在执行中，请等待执行完成");
                return;
            }

            _currentChoices.Clear();
            _isCollectingChoices = false;

            // 清除存储中的对话状态
            Storage.ClearDialogueState();

            // 清空执行栈
            _executionStack.Clear();

            OnDialogueCompleted?.Invoke();
            await Task.CompletedTask;
        }

        /// <summary>
        /// 构建对话文本
        /// </summary>
        /// <param name="dialogueNode">对话节点</param>
        /// <returns>构建好的对话文本</returns>
        public async Task<string> BuildDialogueText(DialogueNode dialogueNode)
        {
            return await _interpreter.BuildText(dialogueNode.Text);
        }

        /// <summary>
        /// 构建选项文本
        /// </summary>
        /// <param name="choiceNode">选项节点</param>
        /// <returns>选项文本</returns>
        public async Task<string> BuildChoiceText(ChoiceNode choiceNode)
        {
            return await _interpreter.BuildText(choiceNode.Text);
        }

        /// <summary>
        /// 构建文本
        /// </summary>
        /// <param name="textSegments">文本段列表</param>
        /// <returns>构建好的文本</returns>
        public async Task<string> BuildText(List<TextSegmentNode> textSegments)
        {
            return await _interpreter.BuildText(textSegments);
        }

        /// <summary>
        /// 检查是否还有下一个可显示的内容（对话、选项或跳转命令）
        /// </summary>
        /// <returns>
        /// 返回整数表示下一个内容的类型：
        /// 0 - 没有任何内容了
        /// 1 - 对话内容
        /// 2 - 选项内容
        /// 3 - 跳转命令
        /// 4 - 其他命令
        /// </returns>
        public async Task<int> HasNextContent()
        {
            // 如果执行栈为空，说明没有更多内容
            if (_executionStack.Count == 0)
            {
                return 0;
            }

            // 获取当前执行上下文的副本
            var stackCopy = new Stack<(ASTNode node, int index)>(_executionStack.Reverse());

            while (stackCopy.Count > 0)
            {
                (var currentNode, int currentIndex) = stackCopy.Pop();
                List<ContentNode> contents;

                // 根据节点类型获取内容列表
                if (currentNode is NodeDefinitionNode nodeDef)
                {
                    contents = nodeDef.Content;
                }
                else if (currentNode is ChoiceNode choice)
                {
                    contents = choice.Content;
                }
                else if (currentNode is DialogueNode dialogue)
                {
                    contents = dialogue.Content;
                }
                else if (currentNode is ConditionNode condition)
                {
                    // 对于条件节点，需要评估条件并获取对应分支的内容
                    contents = await GetConditionBranchContents(condition);
                    if (contents == null)
                    {
                        continue; // 没有匹配的条件分支，检查下一个栈帧
                    }
                }
                else
                {
                    continue; // 不支持的节点类型，检查下一个栈帧
                }

                // 检查从当前索引开始的内容
                for (int i = currentIndex; i < contents.Count; i++)
                {
                    var content = contents[i];
                    int result = await CheckContentType(content);
                    if (result > 0) // 找到了有效内容
                    {
                        return result;
                    }
                    else if (result == -1) // 需要继续检查条件节点
                    {
                        stackCopy.Push((content as ConditionNode, 0));
                        break; // 退出当前内容循环，处理新压入的条件节点
                    }
                }
            }

            return 0; // 没有找到任何内容
        }

        /// <summary>
        /// 获取条件节点对应分支的内容
        /// </summary>
        private async Task<List<ContentNode>> GetConditionBranchContents(ConditionNode condition)
        {
            // 评估主条件
            var result = await _interpreter.EvaluateExpression(condition.Condition);
            if (result.Type != ValueType.Boolean)
            {
                MLogger.Error("条件必须计算为布尔类型");
                return condition.ElseContent ?? new List<ContentNode>();
            }

            if ((bool)result.Value)
            {
                return condition.ThenContent;
            }

            // 检查elif分支
            foreach (var (elifCondition, elifContent) in condition.ElifContents)
            {
                var elifResult = await _interpreter.EvaluateExpression(elifCondition);
                if (elifResult.Type != ValueType.Boolean)
                {
                    MLogger.Error("Elif条件必须计算为布尔类型");
                    continue;
                }

                if ((bool)elifResult.Value)
                {
                    return elifContent;
                }
            }

            // 如果有else分支则返回else分支内容
            return condition.ElseContent is {Count: > 0} ? condition.ElseContent : null;
        }

        /// <summary>
        /// 检查内容节点的类型
        /// </summary>
        /// <returns>
        /// 返回值说明：
        /// >0 - 内容类型（1-对话，2-选项，3-跳转，4-其他命令）
        /// 0 - 继续检查下一个内容
        /// -1 - 需要检查条件节点
        /// </returns>
        private async Task<int> CheckContentType(ContentNode content)
        {
            // 对话和旁白是直接可显示的
            if (content is DialogueNode)
            {
                return 1; // 对话内容
            }
            // 选项需要检查是否有可用的选项
            if (content is ChoiceNode choiceNode)
            {
                if (await EvaluateChoiceCondition(choiceNode))
                {
                    return 2; // 选项内容
                }
            }
            // 命令节点需要检查是否是跳转命令
            else if (content is CommandNode)
            {
                if (content is JumpCommandNode)
                {
                    return 3; // 跳转命令
                }
                return 4; // 其他命令
            }
            // 条件节点需要递归检查
            else if (content is ConditionNode)
            {
                return -1; // 需要继续检查条件节点
            }

            return 0; // 继续检查下一个内容
        }

        /// <summary>
        /// 获取节点的元数据值
        /// </summary>
        /// <param name="nodeName">节点名称，为null则使用当前节点</param>
        /// <param name="key">元数据键</param>
        /// <returns>元数据值，节点不存在或键不存在时返回null</returns>
        [Preserve]
        public string GetMetadata(string nodeName, string key)
        {
            try
            {
                if (nodeName != null)
                {
                    // 使用DialogueContext获取指定节点的元数据
                    return _context.GetMetadata(nodeName, key);
                }

                // 获取当前节点的元数据
                if (_executionStack.Count != 0 && _executionStack.Peek().node is NodeDefinitionNode currentNode) return currentNode.Metadata.GetValueOrDefault(key, null);
                MLogger.Warning("当前没有活动节点");
                return null;
            }
            catch (Exception ex)
            {
                MLogger.Error($"获取元数据时出错: {ex}");
                return null;
            }
        }

        /// <summary>
        /// 获取节点的所有元数据
        /// </summary>
        /// <param name="nodeName">节点名称，为null则使用当前节点</param>
        /// <returns>节点的所有元数据，如果节点不存在则返回null</returns>
        [Preserve]
        public Dictionary<string, string> GetAllMetadata(string nodeName = null)
        {
            try
            {
                if (nodeName != null)
                {
                    // 使用DialogueContext获取指定节点的元数据
                    return _context.GetAllMetadata(nodeName);
                }

                // 获取当前节点的元数据
                if (_executionStack.Count != 0 && _executionStack.Peek().node is NodeDefinitionNode currentNode) return currentNode.Metadata;
                MLogger.Warning("当前没有活动节点");
                return null;
            }
            catch (Exception ex)
            {
                MLogger.Error($"获取元数据时出错: {ex}");
                return null;
            }
        }

        private void PrintCommand(CommandNode command)
        {
            if (command is JumpCommandNode jumpCommand)
            {
                MLogger.Info($"[命令] 跳转到节点: {jumpCommand.TargetNode}");
            }
            else if (command is VarCommandNode varCommand)
            {
                MLogger.Info($"[命令] 设置变量: {varCommand.VariableName} = {varCommand.Value}");
            }
            else if (command is CallCommandNode callCommand)
            {
                MLogger.Info($"[命令] 调用函数: {callCommand.Call}");
            }
            else if (command is WaitCommandNode waitCommand)
            {
                MLogger.Info($"[命令] 等待: {waitCommand.Duration} 秒");
            }
            else
            {
                MLogger.Info("[命令] 未知命令类型");
            }
        }
    }
}
