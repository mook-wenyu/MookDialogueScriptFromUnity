using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace MookDialogueScript
{
    /// <summary>
    /// 表达式解释器，专注于表达式求值（增强版本，包含性能优化）
    /// </summary>
    public class Interpreter
    {
        private readonly DialogueContext _context;
        private readonly Dictionary<string, Func<ExpressionNode, Task<RuntimeValue>>> _operators;

        // 表达式缓存：缓存不可变表达式的求值结果
        private readonly ConcurrentDictionary<int, RuntimeValue> _expressionCache = new();

        // 参数列表缓存：重用参数列表对象
        private readonly Queue<List<RuntimeValue>> _argsPool = new();
        private readonly object _argsPoolLock = new();

        // 缓存性能统计
        private long _cacheHits = 0;
        private long _cacheMisses = 0;

        public Interpreter(DialogueContext context)
        {
            _context = context;
            _operators = new Dictionary<string, Func<ExpressionNode, Task<RuntimeValue>>>(StringComparer.OrdinalIgnoreCase)
            {
                ["-"] = async (right) => new RuntimeValue(-await GetNumberValue(right)),
                ["!"] = async (right) => new RuntimeValue(!await GetBooleanValue(right)),
                ["not"] = async (right) => new RuntimeValue(!await GetBooleanValue(right))
            };
        }

        /// <summary>
        /// 将语义关键字映射到对应的运算符
        /// </summary>
        /// <param name="op">原始运算符</param>
        /// <returns>映射后的运算符</returns>
        private string MapOperator(string op)
        {
            return op.ToLower() switch
            {
                "eq" or "is" => "==",
                "neq" or "！=" => "!=",
                "gt" or "》" => ">",
                "lt" or "《" => "<",
                "gte" or "》=" => ">=",
                "lte" or "《=" => "<=",
                "and" => "&&",
                "or" => "||",
                "xor" => "^",
                _ => op
            };
        }

        #region 性能优化方法
        /// <summary>
        /// 检查表达式是否为不可变表达式（可以缓存结果）
        /// </summary>
        private bool IsImmutableExpression(ExpressionNode node)
        {
            return node switch
            {
                NumberNode => true,
                BooleanNode => true,
                StringInterpolationExpressionNode stringInterp =>
                    stringInterp.Segments.All(part => part is TextNode), // 仅包含字面量的字符串插值
                BinaryOpNode binary =>
                    IsImmutableExpression(binary.Left) && IsImmutableExpression(binary.Right),
                UnaryOpNode unary => IsImmutableExpression(unary.Operand),
                _ => false
            };
        }

        /// <summary>
        /// 获取参数列表对象（使用对象池）
        /// </summary>
        private List<RuntimeValue> GetArgumentsList()
        {
            lock (_argsPoolLock)
            {
                if (_argsPool.Count > 0)
                {
                    var args = _argsPool.Dequeue();
                    args.Clear();
                    return args;
                }
            }
            return new List<RuntimeValue>();
        }

        /// <summary>
        /// 归还参数列表对象到对象池
        /// </summary>
        private void ReturnArgumentsList(List<RuntimeValue> args)
        {
            if (args == null || args.Count > 20) // 避免缓存过大的列表
                return;

            lock (_argsPoolLock)
            {
                if (_argsPool.Count < 10) // 限制池大小
                {
                    args.Clear();
                    _argsPool.Enqueue(args);
                }
            }
        }

        /// <summary>
        /// 获取缓存性能统计
        /// </summary>
        public Dictionary<string, object> GetCacheStatistics()
        {
            return new Dictionary<string, object>
            {
                ["ExpressionCacheSize"] = _expressionCache.Count,
                ["CacheHits"] = _cacheHits,
                ["CacheMisses"] = _cacheMisses,
                ["HitRate"] = _cacheHits + _cacheMisses > 0
                    ? (double)_cacheHits / (_cacheHits + _cacheMisses) * 100.0
                    : 0.0,
                ["ArgsPoolSize"] = _argsPool.Count
            };
        }

        /// <summary>
        /// 清理缓存
        /// </summary>
        public void ClearCache()
        {
            _expressionCache.Clear();

            lock (_argsPoolLock)
            {
                _argsPool.Clear();
            }

            _cacheHits = 0;
            _cacheMisses = 0;
        }
        #endregion

        #region 性能监控和调试
        /// <summary>
        /// 获取解释器性能统计
        /// </summary>
        public Dictionary<string, object> GetPerformanceStatistics()
        {
            var helperStats = Helper.GetCacheStatistics();
            var interpreterStats = GetCacheStatistics();

            var combined = new Dictionary<string, object>();

            // 合并 Helper 统计
            foreach (var kvp in helperStats)
            {
                combined[$"Helper_{kvp.Key}"] = kvp.Value;
            }

            // 合并解释器统计
            foreach (var kvp in interpreterStats)
            {
                combined[$"Interpreter_{kvp.Key}"] = kvp.Value;
            }

            return combined;
        }

        /// <summary>
        /// 预热缓存（针对常用表达式）
        /// </summary>
        public async Task WarmupCache(IEnumerable<ExpressionNode> commonExpressions)
        {
            foreach (var expr in commonExpressions)
            {
                try
                {
                    await EvaluateExpression(expr);
                }
                catch
                {
                    // 忽略预热期间的异常
                }
            }
        }
        #endregion

        /// <summary>
        /// 评估表达式并返回运行时值（带缓存优化）
        /// </summary>
        /// <param name="node">表达式节点</param>
        /// <returns>计算结果的运行时值</returns>
        public async Task<RuntimeValue> EvaluateExpression(ExpressionNode node)
        {
            // 检查是否可以使用缓存
            if (IsImmutableExpression(node))
            {
                var hash = GetExpressionHash(node);
                if (_expressionCache.TryGetValue(hash, out var cachedResult))
                {
                    System.Threading.Interlocked.Increment(ref _cacheHits);
                    return cachedResult;
                }

                // 计算结果并缓存
                var result = await EvaluateExpressionInternal(node);
                _expressionCache.TryAdd(hash, result);
                System.Threading.Interlocked.Increment(ref _cacheMisses);
                return result;
            }

            // 对于可变表达式，直接计算
            return await EvaluateExpressionInternal(node);
        }

        /// <summary>
        /// 获取表达式的哈希值用于缓存键
        /// </summary>
        private int GetExpressionHash(ExpressionNode node)
        {
            // 简化的哈希计算，实际可以更精确
            return node switch
            {
                NumberNode n => HashCode.Combine("Number", n.Value),
                BooleanNode b => HashCode.Combine("Boolean", b.Value),
                StringInterpolationExpressionNode s => HashCode.Combine("StringInterp", s.Segments.Count),
                BinaryOpNode bin => HashCode.Combine("Binary", bin.Operator,
                    GetExpressionHash(bin.Left), GetExpressionHash(bin.Right)),
                UnaryOpNode un => HashCode.Combine("Unary", un.Operator,
                    GetExpressionHash(un.Operand)),
                _ => node.GetHashCode()
            };
        }

        /// <summary>
        /// 内部表达式求值方法（无缓存）
        /// </summary>
        private async Task<RuntimeValue> EvaluateExpressionInternal(ExpressionNode node)
        {
            switch (node)
            {
                case NumberNode n:
                    return new RuntimeValue(n.Value);

                case StringInterpolationExpressionNode i:
                    var sb = Helper.GetStringBuilder();
                    try
                    {
                        foreach (var segment in i.Segments)
                        {
                            switch (segment)
                            {
                                case TextNode t:
                                    sb.Append(t.Text);
                                    break;

                                case InterpolationNode interpolation:
                                    // 简化插值表达式处理，只评估表达式
                                    var interpolationValue = await EvaluateExpression(interpolation.Expression);
                                    sb.Append(interpolationValue.ToString());
                                    break;
                            }
                        }
                        return new RuntimeValue(sb.ToString());
                    }
                    finally
                    {
                        Helper.ReturnStringBuilder(sb);
                    }

                case BooleanNode b:
                    return new RuntimeValue(b.Value);

                case VariableNode v:
                    return _context.GetVariable(v.Name);

                case UnaryOpNode u:
                    if (_operators.TryGetValue(u.Operator, out var @operator)) return await @operator(u.Operand);
                    MLogger.Error($"未知的一元运算符 '{u.Operator}'");
                    return RuntimeValue.Null;

                case BinaryOpNode b:
                    var left = await EvaluateExpression(b.Left);
                    var right = await EvaluateExpression(b.Right);

                    // 将语义关键字映射到对应的运算符
                    string op = MapOperator(b.Operator);

                    // 如果任一操作数是函数调用的结果，确保类型匹配
                    if (op is "-" or "*" or "/" or "%" or ">" or "<" or ">=" or "<=")
                    {
                        if (left.Type != ValueType.Number)
                        {
                            MLogger.Error($"运算符 '{op}' 的左操作数必须是数值类型");
                            return RuntimeValue.Null;
                        }
                        if (right.Type != ValueType.Number)
                        {
                            MLogger.Error($"运算符 '{op}' 的右操作数必须是数值类型");
                            return RuntimeValue.Null;
                        }
                    }
                    else if (op is "&&" or "||" or "^")
                    {
                        if (left.Type != ValueType.Boolean)
                        {
                            MLogger.Error($"运算符 '{op}' 的左操作数必须是布尔类型");
                            return RuntimeValue.Null;
                        }
                        if (right.Type != ValueType.Boolean)
                        {
                            MLogger.Error($"运算符 '{op}' 的右操作数必须是布尔类型");
                            return RuntimeValue.Null;
                        }
                    }

                    switch (op)
                    {
                        case "+":
                            if (left.Type == ValueType.String || right.Type == ValueType.String)
                                return new RuntimeValue(left.ToString() + right.ToString());
                            return new RuntimeValue((double)left.Value + (double)right.Value);

                        case "-":
                            return new RuntimeValue((double)left.Value - (double)right.Value);

                        case "*":
                            return new RuntimeValue((double)left.Value * (double)right.Value);

                        case "/":
                            if ((double)right.Value != 0) return new RuntimeValue((double)left.Value / (double)right.Value);
                            MLogger.Error("除数不能为零");
                            return new RuntimeValue(0);

                        case "%":
                            if ((double)right.Value != 0) return new RuntimeValue((double)left.Value % (double)right.Value);
                            MLogger.Error("取模运算的除数不能为零");
                            return new RuntimeValue(0);

                        case "==":
                            return new RuntimeValue(left == right);

                        case "!=":
                            return new RuntimeValue(left != right);

                        case ">":
                            return new RuntimeValue((double)left.Value > (double)right.Value);

                        case "<":
                            return new RuntimeValue((double)left.Value < (double)right.Value);

                        case ">=":
                            return new RuntimeValue((double)left.Value >= (double)right.Value);

                        case "<=":
                            return new RuntimeValue((double)left.Value <= (double)right.Value);

                        case "&&":
                            return new RuntimeValue((bool)left.Value && (bool)right.Value);

                        case "||":
                            return new RuntimeValue((bool)left.Value || (bool)right.Value);

                        case "^":
                            return new RuntimeValue((bool)left.Value ^ (bool)right.Value);

                        default:
                            MLogger.Error($"未知的二元运算符 '{op}'");
                            return RuntimeValue.Null;
                    }

                case CallExpressionNode call:
                    return await EvaluateCallExpression(call);

                case MemberAccessNode member:
                    return await EvaluateMemberAccess(member);

                case IndexAccessNode index:
                    return await EvaluateIndexAccess(index);

                case IdentifierNode identifier:
                    // 检查是否为变量
                    var varValue = _context.GetVariable(identifier.Name);
                    if (varValue.Type != ValueType.Null)
                    {
                        return varValue;
                    }

                    MLogger.Warning($"遇到未定义的标识符: {identifier.Name}");
                    return RuntimeValue.Null;

                default:
                    MLogger.Error($"未知的表达式类型 {node.GetType().Name}");
                    return RuntimeValue.Null;
            }
        }

        /// <summary>
        /// 获取表达式的数值结果
        /// </summary>
        /// <param name="expression">表达式节点</param>
        /// <returns>数值</returns>
        public async Task<double> GetNumberValue(ExpressionNode expression)
        {
            var value = await EvaluateExpression(expression);
            if (value.Type == ValueType.Number)
                return (double)value.Value;

            MLogger.Error("表达式必须计算为数值类型");
            return 0;
        }

        /// <summary>
        /// 获取表达式的布尔结果
        /// </summary>
        /// <param name="node">表达式节点</param>
        /// <returns>布尔值</returns>
        public async Task<bool> GetBooleanValue(ExpressionNode node)
        {
            var value = await EvaluateExpression(node);
            if (value.Type == ValueType.Boolean)
                return (bool)value.Value;

            MLogger.Error("表达式必须计算为布尔类型");
            return false;
        }

        /// <summary>
        /// 获取表达式的字符串结果
        /// </summary>
        /// <param name="node">表达式节点</param>
        /// <returns>字符串</returns>
        public async Task<string> GetStringValue(ExpressionNode node)
        {
            var value = await EvaluateExpression(node);
            if (value.Type == ValueType.String)
                return (string)value.Value;

            MLogger.Error("表达式必须计算为字符串类型");
            return string.Empty;
        }

        /// <summary>
        /// 构建文本
        /// </summary>
        /// <param name="segments">文本段列表</param>
        /// <returns>构建后的文本</returns>
        public async Task<string> BuildText(List<TextSegmentNode> segments)
        {
            var result = new System.Text.StringBuilder();
            foreach (var segment in segments)
            {
                switch (segment)
                {
                    case TextNode t:
                        result.Append(t.Text);
                        break;

                    case InterpolationNode i:
                        try
                        {
                            // 分类处理不同类型的表达式，提高效率
                            if (i.Expression is VariableNode varNode)
                            {
                                // 显式检查变量是否存在
                                if (_context.HasVariable(varNode.Name))
                                {
                                    var value = _context.GetVariable(varNode.Name);
                                    if (value.Type != ValueType.Null && value.Value != null)
                                    {
                                        result.Append(value.ToString());
                                    }
                                    else
                                    {
                                        MLogger.Warning($"变量 '{varNode.Name}' 的值为null");
                                        // 变量为null时显示null，包括花括号和$符号
                                        result.Append("{null}");
                                    }
                                }
                                else
                                {
                                    MLogger.Error($"变量 '{varNode.Name}' 不存在");
                                    // 变量不存在时显示完整原始文本，包括花括号和$符号
                                    result.Append($"{{${varNode.Name}}}");
                                }
                            }
                            else
                            {
                                // 其他类型表达式通过EvaluateExpression评估
                                try
                                {
                                    var value = await EvaluateExpression(i.Expression);
                                    if (value.Type != ValueType.Null && value.Value != null)
                                    {
                                        result.Append(value.ToString());
                                    }
                                    else
                                    {
                                        MLogger.Warning("表达式返回null");
                                        // 表达式返回null时显示null，包括花括号
                                        result.Append("{null}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MLogger.Error($"表达式评估错误: {ex}");
                                    // 表达式评估异常时显示原始文本，包括花括号
                                    result.Append($"{{{FormatExpressionNode(i.Expression)}}}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // 发生异常，记录错误并显示原始表达式，包括花括号
                            MLogger.Error($"插值表达式错误: {ex}");
                            result.Append($"{{{FormatExpressionNode(i.Expression)}}}");
                        }
                        break;
                }
            }
            return result.ToString();
        }

        /// <summary>
        /// 格式化表达式节点
        /// </summary>
        /// <param name="node">表达式节点</param>
        /// <returns>格式化后的表达式字符串</returns>
        private string FormatExpressionNode(ExpressionNode node)
        {
            switch (node)
            {
                case NumberNode numNode:
                    return numNode.Value.ToString(CultureInfo.CurrentCulture);
                case StringInterpolationExpressionNode strNode:
                {
                    var result = new System.Text.StringBuilder();
                    foreach (var segment in strNode.Segments)
                    {
                        switch (segment)
                        {
                            case TextNode t:
                                result.Append(t.Text);
                                break;

                            case InterpolationNode interpolation:
                                result.Append($"{FormatExpressionNode(interpolation.Expression)}");
                                break;
                        }
                    }
                    return result.ToString();
                }
                case BooleanNode boolNode:
                    return boolNode.Value ? "true" : "false";
                case VariableNode varNode:
                    return $"${varNode.Name}";
                case BinaryOpNode binNode:
                    return $"({FormatExpressionNode(binNode.Left)} {binNode.Operator} {FormatExpressionNode(binNode.Right)})";
                case UnaryOpNode unaryNode:
                    return $"{unaryNode.Operator}{FormatExpressionNode(unaryNode.Operand)}";
                default:
                    return "?"; // 其他类型表达式用?表示
            }
        }

        /// <summary>
        /// 注册脚本中的所有节点
        /// </summary>
        /// <param name="script">脚本</param>
        public void RegisterNodes(ScriptNode script)
        {
            // 注册所有节点
            foreach (var node in script.Nodes)
            {
                Debug.Log(node.ToString());
                _context.RegisterNode(node.Name, node);
            }
        }

        /// <summary>
        /// 运行时执行命令
        /// </summary>
        /// <param name="command">命令节点</param>
        /// <returns>如果是跳转命令则返回目标节点名称，否则返回空字符串</returns>
        public async Task<string> ExecuteCommand(CommandNode command)
        {
            switch (command)
            {
                case VarCommandNode v:
                    var value = await EvaluateExpression(v.Value);
                    switch (v.Operation.ToLower())
                    {
                        case "var":
                            if (!_context.HasVariable(v.VariableName))
                            {
                                _context.SetVariable(v.VariableName, value);
                            }
                            else
                            {
                                MLogger.Warning($"变量 '{v.VariableName}' 已存在");
                            }
                            break;

                        case "set":
                            _context.SetVariable(v.VariableName, value);
                            break;

                        case "add":
                            var current = _context.GetVariable(v.VariableName);
                            if (current.Type != ValueType.Number || value.Type != ValueType.Number)
                            {
                                MLogger.Error("Add操作需要数值类型");
                                return string.Empty;
                            }
                            _context.SetVariable(v.VariableName, new RuntimeValue((double)current.Value + (double)value.Value));
                            break;

                        case "sub":
                            current = _context.GetVariable(v.VariableName);
                            if (current.Type != ValueType.Number || value.Type != ValueType.Number)
                            {
                                MLogger.Error("Sub操作需要数值类型");
                                return string.Empty;
                            }
                            _context.SetVariable(v.VariableName, new RuntimeValue((double)current.Value - (double)value.Value));
                            break;

                        case "mul":
                            current = _context.GetVariable(v.VariableName);
                            if (current.Type != ValueType.Number || value.Type != ValueType.Number)
                            {
                                MLogger.Error("Mul操作需要数值类型");
                                return string.Empty;
                            }
                            _context.SetVariable(v.VariableName, new RuntimeValue((double)current.Value * (double)value.Value));
                            break;

                        case "div":
                            current = _context.GetVariable(v.VariableName);
                            if (current.Type != ValueType.Number || value.Type != ValueType.Number)
                            {
                                MLogger.Error("Div操作需要数值类型");
                                return string.Empty;
                            }
                            if ((double)value.Value == 0)
                            {
                                MLogger.Error("Div操作的除数不能为零");
                                return string.Empty;
                            }
                            _context.SetVariable(v.VariableName, new RuntimeValue((double)current.Value / (double)value.Value));
                            break;

                        case "mod":
                            current = _context.GetVariable(v.VariableName);
                            if (current.Type != ValueType.Number || value.Type != ValueType.Number)
                            {
                                MLogger.Error("Mod操作需要数值类型");
                                return string.Empty;
                            }
                            if ((double)value.Value == 0)
                            {
                                MLogger.Error("Mod操作的除数不能为零");
                                return string.Empty;
                            }
                            _context.SetVariable(v.VariableName, new RuntimeValue((double)current.Value % (double)value.Value));
                            break;

                        default:
                            MLogger.Error($"未知的变量操作 '{v.Operation}'");
                            break;
                    }
                    return string.Empty;

                case CallCommandNode c:
                    // 直接计算调用表达式，但不返回结果
                    await EvaluateExpression(c.Call);
                    return string.Empty;

                case WaitCommandNode w:
                    double duration = await GetNumberValue(w.Duration);
                    await Task.Delay(TimeSpan.FromSeconds(duration));
                    return string.Empty;

                case JumpCommandNode j:
                    return j.TargetNode;

                default:
                    MLogger.Error($"未知的命令类型 {command.GetType().Name}");
                    return string.Empty;
            }
        }

        /// <summary>
        /// 调用可调用对象的方法（处理各种委托类型和可调用对象）
        /// 注意：不再处理字符串函数名回退，该职责由ProcessFunctionCall负责
        /// </summary>
        /// <param name="callee">被调用对象</param>
        /// <param name="args">参数列表</param>
        /// <param name="line">行号</param>
        /// <param name="column">列号</param>
        /// <returns>调用结果</returns>
        private async Task<RuntimeValue> InvokeCallable(object callee, List<RuntimeValue> args, int line, int column)
        {
            try
            {
                switch (callee)
                {
                    // 函数管理器的编译委托
                    case Func<List<RuntimeValue>, Task<RuntimeValue>> compiledFunc:
                        return await compiledFunc(args);

                    // 支持同步Func和Action委托
                    case Func<int> func0Int:
                        return new RuntimeValue((double)func0Int());
                    case Func<double> func0Double:
                        return new RuntimeValue(func0Double());
                    case Func<string> func0String:
                        return new RuntimeValue(func0String());
                    case Func<bool> func0Bool:
                        return new RuntimeValue(func0Bool());
                    case Action action0:
                        action0();
                        return RuntimeValue.Null;

                    // 支持一个参数的委托
                    case Action<int> action1Int when args.Count >= 1 && args[0].Type == ValueType.Number:
                        action1Int((int)(double)args[0].Value);
                        return RuntimeValue.Null;
                    case Action<string> action1String when args.Count >= 1:
                        action1String(args[0].ToString());
                        return RuntimeValue.Null;
                    case Func<int, int> func1IntInt when args.Count >= 1 && args[0].Type == ValueType.Number:
                        return new RuntimeValue((double)func1IntInt((int)(double)args[0].Value));
                    case Func<string, string> func1StringString when args.Count >= 1:
                        return new RuntimeValue(func1StringString(args[0].ToString()));

                    // 支持异步Task委托（需要先匹配具体类型，再匹配基类）
                    case Func<Task<int>> funcTaskInt:
                        var taskIntResult = await funcTaskInt();
                        return new RuntimeValue((double)taskIntResult);
                    case Func<Task<double>> funcTaskDouble:
                        var taskDoubleResult = await funcTaskDouble();
                        return new RuntimeValue(taskDoubleResult);
                    case Func<Task<string>> funcTaskString:
                        var taskStringResult = await funcTaskString();
                        return new RuntimeValue(taskStringResult);
                    case Func<Task<bool>> funcTaskBool:
                        var taskBoolResult = await funcTaskBool();
                        return new RuntimeValue(taskBoolResult);
                    case Func<Task> funcTask:
                        await funcTask();
                        return RuntimeValue.Null;

                    default:
                        throw ExceptionFactory.CreateCallableNotSupportedException(callee?.GetType().Name ?? "null", line, column);
                }
            }
            catch (Exception ex) when (!(ex is InterpreterException))
            {
                // 将非InterpreterException包装为统一异常
                throw ExceptionFactory.CreateFunctionInvokeFailException("可调用对象", ex, line, column);
            }
        }
        /// <summary>
        /// 评估调用表达式
        /// </summary>
        /// <param name="call">调用表达式节点</param>
        /// <returns>调用结果</returns>
        private async Task<RuntimeValue> EvaluateCallExpression(CallExpressionNode call)
        {
            // 使用对象池获取参数列表
            var args = GetArgumentsList();
            try
            {
                // 评估参数
                foreach (var arg in call.Arguments)
                {
                    args.Add(await EvaluateExpression(arg));
                }

                // 分析被调用者
                return await ProcessFunctionCall(call, args);
            }
            finally
            {
                // 归还参数列表到对象池
                ReturnArgumentsList(args);
            }
        }

        /// <summary>
        /// 处理函数调用逻辑，简化的按名调用架构：
        /// 1. 优先检查 IdentifierNode 的按名调用
        /// 2. 其他情况通过表达式求值后处理委托调用
        /// </summary>
        private async Task<RuntimeValue> ProcessFunctionCall(CallExpressionNode call, List<RuntimeValue> args)
        {
            // 第1层：IdentifierNode的按名调用（避免无意义的变量查找）
            if (call.Callee is IdentifierNode identifier)
            {
                // 检查是否为注册的函数名
                if (_context.HasFunction(identifier.Name))
                {
                    return await _context.CallFunction(identifier.Name, args, call.Line, call.Column);
                }

                // 函数未找到，使用统一异常系统
                var availableFunctions = _context.GetAllFunctionNames();
                throw ExceptionFactory.CreateFunctionNotFoundException(identifier.Name, availableFunctions, call.Line, call.Column);
            }

            // 第2层：对其他类型的callee进行求值
            var calleeValue = await EvaluateExpression(call.Callee);

            // 对象可转Delegate - 处理所有其他情况（成员访问、变量、表达式结果等）
            // 特殊处理MethodReference类型（注册对象的方法）
            if (calleeValue is {Type: ValueType.Object, Value: MethodReference methodRef})
            {
                // 通过函数管理器调用已注册的方法引用
                return await _context.CallFunction(methodRef.FunctionKey, args, call.Line, call.Column);
            }

            // 检查调用值是否为null或无效
            if (calleeValue.Type == ValueType.Null || calleeValue.Value == null)
            {
                // 提供更精确的错误信息
                string calleeDescription = FormatCalleeDescription(call.Callee);
                throw ExceptionFactory.CreateCallableNotSupportedException(
                    $"表达式 '{calleeDescription}' 的求值结果为null",
                    call.Line,
                    call.Column);
            }

            // 通过InvokeCallable处理委托转换和其他可调用对象
            return await InvokeCallable(calleeValue.Value, args, call.Line, call.Column);
        }

        /// <summary>
        /// 格式化callee表达式的描述文本（用于错误报告）
        /// </summary>
        private string FormatCalleeDescription(ExpressionNode callee)
        {
            switch (callee)
            {
                case IdentifierNode id:
                    return id.Name;
                case VariableNode var:
                    return $"${var.Name}";
                case MemberAccessNode member:
                    return $"{FormatCalleeDescription(member.Target)}.{member.MemberName}";
                case CallExpressionNode call:
                    return $"{FormatCalleeDescription(call.Callee)}(...)";
                default:
                    return callee.GetType().Name;
            }
        }

        /// <summary>
        /// 评估成员访问
        /// </summary>
        /// <param name="member">成员访问节点</param>
        /// <returns>成员值</returns>
        private async Task<RuntimeValue> EvaluateMemberAccess(MemberAccessNode member)
        {
            try
            {
                RuntimeValue target;

                // 特殊处理：如果 Target 是 IdentifierNode，优先检查注册的对象
                if (member.Target is IdentifierNode identifier)
                {
                    // 首先尝试从注册的对象中获取
                    if (_context.TryGetObjectByName(identifier.Name, out var objectInstance))
                    {
                        target = new RuntimeValue(objectInstance);
                    }
                    else
                    {
                        // 回退到正常的表达式求值（变量查找）
                        target = await EvaluateExpression(member.Target);
                    }
                }
                else
                {
                    // 对于非 IdentifierNode（如嵌套的成员访问），正常求值
                    target = await EvaluateExpression(member.Target);
                }

                // 处理对象成员访问
                return await _context.GetObjectMember(target, member.MemberName);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"运行时错误: 第{member.Line}行，第{member.Column}列，成员访问失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 评估索引访问
        /// </summary>
        /// <param name="index">索引访问节点</param>
        /// <returns>索引值</returns>
        private async Task<RuntimeValue> EvaluateIndexAccess(IndexAccessNode index)
        {
            try
            {
                var target = await EvaluateExpression(index.Target);
                var indexValue = await EvaluateExpression(index.Index);

                // 处理不同类型的索引访问
                return GetIndexValue(target, indexValue, index.Line, index.Column);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"运行时错误: 第{index.Line}行，第{index.Column}列，索引访问失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取索引值（委托给Helper的高性能实现）
        /// </summary>
        /// <param name="target">目标对象</param>
        /// <param name="index">索引值</param>
        /// <param name="line">行号</param>
        /// <param name="column">列号</param>
        /// <returns>索引结果</returns>
        private RuntimeValue GetIndexValue(RuntimeValue target, RuntimeValue index, int line, int column)
        {
            return Helper.GetIndexValue(target, index, line, column);
        }
    }
}
