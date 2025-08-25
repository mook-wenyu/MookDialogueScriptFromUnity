using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace MookDialogueScript
{
    public class Parser
    {
        private readonly List<Token> _tokens;
        private int _tokenIndex;
        private Token _currentToken;

        // 自动标签生成
        private int _lineCounter;
        private string _currentNodeName = "";

        // 嵌套层数警告
        private const int MAX_SAFE_NESTING_LEVEL = 10;
        private int _currentNestingLevel;

        public Parser(List<Token> tokens)
        {
            if (tokens == null || tokens.Count == 0)
            {
                throw new ArgumentException("Token列表不能为空");
            }

            _tokens = tokens;
            _tokenIndex = 0;
            _currentToken = _tokens[0];
        }

        /// <summary>
        /// 消耗一个Token
        /// </summary>
        private void Consume(TokenType type)
        {
            if (_currentToken.Type == type)
            {
                GetNextToken();
            }
            else if (_currentToken.Type == TokenType.EOF && type == TokenType.NEWLINE)
            {
                // 如果期望换行符但遇到了EOF，视为合法情况
                // 不需要获取下一个Token，因为已经到达文件末尾
            }
            else
            {
                throw new InvalidOperationException($"语法错误: 第{_currentToken.Line}行，第{_currentToken.Column}列，期望 {type}，但得到 {_currentToken.Type}");
            }
        }

        /// <summary>
        /// 获取下一个Token
        /// </summary>
        private void GetNextToken()
        {
            _tokenIndex++;
            if (_tokenIndex < _tokens.Count)
            {
                _currentToken = _tokens[_tokenIndex];
            }
            else
            {
                _currentToken = new Token(TokenType.EOF, string.Empty, _tokens[^1].Line, _tokens[^1].Column);
            }
        }

        /// <summary>
        /// 回退到上一个Token
        /// </summary>
        private void PreviousToken()
        {
            if (_tokenIndex > 0)
            {
                _tokenIndex--;
                _currentToken = _tokens[_tokenIndex];
            }
        }

        /// <summary>
        /// 查看当前Token类型
        /// </summary>
        private bool Check(TokenType type)
        {
            return _currentToken.Type == type;
        }

        /// <summary>
        /// 查看下一个Token类型
        /// </summary>
        private bool CheckNext(TokenType type)
        {
            if (_tokenIndex + 1 < _tokens.Count)
            {
                return _tokens[_tokenIndex + 1].Type == type;
            }
            return false;
        }

        /// <summary>
        /// 匹配并消耗一个Token
        /// </summary>
        private bool Match(TokenType type)
        {
            if (Check(type))
            {
                GetNextToken();
                return true;
            }
            return false;
        }

        /// <summary>
        /// 解析脚本
        /// </summary>
        public ScriptNode Parse()
        {
            var nodes = new List<NodeDefinitionNode>();

            while (!IsEOF())
            {
                var node = ParseNodeDefinition();
                if (node != null)
                {
                    nodes.Add(node);
                }
            }

            return new ScriptNode(nodes);
        }

        /// <summary>
        /// 解析节点定义
        /// </summary>
        private NodeDefinitionNode ParseNodeDefinition()
        {
            // 解析节点元数据
            var metadata = ParseNodeMetadata();

            // 需要有节点开始标记
            if (!Check(TokenType.NODE_START))
            {
                throw new InvalidOperationException($"语法错误: 第{_currentToken.Line}行，第{_currentToken.Column}列，缺少节点开始标记 ---");
            }

            int line = _currentToken.Line;
            int column = _currentToken.Column;
            Consume(TokenType.NODE_START);

            // 重置行计数器并设置当前节点名
            _lineCounter = 0;
            _currentNodeName = metadata.GetValueOrDefault("node", "unnamed");

            var content = new List<ContentNode>();
            while (!Check(TokenType.NODE_END) && !IsEOF())
            {
                if (Check(TokenType.NEWLINE))
                {
                    Consume(TokenType.NEWLINE);
                    continue;
                }

                var node = ParseCollectionContent();
                if (node != null && !IsEmptyContentNode(node))
                {
                    content.Add(node);
                }
            }

            if (!Check(TokenType.NODE_END))
            {
                throw new InvalidOperationException($"语法错误: 第{_currentToken.Line}行，第{_currentToken.Column}列，缺少节点结束标记 ===");
            }
            Consume(TokenType.NODE_END);

            return new NodeDefinitionNode(_currentNodeName, metadata, content, line, column);
        }

        /// <summary>
        /// 解析节点元数据
        /// </summary>
        private Dictionary<string, string> ParseNodeMetadata()
        {
            var metadata = new Dictionary<string, string>();

            // 解析节点元数据（key: value 格式）
            while (!Check(TokenType.NODE_START) && !IsEOF())
            {
                if (Check(TokenType.NEWLINE))
                {
                    Consume(TokenType.NEWLINE);
                    continue;
                }

                if (Check(TokenType.IDENTIFIER) && CheckNext(TokenType.METADATA_SEPARATOR))
                {
                    string key = _currentToken.Value;
                    Consume(TokenType.IDENTIFIER);
                    Consume(TokenType.METADATA_SEPARATOR);

                    var value = new StringBuilder();
                    while (!Check(TokenType.NEWLINE) && !IsEOF())
                    {
                        value.Append(_currentToken.Value);
                        Consume(_currentToken.Type);
                    }

                    metadata[key] = value.ToString();

                    if (Check(TokenType.NEWLINE))
                    {
                        Consume(TokenType.NEWLINE);
                    }
                }
                else
                {
                    break;
                }
            }

            return metadata;
        }

        /// <summary>
        /// 检查内容节点是否为空
        /// </summary>
        private bool IsEmptyContentNode(ContentNode node)
        {
            // 对于对话节点（包括旁白），检查是否只包含空文本
            if (node is DialogueNode dialogue)
            {
                // 如果没有文本段落，或者所有文本段落都是空的
                return dialogue.Text.Count == 0 ||
                       dialogue.Text.All(segment => segment is TextNode textNode && string.IsNullOrWhiteSpace(textNode.Text));
            }

            // 对于选择节点，检查是否没有内容
            if (node is ChoiceNode choice)
            {
                // 首先检查选择文本是否为空
                bool emptyText = choice.Text.Count == 0 ||
                                 choice.Text.All(segment => segment is TextNode textNode && string.IsNullOrWhiteSpace(textNode.Text));

                // 如果选择文本和内容都是空的，则认为此节点为空
                return emptyText && (choice.Content == null || choice.Content.Count == 0);
            }

            // 对于条件节点，检查是否没有内容
            if (node is ConditionNode condition)
            {
                return (condition.ThenBranch == null || condition.ThenBranch.Count == 0) &&
                       (condition.ElifBranches == null || condition.ElifBranches.Count == 0) &&
                       (condition.ElseBranch == null || condition.ElseBranch.Count == 0);
            }

            // 其他类型的节点通常不会是空的
            return false;
        }

        /// <summary>
        /// 解析集合内容（新设计）
        /// </summary>
        private ContentNode ParseCollectionContent()
        {
            return _currentToken.Type switch
            {
                // 角色对话：角色名:
                TokenType.TEXT when CheckNext(TokenType.COLON) => ParseDialogue(),

                // 选项：-> 文本
                TokenType.ARROW => ParseChoice(),

                // 条件：<<if>>
                TokenType.COMMAND_START when CheckNextCommand(TokenType.IF) => ParseCondition(),

                // 其他命令：<<command>>
                TokenType.COMMAND_START => ParseCommand(),

                // 旁白：所有其他情况（包括:文本和普通文本）
                _ => ParseNarration()
            };
        }

        /// <summary>
        /// 解析条件（新设计）
        /// </summary>
        private ConditionNode ParseCondition()
        {
            int line = _currentToken.Line;
            int column = _currentToken.Column;

            Consume(TokenType.COMMAND_START);
            Consume(TokenType.IF);
            var condition = ParseExpression();
            Consume(TokenType.COMMAND_END);

            // 解析then分支
            var thenBranch = ParseConditionalBranch(TokenType.ELIF, TokenType.ELSE, TokenType.ENDIF);

            // 解析elif分支
            var elifBranches = new List<(ExpressionNode Condition, List<ContentNode> Content)>();
            while (Check(TokenType.COMMAND_START) && CheckNextCommand(TokenType.ELIF))
            {
                Consume(TokenType.COMMAND_START);
                Consume(TokenType.ELIF);
                var elifCondition = ParseExpression();
                Consume(TokenType.COMMAND_END);

                var elifContent = ParseConditionalBranch(TokenType.ELIF, TokenType.ELSE, TokenType.ENDIF);
                elifBranches.Add((elifCondition, elifContent));
            }

            // 解析else分支
            List<ContentNode> elseBranch = null;
            if (Check(TokenType.COMMAND_START) && CheckNextCommand(TokenType.ELSE))
            {
                Consume(TokenType.COMMAND_START);
                Consume(TokenType.ELSE);
                Consume(TokenType.COMMAND_END);

                elseBranch = ParseConditionalBranch(TokenType.ENDIF);
            }

            // 消耗endif
            if (!Check(TokenType.COMMAND_START) || !CheckNextCommand(TokenType.ENDIF))
            {
                throw new InvalidOperationException($"语法错误: 第{_currentToken.Line}行，第{_currentToken.Column}列，缺少条件结束标记 <<endif>>");
            }

            Consume(TokenType.COMMAND_START);
            Consume(TokenType.ENDIF);
            Consume(TokenType.COMMAND_END);

            return new ConditionNode(condition, thenBranch, elifBranches, elseBranch, line, column);
        }

        /// <summary>
        /// 解析条件分支内容
        /// </summary>
        private List<ContentNode> ParseConditionalBranch(params TokenType[] endTokens)
        {
            var content = new List<ContentNode>();

            while (!IsConditionalEnd(endTokens) && !IsEOF())
            {
                if (Check(TokenType.NEWLINE))
                {
                    Consume(TokenType.NEWLINE);
                    continue;
                }

                // 忽略缩进控制符：条件分支的边界由 <<elif/else/endif>> 决定，缩进仅为排版用
                if (Check(TokenType.INDENT))
                {
                    Consume(TokenType.INDENT);
                    continue;
                }
                if (Check(TokenType.DEDENT))
                {
                    Consume(TokenType.DEDENT);
                    continue;
                }

                // 边界保护：如果遇到节点边界，说明缺失了期望的 <<endif>> / <<elif>> / <<else>>，需提前退出避免死循环
                if (Check(TokenType.NODE_END) || Check(TokenType.NODE_START))
                {
                    // 记录错误但不中断整个脚本解析流程
                    MLogger.Error($"语法错误: 第{_currentToken.Line}行，第{_currentToken.Column}列，在条件块内遇到节点边界但未找到结束标记（可能缺少 <<endif>>）");
                    break;
                }

                // 记录当前位置用于无前进保护
                int beforeIndex = _tokenIndex;

                var node = ParseCollectionContent();
                if (node != null && !IsEmptyContentNode(node))
                {
                    content.Add(node);
                }

                // 无前进保护：若本轮未消耗任何Token，抛出异常避免死循环
                if (beforeIndex == _tokenIndex)
                {
                    throw new InvalidOperationException($"语法错误: 第{_currentToken.Line}行，第{_currentToken.Column}列，无法在条件分支内前进（可能缺少 <<endif>> 或存在无法识别的结构）");
                }
            }

            return content;
        }

        /// <summary>
        /// 检查是否到达条件结束
        /// </summary>
        private bool IsConditionalEnd(TokenType[] endTokens)
        {
            if (!Check(TokenType.COMMAND_START)) return false;

            foreach (var endToken in endTokens)
            {
                if (CheckNextCommand(endToken))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 生成自动行号标签
        /// </summary>
        private string GenerateLineTag()
        {
            _lineCounter++;
            return $"line:{_currentNodeName.ToLower()}{_lineCounter}";
        }

        /// <summary>
        /// 解析旁白（统一处理:文本和普通文本）
        /// </summary>
        private DialogueNode ParseNarration()
        {
            int line = _currentToken.Line;
            int column = _currentToken.Column;

            // 不特殊处理冒号，让ParseText()自然处理所有文本内容
            var text = ParseText();
            var tags = ParseTags();
            tags.Add(GenerateLineTag());

            var content = ParseNestedContent();

            return new DialogueNode(null, text, tags, content, line, column);
        }

        /// <summary>
        /// 解析对话
        /// </summary>
        private DialogueNode ParseDialogue()
        {
            int line = _currentToken.Line;
            int column = _currentToken.Column;

            string speaker = _currentToken.Value;
            Consume(TokenType.TEXT);
            Consume(TokenType.COLON);

            var text = ParseText();
            var tags = ParseTags();
            tags.Add(GenerateLineTag());

            var content = ParseNestedContent();

            return new DialogueNode(speaker, text, tags, content, line, column);
        }

        /// <summary>
        /// 解析文本
        /// </summary>
        private List<TextSegmentNode> ParseText()
        {
            var segments = new List<TextSegmentNode>();
            var textBuilder = new StringBuilder(128);

            while (_currentToken.Type != TokenType.NEWLINE
                   && _currentToken.Type != TokenType.COMMAND_START
                   && _currentToken.Type != TokenType.HASH
                   && _currentToken.Type != TokenType.QUOTE
                   && _currentToken.Type != TokenType.NODE_START
                   && _currentToken.Type != TokenType.NODE_END
                   && !IsEOF())
            {
                if (_currentToken.Type == TokenType.LEFT_BRACE)
                {
                    if (textBuilder.Length > 0)
                    {
                        segments.Add(new TextNode(textBuilder.ToString(), _currentToken.Line, _currentToken.Column));
                        textBuilder.Clear();
                    }
                    segments.Add(ParseInterpolation());
                }
                else if (_currentToken.Type == TokenType.TEXT)
                {
                    if (textBuilder.Length > 0)
                    {
                        segments.Add(new TextNode(textBuilder.ToString().TrimEnd(), _currentToken.Line, _currentToken.Column));
                        textBuilder.Clear();
                    }
                    segments.Add(new TextNode(_currentToken.Value, _currentToken.Line, _currentToken.Column));
                    Consume(TokenType.TEXT);
                }
                else
                {
                    // 合并各种标记为文本的逻辑，减少条件分支
                    textBuilder.Append(_currentToken.Value);
                    Consume(_currentToken.Type);
                }
            }

            if (textBuilder.Length > 0)
            {
                segments.Add(new TextNode(textBuilder.ToString().TrimEnd(), _currentToken.Line, _currentToken.Column));
            }

            return segments;
        }

        /// <summary>
        /// 解析插值
        /// </summary>
        private InterpolationNode ParseInterpolation()
        {
            int line = _currentToken.Line;
            int column = _currentToken.Column;
            Consume(TokenType.LEFT_BRACE);
            var expr = ParseExpression();
            Consume(TokenType.RIGHT_BRACE);
            return new InterpolationNode(expr, line, column);
        }

        /// <summary>
        /// 解析标签
        /// </summary>
        private List<string> ParseTags()
        {
            var tags = new List<string>();

            // 跳过空字符
            while (string.IsNullOrEmpty(_currentToken.Value.Trim())
                   && _currentToken.Type != TokenType.NEWLINE
                   && _currentToken.Type != TokenType.EOF)
            {
                Consume(_currentToken.Type);
            }

            while (Check(TokenType.HASH))
            {
                Consume(TokenType.HASH);

                var tagBuilder = new StringBuilder();

                // 读取直到遇到下一个标签符号或换行
                while (!Check(TokenType.NEWLINE) && !Check(TokenType.HASH) && !IsEOF())
                {
                    tagBuilder.Append(_currentToken.Value);
                    Consume(_currentToken.Type);
                }

                string tag = tagBuilder.ToString().TrimEnd();
                if (!string.IsNullOrEmpty(tag))
                {
                    tags.Add(tag);
                }
            }

            return tags;
        }

        /// <summary>
        /// 解析嵌套内容
        /// </summary>
        private List<ContentNode> ParseNestedContent()
        {
            var content = new List<ContentNode>();

            if (Check(TokenType.NEWLINE) && CheckNext(TokenType.INDENT))
            {
                Consume(TokenType.NEWLINE);
                Consume(TokenType.INDENT);

                _currentNestingLevel++;
                if (_currentNestingLevel > MAX_SAFE_NESTING_LEVEL)
                {
                    MLogger.Warning($"警告: 第{_currentToken.Line}行，嵌套层数过深（{_currentNestingLevel}层），可能影响性能");
                }

                while (!Check(TokenType.DEDENT) && !IsEOF() && !IsCollectionEnd())
                {
                    if (Check(TokenType.NEWLINE))
                    {
                        Consume(TokenType.NEWLINE);
                        continue;
                    }

                    var node = ParseCollectionContent();
                    if (node != null && !IsEmptyContentNode(node))
                    {
                        content.Add(node);
                    }
                }

                if (Check(TokenType.DEDENT))
                {
                    Consume(TokenType.DEDENT);
                }
                _currentNestingLevel--;
            }

            return content;
        }

        private ChoiceNode ParseChoice()
        {
            int line = _currentToken.Line;
            int column = _currentToken.Column;
            Consume(TokenType.ARROW);

            var text = ParseText();

            ExpressionNode condition = null;
            var tags = new List<string>();

            // 解析 <<if 条件>> 语法
            if (Check(TokenType.COMMAND_START))
            {
                Consume(TokenType.COMMAND_START);
                if (Check(TokenType.IF))
                {
                    Consume(TokenType.IF);
                    condition = ParseExpression();
                }
                Consume(TokenType.COMMAND_END);
            }

            // 解析其他标签
            tags.AddRange(ParseTags());

            // 添加自动生成的行号标签
            tags.Add(GenerateLineTag());

            var content = ParseNestedContent();

            return new ChoiceNode(text, condition, tags, content, line, column);
        }

        /// <summary>
        /// 解析函数调用命令
        /// </summary>
        private CallCommandNode ParseFunctionCallCommand(string functionName, int line, int column)
        {
            Consume(TokenType.LEFT_PAREN);
            var parameters = ParseParameterList();
            Consume(TokenType.RIGHT_PAREN);
            return new CallCommandNode(functionName, parameters, line, column);
        }

        /// <summary>
        /// 从CallExpressionNode的Callee中提取函数名
        /// </summary>
        private string ExtractFunctionName(ExpressionNode callee)
        {
            switch (callee)
            {
                case IdentifierNode identifier:
                    // 简单函数调用：func()
                    return identifier.Name;

                case MemberAccessNode memberAccess:
                    // 对象方法调用：obj.method()
                    string objectName = ExtractFunctionName(memberAccess.Target);
                    return $"{objectName}.{memberAccess.Member}";

                case VariableNode variable:
                    // 变量函数调用：$func()
                    return variable.Name;

                default:
                    throw new InvalidOperationException($"不支持的函数调用类型: {callee.GetType().Name}");
            }
        }



        /// <summary>
        /// 解析命令
        /// </summary>
        private CommandNode ParseCommand()
        {
            int line = _currentToken.Line;
            int column = _currentToken.Column;

            Consume(TokenType.COMMAND_START); // 先消耗 <<

            string commandValue = _currentToken.Value.ToLower();
            var commandType = _currentToken.Type;
            Consume(commandType);

            switch (commandType)
            {
                case TokenType.SET:
                case TokenType.ADD:
                case TokenType.SUB:
                case TokenType.MUL:
                case TokenType.DIV:
                case TokenType.MOD:
                {
                    var result = ParseVarOperation(commandValue, line, column);
                    Consume(TokenType.COMMAND_END);
                    return result;
                }

                case TokenType.WAIT:
                {
                    var result = ParseWaitCommand(line, column);
                    Consume(TokenType.COMMAND_END);
                    return result;
                }

                case TokenType.VAR:
                {
                    var result = ParseVarDeclaration(line, column);
                    Consume(TokenType.COMMAND_END);
                    return result;
                }

                case TokenType.JUMP:
                {
                    var result = ParseJumpCommand(line, column);
                    Consume(TokenType.COMMAND_END);
                    return result;
                }

                case TokenType.IDENTIFIER:
                    // 回退到IDENTIFIER token，然后通过ParseExpression解析完整的表达式
                    // 这样可以正确处理各种情况：func()、obj.method()、obj.property等
                    PreviousToken(); // 回退到IDENTIFIER
                    var expression = ParseExpression();

                    // 检查解析出的表达式是否为调用表达式
                    if (expression is CallExpressionNode callExpr)
                    {
                        // 将CallExpressionNode转换为CallCommandNode
                        string functionName = ExtractFunctionName(callExpr.Callee);
                        var result = new CallCommandNode(functionName, callExpr.Arguments, line, column);
                        Consume(TokenType.COMMAND_END);
                        return result;
                    }

                    throw new InvalidOperationException($"语法错误: 第{line}行，第{column}列，命令中的表达式必须是函数调用");

                default:
                    throw new InvalidOperationException($"语法错误: 第{line}行，第{column}列，未知命令 {commandValue}");
            }
        }

        /// <summary>
        /// 解析变量操作命令
        /// </summary>
        private VarCommandNode ParseVarOperation(string operation, int line, int column)
        {
            string variable = _currentToken.Value;
            Consume(TokenType.VARIABLE);

            // 赋值符号是可选的
            if (_currentToken.Type == TokenType.ASSIGN)
            {
                Consume(TokenType.ASSIGN);
            }

            var value = ParseExpression();

            return new VarCommandNode(variable, value, operation, line, column);
        }


        /// <summary>
        /// 解析参数列表
        /// </summary>
        private List<ExpressionNode> ParseParameterList()
        {
            var parameters = new List<ExpressionNode>();

            if (!Check(TokenType.RIGHT_PAREN))
            {
                parameters.Add(ParseExpression());
                while (Match(TokenType.COMMA))
                {
                    parameters.Add(ParseExpression());
                }
            }

            return parameters;
        }

        /// <summary>
        /// 解析等待命令
        /// </summary>
        private WaitCommandNode ParseWaitCommand(int line, int column)
        {
            var duration = ParseExpression();
            return new WaitCommandNode(duration, line, column);
        }

        /// <summary>
        /// 解析变量声明
        /// </summary>
        private VarCommandNode ParseVarDeclaration(int line, int column)
        {
            string varName = _currentToken.Value;
            Consume(TokenType.VARIABLE);

            // 赋值符号是可选的
            if (_currentToken.Type == TokenType.ASSIGN)
            {
                Consume(TokenType.ASSIGN);
            }

            var initialValue = ParseExpression();

            return new VarCommandNode(varName, initialValue, "var", line, column);
        }

        /// <summary>
        /// 解析跳转命令
        /// </summary>
        private JumpCommandNode ParseJumpCommand(int line, int column)
        {
            string targetNode = _currentToken.Value;
            Consume(TokenType.IDENTIFIER);
            return new JumpCommandNode(targetNode, line, column);
        }

        /// <summary>
        /// 解析字符串
        /// </summary>
        private ExpressionNode ParseString(Token token)
        {
            var segments = new List<TextSegmentNode>();
            var textBuilder = new StringBuilder(128);

            int exprLine = token.Line;
            int exprColumn = token.Column;

            Consume(TokenType.QUOTE);

            while (_currentToken.Type != TokenType.QUOTE
                   && _currentToken.Type != TokenType.NEWLINE
                   && !IsEOF())
            {
                if (_currentToken.Type == TokenType.LEFT_BRACE)
                {
                    if (textBuilder.Length > 0)
                    {
                        segments.Add(new TextNode(textBuilder.ToString(), _currentToken.Line, _currentToken.Column));
                        textBuilder.Clear();
                    }
                    segments.Add(ParseInterpolation());
                }
                else if (_currentToken.Type == TokenType.TEXT)
                {
                    if (textBuilder.Length > 0)
                    {
                        segments.Add(new TextNode(textBuilder.ToString().TrimEnd(), _currentToken.Line, _currentToken.Column));
                        textBuilder.Clear();
                    }
                    segments.Add(new TextNode(_currentToken.Value, _currentToken.Line, _currentToken.Column));
                    Consume(TokenType.TEXT);
                }
                else
                {
                    // 合并各种标记为文本的逻辑，减少条件分支
                    textBuilder.Append(_currentToken.Value);
                    Consume(_currentToken.Type);
                }
            }

            if (textBuilder.Length > 0)
            {
                segments.Add(new TextNode(textBuilder.ToString().TrimEnd(), _currentToken.Line, _currentToken.Column));
            }

            Consume(TokenType.QUOTE);

            return new StringInterpolationExpressionNode(segments, exprLine, exprColumn);
        }

        /// <summary>
        /// 定义所有二元操作符的优先级(数字越大优先级越高)
        /// </summary>
        private static readonly Dictionary<TokenType, int> _binaryOperators = new()
        {
            // 逻辑运算符 (优先级 1-2)
            {TokenType.OR, 1},
            {TokenType.AND, 2},
            {TokenType.XOR, 2},

            // 比较运算符 (优先级 3)
            {TokenType.EQUALS, 3},
            {TokenType.NOT_EQUALS, 3},
            {TokenType.GREATER, 3},
            {TokenType.LESS, 3},
            {TokenType.GREATER_EQUALS, 3},
            {TokenType.LESS_EQUALS, 3},

            // 加减运算符 (优先级 4)
            {TokenType.PLUS, 4},
            {TokenType.MINUS, 4},

            // 乘除模运算符 (优先级 5)
            {TokenType.MULTIPLY, 5},
            {TokenType.DIVIDE, 5},
            {TokenType.MODULO, 5}
        };

        /// <summary>
        /// 定义一元运算符
        /// </summary>
        private static readonly HashSet<TokenType> _unaryOperators = new()
        {
            TokenType.NOT,
            TokenType.MINUS
        };

        /// <summary>
        /// 解析表达式
        /// </summary>
        private ExpressionNode ParseExpression()
        {
            return ParseExpressionWithPrecedence(0);
        }

        /// <summary>
        /// 使用优先级解析表达式
        /// </summary>
        /// <param name="minPrecedence">最小优先级，低于此优先级的运算符将不被处理</param>
        /// <returns>表达式节点</returns>
        private ExpressionNode ParseExpressionWithPrecedence(int minPrecedence)
        {
            // 解析一个表达式项（可能是一元运算符后跟表达式项，或者是基本表达式）
            var left = ParseExpressionTerm();

            // 继续解析可能的中缀运算符
            while (true)
            {
                // 如果当前token不是运算符，或者优先级低于最小优先级，则退出循环
                if (!IsTokenBinaryOperator(_currentToken.Type, out int precedence) || precedence < minPrecedence)
                {
                    break;
                }

                // 获取运算符信息
                string op = _currentToken.Value;
                int line = _currentToken.Line;
                int column = _currentToken.Column;
                Consume(_currentToken.Type);

                // 解析运算符右侧的表达式，传入当前运算符的优先级+1确保同优先级的右结合性
                var right = ParseExpressionWithPrecedence(precedence + 1);

                // 构建二元运算符节点
                left = new BinaryOpNode(left, op, right, line, column);
            }

            return left;
        }

        /// <summary>
        /// 判断Token类型是否为二元运算符
        /// </summary>
        private bool IsTokenBinaryOperator(TokenType type, out int precedence)
        {
            return _binaryOperators.TryGetValue(type, out precedence);
        }

        /// <summary>
        /// 解析表达式项（一元运算或基本表达式 + 后缀链）
        /// </summary>
        private ExpressionNode ParseExpressionTerm()
        {
            // 检查是否是一元运算符
            if (_unaryOperators.Contains(_currentToken.Type))
            {
                string op = _currentToken.Value;
                int line = _currentToken.Line;
                int column = _currentToken.Column;
                Consume(_currentToken.Type);

                // 递归解析操作数
                var operand = ParseExpressionTerm();
                return new UnaryOpNode(op, operand, line, column);
            }

            // 解析基本表达式
            var baseExpr = ParsePrimary();

            // 应用后缀链循环
            return ParsePostfixChain(baseExpr);
        }

        /// <summary>
        /// 解析后缀链（函数调用、成员访问、索引访问）
        /// </summary>
        private ExpressionNode ParsePostfixChain(ExpressionNode baseExpr)
        {
            var current = baseExpr;

            while (true)
            {
                // 前进保护：记录当前位置
                int beforeIndex = _tokenIndex;

                switch (_currentToken.Type)
                {
                    case TokenType.LEFT_PAREN:
                        // 函数调用：expr(args)
                        var line = _currentToken.Line;
                        var column = _currentToken.Column;
                        Consume(TokenType.LEFT_PAREN);

                        List<ExpressionNode> parameters;
                        try
                        {
                            parameters = ParseParameterList();

                            // 错误恢复：处理缺失的右括号
                            if (_currentToken.Type != TokenType.RIGHT_PAREN)
                            {
                                // 尝试同步到右括号或其他停止符号
                                SynchronizeToTokens(TokenType.RIGHT_PAREN, TokenType.COMMA, TokenType.COMMAND_END,
                                    TokenType.NEWLINE, TokenType.NODE_END, TokenType.NODE_START);

                                if (_currentToken.Type == TokenType.RIGHT_PAREN)
                                {
                                    MLogger.Warning($"语法警告: 第{line}行，第{column}列，自动修复缺失的右括号");
                                    Consume(TokenType.RIGHT_PAREN);
                                }
                                else
                                {
                                    throw new InvalidOperationException($"语法错误: 第{line}行，第{column}列，期望 )，但得到 {_currentToken.Type}");
                                }
                            }
                            else
                            {
                                Consume(TokenType.RIGHT_PAREN);
                            }
                        }
                        catch (Exception ex)
                        {
                            MLogger.Error($"函数调用解析错误: {ex.Message}");
                            // 返回当前节点，结束后缀链解析
                            return current;
                        }

                        current = new CallExpressionNode(current, parameters, line, column);
                        break;

                    case TokenType.DOT:
                        // 成员访问：expr.member
                        line = _currentToken.Line;
                        column = _currentToken.Column;
                        Consume(TokenType.DOT);

                        if (_currentToken.Type != TokenType.IDENTIFIER)
                        {
                            // 统一错误策略：尝试同步并返回当前节点
                            MLogger.Error($"语法错误: 第{_currentToken.Line}行，第{_currentToken.Column}列，期望标识符，但得到 {_currentToken.Type}");
                            SynchronizeToTokens(TokenType.IDENTIFIER, TokenType.DOT, TokenType.LEFT_PAREN,
                                TokenType.LEFT_BRACKET, TokenType.COMMAND_END, TokenType.NEWLINE,
                                TokenType.NODE_END, TokenType.NODE_START);
                            return current;
                        }

                        var memberName = _currentToken.Value;
                        Consume(TokenType.IDENTIFIER);
                        current = new MemberAccessNode(current, memberName, line, column);
                        break;

                    case TokenType.LEFT_BRACKET:
                        // 索引访问：expr[index]
                        line = _currentToken.Line;
                        column = _currentToken.Column;
                        Consume(TokenType.LEFT_BRACKET);

                        ExpressionNode indexExpr;
                        try
                        {
                            indexExpr = ParseExpression();

                            // 错误恢复：处理缺失的右中括号
                            if (_currentToken.Type != TokenType.RIGHT_BRACKET)
                            {
                                // 尝试同步到右中括号或其他停止符号
                                SynchronizeToTokens(TokenType.RIGHT_BRACKET, TokenType.COMMA, TokenType.COMMAND_END,
                                    TokenType.NEWLINE, TokenType.NODE_END, TokenType.NODE_START);

                                if (_currentToken.Type == TokenType.RIGHT_BRACKET)
                                {
                                    MLogger.Warning($"语法警告: 第{line}行，第{column}列，自动修复缺失的右中括号");
                                    Consume(TokenType.RIGHT_BRACKET);
                                }
                                else
                                {
                                    throw new InvalidOperationException($"语法错误: 第{line}行，第{column}列，期望 ]，但得到 {_currentToken.Type}");
                                }
                            }
                            else
                            {
                                Consume(TokenType.RIGHT_BRACKET);
                            }
                        }
                        catch (Exception ex)
                        {
                            MLogger.Error($"索引访问解析错误: {ex.Message}");
                            // 返回当前节点，结束后缀链解析
                            return current;
                        }

                        current = new IndexAccessNode(current, indexExpr, line, column);
                        break;

                    default:
                        // 没有更多后缀操作，返回当前表达式
                        return current;
                }

                // 前进保护：检查是否前进
                if (_tokenIndex <= beforeIndex)
                {
                    throw new InvalidOperationException($"语法错误: 第{_currentToken.Line}行，第{_currentToken.Column}列，无法在后缀链内前进，可能存在死循环");
                }
            }
        }

        /// <summary>
        /// 同步到指定的Token类型（用于错误恢复）
        /// </summary>
        /// <param name="tokens">目标Token类型数组</param>
        private void SynchronizeToTokens(params TokenType[] tokens)
        {
            int maxAdvance = 50; // 防止无限循环
            int advance = 0;

            while (_currentToken.Type != TokenType.EOF && advance < maxAdvance)
            {
                if (tokens.Contains(_currentToken.Type))
                {
                    return; // 找到目标Token
                }
                GetNextToken();
                advance++;
            }

            if (advance >= maxAdvance)
            {
                MLogger.Warning($"错误恢复: 第{_currentToken.Line}行，第{_currentToken.Column}列，达到最大前进限制，停止同步");
            }
        }

        /// <summary>
        /// 解析主表达式
        /// </summary>
        private ExpressionNode ParsePrimary()
        {
            var token = _currentToken;
            switch (token.Type)
            {
                case TokenType.NUMBER:
                    Consume(TokenType.NUMBER);
                    return new NumberNode(double.Parse(token.Value), token.Line, token.Column);

                case TokenType.QUOTE:
                    return ParseString(token);

                case TokenType.TRUE:
                    Consume(TokenType.TRUE);
                    return new BooleanNode(true, token.Line, token.Column);

                case TokenType.FALSE:
                    Consume(TokenType.FALSE);
                    return new BooleanNode(false, token.Line, token.Column);

                case TokenType.VARIABLE:
                    string variableValue = _currentToken.Value;
                    int line = _currentToken.Line;
                    int column = _currentToken.Column;
                    Consume(TokenType.VARIABLE);
                    return new VariableNode(variableValue, line, column);

                case TokenType.IDENTIFIER:
                    string identifierValue = _currentToken.Value;
                    line = _currentToken.Line;
                    column = _currentToken.Column;
                    Consume(TokenType.IDENTIFIER);
                    return new IdentifierNode(identifierValue, line, column);

                case TokenType.LEFT_PAREN:
                    Consume(TokenType.LEFT_PAREN);
                    var expr = ParseExpression();
                    Consume(TokenType.RIGHT_PAREN);
                    return expr;

                default:
                    throw new InvalidOperationException($"语法错误: 第{token.Line}行，第{token.Column}列，意外的符号 {token.Type}");
            }
        }

        /// <summary>
        /// 检查是否到达文件结束
        /// </summary>
        private bool IsEOF()
        {
            return _currentToken.Type == TokenType.EOF;
        }

        /// <summary>
        /// 检查是否到达集合结束
        /// </summary>
        private bool IsCollectionEnd()
        {
            return Check(TokenType.NODE_END) || Check(TokenType.NODE_START);
        }

        /// <summary>
        /// 检查下一个命令Token类型
        /// </summary>
        private bool CheckNextCommand(TokenType commandType)
        {
            if (_tokenIndex + 1 < _tokens.Count)
            {
                return _tokens[_tokenIndex + 1].Type == commandType;
            }
            return false;
        }
    }
}
