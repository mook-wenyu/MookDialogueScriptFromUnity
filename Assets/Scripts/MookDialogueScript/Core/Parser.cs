using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using UnityEngine;

namespace MookDialogueScript
{

    /// <summary>
    /// 定义操作符信息，包含优先级、相关的TokenType和操作符描述
    /// </summary>
    public class OperatorInfo
    {
        public int Precedence { get; }
        public TokenType[] TokenTypes { get; }
        public string Description { get; }

        public OperatorInfo(int precedence, string description, params TokenType[] tokenTypes)
        {
            Precedence = precedence;
            Description = description;
            TokenTypes = tokenTypes;
        }
    }

    public class Parser
    {
        private readonly Lexer _lexer;
        private Token _currentToken;
        private readonly List<Token> _tokens;
        private int _tokenIndex;
        private readonly bool _useTokenList;

        public Parser(Lexer lexer)
        {
            _lexer = lexer;
            _tokens = null;
            _tokenIndex = 0;
            _useTokenList = false;
            _currentToken = _lexer.GetNextToken();
        }

        public Parser(List<Token> tokens)
        {
            if (tokens == null || tokens.Count == 0)
            {
                Debug.LogError("Token列表不能为空");
                throw new ArgumentException("Token列表不能为空");
            }

            _lexer = null;
            _tokens = tokens;
            _tokenIndex = 0;
            _useTokenList = true;
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
                Debug.LogError($"语法错误: 第{_currentToken.Line}行，第{_currentToken.Column}列，期望 {type}，但得到 {_currentToken.Type}");
                // 不再抛出异常，尝试继续解析
            }
        }

