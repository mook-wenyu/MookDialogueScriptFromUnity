namespace MookDialogueScript
{
    public enum TokenType
    {
        // 特殊标记
        /// <summary>
        /// 文件结束
        /// </summary>
        EOF,
        
        // 符号标记
        /// <summary>
        /// 元数据键值对分隔符 :
        /// </summary>
        METADATA_SEPARATOR,
        /// <summary>
        /// 节点开始标记 三重减号 ---
        /// </summary>
        NODE_START,
        /// <summary>
        /// 节点结束标记 三重等号 ===
        /// </summary>
        NODE_END,
        /// <summary>
        /// 命令开始标记 《《
        /// </summary>
        COMMAND_START,
        /// <summary>
        /// 命令结束标记 >>
        /// </summary>
        COMMAND_END,
        /// <summary>
        /// 选项标记 箭头 ->
        /// </summary>
        ARROW,

        // 标点符号
        /// <summary>
        /// 冒号 :
        /// </summary>
        COLON,
        /// <summary>
        /// 引号 ' "
        /// </summary>
        QUOTE,
        /// <summary>
        /// 左中括号 [
        /// </summary>
        LEFT_BRACKET,
        /// <summary>
        /// 右中括号 ]
        /// </summary>
        RIGHT_BRACKET,
        /// <summary>
        /// 左大括号 {
        /// </summary>
        LEFT_BRACE,
        /// <summary>
        /// 右大括号 }
        /// </summary>
        RIGHT_BRACE,
        /// <summary>
        /// 左括号 (
        /// </summary>
        LEFT_PAREN,
        /// <summary>
        /// 右括号 )
        /// </summary>
        RIGHT_PAREN,
        /// <summary>
        /// 逗号 ,
        /// </summary>
        COMMA,
        /// <summary>
        /// 井号 #
        /// </summary>
        HASH,

        // 运算符
        /// <summary>
        /// 加号 +
        /// </summary>
        PLUS,
        /// <summary>
        /// 减号 -
        /// </summary>
        MINUS,
        /// <summary>
        /// 乘号 *
        /// </summary>
        MULTIPLY,
        /// <summary>
        /// 除号 /
        /// </summary>
        DIVIDE,
        /// <summary>
        /// 取模 %
        /// </summary>
        MODULO,
        /// <summary>
        /// 等于 == 或 eq 或 is
        /// </summary>
        EQUALS,
        /// <summary>
        /// 不等于 != 或 neq
        /// </summary>
        NOT_EQUALS,
        /// <summary>
        /// 大于 > 或 gt
        /// </summary>
        GREATER,
        /// <summary>
        /// 小于 ＜ 或 lt
        /// </summary>
        LESS,
        /// <summary>
        /// 大于等于 >= 或 gte
        /// </summary>
        GREATER_EQUALS,
        /// <summary>
        /// 小于等于 ＜= 或 lte
        /// </summary>
        LESS_EQUALS,
        /// <summary>
        /// 与 && 或 and
        /// </summary>
        AND,
        /// <summary>
        /// 或 || 或 or
        /// </summary>
        OR,
        /// <summary>
        /// 非 ! 或 not
        /// </summary>
        NOT,
        /// <summary>
        /// 异或 ^ 或 xor
        /// </summary>
        XOR,

        // 关键字
        /// <summary>
        /// 如果 if
        /// </summary>
        IF,
        /// <summary>
        /// 否则如果 elif
        /// </summary>
        ELIF,
        /// <summary>
        /// 否则 else
        /// </summary>
        ELSE,
        /// <summary>
        /// 结束if
        /// </summary>
        ENDIF,
        /// <summary>
        /// 真 true
        /// </summary>
        TRUE,
        /// <summary>
        /// 假 false
        /// </summary>
        FALSE,

        // 命令
        /// <summary>
        /// 声明变量
        /// </summary>
        VAR,
        /// <summary>
        /// 设置变量
        /// </summary>
        SET,
        /// <summary>
        /// 加 +
        /// </summary>
        ADD,
        /// <summary>
        /// 减 -
        /// </summary>
        SUB,
        /// <summary>
        /// 乘 *
        /// </summary>
        MUL,
        /// <summary>
        /// 除 /
        /// </summary>
        DIV,
        /// <summary>
        /// 取模 %
        /// </summary>
        MOD,
        /// <summary>
        /// 跳转 jump
        /// </summary>
        JUMP,
        /// <summary>
        /// 等待 wait
        /// </summary>
        WAIT,

        // 标识符和字面量
        /// <summary>
        /// 标识符
        /// </summary>
        IDENTIFIER,
        /// <summary>
        /// 变量，以$开头 如 $name
        /// </summary>
        VARIABLE,
        /// <summary>
        /// 数字
        /// </summary>
        NUMBER,
        /// <summary>
        /// 字符串
        /// </summary>
        STRING,
        /// <summary>
        /// 文本 字符串
        /// </summary>
        TEXT,
        /// <summary>
        /// 赋值 =
        /// </summary>
        ASSIGN,

        // 其他
        /// <summary>
        /// 换行
        /// </summary>
        NEWLINE,
        /// <summary>
        /// 缩进
        /// </summary>
        INDENT,
        /// <summary>
        /// 减少缩进
        /// </summary>
        DEDENT,
    }

    public class Token
    {
        public TokenType Type { get; }
        public string Value { get; }
        public int Line { get; }
        public int Column { get; }

        public Token(TokenType type, string value, int line, int column)
        {
            Type = type;
            Value = value;
            Line = line;
            Column = column;
        }

        public override string ToString()
        {
            return $"Token({Type}, '{Value}', line={Line}, col={Column})";
        }
    }
}