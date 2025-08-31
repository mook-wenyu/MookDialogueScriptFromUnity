using System;
using System.Collections.Generic;

namespace MookDialogueScript.Lexers
{
    /// <summary>
    /// 词法分析器主类
    /// 设计原则：组合优于继承 - 通过组合各个专业化组件实现功能
    /// </summary>
    public class Lexer : IDisposable
    {
        // 组合的核心组件
        private readonly CharStream _stream = new();
        private readonly LexerState _state = new();
        private readonly IndentationHandler _indentHandler = new();
        private readonly List<ITokenizer> _tokenizers = CreateTokenizers();
        private readonly List<Token> _tokens = new();

        // 同步锁：保持原有的线程安全保障
        private readonly object _syncLock = new();

        // IDisposable 支持
        private volatile bool _disposed;

        /// <summary>
        /// 解析源代码字符串
        /// 重置所有组件状态，准备词法分析
        /// 获取所有Token
        /// </summary>
        /// <param name="source">源代码字符串</param>
        /// <returns>Token列表的独立副本，避免外部引用受Reset影响</returns>
        public List<Token> Tokenize(string source)
        {
            ThrowIfDisposed();

            lock (_syncLock)
            {
                // 重置所有组件
                _state.Clear();
                _indentHandler.Init();
                _tokens.Clear();
                _stream.Init(source);

                Token token;
                do
                {
                    token = GetNextToken();
                    _tokens.Add(token);
                } while (token.Type != TokenType.EOF);

                // 返回独立副本，防止外部代码持有内部_tokens引用
                return new List<Token>(_tokens);
            }
        }

        /// <summary>
        /// 创建所有Token处理器，优先级从高到低
        /// </summary>
        private static List<ITokenizer> CreateTokenizers()
        {
            var tokenizers = new List<ITokenizer>
            {
                // 注释和换行处理
                new CommentAndNewlineTokenizer(),

                // 节点标记处理
                new NodeMarkerTokenizer(),

                // 字符串和文本处理
                new StringTokenizer(),

                // 文本处理
                new TextTokenizer(),

                // 命令处理
                new CommandTokenizer(),

                // 数字和标识符
                new NumberTokenizer(),
                new IdentifierTokenizer(),

                // 符号处理
                new SymbolTokenizer(),

                // 最后兜底：文本处理器
                new TextTokenizer()
            };

            return tokenizers;
        }

        /// <summary>
        /// 获取下一个Token的核心逻辑
        /// 重构为组件协调模式，但保持完全相同的行为
        /// </summary>
        private Token GetNextToken()
        {
            ThrowIfDisposed();

            // 1. 处理pending dedent
            if (_indentHandler.HasPendingDedents)
            {
                var pendingToken = _indentHandler.GetPendingDedentToken(_stream.Line, _stream.Column);
                if (pendingToken != null) return pendingToken;
            }

            // 2. 处理EOF和缩进清理
            if (_stream.IsAtEnd)
            {
                var eofIndentToken = _indentHandler.HandleEOFIndentation(_stream.Line, _stream.Column);
                if (eofIndentToken != null) return eofIndentToken;

                return TokenFactory.EOFToken(_stream.Line, _stream.Column);
            }

            // 3. 主要Token处理循环
            while (!_stream.IsAtEnd)
            {
                // 处理行首缩进（仅在节点内容中）
                if (_stream.Column == 1 && _state.IsInNodeContent)
                {
                    // 预判空行和注释行，交由CommentAndNewlineTokenizer处理
                    if (IsEmptyOrCommentLine())
                    {
                        // 继续执行后续的tokenizer处理
                    }
                    else
                    {
                        // 内容行才进行缩进处理
                        var indentToken = _indentHandler.HandleIndentation(_stream, _state);
                        if (indentToken != null) return indentToken;

                        // 消费前导空白，使后续Token处理从非空白字符开始
                        ConsumeIndentationWhitespace();
                    }
                }

                // 跳过空白字符（在特定模式下）
                if (ShouldSkipWhitespace())
                {
                    SkipWhitespace();
                }

                // 按优先级尝试各个tokenizer
                foreach (var tokenizer in _tokenizers)
                {
                    if (!tokenizer.CanHandle(_stream, _state)) continue;

                    var token = tokenizer.TryTokenize(_stream, _state);
                    if (token != null) return token;
                }

                // 如果所有tokenizer都无法处理，前进一个字符避免死循环
                // 这种情况理论上不应该发生，因为TextTokenizer应该能处理所有情况
                if (!_stream.IsAtEnd)
                {
                    MLogger.Warning($"词法警告: 第{_stream.Line}行，第{_stream.Column}列，无法识别的字符 '{_stream.CurrentChar}'");
                    _stream.Advance();
                }
            }

            // 文件结束
            return TokenFactory.EOFToken(_stream.Line, _stream.Column);
        }

        /// <summary>
        /// 判断是否应该跳过空白字符
        /// </summary>
        private bool ShouldSkipWhitespace()
        {
            return !_state.IsInNodeContent || _state.IsInCommandMode || _state.IsInInterpolation;
        }

        /// <summary>
        /// 跳过空白字符（非换行符）
        /// </summary>
        private void SkipWhitespace()
        {
            while (!_stream.IsAtEnd && CharClassifier.IsWhitespace(_stream.CurrentChar) &&
                   !_stream.IsNewlineMark())
            {
                _stream.Advance();
            }
        }

        /// <summary>
        /// 消费行首的缩进空白字符
        /// </summary>
        private void ConsumeIndentationWhitespace()
        {
            while (_stream.IsSpaceOrIndentMark())
            {
                _stream.Advance();
            }
        }

        /// <summary>
        /// 预判当前行是否为空行或注释行
        /// </summary>
        private bool IsEmptyOrCommentLine()
        {
            int p = _stream.Position;

            // 跳过前导空白
            char c = _stream.GetCharAt(p);
            while (CharClassifier.IsSpaceOrIndent(c))
            {
                p++;
                c = _stream.GetCharAt(p);
            }

            // 空行或注释行
            bool isCommentLine = c == '/' && _stream.GetCharAt(p + 1) == '/';
            bool isEmptyLine = CharClassifier.IsNewlineOrEOF(c);

            return isCommentLine || isEmptyLine;
        }

        /// <summary>
        /// 释放Lexer实例占用的资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源的核心逻辑
        /// </summary>
        /// <param name="disposing">是否正在释放托管资源</param>
        protected void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 释放托管资源
                    // 清理token列表
                    lock (_syncLock)
                    {
                        _tokens.Clear();
                    }

                    // 如果组件实现了IDisposable，释放它们
                    if (_stream is IDisposable disposableStream)
                        disposableStream.Dispose();
                    if (_state is IDisposable disposableState)
                        disposableState.Dispose();
                    if (_indentHandler is IDisposable disposableIndentHandler)
                        disposableIndentHandler.Dispose();
                    if (_tokenizers != null)
                    {
                        foreach (var tokenizer in _tokenizers)
                        {
                            if (tokenizer is IDisposable disposableTokenizer)
                                disposableTokenizer.Dispose();
                        }
                    }
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// 检查对象是否已被释放
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(Lexer));
            }
        }

        /// <summary>
        /// 析构函数 - 确保资源被释放
        /// </summary>
        ~Lexer()
        {
            Dispose(false);
        }
    }
}
