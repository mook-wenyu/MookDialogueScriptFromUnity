using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine.Scripting;

namespace MookDialogueScript
{
    /// <summary>
    /// 表达式解释器，专注于表达式求值
    /// </summary>
    public class Interpreter
    {
        private readonly DialogueContext _context;
        private readonly Dictionary<string, Func<ExpressionNode, Task<RuntimeValue>>> _operators;

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

        /// <summary>
        /// 评估表达式并返回运行时值
        /// </summary>
        /// <param name="node">表达式节点</param>
        /// <returns>计算结果的运行时值</returns>
        public async Task<RuntimeValue> EvaluateExpression(ExpressionNode node)
        {
            switch (node)
            {
                case NumberNode n:
                    return new RuntimeValue(n.Value);

                case StringInterpolationExpressionNode i:
                    var result = new System.Text.StringBuilder();
                    foreach (var segment in i.Segments)
                    {
                        switch (segment)
                        {
                            case TextNode t:
                                result.Append(t.Text);
                                break;

                            case InterpolationNode interpolation:
                                // 简化插值表达式处理，只评估表达式
                                var interpolationValue = await EvaluateExpression(interpolation.Expression);
                                result.Append(interpolationValue.ToString());
                                break;
                        }
                    }
                    return new RuntimeValue(result.ToString());

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
                        if (left.Type != RuntimeValue.ValueType.Number)
                        {
                            MLogger.Error($"运算符 '{op}' 的左操作数必须是数值类型");
                            return RuntimeValue.Null;
                        }
                        if (right.Type != RuntimeValue.ValueType.Number)
                        {
                            MLogger.Error($"运算符 '{op}' 的右操作数必须是数值类型");
                            return RuntimeValue.Null;
                        }
                    }
                    else if (op is "&&" or "||" or "^")
                    {
                        if (left.Type != RuntimeValue.ValueType.Boolean)
                        {
                            MLogger.Error($"运算符 '{op}' 的左操作数必须是布尔类型");
                            return RuntimeValue.Null;
                        }
                        if (right.Type != RuntimeValue.ValueType.Boolean)
                        {
                            MLogger.Error($"运算符 '{op}' 的右操作数必须是布尔类型");
                            return RuntimeValue.Null;
                        }
                    }

                    switch (op)
                    {
                        case "+":
                            if (left.Type == RuntimeValue.ValueType.String || right.Type == RuntimeValue.ValueType.String)
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

                case FunctionCallNode f:
                    var args = new List<RuntimeValue>();
                    // 递归评估每个参数，支持嵌套函数调用
                    foreach (var arg in f.Arguments)
                    {
                        args.Add(await EvaluateExpression(arg));
                    }
                    return await _context.CallFunction(f.Name, args);

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
            if (value.Type == RuntimeValue.ValueType.Number)
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
            if (value.Type == RuntimeValue.ValueType.Boolean)
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
            if (value.Type == RuntimeValue.ValueType.String)
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
                                    if (value.Type != RuntimeValue.ValueType.Null && value.Value != null)
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
                            else if (i.Expression is FunctionCallNode funcNode)
                            {
                                // 显式检查函数是否存在
                                if (_context.HasFunction(funcNode.Name))
                                {
                                    try
                                    {
                                        var functionArgs = new List<RuntimeValue>();
                                        foreach (var arg in funcNode.Arguments)
                                        {
                                            functionArgs.Add(await EvaluateExpression(arg));
                                        }

                                        var value = await _context.CallFunction(funcNode.Name, functionArgs);
                                        if (value.Type != RuntimeValue.ValueType.Null && value.Value != null)
                                        {
                                            result.Append(value.ToString());
                                        }
                                        else
                                        {
                                            MLogger.Warning($"函数 '{funcNode.Name}' 返回null");
                                            // 函数返回null时显示null，包括花括号
                                            result.Append("{null}");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        MLogger.Error($"函数 '{funcNode.Name}' 调用错误: {ex}");
                                        // 函数调用异常时显示原始文本，包括花括号
                                        result.Append($"{{{FormatFunctionCall(funcNode)}}}");
                                    }
                                }
                                else
                                {
                                    MLogger.Error($"函数 '{funcNode.Name}' 不存在");
                                    // 函数不存在时显示原始文本，包括花括号
                                    result.Append($"{{{FormatFunctionCall(funcNode)}}}");
                                }
                            }
                            else
                            {
                                // 其他类型表达式通过EvaluateExpression评估
                                try
                                {
                                    var value = await EvaluateExpression(i.Expression);
                                    if (value.Type != RuntimeValue.ValueType.Null && value.Value != null)
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
                case FunctionCallNode funcNode:
                    return FormatFunctionCall(funcNode); // 递归处理嵌套函数
                case BinaryOpNode binNode:
                    return $"({FormatExpressionNode(binNode.Left)} {binNode.Operator} {FormatExpressionNode(binNode.Right)})";
                case UnaryOpNode unaryNode:
                    return $"{unaryNode.Operator}{FormatExpressionNode(unaryNode.Operand)}";
                default:
                    return "?"; // 其他类型表达式用?表示
            }
        }

        /// <summary>
        /// 格式化函数调用，包括递归处理嵌套函数
        /// </summary>
        /// <param name="funcNode">函数调用节点</param>
        /// <returns>格式化后的函数调用字符串</returns>
        private string FormatFunctionCall(FunctionCallNode funcNode)
        {
            var funcText = new System.Text.StringBuilder(funcNode.Name);
            funcText.Append('(');

            for (int j = 0; j < funcNode.Arguments.Count; j++)
            {
                var arg = funcNode.Arguments[j];
                funcText.Append(FormatExpressionNode(arg));

                if (j < funcNode.Arguments.Count - 1)
                    funcText.Append(", ");
            }

            funcText.Append(')');
            return funcText.ToString();
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
                _context.RegisterNode(node.NodeName, node);
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
                            if (!_context.HasVariable(v.Variable))
                            {
                                _context.SetVariable(v.Variable, value);
                            }
                            else
                            {
                                MLogger.Warning($"变量 '{v.Variable}' 已存在");
                            }
                            break;

                        case "set":
                            _context.SetVariable(v.Variable, value);
                            break;

                        case "add":
                            var current = _context.GetVariable(v.Variable);
                            if (current.Type != RuntimeValue.ValueType.Number || value.Type != RuntimeValue.ValueType.Number)
                            {
                                MLogger.Error("Add操作需要数值类型");
                                return string.Empty;
                            }
                            _context.SetVariable(v.Variable, new RuntimeValue((double)current.Value + (double)value.Value));
                            break;

                        case "sub":
                            current = _context.GetVariable(v.Variable);
                            if (current.Type != RuntimeValue.ValueType.Number || value.Type != RuntimeValue.ValueType.Number)
                            {
                                MLogger.Error("Sub操作需要数值类型");
                                return string.Empty;
                            }
                            _context.SetVariable(v.Variable, new RuntimeValue((double)current.Value - (double)value.Value));
                            break;

                        case "mul":
                            current = _context.GetVariable(v.Variable);
                            if (current.Type != RuntimeValue.ValueType.Number || value.Type != RuntimeValue.ValueType.Number)
                            {
                                MLogger.Error("Mul操作需要数值类型");
                                return string.Empty;
                            }
                            _context.SetVariable(v.Variable, new RuntimeValue((double)current.Value * (double)value.Value));
                            break;

                        case "div":
                            current = _context.GetVariable(v.Variable);
                            if (current.Type != RuntimeValue.ValueType.Number || value.Type != RuntimeValue.ValueType.Number)
                            {
                                MLogger.Error("Div操作需要数值类型");
                                return string.Empty;
                            }
                            if ((double)value.Value == 0)
                            {
                                MLogger.Error("Div操作的除数不能为零");
                                return string.Empty;
                            }
                            _context.SetVariable(v.Variable, new RuntimeValue((double)current.Value / (double)value.Value));
                            break;

                        case "mod":
                            current = _context.GetVariable(v.Variable);
                            if (current.Type != RuntimeValue.ValueType.Number || value.Type != RuntimeValue.ValueType.Number)
                            {
                                MLogger.Error("Mod操作需要数值类型");
                                return string.Empty;
                            }
                            if ((double)value.Value == 0)
                            {
                                MLogger.Error("Mod操作的除数不能为零");
                                return string.Empty;
                            }
                            _context.SetVariable(v.Variable, new RuntimeValue((double)current.Value % (double)value.Value));
                            break;

                        default:
                            MLogger.Error($"未知的变量操作 '{v.Operation}'");
                            break;
                    }
                    return string.Empty;

                case CallCommandNode c:
                    var args = new List<RuntimeValue>();
                    foreach (var arg in c.Parameters)
                    {
                        args.Add(await EvaluateExpression(arg));
                    }
                    await _context.CallFunction(c.FunctionName, args);
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
    }
}
