using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MookDialogueScript.Parsing
{
    /// <summary>
    /// Token缓冲区
    /// 专门负责Token的访问、导航和缓冲管理
    /// 提供快照功能用于错误恢复和回溯
    /// </summary>
    public class TokenBuffer : ITokenBuffer
    {
        private List<Token> _tokens;
        private int _position;
        private Token _current;

        /// <summary>
        /// 当前Token
        /// </summary>
        public Token Current => _current;

        /// <summary>
        /// 当前位置
        /// </summary>
        public int Position => _position;

        /// <summary>
        /// Token总数
        /// </summary>
        public int Count => _tokens?.Count ?? 0;

        /// <summary>
        /// 是否到达结束
        /// </summary>
        public bool IsAtEnd => _position >= (_tokens?.Count ?? 0) || _current.Type == TokenType.EOF;

        /// <summary>
        /// 重置Token缓冲区
        /// </summary>
        public void Init(List<Token> tokens)
        {
            _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
            _position = 0;

            UpdateCurrentToken();
        }

        /// <summary>
        /// 前进到下一个Token
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance()
        {
            if (_position < _tokens.Count - 1)
            {
                _position++;
                UpdateCurrentToken();
            }
        }

        /// <summary>
        /// 后退到上一个Token
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GoBack()
        {
            if (_position > 0)
            {
                _position--;
                UpdateCurrentToken();
            }
        }

        /// <summary>
        /// 跳转到指定位置
        /// </summary>
        public void Seek(int position)
        {
            if (position < 0 || position >= _tokens.Count)
                throw new ArgumentOutOfRangeException(nameof(position));

            _position = position;
            UpdateCurrentToken();
        }

        /// <summary>
        /// 查看指定偏移位置的Token
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Token Peek(int offset = 1)
        {
            var targetPos = _position + offset;
            if (targetPos >= 0 && targetPos < _tokens.Count)
            {
                return _tokens[targetPos];
            }

            // 超出范围返回EOF Token
            var lastToken = _tokens.Count > 0 ? _tokens[^1] : Token.Empty;
            return new Token(TokenType.EOF, string.Empty, lastToken.Line, lastToken.Column);
        }

        /// <summary>
        /// 检查当前Token类型
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Check(TokenType type)
        {
            return _current.Type == type;
        }

        /// <summary>
        /// 匹配并消耗Token
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Match(TokenType type)
        {
            if (Check(type))
            {
                Advance();
                return true;
            }
            return false;
        }

        /// <summary>
        /// 强制消耗指定类型Token
        /// </summary>
        public void Consume(TokenType type)
        {
            if (_current.Type == type)
            {
                Advance();
            }
            else if (_current.Type == TokenType.EOF && type == TokenType.NEWLINE)
            {
                // 文件结束时期望换行符是合法的
            }
            else
            {
                MLogger.Error($"语法错误: 第{_current.Line}行，第{_current.Column}列，期望 {type}，但得到 {_current.Type}");
            }
        }

        /// <summary>
        /// 同步到指定Token类型（错误恢复）
        /// </summary>
        public void SynchronizeTo(params TokenType[] types)
        {
            const int maxAdvance = 50; // 防止无限循环
            int advance = 0;

            while (!IsAtEnd && advance < maxAdvance)
            {
                if (ContainsToken(_current.Type, types))
                {
                    return; // 找到目标Token
                }

                Advance();
                advance++;
            }

            if (advance >= maxAdvance)
            {
                MLogger.Warning($"错误恢复: 第{_current.Line}行，第{_current.Column}列，达到最大前进限制，停止同步");
            }
        }

        /// <summary>
        /// 更新当前Token
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateCurrentToken()
        {
            if (_tokens != null && _position < _tokens.Count)
            {
                _current = _tokens[_position];
            }
            else
            {
                // 生成EOF Token
                var lastToken = _tokens?.Count > 0 ? _tokens[^1] : Token.Empty;
                _current = new Token(TokenType.EOF, string.Empty, lastToken.Line, lastToken.Column);
            }
        }

        /// <summary>
        /// 检查Token类型是否在数组中
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ContainsToken(TokenType target, TokenType[] types)
        {
            foreach (var type in types)
            {
                if (type == target) return true;
            }
            return false;
        }

        public void Dispose()
        {
            _tokens = null;
            _position = 0;
            _current = null;
        }
    }
}