        /// <summary>
        /// 获取下一个Token
        /// </summary>
        private void GetNextToken()
        {
            if (_useTokenList)
            {
                _tokenIndex++;
                if (_tokenIndex < _tokens.Count)
                {
                    _currentToken = _tokens[_tokenIndex];
                }
                else
                {
                    _currentToken = new Token(TokenType.EOF, "", _tokens[_tokens.Count - 1].Line, _tokens[_tokens.Count - 1].Column);
                }
            }
            else
            {
                _currentToken = _lexer.GetNextToken();
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
            if (_useTokenList)
            {
                if (_tokenIndex + 1 < _tokens.Count)
                {
                    return _tokens[_tokenIndex + 1].Type == type;
                }
                return false;
            }
            else
            {
                // 使用Lexer的PeekNextToken方法，但不缓存结果
                Token nextToken = _lexer.PeekNextToken();
                return nextToken.Type == type;
            }
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

            while (_currentToken.Type != TokenType.EOF)
            {
                if (_currentToken.Type == TokenType.DOUBLE_COLON ||
                    _currentToken.Type == TokenType.NODE_START)
                {
                    var node = ParseNodeDefinition();

                    // 如果节点内容为空，则跳过该节点
                    if (node != null && node.Content.Count > 0)
                    {
                        nodes.Add(node);
                    }
                }
                else if (_currentToken.Type == TokenType.NEWLINE)
                {
                    Consume(TokenType.NEWLINE);
                }
                else
                {
                    Debug.LogError($"语法错误: 第{_currentToken.Line}行，第{_currentToken.Column}列，意外的Token {_currentToken.Type}");
                    // 尝试跳过有问题的token
                    GetNextToken();
                }
            }

            return new ScriptNode(nodes, 1, 1);
        }

        /// <summary>
        /// 解析节点定义
        /// </summary>
        private NodeDefinitionNode ParseNodeDefinition()
        {
            if (_currentToken.Type == TokenType.NODE_START)
            {
                Consume(TokenType.NODE_START);
            }
            else if (_currentToken.Type == TokenType.DOUBLE_COLON)
            {
                Consume(TokenType.DOUBLE_COLON);
            }

            if (_currentToken.Type != TokenType.IDENTIFIER)
            {
                Debug.LogError($"语法错误: 第{_currentToken.Line}行，第{_currentToken.Column}列，期望节点名称，但得到 {_currentToken.Type}");
                return null;
            }

            string nodeName = _currentToken.Value;
            int line = _currentToken.Line;
            int column = _currentToken.Column;

            Consume(TokenType.IDENTIFIER);
            Consume(TokenType.NEWLINE);

            var content = new List<ContentNode>();
            while (_currentToken.Type != TokenType.EOF &&
                   _currentToken.Type != TokenType.DOUBLE_COLON &&
                   _currentToken.Type != TokenType.NODE_START)
            {
                if (_currentToken.Type == TokenType.NEWLINE)
                {
                    Consume(TokenType.NEWLINE);
                    continue;
                }

                if (_currentToken.Type == TokenType.NODE_END)
                {
                    Consume(TokenType.NODE_END);
                    break;
                }

                ContentNode node = ParseContent();
                // 跳过空内容节点或解析错误的节点
                if (node != null && !IsEmptyContentNode(node))
                {
                    content.Add(node);
                }
            }

            return new NodeDefinitionNode(nodeName, content, line, column);
        }

        /// <summary>
        /// 检查内容节点是否为空
        /// </summary>
        private bool IsEmptyContentNode(ContentNode node)
        {
            // 对于旁白节点，检查是否只包含空文本
            if (node is NarrationNode narration)
            {
                // 如果没有文本段落，或者所有文本段落都是空的
                return narration.Text.Count == 0 ||
                       narration.Text.All(segment => segment is TextNode textNode && string.IsNullOrWhiteSpace(textNode.Text));
            }

            // 对于对话节点，检查是否只包含空文本
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

        private ContentNode ParseContent()
        {
            switch (_currentToken.Type)
            {
                case TokenType.TEXT:
                case TokenType.IDENTIFIER:  // 增加对IDENTIFIER类型的支持，使其也被识别为对话的开始
                    // 如果后面有冒号，则认为是对话
                    if (CheckNext(TokenType.LEFT_BRACKET) || CheckNext(TokenType.COLON))
                    {
                        return ParseDialogue();
                    }
                    return ParseNarration();
                case TokenType.ARROW:
                    return ParseChoice();
                case TokenType.IF:
                    return ParseCondition();
                case TokenType.ENDIF:
                    Debug.LogError($"语法错误: 第{_currentToken.Line}行，第{_currentToken.Column}列，意外的ENDIF");
                    throw new InvalidOperationException($"语法错误: 第{_currentToken.Line}行，第{_currentToken.Column}列，意外的ENDIF");
                case TokenType.COMMAND:
                case TokenType.SET:
                case TokenType.ADD:
                case TokenType.SUB:
                case TokenType.MUL:
                case TokenType.DIV:
                case TokenType.MOD:
                case TokenType.CALL:
                case TokenType.WAIT:
                case TokenType.VAR:
                case TokenType.JUMP:
                    return ParseCommand();
                default:
                    return ParseNarration();
            }
        }

        /// <summary>
        /// 解析对话
        /// </summary>
        private DialogueNode ParseDialogue()
        {
            string speaker = _currentToken.Value;
            int line = _currentToken.Line;
            int column = _currentToken.Column;

            // 消耗当前token，可能是TEXT或IDENTIFIER
            Consume(_currentToken.Type);

            string emotion = null;
            // 记录扫描到的位置，用于防止重复处理同一个LEFT_BRACKET
            int lastBracketLine = -1;
            int lastBracketColumn = -1;

            if (_currentToken.Type == TokenType.LEFT_BRACKET)
            {
                // 记录当前LEFT_BRACKET的位置
                lastBracketLine = _currentToken.Line;
                lastBracketColumn = _currentToken.Column;

                // 直接消耗左括号
                GetNextToken();

                // 防止重复处理同一个LEFT_BRACKET
                if (_currentToken.Type == TokenType.LEFT_BRACKET &&
                    _currentToken.Line == lastBracketLine &&
                    _currentToken.Column == lastBracketColumn)
                {
                    // 这是重复的LEFT_BRACKET，跳过它
                    Debug.LogWarning($"警告: 第{_currentToken.Line}行，第{_currentToken.Column}列，跳过重复的左括号");
                    GetNextToken();
                }

                if (_currentToken.Type == TokenType.IDENTIFIER || _currentToken.Type == TokenType.TEXT)
                {
                    emotion = _currentToken.Value;
                    GetNextToken();
                }
                else
                {
                    Debug.LogError($"语法错误: 第{_currentToken.Line}行，第{_currentToken.Column}列，期望表情名称，但得到 {_currentToken.Type}");
                    throw new InvalidOperationException($"语法错误: 第{_currentToken.Line}行，第{_currentToken.Column}列，期望表情名称，但得到 {_currentToken.Type}");
                }

                if (_currentToken.Type == TokenType.RIGHT_BRACKET)
                {
                    GetNextToken();
                }
                else
                {
                    Debug.LogError($"语法错误: 第{_currentToken.Line}行，第{_currentToken.Column}列，期望右括号，但得到 {_currentToken.Type}");
                    throw new InvalidOperationException($"语法错误: 第{_currentToken.Line}行，第{_currentToken.Column}列，期望右括号，但得到 {_currentToken.Type}");
                }
            }

            if (_currentToken.Type == TokenType.COLON)
            {
                GetNextToken();
            }
            else
            {
                Debug.LogError($"语法错误: 第{_currentToken.Line}行，第{_currentToken.Column}列，期望冒号，但得到 {_currentToken.Type}");
                throw new InvalidOperationException($"语法错误: 第{_currentToken.Line}行，第{_currentToken.Column}列，期望冒号，但得到 {_currentToken.Type}");
            }

            var text = ParseText();

            List<string> labels = null;
            while (_currentToken.Type == TokenType.HASH)
            {
                if (labels == null)
                {
                    labels = new List<string>();
                }

                // 消耗HASH标记
                Consume(TokenType.HASH);

                // 获取标签名称（应该是IDENTIFIER）
                if (_currentToken.Type != TokenType.IDENTIFIER)
                {
                    Debug.LogError($"语法错误: 第{_currentToken.Line}行，第{_currentToken.Column}列，期望标签名称，但得到 {_currentToken.Type}");
                    throw new InvalidOperationException($"语法错误: 第{_currentToken.Line}行，第{_currentToken.Column}列，期望标签名称，但得到 {_currentToken.Type}");
                }

                labels.Add(_currentToken.Value);
                Consume(TokenType.IDENTIFIER);
            }

            // 处理文件末尾情况
            if (_currentToken.Type != TokenType.EOF)
            {
                Consume(TokenType.NEWLINE);
            }

            return new DialogueNode(speaker, emotion, text, labels, line, column);
        }

        /// <summary>
        /// 解析文本
        /// </summary>
        private List<TextSegmentNode> ParseText()
        {
            var segments = new List<TextSegmentNode>();
            StringBuilder textBuilder = new StringBuilder(64); // 预分配更大的初始容量

            while (_currentToken.Type != TokenType.NEWLINE &&
                   _currentToken.Type != TokenType.HASH &&
                   _currentToken.Type != TokenType.LEFT_BRACKET) // 添加对LEFT_BRACKET的判断，使其提前结束
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
                    if (textBuilder.Length > 0)
                    {
                        textBuilder.Append(" ");
                    }
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
        /// 解析缩进的内容块，直到遇到指定的结束标记
        /// </summary>
        private List<ContentNode> ParseIndentedContent(params TokenType[] endTokens)
        {
            Consume(TokenType.NEWLINE);
            Consume(TokenType.INDENT);

            var content = new List<ContentNode>();

            // 检查当前标记是否是任一结束标记
            bool IsEndToken()
            {
                if (_currentToken.Type == TokenType.DEDENT || _currentToken.Type == TokenType.EOF ||
                    _currentToken.Type == TokenType.DOUBLE_COLON)
                    return true;

                foreach (var endToken in endTokens)
                {
                    if (_currentToken.Type == endToken)
                        return true;
                }
                return false;
            }

            while (!IsEndToken())
            {
                ContentNode node;
                if (_currentToken.Type == TokenType.IF)
                {
                    node = ParseCondition();
                }
                else
                {
                    node = ParseContent();
                }

                // 跳过空的内容节点
                if (!IsEmptyContentNode(node))
                {
                    content.Add(node);
                }
            }

            // 只在遇到DEDENT时消耗它，其他结束标记需要留给调用者处理
            if (_currentToken.Type == TokenType.DEDENT)
            {
                Consume(TokenType.DEDENT);
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
            if (Match(TokenType.LEFT_BRACKET))
            {
                if (_currentToken.Type == TokenType.IF)
                {
                    Consume(TokenType.IF);
                    condition = ParseExpression();
                }
                else
                {
                    // 如果没有IF关键字，回退并将内容视为选项文本的一部分
                    Debug.LogError($"语法错误: 第{_currentToken.Line}行，第{_currentToken.Column}列，在条件表达式中期望IF关键字");
                }

                Consume(TokenType.RIGHT_BRACKET);
            }

            var content = ParseIndentedContent();

            return new ChoiceNode(text, condition, content, line, column);
        }

        /// <summary>
        /// 解析条件
        /// </summary>
        private ConditionNode ParseCondition()
        {
            int line = _currentToken.Line;
            int column = _currentToken.Column;
            Consume(TokenType.IF);

            var condition = ParseExpression();

            // 解析then分支
            var thenBranch = ParseIndentedContent(TokenType.ELIF, TokenType.ELSE, TokenType.ENDIF);

            // 解析elif分支
            var elifBranches = new List<(ExpressionNode Condition, List<ContentNode> Content)>();
            while (_currentToken.Type == TokenType.ELIF)
            {
                Consume(TokenType.ELIF);
                var elifCondition = ParseExpression();

                var elifContent = ParseIndentedContent(TokenType.ELIF, TokenType.ELSE, TokenType.ENDIF);
                elifBranches.Add((elifCondition, elifContent));
            }

            // 解析else分支
            List<ContentNode> elseBranch = null;
            if (_currentToken.Type == TokenType.ELSE)
            {
                Consume(TokenType.ELSE);
                elseBranch = ParseIndentedContent(TokenType.ENDIF);
            }

            // 验证并消耗结束标记
            if (_currentToken.Type != TokenType.ENDIF)
            {
                Debug.LogError($"语法错误: 第{_currentToken.Line}行，第{_currentToken.Column}列，期望 ENDIF，但得到了 {_currentToken.Type}");
                // 尝试恢复 - 搜索下一个ENDIF或文件结束
                while (_currentToken.Type != TokenType.ENDIF && _currentToken.Type != TokenType.EOF)
                {
                    GetNextToken();
                }
            }

            if (_currentToken.Type == TokenType.ENDIF)
            {
                Consume(TokenType.ENDIF);
                Consume(TokenType.NEWLINE);
            }

            return new ConditionNode(condition, thenBranch, elifBranches, elseBranch, line, column);
        }

        /// <summary>
        /// 解析命令
        /// </summary>
        private CommandNode ParseCommand()
        {
            int line = _currentToken.Line;
            int column = _currentToken.Column;

            string commandValue = _currentToken.Value.ToLower();
            TokenType commandType = _currentToken.Type;
            Consume(commandType);

            switch (commandType)
            {
                case TokenType.SET:
                case TokenType.ADD:
                case TokenType.SUB:
                case TokenType.MUL:
                case TokenType.DIV:
                case TokenType.MOD:
                    return ParseVarOperation(commandValue, line, column);

                case TokenType.CALL:
                    return ParseFunctionCall(line, column);

                case TokenType.WAIT:
                    return ParseWaitCommand(line, column);

                case TokenType.VAR:
                    return ParseVarDeclaration(line, column);

                case TokenType.JUMP:
                    return ParseJumpCommand(line, column);

                case TokenType.COMMAND:
                    Debug.LogError($"未知命令 {commandValue}");
                    return null; // 不会执行到这里

                default:
                    Debug.LogError($"未知命令类型 {commandType}");
                    return null; // 不会执行到这里
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
            ConsumeNewlineOrEOF();

            return new VarCommandNode(variable, value, operation, line, column);
        }

        /// <summary>
        /// 解析函数调用命令
        /// </summary>
        private CallCommandNode ParseFunctionCall(int line, int column)
        {
            string functionName = _currentToken.Value;
            Consume(TokenType.IDENTIFIER);
            Consume(TokenType.LEFT_PAREN);

            var parameters = ParseParameterList();

            Consume(TokenType.RIGHT_PAREN);
            ConsumeNewlineOrEOF();

            return new CallCommandNode(functionName, parameters, line, column);
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
            ConsumeNewlineOrEOF();
            return new WaitCommandNode(duration, line, column);
        }

        /// <summary>
        /// 解析变量声明
        /// </summary>
        private VarCommandNode ParseVarDeclaration(int line, int column)
        {
            if (_currentToken.Type != TokenType.VARIABLE)
            {
                Debug.LogError($"期望变量名，但得到了 {_currentToken.Type}");
            }

            string varName = _currentToken.Value;
            Consume(TokenType.VARIABLE);

            // 赋值符号是可选的
            if (_currentToken.Type == TokenType.ASSIGN)
            {
                Consume(TokenType.ASSIGN);
            }

            var initialValue = ParseExpression();
            ConsumeNewlineOrEOF();

            return new VarCommandNode(varName, initialValue, "var", line, column);
        }

        /// <summary>
        /// 解析跳转命令
        /// </summary>
        private JumpCommandNode ParseJumpCommand(int line, int column)
        {
            string targetNode = _currentToken.Value;
            Consume(TokenType.IDENTIFIER);
            ConsumeNewlineOrEOF();
            return new JumpCommandNode(targetNode, line, column);
        }

        /// <summary>
        /// 解析旁白
        /// </summary>
        private NarrationNode ParseNarration()
        {
            int line = _currentToken.Line;
            int column = _currentToken.Column;
            var text = ParseText();

            List<string> labels = null;
            while (Match(TokenType.HASH))
            {
                if (labels == null)
                {
                    labels = new List<string>();
                }
                labels.Add(_currentToken.Value);
                Consume(TokenType.IDENTIFIER);
            }

            // 处理文件末尾情况
            if (_currentToken.Type != TokenType.EOF)
            {
                Consume(TokenType.NEWLINE);
            }

            return new NarrationNode(text, labels, line, column);
        }

        /// <summary>
        /// 处理换行符（包括文件末尾情况）
        /// </summary>
        private void ConsumeNewlineOrEOF()
        {
            if (_currentToken.Type != TokenType.EOF)
            {
                Consume(TokenType.NEWLINE);
            }
        }

        /// <summary>
        /// 解析字符串插值
        /// </summary>
        private ExpressionNode ParseStringInterpolation(Token token)
        {
            var segments = new List<TextSegmentNode>();
            string text = token.Value;
            int startIndex = 0;
            int exprLine = token.Line;
            int exprColumn = token.Column;

            while (true)
            {
                int leftBrace = text.IndexOf('{', startIndex);
                if (leftBrace == -1)
                {
                    if (startIndex < text.Length)
                    {
                        segments.Add(new TextNode(text.Substring(startIndex), exprLine, exprColumn + startIndex));
                    }
                    break;
                }

                if (leftBrace > startIndex)
                {
                    segments.Add(new TextNode(text.Substring(startIndex, leftBrace - startIndex), exprLine, exprColumn + startIndex));
                }

                int rightBrace = text.IndexOf('}', leftBrace);
                if (rightBrace == -1)
                {
                    Debug.LogError($"语法错误: 第{exprLine}行，第{exprColumn + leftBrace}列，未闭合的插值表达式");
                    // 如果错误，返回已解析的部分作为普通文本
                    if (segments.Count == 0)
                    {
                        return new StringNode(text, exprLine, exprColumn);
                    }
                    else if (segments.Count == 1 && segments[0] is TextNode firstTextNode)
                    {
                        return new StringNode(firstTextNode.Text, exprLine, exprColumn);
                    }
                    return new InterpolationExpressionNode(segments, exprLine, exprColumn);
                }

                string varName = text.Substring(leftBrace + 1, rightBrace - leftBrace - 1).Trim();
                if (varName.StartsWith("$"))
                {
                    segments.Add(new InterpolationNode(
                        new VariableNode(varName.Substring(1), exprLine, exprColumn + leftBrace),
                        exprLine, exprColumn + leftBrace));
                }
                else
                {
                    Debug.LogError($"语法错误: 第{exprLine}行，第{exprColumn + leftBrace}列，插值表达式中的变量名必须以$开头");
                    // 尝试作为普通文本处理
                    segments.Add(new TextNode($"{{{varName}}}", exprLine, exprColumn + leftBrace));
                }

                startIndex = rightBrace + 1;
            }

            // 如果只有一个文本段，直接返回StringNode
            if (segments.Count == 1 && segments[0] is TextNode singleTextNode)
            {
                return new StringNode(singleTextNode.Text, exprLine, exprColumn);
            }

            // 否则创建一个插值表达式
            return new InterpolationExpressionNode(segments, exprLine, exprColumn);
        }

        /// <summary>
        /// 定义所有二元操作符的优先级(数字越大优先级越高)
        /// </summary>
        private static readonly Dictionary<TokenType, OperatorInfo> BinaryOperators = new Dictionary<TokenType, OperatorInfo>
        {
            // 逻辑运算符 (优先级 1-2)
            { TokenType.OR, new OperatorInfo(1, "逻辑或", TokenType.OR) },
            { TokenType.AND, new OperatorInfo(2, "逻辑与", TokenType.AND) },
            { TokenType.XOR, new OperatorInfo(2, "异或", TokenType.XOR) },
            
            // 比较运算符 (优先级 3)
            { TokenType.EQUALS, new OperatorInfo(3, "等于", TokenType.EQUALS) },
            { TokenType.NOT_EQUALS, new OperatorInfo(3, "不等于", TokenType.NOT_EQUALS) },
            { TokenType.GREATER, new OperatorInfo(3, "大于", TokenType.GREATER) },
            { TokenType.LESS, new OperatorInfo(3, "小于", TokenType.LESS) },
            { TokenType.GREATER_EQUALS, new OperatorInfo(3, "大于等于", TokenType.GREATER_EQUALS) },
            { TokenType.LESS_EQUALS, new OperatorInfo(3, "小于等于", TokenType.LESS_EQUALS) },
            
            // 加减运算符 (优先级 4)
            { TokenType.PLUS, new OperatorInfo(4, "加法", TokenType.PLUS) },
            { TokenType.MINUS, new OperatorInfo(4, "减法", TokenType.MINUS) },
            
            // 乘除模运算符 (优先级 5)
            { TokenType.MULTIPLY, new OperatorInfo(5, "乘法", TokenType.MULTIPLY) },
            { TokenType.DIVIDE, new OperatorInfo(5, "除法", TokenType.DIVIDE) },
            { TokenType.MODULO, new OperatorInfo(5, "取模", TokenType.MODULO) }
        };

        /// <summary>
        /// 定义一元运算符
        /// </summary>
        private static readonly HashSet<TokenType> UnaryOperators = new HashSet<TokenType>
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
            ExpressionNode left = ParseExpressionTerm();

            // 继续解析可能的中缀运算符
            while (true)
            {
                // 如果当前token不是运算符，或者优先级低于最小优先级，则退出循环
                if (!IsTokenBinaryOperator(_currentToken.Type, out OperatorInfo opInfo) || opInfo.Precedence < minPrecedence)
                {
                    break;
                }

                // 获取运算符信息
                string op = _currentToken.Value;
                int line = _currentToken.Line;
                int column = _currentToken.Column;
                Consume(_currentToken.Type);

                // 解析运算符右侧的表达式，传入当前运算符的优先级+1确保同优先级的右结合性
                ExpressionNode right = ParseExpressionWithPrecedence(opInfo.Precedence + 1);

                // 构建二元运算符节点
                left = new BinaryOpNode(left, op, right, line, column);
            }

            return left;
        }

        /// <summary>
        /// 判断Token类型是否为二元运算符
        /// </summary>
        private bool IsTokenBinaryOperator(TokenType type, out OperatorInfo operatorInfo)
        {
            return BinaryOperators.TryGetValue(type, out operatorInfo);
        }

        /// <summary>
        /// 解析表达式项（一元运算或基本表达式）
        /// </summary>
        private ExpressionNode ParseExpressionTerm()
        {
            // 检查是否是一元运算符
            if (UnaryOperators.Contains(_currentToken.Type))
            {
                string op = _currentToken.Value;
                int line = _currentToken.Line;
                int column = _currentToken.Column;
                Consume(_currentToken.Type);

                // 递归解析操作数
                ExpressionNode operand = ParseExpressionTerm();
                return new UnaryOpNode(op, operand, line, column);
            }

            // 否则解析基本表达式
            return ParsePrimary();
        }

        /// <summary>
        /// 解析主表达式
        /// </summary>
        private ExpressionNode ParsePrimary()
        {
            Token token = _currentToken;
            switch (token.Type)
            {
                case TokenType.NUMBER:
                    Consume(TokenType.NUMBER);
                    return new NumberNode(double.Parse(token.Value), token.Line, token.Column);

                case TokenType.STRING:
                    Consume(TokenType.STRING);
                    // 检查字符串中是否包含插值表达式
                    if (token.Value.Contains("{") && token.Value.Contains("}"))
                    {
                        return ParseStringInterpolation(token);
                    }
                    return new StringNode(token.Value, token.Line, token.Column);

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

                    // 检查是否是函数调用
                    if (Check(TokenType.LEFT_PAREN))
                    {
                        Consume(TokenType.LEFT_PAREN);
                        var parameters = ParseParameterList();
                        Consume(TokenType.RIGHT_PAREN);
                        return new FunctionCallNode(identifierValue, parameters, line, column);
                    }

                    return new IdentifierNode(identifierValue, line, column);

                case TokenType.LEFT_PAREN:
                    Consume(TokenType.LEFT_PAREN);
                    var expr = ParseExpression();
                    Consume(TokenType.RIGHT_PAREN);
                    return expr;

                default:
                    Debug.LogError($"语法错误: 第{token.Line}行，第{token.Column}列，意外的符号 {token.Type}");
                    // 返回一个默认值
                    GetNextToken(); // 跳过当前token
                    return new StringNode("", token.Line, token.Column);
            }
        }

    }
}