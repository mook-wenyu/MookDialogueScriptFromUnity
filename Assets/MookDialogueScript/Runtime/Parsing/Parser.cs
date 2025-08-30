using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MookDialogueScript.Parsing
{
    /// <summary>
    /// 重构后的语法解析器
    /// 遵循SOLID原则，采用组合设计模式
    /// 职责分离：依赖专业组件完成各项解析任务
    /// </summary>
    public class Parser : IDisposable
    {
        #region 组合组件
        private readonly ITokenBuffer _tokenBuffer;
        private readonly IExpressionParser _expressionParser;
        private readonly NodeCacheManager _nodeCache;
        private readonly ParseContext _context;
        #endregion

        #region 解析状态
        private bool _disposed;
        #endregion

        #region 构造函数
        /// <summary>
        /// 使用默认组件创建解析器
        /// </summary>
        public Parser()
        {
            _nodeCache = new NodeCacheManager();
            _tokenBuffer = new TokenBufferManager();
            _expressionParser = new ExpressionParser(_nodeCache, _tokenBuffer);
            _context = new ParseContext();
        }
        #endregion

        #region Parser 实现
        /// <summary>
        /// 解析Token列表生成AST
        /// </summary>
        public ScriptNode Parse(List<Token> tokens)
        {
            ThrowIfDisposed();

            if (tokens == null || tokens.Count == 0)
                throw new ArgumentException("Token列表不能为空", nameof(tokens));

            _context.Clear();
            _tokenBuffer.Reset(tokens);

            var nodes = new List<NodeDefinitionNode>();

            while (!_tokenBuffer.IsAtEnd)
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
        /// 获取缓存统计信息
        /// </summary>
        public Dictionary<string, object> GetCacheStatistics()
        {
            ThrowIfDisposed();
            return _nodeCache.GetStatistics();
        }

        /// <summary>
        /// 清理缓存
        /// </summary>
        public void ClearCache()
        {
            ThrowIfDisposed();
            _nodeCache.Clear();
        }
        #endregion

        #region 主要解析方法
        /// <summary>
        /// 解析节点定义
        /// </summary>
        private NodeDefinitionNode ParseNodeDefinition()
        {
            // 解析节点元数据
            var metadata = ParseNodeMetadata();

            // 需要有节点开始标记
            if (!_tokenBuffer.Check(TokenType.NODE_START))
            {
                throw new InvalidOperationException(
                    $"语法错误: 第{_tokenBuffer.Current.Line}行，第{_tokenBuffer.Current.Column}列，缺少节点开始标记 ---");
            }

            int line = _tokenBuffer.Current.Line;
            int column = _tokenBuffer.Current.Column;
            _tokenBuffer.Consume(TokenType.NODE_START);

            // 设置解析上下文
            var nodeName = GetMetadataValue(metadata, "node", "unnamed");
            _context.EnterNode(nodeName);

            var content = new List<ContentNode>();
            while (!_tokenBuffer.Check(TokenType.NODE_END) && !_tokenBuffer.IsAtEnd)
            {
                if (_tokenBuffer.Check(TokenType.NEWLINE))
                {
                    _tokenBuffer.Advance();
                    continue;
                }

                var node = ParseContentNode();
                if (node != null && !IsEmptyContentNode(node))
                {
                    content.Add(node);
                }
            }

            if (!_tokenBuffer.Check(TokenType.NODE_END))
            {
                throw new InvalidOperationException(
                    $"语法错误: 第{_tokenBuffer.Current.Line}行，第{_tokenBuffer.Current.Column}列，缺少节点结束标记 ===");
            }
            _tokenBuffer.Consume(TokenType.NODE_END);

            _context.ExitNode();

            return new NodeDefinitionNode(nodeName, metadata, content, line, column);
        }

        /// <summary>
        /// 解析节点元数据
        /// </summary>
        private Dictionary<string, string> ParseNodeMetadata()
        {
            var metadata = new Dictionary<string, string>();

            while (!_tokenBuffer.Check(TokenType.NODE_START) && !_tokenBuffer.IsAtEnd)
            {
                if (_tokenBuffer.Check(TokenType.NEWLINE))
                {
                    _tokenBuffer.Advance();
                    continue;
                }

                if (_tokenBuffer.Check(TokenType.IDENTIFIER) &&
                    _tokenBuffer.Peek().Type == TokenType.METADATA_SEPARATOR)
                {
                    string key = _tokenBuffer.Current.Value;
                    _tokenBuffer.Advance();
                    _tokenBuffer.Consume(TokenType.METADATA_SEPARATOR);

                    var valueBuilder = new System.Text.StringBuilder();
                    while (!_tokenBuffer.Check(TokenType.NEWLINE) && !_tokenBuffer.IsAtEnd)
                    {
                        valueBuilder.Append(_tokenBuffer.Current.Value);
                        _tokenBuffer.Advance();
                    }

                    metadata[key] = valueBuilder.ToString();

                    if (_tokenBuffer.Check(TokenType.NEWLINE))
                    {
                        _tokenBuffer.Advance();
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
        /// 解析内容节点
        /// </summary>
        private ContentNode ParseContentNode()
        {
            return _tokenBuffer.Current.Type switch
            {
                // 角色对话：角色名:
                TokenType.TEXT when _tokenBuffer.Peek().Type == TokenType.COLON => ParseDialogue(),

                // 选项：-> 文本
                TokenType.ARROW => ParseChoice(),

                // 条件：<<if>>
                TokenType.COMMAND_START when IsNextCommand(TokenType.IF) => ParseCondition(),

                // 其他命令：<<command>>
                TokenType.COMMAND_START => ParseCommand(),

                // 旁白：所有其他情况
                _ => ParseNarration()
            };
        }

        /// <summary>
        /// 解析对话
        /// </summary>
        private DialogueNode ParseDialogue()
        {
            int line = _tokenBuffer.Current.Line;
            int column = _tokenBuffer.Current.Column;

            string speaker = _tokenBuffer.Current.Value;
            _tokenBuffer.Advance();
            _tokenBuffer.Consume(TokenType.COLON);

            var text = ParseText();
            var tags = ParseTags();
            tags.Add(_context.GenerateLineTag());

            var content = ParseNestedContent();

            return new DialogueNode(speaker, text, tags, content, line, column);
        }

        /// <summary>
        /// 解析旁白
        /// </summary>
        private DialogueNode ParseNarration()
        {
            int line = _tokenBuffer.Current.Line;
            int column = _tokenBuffer.Current.Column;

            var text = ParseText();
            var tags = ParseTags();
            tags.Add(_context.GenerateLineTag());

            var content = ParseNestedContent();

            return new DialogueNode(null, text, tags, content, line, column);
        }

        /// <summary>
        /// 解析选择
        /// </summary>
        private ChoiceNode ParseChoice()
        {
            int line = _tokenBuffer.Current.Line;
            int column = _tokenBuffer.Current.Column;
            _tokenBuffer.Consume(TokenType.ARROW);

            var text = ParseText();

            ExpressionNode condition = null;
            var tags = new List<string>();

            // 解析 <<if 条件>> 语法
            if (_tokenBuffer.Check(TokenType.COMMAND_START))
            {
                _tokenBuffer.Advance();
                if (_tokenBuffer.Check(TokenType.IF))
                {
                    _tokenBuffer.Advance();
                    var (expr, _) = _expressionParser.ParseExpression();
                    condition = expr;
                }
                _tokenBuffer.Consume(TokenType.COMMAND_END);
            }

            // 解析标签
            var parsedTags = ParseTags();
            tags.AddRange(parsedTags);
            tags.Add(_context.GenerateLineTag());

            var content = ParseNestedContent();

            return new ChoiceNode(text, condition, tags, content, line, column);
        }

        /// <summary>
        /// 解析条件
        /// </summary>
        private ConditionNode ParseCondition()
        {
            int line = _tokenBuffer.Current.Line;
            int column = _tokenBuffer.Current.Column;

            _tokenBuffer.Consume(TokenType.COMMAND_START);
            _tokenBuffer.Consume(TokenType.IF);

            var (condition, _) = _expressionParser.ParseExpression();

            _tokenBuffer.Consume(TokenType.COMMAND_END);

            // 解析then分支
            var thenBranch = ParseConditionalBranch(TokenType.ELIF, TokenType.ELSE, TokenType.ENDIF);

            // 解析elif分支
            var elifBranches = new List<(ExpressionNode Condition, List<ContentNode> Content)>();
            while (_tokenBuffer.Check(TokenType.COMMAND_START) && IsNextCommand(TokenType.ELIF))
            {
                _tokenBuffer.Consume(TokenType.COMMAND_START);
                _tokenBuffer.Consume(TokenType.ELIF);

                var (elifCondition, _) = _expressionParser.ParseExpression();

                _tokenBuffer.Consume(TokenType.COMMAND_END);

                var elifContent = ParseConditionalBranch(TokenType.ELIF, TokenType.ELSE, TokenType.ENDIF);
                elifBranches.Add((elifCondition, elifContent));
            }

            // 解析else分支
            List<ContentNode> elseBranch = null;
            if (_tokenBuffer.Check(TokenType.COMMAND_START) && IsNextCommand(TokenType.ELSE))
            {
                _tokenBuffer.Consume(TokenType.COMMAND_START);
                _tokenBuffer.Consume(TokenType.ELSE);
                _tokenBuffer.Consume(TokenType.COMMAND_END);

                elseBranch = ParseConditionalBranch(TokenType.ENDIF);
            }

            // 消耗endif
            if (!_tokenBuffer.Check(TokenType.COMMAND_START) || !IsNextCommand(TokenType.ENDIF))
            {
                throw new InvalidOperationException(
                    $"语法错误: 第{_tokenBuffer.Current.Line}行，第{_tokenBuffer.Current.Column}列，缺少条件结束标记 <<endif>>");
            }

            _tokenBuffer.Consume(TokenType.COMMAND_START);
            _tokenBuffer.Consume(TokenType.ENDIF);
            _tokenBuffer.Consume(TokenType.COMMAND_END);

            return new ConditionNode(condition, thenBranch, elifBranches, elseBranch, line, column);
        }

        /// <summary>
        /// 解析条件分支内容
        /// </summary>
        private List<ContentNode> ParseConditionalBranch(params TokenType[] endTokens)
        {
            var content = new List<ContentNode>();

            while (!IsConditionalEnd(endTokens) && !_tokenBuffer.IsAtEnd)
            {
                if (_tokenBuffer.Check(TokenType.NEWLINE))
                {
                    _tokenBuffer.Advance();
                    continue;
                }

                // 忽略缩进控制符
                if (_tokenBuffer.Check(TokenType.INDENT) || _tokenBuffer.Check(TokenType.DEDENT))
                {
                    _tokenBuffer.Advance();
                    continue;
                }

                // 边界保护
                if (_tokenBuffer.Check(TokenType.NODE_END) || _tokenBuffer.Check(TokenType.NODE_START))
                {
                    MLogger.Error(
                        $"语法错误: 第{_tokenBuffer.Current.Line}行，第{_tokenBuffer.Current.Column}列，在条件块内遇到节点边界但未找到结束标记");
                    break;
                }

                var beforePosition = _tokenBuffer.Position;
                var node = ParseContentNode();

                if (node != null && !IsEmptyContentNode(node))
                {
                    content.Add(node);
                }

                // 无前进保护
                if (_tokenBuffer.Position <= beforePosition)
                {
                    throw new InvalidOperationException(
                        $"语法错误: 第{_tokenBuffer.Current.Line}行，第{_tokenBuffer.Current.Column}列，无法在条件分支内前进");
                }
            }

            return content;
        }

        /// <summary>
        /// 解析命令
        /// </summary>
        private CommandNode ParseCommand()
        {
            int line = _tokenBuffer.Current.Line;
            int column = _tokenBuffer.Current.Column;

            _tokenBuffer.Consume(TokenType.COMMAND_START);

            var commandType = _tokenBuffer.Current.Type;
            _tokenBuffer.Advance();

            CommandNode result = commandType switch
            {
                TokenType.SET or TokenType.ADD or TokenType.SUB
                    or TokenType.MUL or TokenType.DIV or TokenType.MOD =>
                    ParseVarOperation(commandType.ToString().ToLower(), line, column),

                TokenType.WAIT => ParseWaitCommand(line, column),
                TokenType.VAR => ParseVarDeclaration(line, column),
                TokenType.JUMP => ParseJumpCommand(line, column),
                TokenType.IDENTIFIER => ParseCallCommand(line, column),

                _ => throw new InvalidOperationException(
                    $"语法错误: 第{line}行，第{column}列，未知命令 {commandType}")
            };

            _tokenBuffer.Consume(TokenType.COMMAND_END);
            return result;
        }

        /// <summary>
        /// 解析文本
        /// </summary>
        private List<TextSegmentNode> ParseText()
        {
            var segments = new List<TextSegmentNode>();
            var textBuilder = new System.Text.StringBuilder();

            int line = _tokenBuffer.Current.Line;
            int column = _tokenBuffer.Current.Column;

            while (!IsTextTerminator(_tokenBuffer.Current.Type))
            {
                if (_tokenBuffer.Current.Type == TokenType.LEFT_BRACE)
                {
                    if (textBuilder.Length > 0)
                    {
                        var text = textBuilder.ToString().TrimEnd();
                        if (!string.IsNullOrEmpty(text))
                            segments.Add(new TextNode(text, line, column));
                        textBuilder.Clear();
                    }

                    var (interpolation, _) = ParseInterpolation();
                    segments.Add(interpolation);
                }
                else
                {
                    textBuilder.Append(_tokenBuffer.Current.Value);
                    _tokenBuffer.Advance();
                }
            }

            if (textBuilder.Length > 0)
            {
                var text = textBuilder.ToString().TrimEnd();
                if (!string.IsNullOrEmpty(text))
                    segments.Add(new TextNode(text, line, column));
            }

            return segments;
        }

        /// <summary>
        /// 解析插值
        /// </summary>
        private (InterpolationNode node, int tokensConsumed) ParseInterpolation()
        {
            int line = _tokenBuffer.Current.Line;
            int column = _tokenBuffer.Current.Column;

            _tokenBuffer.Consume(TokenType.LEFT_BRACE);
            var (expr, tokensConsumed) = _expressionParser.ParseExpression();
            _tokenBuffer.Consume(TokenType.RIGHT_BRACE);

            return (new InterpolationNode(expr, line, column), tokensConsumed + 2);
        }

        /// <summary>
        /// 解析标签
        /// </summary>
        private List<string> ParseTags()
        {
            var tags = new List<string>();

            while (_tokenBuffer.Check(TokenType.HASH))
            {
                _tokenBuffer.Advance();
                var tagBuilder = new System.Text.StringBuilder();

                while (!_tokenBuffer.Check(TokenType.NEWLINE) &&
                       !_tokenBuffer.Check(TokenType.HASH) &&
                       !_tokenBuffer.IsAtEnd)
                {
                    tagBuilder.Append(_tokenBuffer.Current.Value);
                    _tokenBuffer.Advance();
                }

                var tag = tagBuilder.ToString().Trim();
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

            if (_tokenBuffer.Check(TokenType.NEWLINE) &&
                _tokenBuffer.Peek().Type == TokenType.INDENT)
            {
                _tokenBuffer.Advance(); // consume NEWLINE
                _tokenBuffer.Advance(); // consume INDENT

                _context.EnterNesting();

                while (!_tokenBuffer.Check(TokenType.DEDENT) &&
                       !_tokenBuffer.IsAtEnd &&
                       !IsCollectionEnd())
                {
                    if (_tokenBuffer.Check(TokenType.NEWLINE))
                    {
                        _tokenBuffer.Advance();
                        continue;
                    }

                    var node = ParseContentNode();
                    if (node != null && !IsEmptyContentNode(node))
                    {
                        content.Add(node);
                    }
                }

                if (_tokenBuffer.Check(TokenType.DEDENT))
                {
                    _tokenBuffer.Advance();
                }

                _context.ExitNesting();
            }

            return content;
        }
        #endregion

        #region 命令解析辅助方法
        private VarCommandNode ParseVarOperation(string operation, int line, int column)
        {
            string variable = _tokenBuffer.Current.Value;
            _tokenBuffer.Advance();

            if (_tokenBuffer.Current.Type == TokenType.ASSIGN)
            {
                _tokenBuffer.Advance();
            }

            var (value, _) = _expressionParser.ParseExpression();

            return new VarCommandNode(variable, value, operation, line, column);
        }

        private WaitCommandNode ParseWaitCommand(int line, int column)
        {
            var (duration, _) = _expressionParser.ParseExpression();
            return new WaitCommandNode(duration, line, column);
        }

        private VarCommandNode ParseVarDeclaration(int line, int column)
        {
            string varName = _tokenBuffer.Current.Value;
            _tokenBuffer.Advance();

            if (_tokenBuffer.Current.Type == TokenType.ASSIGN)
            {
                _tokenBuffer.Advance();
            }

            var (initialValue, _) = _expressionParser.ParseExpression();

            return new VarCommandNode(varName, initialValue, "var", line, column);
        }

        private JumpCommandNode ParseJumpCommand(int line, int column)
        {
            string targetNode = _tokenBuffer.Current.Value;
            _tokenBuffer.Advance();
            return new JumpCommandNode(targetNode, line, column);
        }

        private CallCommandNode ParseCallCommand(int line, int column)
        {
            _tokenBuffer.GoBack(); // 回退到IDENTIFIER
            var (expression, _) = _expressionParser.ParseExpression();

            if (expression is CallExpressionNode callExpr)
            {
                return new CallCommandNode(callExpr, line, column);
            }

            throw new InvalidOperationException(
                $"语法错误: 第{line}行，第{column}列，命令中的表达式必须是函数调用");
        }
        #endregion

        #region 辅助方法
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsNextCommand(TokenType commandType)
        {
            return _tokenBuffer.Peek().Type == commandType;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsTextTerminator(TokenType type)
        {
            return type == TokenType.NEWLINE || type == TokenType.COMMAND_START ||
                   type == TokenType.HASH || type == TokenType.QUOTE ||
                   type == TokenType.NODE_START || type == TokenType.NODE_END ||
                   type == TokenType.EOF;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsConditionalEnd(TokenType[] endTokens)
        {
            if (!_tokenBuffer.Check(TokenType.COMMAND_START)) return false;

            foreach (var endToken in endTokens)
            {
                if (IsNextCommand(endToken))
                {
                    return true;
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsCollectionEnd()
        {
            return _tokenBuffer.Check(TokenType.NODE_END) || _tokenBuffer.Check(TokenType.NODE_START);
        }

        private bool IsEmptyContentNode(ContentNode node)
        {
            return node switch
            {
                DialogueNode dialogue => IsEmptyDialogue(dialogue),
                ChoiceNode choice => IsEmptyChoice(choice),
                ConditionNode condition => IsEmptyCondition(condition),
                _ => false
            };
        }

        private bool IsEmptyDialogue(DialogueNode dialogue)
        {
            if (dialogue.Text.Count == 0) return true;

            foreach (var segment in dialogue.Text)
            {
                if (segment is TextNode textNode && !string.IsNullOrWhiteSpace(textNode.Text))
                    return false;
            }
            return true;
        }

        private bool IsEmptyChoice(ChoiceNode choice)
        {
            bool emptyText = choice.Text.Count == 0;
            if (!emptyText)
            {
                emptyText = true;
                foreach (var segment in choice.Text)
                {
                    if (segment is TextNode textNode && !string.IsNullOrWhiteSpace(textNode.Text))
                    {
                        emptyText = false;
                        break;
                    }
                }
            }

            return emptyText && (choice.Content == null || choice.Content.Count == 0);
        }

        private bool IsEmptyCondition(ConditionNode condition)
        {
            return (condition.ThenContent == null || condition.ThenContent.Count == 0) &&
                   (condition.ElifContents == null || condition.ElifContents.Count == 0) &&
                   (condition.ElseContent == null || condition.ElseContent.Count == 0);
        }

        private string GetMetadataValue(Dictionary<string, string> metadata, string key, string defaultValue)
        {
            return metadata != null && metadata.TryGetValue(key, out var value) ? value : defaultValue;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(Parser));
        }
        #endregion

        #region IDisposable 实现
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (_nodeCache is IDisposable disposableCache)
                        disposableCache.Dispose();

                    _context?.Dispose();
                }
                _disposed = true;
            }
        }
        #endregion
    }
}
