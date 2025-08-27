namespace MookDialogueScript
{
    public class TokenFactory
    {
        /// <summary>
        /// 创建Token
        /// </summary>
        public static Token CreateToken(TokenType type, string value, int line, int column)
        {
            return new Token(type, value, line, column);
        }
        
        /// <summary>
        /// 创建文本Token
        /// </summary>
        public static Token TextToken(string value, int line, int column)
        {
            return new Token(TokenType.TEXT, value, line, column);
        }
        
        /// <summary>
        /// 创建数字Token
        /// </summary>
        public static Token NumberToken(string value, int line, int column)
        {
            return new Token(TokenType.NUMBER, value, line, column);
        }
        
        /// <summary>
        /// 创建变量Token
        /// </summary>
        public static Token VariableToken(string value, int line, int column)
        {
            return new Token(TokenType.VARIABLE, value, line, column);
        }
        
        /// <summary>
        /// 创建标识符Token
        /// </summary>
        public static Token IdentifierToken(string value, int line, int column)
        {
            return new Token(TokenType.IDENTIFIER, value, line, column);
        }

        /// <summary>
        /// 创建缩进Token
        /// </summary>
        public static Token IndentToken(int line, int column)
        {
            return new Token(TokenType.INDENT, string.Empty, line, column);
        }
        
        /// <summary>
        /// 创建缩进Token
        /// </summary>
        public static Token DedentToken(int line, int column)
        {
            return new Token(TokenType.DEDENT, string.Empty, line, column);
        }
        
        /// <summary>
        /// 创建换行Token
        /// </summary>
        public static Token NewLineToken(int line, int column)
        {
            return new Token(TokenType.NEWLINE, @"\n", line, column);
        }

        /// <summary>
        /// 创建EOF Token
        /// </summary>
        public static Token EOFToken(int line, int column)
        {
            return new Token(TokenType.EOF, string.Empty, line, column);
        }

    }
}
