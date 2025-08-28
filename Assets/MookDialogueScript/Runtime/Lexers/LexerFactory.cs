using System.Collections.Generic;
using System.Linq;

namespace MookDialogueScript.Lexers
{
    /// <summary>
    /// 词法分析器工厂类，负责创建和配置各个组件
    /// 设计原则：依赖注入 - 提供组件的统一创建和配置
    /// </summary>
    public static class LexerFactory
    {
        /// <summary>
        /// 创建默认的字符流实现
        /// </summary>
        public static CharacterStream CreateCharacterStream()
        {
            return new CharacterStream();
        }

        /// <summary>
        /// 创建默认的字符分类器实现
        /// </summary>
        public static CharacterClassifier CreateCharacterClassifier()
        {
            return new CharacterClassifier();
        }

        /// <summary>
        /// 创建默认的词法分析器状态实现
        /// </summary>
        public static LexerState CreateLexerState()
        {
            return new LexerState();
        }

        /// <summary>
        /// 创建默认的缩进处理器实现
        /// </summary>
        public static IndentationHandler CreateIndentationHandler()
        {
            return new IndentationHandler();
        }

        /// <summary>
        /// 创建所有Token处理器，优先级从高到低
        /// </summary>
        public static List<ITokenizer> CreateTokenizers()
        {
            var tokenizers = new List<ITokenizer>
            {
                new CommentAndNewlineTokenizer(),
                new NodeMarkerTokenizer(),
                new StringTokenizer(),
                new TextTokenizer(),
                new CommandTokenizer(),
                new NumberTokenizer(),
                new IdentifierTokenizer(),
                new SymbolTokenizer(),
                new TextTokenizer()
            };

            return tokenizers;
        }

        /// <summary>
        /// 创建完整的词法分析器实例
        /// </summary>
        public static Lexer CreateLexer()
        {
            return new Lexer(
                CreateCharacterStream(),
                CreateCharacterClassifier(),
                CreateLexerState(),
                CreateIndentationHandler(),
                CreateTokenizers()
            );
        }

        /// <summary>
        /// 创建自定义配置的词法分析器实例
        /// </summary>
        /// <param name="stream">自定义字符流</param>
        /// <param name="classifier">自定义字符分类器</param>
        /// <param name="state">自定义状态管理器</param>
        /// <param name="indentHandler">自定义缩进处理器</param>
        /// <param name="tokenizers">自定义Token处理器列表</param>
        public static Lexer CreateLexer(
            CharacterStream stream,
            CharacterClassifier classifier,
            LexerState state,
            IndentationHandler indentHandler,
            List<ITokenizer> tokenizers)
        {
            return new Lexer(stream, classifier, state, indentHandler, tokenizers);
        }
    }
}
