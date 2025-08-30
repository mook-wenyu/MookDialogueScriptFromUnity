using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MookDialogueScript.Lexers
{
    /// <summary>
    /// 标识符和关键字Token处理器
    /// 设计原则：单一职责 - 专注于标识符、关键字和变量的识别
    /// </summary>
    public class IdentifierTokenizer : ITokenizer
    {
        public int Priority => 500;
        public string Description => "标识符和关键字Token处理器";
        public TokenType[] SupportedTokenTypes => new[]
        {
            TokenType.IDENTIFIER, TokenType.VARIABLE,
            TokenType.IF, TokenType.ELIF, TokenType.ELSE, TokenType.ENDIF,
            TokenType.TRUE, TokenType.FALSE, TokenType.VAR, TokenType.SET,
            TokenType.ADD, TokenType.SUB, TokenType.MUL, TokenType.DIV, TokenType.MOD,
            TokenType.JUMP, TokenType.WAIT, TokenType.EQUALS, TokenType.NOT_EQUALS,
            TokenType.GREATER, TokenType.LESS, TokenType.GREATER_EQUALS, TokenType.LESS_EQUALS,
            TokenType.AND, TokenType.OR, TokenType.NOT, TokenType.XOR
        };

        // 保持原有的关键字映射，使用静态只读字典优化性能
        private static readonly Dictionary<string, TokenType> _keywords = new(StringComparer.OrdinalIgnoreCase)
        {
            {"if", TokenType.IF},
            {"elif", TokenType.ELIF},
            {"else", TokenType.ELSE},
            {"endif", TokenType.ENDIF},
            {"true", TokenType.TRUE},
            {"false", TokenType.FALSE},
            {"var", TokenType.VAR},
            {"set", TokenType.SET},
            {"add", TokenType.ADD},
            {"sub", TokenType.SUB},
            {"mul", TokenType.MUL},
            {"div", TokenType.DIV},
            {"mod", TokenType.MOD},
            {"jump", TokenType.JUMP},
            {"wait", TokenType.WAIT},
            {"eq", TokenType.EQUALS},
            {"is", TokenType.EQUALS},
            {"neq", TokenType.NOT_EQUALS},
            {"gt", TokenType.GREATER},
            {"lt", TokenType.LESS},
            {"gte", TokenType.GREATER_EQUALS},
            {"lte", TokenType.LESS_EQUALS},
            {"and", TokenType.AND},
            {"or", TokenType.OR},
            {"not", TokenType.NOT},
            {"xor", TokenType.XOR},
        };

        /// <summary>
        /// 快速判断是否为标识符或变量起始
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CanHandle(CharStream stream, LexerState state)
        {
            // 处理变量（$ 开头）
            if (stream.CurrentChar == '$' && state.IsInNodeContent)
                return true;

            // 处理标识符（字母或下划线开头）
            return !stream.IsEOFMark()
                   && (char.IsLetter(stream.CurrentChar) || stream.CurrentChar == '_');
        }

        /// <summary>
        /// 处理标识符、关键字和变量Token
        /// </summary>
        public Token TryTokenize(CharStream stream, LexerState state)
        {
            // 处理变量
            if (stream.CurrentChar == '$' && state.IsInNodeContent)
            {
                return HandleVariable(stream);
            }

            // 处理标识符和关键字
            if (CharClassifier.IsIdentifierStart(stream.CurrentChar))
            {
                return HandleIdentifierOrKeyword(stream);
            }

            return null;
        }

        /// <summary>
        /// 处理变量Token（保持原有逻辑）
        /// </summary>
        private Token HandleVariable(CharStream stream)
        {
            var start = SourceLocation.FromStream(stream);
            stream.Advance(); // 跳过$符号
            int startPosition = stream.Position;

            if (!stream.IsEOFMark() && CharClassifier.IsIdentifierStart(stream.CurrentChar))
            {
                stream.Advance();

                while (!stream.IsEOFMark() && CharClassifier.IsIdentifierPart(stream.CurrentChar))
                {
                    stream.Advance();
                }
            }

            string variableName = stream.GetRange(startPosition, stream.Position);
            return TokenFactory.VariableToken(variableName, start.Line, start.Column);
        }

        /// <summary>
        /// 处理标识符和关键字Token（保持原有优化）
        /// </summary>
        private Token HandleIdentifierOrKeyword(CharStream stream)
        {
            var start = SourceLocation.FromStream(stream);

            // 快速处理标识符字符
            stream.Advance(); // 跳过第一个字符（已验证）

            while (!stream.IsEOFMark() && CharClassifier.IsIdentifierPart(stream.CurrentChar))
            {
                stream.Advance();
            }

            string text = stream.GetRange(start.Position, stream.Position);

            // 检查是否是关键字
            if (_keywords.TryGetValue(text, out var keywordType))
            {
                return TokenFactory.CreateToken(keywordType, text, start.Line, start.Column);
            }

            return TokenFactory.IdentifierToken(text, start.Line, start.Column);
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
