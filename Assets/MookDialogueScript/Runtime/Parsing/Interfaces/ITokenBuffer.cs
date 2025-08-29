using System.Collections.Generic;

namespace MookDialogueScript.Parsing
{
    /// <summary>
    /// Token缓冲区管理接口
    /// 专门负责Token的访问、导航和缓冲管理
    /// </summary>
    public interface ITokenBuffer
    {
        /// <summary>
        /// 当前Token
        /// </summary>
        Token Current { get; }
        
        /// <summary>
        /// 当前位置
        /// </summary>
        int Position { get; }
        
        /// <summary>
        /// Token总数
        /// </summary>
        int Count { get; }
        
        /// <summary>
        /// 是否到达结束
        /// </summary>
        bool IsAtEnd { get; }
        
        /// <summary>
        /// 重置Token缓冲区
        /// </summary>
        /// <param name="tokens">新的Token列表</param>
        void Reset(List<Token> tokens);
        
        /// <summary>
        /// 前进到下一个Token
        /// </summary>
        void Advance();
        
        /// <summary>
        /// 后退到上一个Token
        /// </summary>
        void GoBack();
        
        /// <summary>
        /// 跳转到指定位置
        /// </summary>
        void Seek(int position);
        
        /// <summary>
        /// 查看指定偏移位置的Token
        /// </summary>
        Token Peek(int offset = 1);
        
        /// <summary>
        /// 检查当前Token类型
        /// </summary>
        bool Check(TokenType type);
        
        /// <summary>
        /// 匹配并消耗Token
        /// </summary>
        bool Match(TokenType type);
        
        /// <summary>
        /// 强制消耗指定类型Token
        /// </summary>
        void Consume(TokenType type);
        
        /// <summary>
        /// 同步到指定Token类型（错误恢复）
        /// </summary>
        void SynchronizeTo(params TokenType[] types);
        
        /// <summary>
        /// 创建当前位置的快照
        /// </summary>
        int CreateSnapshot();
        
        /// <summary>
        /// 恢复到快照位置
        /// </summary>
        void RestoreSnapshot(int snapshot);
    }
}