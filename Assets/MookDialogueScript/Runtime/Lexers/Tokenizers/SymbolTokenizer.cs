using System.Runtime.CompilerServices;

namespace MookDialogueScript.Lexers
{
    /// <summary>
    /// 符号和操作符Token处理器
    /// 设计原则：单一职责 - 专注于符号和操作符的识别
    /// </summary>
    public class SymbolTokenizer : ITokenizer
    {
        public string Description => "符号和操作符Token处理器";
        public TokenType[] SupportedTokenTypes => new[]
        {
            TokenType.LEFT_PAREN, TokenType.RIGHT_PAREN,
            TokenType.LEFT_BRACE, TokenType.RIGHT_BRACE,
            TokenType.LEFT_BRACKET, TokenType.RIGHT_BRACKET,
            TokenType.DOT, TokenType.COMMA, TokenType.COLON, TokenType.HASH,
            TokenType.PLUS, TokenType.MINUS, TokenType.MULTIPLY, TokenType.DIVIDE, TokenType.MODULO,
            TokenType.ASSIGN, TokenType.EQUALS, TokenType.NOT_EQUALS,
            TokenType.GREATER, TokenType.GREATER_EQUALS, TokenType.LESS, TokenType.LESS_EQUALS,
            TokenType.AND, TokenType.OR, TokenType.NOT, TokenType.ARROW,
            TokenType.METADATA_SEPARATOR
        };

        /// <summary>
        /// 快速判断是否为符号或操作符起始
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CanHandle(CharacterStream stream, LexerState state, CharacterClassifier classifier)
        {
            char c = stream.CurrentChar;
            return c is '(' or ')' or '{' or '}' or '[' or ']' or '#' or '.' or ',' or ':' 
                     or '+' or '-' or '*' or '/' or '%' or '=' or '!' or '>' or '<' or '&' or '|';
        }

        /// <summary>
        /// 处理符号和操作符Token
        /// </summary>
        public Token TryTokenize(CharacterStream stream, LexerState state, CharacterClassifier classifier)
        {
            char currentChar = stream.CurrentChar;
            int startLine = stream.Line;
            int startColumn = stream.Column;

            // 括号和分隔符
            switch (currentChar)
            {
                case '(':
                    stream.Advance();
                    return TokenFactory.CreateToken(TokenType.LEFT_PAREN, "(", startLine, startColumn);

                case ')':
                    stream.Advance();
                    return TokenFactory.CreateToken(TokenType.RIGHT_PAREN, ")", startLine, startColumn);

                case '{':
                    stream.Advance();
                    state.EnterInterpolation();
                    return TokenFactory.CreateToken(TokenType.LEFT_BRACE, "{", startLine, startColumn);

                case '}':
                    stream.Advance();
                    state.ExitInterpolation();
                    return TokenFactory.CreateToken(TokenType.RIGHT_BRACE, "}", startLine, startColumn);

                case '[':
                    stream.Advance();
                    return TokenFactory.CreateToken(TokenType.LEFT_BRACKET, "[", startLine, startColumn);

                case ']':
                    stream.Advance();
                    return TokenFactory.CreateToken(TokenType.RIGHT_BRACKET, "]", startLine, startColumn);

                case '#':
                    stream.Advance();
                    return TokenFactory.CreateToken(TokenType.HASH, "#", startLine, startColumn);

                case '.':
                    stream.Advance();
                    return TokenFactory.CreateToken(TokenType.DOT, ".", startLine, startColumn);

                case ',':
                    stream.Advance();
                    return TokenFactory.CreateToken(TokenType.COMMA, ",", startLine, startColumn);

                case ':':
                    stream.Advance();
                    // 根据状态决定Token类型
                    if (!state.IsInNodeContent)
                    {
                        return TokenFactory.CreateToken(TokenType.METADATA_SEPARATOR, ":", startLine, startColumn);
                    }
                    return TokenFactory.CreateToken(TokenType.COLON, ":", startLine, startColumn);

                // 算术操作符
                case '+':
                    stream.Advance();
                    return TokenFactory.CreateToken(TokenType.PLUS, "+", startLine, startColumn);

                case '-':
                    stream.Advance();
                    if (stream.CurrentChar == '>')
                    {
                        stream.Advance();
                        return TokenFactory.CreateToken(TokenType.ARROW, "->", startLine, startColumn);
                    }
                    return TokenFactory.CreateToken(TokenType.MINUS, "-", startLine, startColumn);

                case '*':
                    stream.Advance();
                    return TokenFactory.CreateToken(TokenType.MULTIPLY, "*", startLine, startColumn);

                case '/':
                    stream.Advance();
                    return TokenFactory.CreateToken(TokenType.DIVIDE, "/", startLine, startColumn);

                case '%':
                    stream.Advance();
                    return TokenFactory.CreateToken(TokenType.MODULO, "%", startLine, startColumn);

                // 比较和逻辑操作符
                case '=':
                    stream.Advance();
                    if (stream.CurrentChar == '=')
                    {
                        stream.Advance();
                        return TokenFactory.CreateToken(TokenType.EQUALS, "==", startLine, startColumn);
                    }
                    return TokenFactory.CreateToken(TokenType.ASSIGN, "=", startLine, startColumn);

                case '!':
                    stream.Advance();
                    if (stream.CurrentChar == '=')
                    {
                        stream.Advance();
                        return TokenFactory.CreateToken(TokenType.NOT_EQUALS, "!=", startLine, startColumn);
                    }
                    return TokenFactory.CreateToken(TokenType.NOT, "!", startLine, startColumn);

                case '>':
                    stream.Advance();
                    if (stream.CurrentChar == '=')
                    {
                        stream.Advance();
                        return TokenFactory.CreateToken(TokenType.GREATER_EQUALS, ">=", startLine, startColumn);
                    }
                    return TokenFactory.CreateToken(TokenType.GREATER, ">", startLine, startColumn);

                case '<':
                    stream.Advance();
                    if (stream.CurrentChar == '=')
                    {
                        stream.Advance();
                        return TokenFactory.CreateToken(TokenType.LESS_EQUALS, "<=", startLine, startColumn);
                    }
                    return TokenFactory.CreateToken(TokenType.LESS, "<", startLine, startColumn);

                case '&':
                    stream.Advance();
                    if (stream.CurrentChar == '&')
                    {
                        stream.Advance();
                        return TokenFactory.CreateToken(TokenType.AND, "&&", startLine, startColumn);
                    }
                    break; // 单个 & 不是有效Token

                case '|':
                    stream.Advance();
                    if (stream.CurrentChar == '|')
                    {
                        stream.Advance();
                        return TokenFactory.CreateToken(TokenType.OR, "||", startLine, startColumn);
                    }
                    break; // 单个 | 不是有效Token
            }

            return null;
        }
        
        public void Clear()
        {
            
        }
        
        public void Dispose()
        {
            Clear();
        }
    }
}