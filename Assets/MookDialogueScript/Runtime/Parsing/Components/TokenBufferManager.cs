using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MookDialogueScript.Parsing
{
    /// <summary>
    /// Token缓冲区管理器
    /// 专门负责Token的访问、导航和缓冲管理
    /// 提供快照功能用于错误恢复和回溯
    /// </summary>
    public class TokenBufferManager : ITokenBuffer
    {
        #region 字段
        private List<Token> _tokens;
        private int _position;
        private Token _current;
        private readonly Stack<int> _snapshots;
        #endregion

        #region 属性
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
        #endregion

        #region 构造函数
        /// <summary>
        /// 创建Token缓冲区管理器
        /// </summary>
        public TokenBufferManager()
        {
            _snapshots = new Stack<int>();
        }

        /// <summary>
        /// 使用指定Token列表创建缓冲区管理器
        /// </summary>
        public TokenBufferManager(List<Token> tokens) : this()
        {
            Reset(tokens);
        }
        #endregion

        #region ITokenBuffer 实现
        /// <summary>
        /// 重置Token缓冲区
        /// </summary>
        public void Reset(List<Token> tokens)
        {
            _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
            _position = 0;
            _snapshots.Clear();
            
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
                return;
            }
            else
            {
                throw new InvalidOperationException(
                    $"语法错误: 第{_current.Line}行，第{_current.Column}列，期望 {type}，但得到 {_current.Type}");
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
        /// 创建当前位置的快照
        /// </summary>
        public int CreateSnapshot()
        {
            _snapshots.Push(_position);
            return _snapshots.Count - 1; // 返回快照ID
        }

        /// <summary>
        /// 恢复到快照位置
        /// </summary>
        public void RestoreSnapshot(int snapshotId)
        {
            if (_snapshots.Count == 0)
                throw new InvalidOperationException("没有可用的快照");
                
            // 简化实现：恢复到最近的快照
            var savedPosition = _snapshots.Pop();
            Seek(savedPosition);
        }
        #endregion

        #region 私有方法
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
        #endregion

        #region 调试支持
        /// <summary>
        /// 获取当前缓冲区状态信息
        /// </summary>
        public string GetStateInfo()
        {
            return $"Position: {_position}/{Count}, Current: {_current.Type}({_current.Value}), " +
                   $"Line: {_current.Line}, Column: {_current.Column}, Snapshots: {_snapshots.Count}";
        }

        /// <summary>
        /// 获取周围Token的上下文
        /// </summary>
        public List<Token> GetContext(int radius = 5)
        {
            var context = new List<Token>();
            var start = Math.Max(0, _position - radius);
            var end = Math.Min(_tokens.Count - 1, _position + radius);

            for (int i = start; i <= end; i++)
            {
                context.Add(_tokens[i]);
            }

            return context;
        }
        #endregion
    }
}