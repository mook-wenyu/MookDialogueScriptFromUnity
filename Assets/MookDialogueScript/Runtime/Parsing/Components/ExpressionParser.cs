using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MookDialogueScript.Parsing
{
    /// <summary>
    /// 表达式解析器组件
    /// 专门负责表达式的解析和优化
    /// 支持运算符优先级、函数调用、成员访问等
    /// </summary>
    public class ExpressionParser : IExpressionParser
    {
        #region 字段
        private readonly INodeCache _nodeCache;
        private readonly ITokenBuffer _tokenBuffer;
        
        // 字符串构建器池（线程本地）
        [ThreadStatic]
        private static System.Text.StringBuilder _cachedStringBuilder;
        #endregion

        #region 构造函数
        /// <summary>
        /// 创建表达式解析器
        /// </summary>
        public ExpressionParser(INodeCache nodeCache, ITokenBuffer tokenBuffer)
        {
            _nodeCache = nodeCache ?? throw new ArgumentNullException(nameof(nodeCache));
            _tokenBuffer = tokenBuffer ?? throw new ArgumentNullException(nameof(tokenBuffer));
        }
        #endregion

        #region IExpressionParser 实现
        /// <summary>
        /// 解析表达式
        /// </summary>
        public (ExpressionNode expression, int tokensConsumed) ParseExpression(
            List<Token> tokens, int startIndex, int endIndex = -1)
        {
            return ParseExpressionWithPrecedence(tokens, startIndex, 0, endIndex);
        }

        /// <summary>
        /// 解析带优先级的表达式
        /// </summary>
        public (ExpressionNode expression, int tokensConsumed) ParseExpressionWithPrecedence(
            List<Token> tokens, int startIndex, int minPrecedence, int endIndex = -1)
        {
            if (tokens == null || startIndex >= tokens.Count)
                throw new ArgumentException("Invalid tokens or start index");

            var originalPosition = _tokenBuffer.Position;
            _tokenBuffer.Seek(startIndex);
            
            try
            {
                var result = ParseExpressionWithPrecedenceInternal(minPrecedence);
                var tokensConsumed = _tokenBuffer.Position - startIndex;
                return (result, tokensConsumed);
            }
            finally
            {
                _tokenBuffer.Seek(originalPosition);
            }
        }

        /// <summary>
        /// 解析主表达式
        /// </summary>
        public (ExpressionNode expression, int tokensConsumed) ParsePrimary(List<Token> tokens, int startIndex)
        {
            if (tokens == null || startIndex >= tokens.Count)
                throw new ArgumentException("Invalid tokens or start index");

            var originalPosition = _tokenBuffer.Position;
            _tokenBuffer.Seek(startIndex);
            
            try
            {
                var result = ParsePrimaryInternal();
                var tokensConsumed = _tokenBuffer.Position - startIndex;
                return (result, tokensConsumed);
            }
            finally
            {
                _tokenBuffer.Seek(originalPosition);
            }
        }

        /// <summary>
        /// 解析后缀链
        /// </summary>
        public (ExpressionNode expression, int tokensConsumed) ParsePostfixChain(
            ExpressionNode baseExpr, List<Token> tokens, int startIndex, int endIndex = -1)
        {
            if (baseExpr == null) throw new ArgumentNullException(nameof(baseExpr));
            if (tokens == null || startIndex >= tokens.Count)
                throw new ArgumentException("Invalid tokens or start index");

            var originalPosition = _tokenBuffer.Position;
            _tokenBuffer.Seek(startIndex);
            
            try
            {
                var result = ParsePostfixChainInternal(baseExpr);
                var tokensConsumed = _tokenBuffer.Position - startIndex;
                return (result, tokensConsumed);
            }
            finally
            {
                _tokenBuffer.Seek(originalPosition);
            }
        }
        #endregion

        #region 私有解析方法
        /// <summary>
        /// 解析带优先级的表达式（内部实现）
        /// </summary>
        private ExpressionNode ParseExpressionWithPrecedenceInternal(int minPrecedence)
        {
            // 解析左操作数
            var left = ParseExpressionTermInternal();

            // 处理二元运算符
            while (true)
            {
                if (!TryGetBinaryPrecedence(_tokenBuffer.Current.Type, out int precedence) 
                    || precedence < minPrecedence)
                {
                    break;
                }

                var op = _tokenBuffer.Current.Value;
                int line = _tokenBuffer.Current.Line;
                int column = _tokenBuffer.Current.Column;
                _tokenBuffer.Advance();

                // 解析右操作数
                var right = ParseExpressionWithPrecedenceInternal(precedence + 1);

                // 创建二元运算符节点
                left = new BinaryOpNode(left, op, right, line, column);
            }

            return left;
        }

        /// <summary>
        /// 解析表达式项（一元运算或基本表达式）
        /// </summary>
        private ExpressionNode ParseExpressionTermInternal()
        {
            // 检查是否是一元运算符
            if (IsUnaryOperator(_tokenBuffer.Current.Type))
            {
                string op = _tokenBuffer.Current.Value;
                int line = _tokenBuffer.Current.Line;
                int column = _tokenBuffer.Current.Column;
                _tokenBuffer.Advance();

                var operand = ParseExpressionTermInternal();
                return new UnaryOpNode(op, operand, line, column);
            }

            // 解析基本表达式并应用后缀链
            var baseExpr = ParsePrimaryInternal();
            return ParsePostfixChainInternal(baseExpr);
        }

        /// <summary>
        /// 解析主表达式（内部实现）
        /// </summary>
        private ExpressionNode ParsePrimaryInternal()
        {
            var token = _tokenBuffer.Current;
            
            switch (token.Type)
            {
                case TokenType.NUMBER:
                    _tokenBuffer.Advance();
                    return _nodeCache.GetOrCreateNumberNode(
                        double.Parse(token.Value), token.Line, token.Column);

                case TokenType.QUOTE:
                    return ParseStringInternal(token);

                case TokenType.TRUE:
                    _tokenBuffer.Advance();
                    return _nodeCache.GetOrCreateBooleanNode(true, token.Line, token.Column);

                case TokenType.FALSE:
                    _tokenBuffer.Advance();
                    return _nodeCache.GetOrCreateBooleanNode(false, token.Line, token.Column);

                case TokenType.VARIABLE:
                    _tokenBuffer.Advance();
                    return _nodeCache.GetOrCreateVariableNode(token.Value, token.Line, token.Column);

                case TokenType.IDENTIFIER:
                    _tokenBuffer.Advance();
                    return _nodeCache.GetOrCreateIdentifierNode(token.Value, token.Line, token.Column);

                case TokenType.LEFT_PAREN:
                    _tokenBuffer.Advance();
                    var expr = ParseExpressionWithPrecedenceInternal(0);
                    _tokenBuffer.Consume(TokenType.RIGHT_PAREN);
                    return expr;

                default:
                    throw new InvalidOperationException(
                        $"语法错误: 第{token.Line}行，第{token.Column}列，意外的符号 {token.Type}");
            }
        }

        /// <summary>
        /// 解析后缀链（内部实现）
        /// </summary>
        private ExpressionNode ParsePostfixChainInternal(ExpressionNode current)
        {
            while (true)
            {
                switch (_tokenBuffer.Current.Type)
                {
                    case TokenType.LEFT_PAREN:
                        // 函数调用
                        current = ParseFunctionCallInternal(current);
                        break;

                    case TokenType.DOT:
                        // 成员访问
                        current = ParseMemberAccessInternal(current);
                        break;

                    case TokenType.LEFT_BRACKET:
                        // 索引访问
                        current = ParseIndexAccessInternal(current);
                        break;

                    default:
                        return current; // 没有更多后缀操作
                }
            }
        }

        /// <summary>
        /// 解析字符串（内部实现）
        /// </summary>
        private ExpressionNode ParseStringInternal(Token startToken)
        {
            var segments = new List<TextSegmentNode>();
            var sb = GetStringBuilder();

            int exprLine = startToken.Line;
            int exprColumn = startToken.Column;

            _tokenBuffer.Consume(TokenType.QUOTE);

            while (_tokenBuffer.Current.Type != TokenType.QUOTE
                   && _tokenBuffer.Current.Type != TokenType.NEWLINE
                   && !_tokenBuffer.IsAtEnd)
            {
                if (_tokenBuffer.Current.Type == TokenType.LEFT_BRACE)
                {
                    if (sb.Length > 0)
                    {
                        TrimEndStringBuilder(sb);
                        if (sb.Length > 0)
                            segments.Add(new TextNode(sb.ToString(), _tokenBuffer.Current.Line, _tokenBuffer.Current.Column));
                        sb.Clear();
                    }
                    segments.Add(ParseInterpolationInternal());
                }
                else
                {
                    sb.Append(_tokenBuffer.Current.Value);
                    _tokenBuffer.Advance();
                }
            }

            if (sb.Length > 0)
            {
                TrimEndStringBuilder(sb);
                if (sb.Length > 0)
                    segments.Add(new TextNode(sb.ToString(), _tokenBuffer.Current.Line, _tokenBuffer.Current.Column));
            }

            _tokenBuffer.Consume(TokenType.QUOTE);
            ReturnStringBuilder(sb);

            return new StringInterpolationExpressionNode(segments, exprLine, exprColumn);
        }

        /// <summary>
        /// 解析插值表达式
        /// </summary>
        private InterpolationNode ParseInterpolationInternal()
        {
            int line = _tokenBuffer.Current.Line;
            int column = _tokenBuffer.Current.Column;
            
            _tokenBuffer.Consume(TokenType.LEFT_BRACE);
            var expr = ParseExpressionWithPrecedenceInternal(0);
            _tokenBuffer.Consume(TokenType.RIGHT_BRACE);
            
            return new InterpolationNode(expr, line, column);
        }

        /// <summary>
        /// 解析函数调用
        /// </summary>
        private ExpressionNode ParseFunctionCallInternal(ExpressionNode function)
        {
            var line = _tokenBuffer.Current.Line;
            var column = _tokenBuffer.Current.Column;
            
            _tokenBuffer.Consume(TokenType.LEFT_PAREN);
            
            var parameters = new List<ExpressionNode>();
            if (!_tokenBuffer.Check(TokenType.RIGHT_PAREN))
            {
                parameters.Add(ParseExpressionWithPrecedenceInternal(0));
                while (_tokenBuffer.Match(TokenType.COMMA))
                {
                    parameters.Add(ParseExpressionWithPrecedenceInternal(0));
                }
            }
            
            _tokenBuffer.Consume(TokenType.RIGHT_PAREN);
            
            return new CallExpressionNode(function, parameters, line, column);
        }

        /// <summary>
        /// 解析成员访问
        /// </summary>
        private ExpressionNode ParseMemberAccessInternal(ExpressionNode obj)
        {
            var line = _tokenBuffer.Current.Line;
            var column = _tokenBuffer.Current.Column;
            
            _tokenBuffer.Consume(TokenType.DOT);
            
            if (_tokenBuffer.Current.Type != TokenType.IDENTIFIER)
            {
                throw new InvalidOperationException(
                    $"语法错误: 第{_tokenBuffer.Current.Line}行，第{_tokenBuffer.Current.Column}列，期望标识符");
            }
            
            var memberName = _tokenBuffer.Current.Value;
            _tokenBuffer.Advance();
            
            return new MemberAccessNode(obj, memberName, line, column);
        }

        /// <summary>
        /// 解析索引访问
        /// </summary>
        private ExpressionNode ParseIndexAccessInternal(ExpressionNode obj)
        {
            var line = _tokenBuffer.Current.Line;
            var column = _tokenBuffer.Current.Column;
            
            _tokenBuffer.Consume(TokenType.LEFT_BRACKET);
            var indexExpr = ParseExpressionWithPrecedenceInternal(0);
            _tokenBuffer.Consume(TokenType.RIGHT_BRACKET);
            
            return new IndexAccessNode(obj, indexExpr, line, column);
        }
        #endregion

        #region 辅助方法
        /// <summary>
        /// 获取二元运算符优先级
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryGetBinaryPrecedence(TokenType type, out int precedence)
        {
            switch (type)
            {
                case TokenType.OR:
                    precedence = 1;
                    return true;
                case TokenType.AND:
                case TokenType.XOR:
                    precedence = 2;
                    return true;
                case TokenType.EQUALS:
                case TokenType.NOT_EQUALS:
                case TokenType.GREATER:
                case TokenType.LESS:
                case TokenType.GREATER_EQUALS:
                case TokenType.LESS_EQUALS:
                    precedence = 3;
                    return true;
                case TokenType.PLUS:
                case TokenType.MINUS:
                    precedence = 4;
                    return true;
                case TokenType.MULTIPLY:
                case TokenType.DIVIDE:
                case TokenType.MODULO:
                    precedence = 5;
                    return true;
                default:
                    precedence = 0;
                    return false;
            }
        }

        /// <summary>
        /// 检查是否为一元运算符
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsUnaryOperator(TokenType type)
        {
            return type == TokenType.NOT || type == TokenType.MINUS;
        }

        /// <summary>
        /// 获取StringBuilder实例
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static System.Text.StringBuilder GetStringBuilder()
        {
            var sb = _cachedStringBuilder;
            if (sb != null)
            {
                _cachedStringBuilder = null;
                sb.Clear();
                return sb;
            }
            return new System.Text.StringBuilder();
        }

        /// <summary>
        /// 归还StringBuilder实例
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ReturnStringBuilder(System.Text.StringBuilder sb)
        {
            if (sb != null && sb.Capacity <= 1024 * 8)
            {
                _cachedStringBuilder = sb;
            }
        }

        /// <summary>
        /// 去除StringBuilder末尾空白
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void TrimEndStringBuilder(System.Text.StringBuilder sb)
        {
            if (sb.Length == 0) return;

            int end = sb.Length - 1;
            while (end >= 0 && (sb[end] == ' ' || sb[end] == '\t'))
            {
                end--;
            }

            sb.Length = end + 1;
        }
        #endregion
    }
}