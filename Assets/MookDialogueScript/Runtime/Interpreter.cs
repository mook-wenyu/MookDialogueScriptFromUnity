using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

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

                case CallExpressionNode call:
                    return await EvaluateCallExpression(call);

                case MemberAccessNode member:
                    return await EvaluateMemberAccess(member);

                case IndexAccessNode index:
                    return await EvaluateIndexAccess(index);

                case IdentifierNode identifier:
                    // 标识符节点，可能代表函数名
                    // 先检查是否为函数名，如果是则返回函数值
                    if (_context.HasFunction(identifier.Name))
                    {
                        return _context.GetFunctionValue(identifier.Name);
                    }
                    
                    // 检查是否为变量
                    var varValue = _context.GetVariable(identifier.Name);
                    if (varValue.Type != RuntimeValue.ValueType.Null)
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
                            if (current.Type != RuntimeValue.ValueType.Number || value.Type != RuntimeValue.ValueType.Number)
                            {
                                MLogger.Error("Add操作需要数值类型");
                                return string.Empty;
                            }
                            _context.SetVariable(v.VariableName, new RuntimeValue((double)current.Value + (double)value.Value));
                            break;

                        case "sub":
                            current = _context.GetVariable(v.VariableName);
                            if (current.Type != RuntimeValue.ValueType.Number || value.Type != RuntimeValue.ValueType.Number)
                            {
                                MLogger.Error("Sub操作需要数值类型");
                                return string.Empty;
                            }
                            _context.SetVariable(v.VariableName, new RuntimeValue((double)current.Value - (double)value.Value));
                            break;

                        case "mul":
                            current = _context.GetVariable(v.VariableName);
                            if (current.Type != RuntimeValue.ValueType.Number || value.Type != RuntimeValue.ValueType.Number)
                            {
                                MLogger.Error("Mul操作需要数值类型");
                                return string.Empty;
                            }
                            _context.SetVariable(v.VariableName, new RuntimeValue((double)current.Value * (double)value.Value));
                            break;

                        case "div":
                            current = _context.GetVariable(v.VariableName);
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
                            _context.SetVariable(v.VariableName, new RuntimeValue((double)current.Value / (double)value.Value));
                            break;

                        case "mod":
                            current = _context.GetVariable(v.VariableName);
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
                            _context.SetVariable(v.VariableName, new RuntimeValue((double)current.Value % (double)value.Value));
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

        /// <summary>
        /// 统一调用可调用对象的方法
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
                    case Action<int> action1Int when args.Count >= 1 && args[0].Type == RuntimeValue.ValueType.Number:
                        action1Int((int)(double)args[0].Value);
                        return RuntimeValue.Null;
                    case Action<string> action1String when args.Count >= 1:
                        action1String(args[0].ToString());
                        return RuntimeValue.Null;
                    case Func<int, int> func1IntInt when args.Count >= 1 && args[0].Type == RuntimeValue.ValueType.Number:
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

                    // 通用委托处理（使用Helper的函数值包装器）
                    case Delegate del:
                        var helperFunc = Helper.CreateFunctionValue(del);
                        if (helperFunc.Type == RuntimeValue.ValueType.Function)
                        {
                            return await _context.CallFunctionValue(helperFunc, args, line, column);
                        }
                        throw ExceptionFactory.CreateCallableNotSupportedException($"委托类型 {del.GetType().Name}", line, column);

                    // 字符串类型（如果被当作函数名）
                    case string funcName:
                        return await _context.CallFunction(funcName, args, line, column);

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
            // 评估参数
            var args = new List<RuntimeValue>();
            foreach (var arg in call.Arguments)
            {
                args.Add(await EvaluateExpression(arg));
            }

            // 分析被调用者
            switch (call.Callee)
            {
                case IdentifierNode identifier:
                    // 简单的函数调用：identifier(args)
                    // 优先检查是否为函数名
                    if (_context.HasFunction(identifier.Name))
                    {
                        return await _context.CallFunction(identifier.Name, args, call.Line, call.Column);
                    }
                    
                    // 检查是否为函数值变量
                    var identifierValue = _context.GetVariable(identifier.Name);
                    if (identifierValue.Type == RuntimeValue.ValueType.Function)
                    {
                        return await _context.CallFunctionValue(identifierValue, args, call.Line, call.Column);
                    }
                    
                    // 使用统一异常系统
                    var availableFunctions = _context.GetAllFunctionNames();
                    throw ExceptionFactory.CreateFunctionNotFoundException(identifier.Name, availableFunctions, call.Line, call.Column);

                case MemberAccessNode memberAccess:
                    // 成员方法调用：obj.method(args)
                    var targetValue = await EvaluateExpression(memberAccess.Target);

                    // 检查是否为已注册对象的方法
                    if (targetValue.Type == RuntimeValue.ValueType.Object &&
                        _context.TryGetObjectName(targetValue.Value, out var objectName))
                    {
                        string functionKey = $"{objectName}.{memberAccess.MemberName}";
                        if (_context.HasFunction(functionKey))
                        {
                            return await _context.CallFunction(functionKey, args, call.Line, call.Column);
                        }
                    }

                    // 尝试获取成员作为函数值
                    var memberValue = await EvaluateMemberAccess(memberAccess);
                    
                    // 统一处理 MethodReference 类型
                    if (memberValue.Type == RuntimeValue.ValueType.Object && memberValue.Value is MethodReference methodRef)
                    {
                        // 通过函数管理器调用方法引用
                        return await _context.CallFunction(methodRef.FunctionKey, args, call.Line, call.Column);
                    }
                    else if (memberValue.Type == RuntimeValue.ValueType.Function)
                    {
                        return await _context.CallFunctionValue(memberValue, args, call.Line, call.Column);
                    }
                    
                    // 回退到可调用对象处理
                    return await InvokeCallable(memberValue.Value, args, call.Line, call.Column);

                case VariableNode variable:
                    // 变量可调用：$fn(args)
                    var varValue = _context.GetVariable(variable.Name);
                    
                    // 优先检查是否为函数值
                    if (varValue.Type == RuntimeValue.ValueType.Function)
                    {
                        return await _context.CallFunctionValue(varValue, args, call.Line, call.Column);
                    }
                    
                    // 回退到原有的可调用对象处理
                    return await InvokeCallable(varValue.Value, args, call.Line, call.Column);

                default:
                    // 其他类型的被调用者，先求值再调用
                    var calleeValue = await EvaluateExpression(call.Callee);
                    
                    // 检查是否为函数值
                    if (calleeValue.Type == RuntimeValue.ValueType.Function)
                    {
                        return await _context.CallFunctionValue(calleeValue, args, call.Line, call.Column);
                    }
                    
                    // 回退到原有的可调用对象处理
                    return await InvokeCallable(calleeValue.Value, args, call.Line, call.Column);
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
                var target = await EvaluateExpression(member.Target);

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
