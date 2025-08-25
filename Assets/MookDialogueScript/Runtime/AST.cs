using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MookDialogueScript
{
    /// <summary>
    /// 抽象语法树节点
    /// </summary>
    public abstract class ASTNode
    {
        /// <summary>
        /// 行号
        /// </summary>
        public int Line { get; }

        /// <summary>
        /// 列号
        /// </summary>
        public int Column { get; }

        protected ASTNode(int line, int column)
        {
            Line = line;
            Column = column;
        }

        public override string ToString()
        {
            return $"{GetType().Name} (行: {Line}, 列: {Column})";
        }
    }

    /// <summary>
    /// 脚本节点
    /// </summary>
    public class ScriptNode : ASTNode
    {
        /// <summary>
        /// 节点定义列表
        /// </summary>
        public List<NodeDefinitionNode> Nodes { get; }

        public ScriptNode(List<NodeDefinitionNode> nodes)
            : base(1, 1)
        {
            Nodes = nodes ?? new List<NodeDefinitionNode>();
        }
    }

    /// <summary>
    /// 节点定义节点
    /// </summary>
    public class NodeDefinitionNode : ASTNode
    {
        /// <summary>
        /// 节点名称
        /// </summary>
        public string NodeName { get; }

        /// <summary>
        /// 节点元数据，存储节点定义时的键值对信息
        /// </summary>
        public Dictionary<string, string> Metadata { get; }

        /// <summary>
        /// 内容列表
        /// </summary>
        public List<ContentNode> Content { get; }

        public NodeDefinitionNode(string nodeName, Dictionary<string, string> metadata, List<ContentNode> content, int line, int column)
            : base(line, column)
        {
            NodeName = nodeName;
            Content = content;
            Metadata = metadata ?? new Dictionary<string, string>();
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"--- {NodeName}");

            // 添加元数据到输出
            if (Metadata is {Count: > 0})
            {
                foreach (var meta in Metadata)
                {
                    sb.AppendLine($"[{meta.Key}:{meta.Value}]");
                }
            }

            foreach (var content in Content)
            {
                sb.AppendLine($"{content}");
            }
            sb.AppendLine("===");
            return sb.ToString();
        }
    }

    /// <summary>
    /// 内容节点
    /// </summary>
    public abstract class ContentNode : ASTNode
    {
        protected ContentNode(int line, int column) : base(line, column) { }
    }

    /// <summary>
    /// 对话节点（当Speaker为空时代表旁白）
    /// </summary>
    public class DialogueNode : ContentNode
    {
        /// <summary>
        /// 说话者（为空时代表旁白）
        /// </summary>
        public string Speaker { get; }

        /// <summary>
        /// 文本列表
        /// </summary>
        public List<TextSegmentNode> Text { get; }

        /// <summary>
        /// 标签列表
        /// </summary>
        public List<string> Tags { get; }

        /// <summary>
        /// 内容列表，用于支持嵌套内容
        /// </summary>
        public List<ContentNode> Content { get; }

        public DialogueNode(string speaker, List<TextSegmentNode> text, List<string> tags, List<ContentNode> content, int line, int column)
            : base(line, column)
        {
            Speaker = speaker;
            Text = text;
            Tags = tags;
            Content = content ?? new List<ContentNode>();
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            if (Speaker != null)
            {
                sb.Append($"{Speaker}: ");
            }
            sb.Append(string.Join("", Text));
            if (Tags is {Count: > 0})
            {
                sb.Append($" {string.Join(" ", Tags.Select(l => $"#{l}"))}");
            }
            sb.AppendLine();
            if (Content is {Count: > 0})
            {
                foreach (var content in Content)
                {
                    sb.AppendLine($"    {content}");
                }
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// 文本段节点
    /// </summary>
    public abstract class TextSegmentNode : ASTNode
    {
        protected TextSegmentNode(int line, int column) : base(line, column) { }
    }

    /// <summary>
    /// 文本节点
    /// </summary>
    public class TextNode : TextSegmentNode
    {
        /// <summary>
        /// 文本
        /// </summary>
        public string Text { get; }

        public TextNode(string text, int line, int column)
            : base(line, column)
        {
            Text = text;
        }

        public override string ToString()
        {
            return Text;
        }
    }

    /// <summary>
    /// 插值节点
    /// </summary>
    public class InterpolationNode : TextSegmentNode
    {
        /// <summary>
        /// 表达式
        /// </summary>
        public ExpressionNode Expression { get; }

        public InterpolationNode(ExpressionNode expression, int line, int column)
            : base(line, column)
        {
            Expression = expression;
        }

        public override string ToString()
        {
            return $"{Expression}";
        }
    }

    /// <summary>
    /// 选项节点
    /// </summary>
    public class ChoiceNode : ContentNode
    {
        /// <summary>
        /// 文本列表
        /// </summary>
        public List<TextSegmentNode> Text { get; }

        /// <summary>
        /// 条件
        /// </summary>
        public ExpressionNode Condition { get; }

        /// <summary>
        /// 标签列表
        /// </summary>
        public List<string> Tags { get; }

        /// <summary>
        /// 内容列表
        /// </summary>
        public List<ContentNode> Content { get; }

        public ChoiceNode(List<TextSegmentNode> text, ExpressionNode condition, List<string> tags, List<ContentNode> content, int line, int column)
            : base(line, column)
        {
            Text = text ?? new List<TextSegmentNode>();
            Condition = condition;
            Tags = tags ?? new List<string>();
            Content = content ?? new List<ContentNode>();
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"-> {string.Join("", Text)}");
            if (Condition != null)
            {
                sb.Append($" <<if {Condition}>>");
            }
            if (Tags is {Count: > 0})
            {
                sb.Append($" {string.Join(" ", Tags.Select(t => $"#{t}"))}");
            }
            sb.AppendLine();
            if (Content is {Count: > 0})
            {
                foreach (var content in Content)
                {
                    sb.AppendLine($"    {content}");
                }
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// 条件节点
    /// </summary>
    public class ConditionNode : ContentNode
    {
        /// <summary>
        /// 条件
        /// </summary>
        public ExpressionNode Condition { get; }

        /// <summary>
        /// 然后分支
        /// </summary>
        public List<ContentNode> ThenBranch { get; }

        /// <summary>
        /// 否则如果分支
        /// </summary>
        public List<(ExpressionNode Condition, List<ContentNode> Content)> ElifBranches { get; }

        /// <summary>
        /// 否则分支
        /// </summary>
        public List<ContentNode> ElseBranch { get; }

        public ConditionNode(
            ExpressionNode condition,
            List<ContentNode> thenBranch,
            List<(ExpressionNode Condition, List<ContentNode> Content)> elifBranches,
            List<ContentNode> elseBranch,
            int line, int column)
            : base(line, column)
        {
            Condition = condition;
            ThenBranch = thenBranch;
            ElifBranches = elifBranches;
            ElseBranch = elseBranch;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"条件节点: {Condition}");
            sb.AppendLine("--- 然后分支 ---");
            foreach (var content in ThenBranch)
            {
                sb.AppendLine($"内容: {content}");
            }
            if (ElifBranches is {Count: > 0})
            {
                sb.AppendLine("--- 否则如果分支 ---");
                foreach (var (condition, content) in ElifBranches)
                {
                    sb.AppendLine($"条件: {condition}");
                    sb.AppendLine($"内容: {content}");
                }
            }
            if (ElseBranch is {Count: > 0})
            {
                sb.AppendLine("--- 否则分支 ---");
                foreach (var content in ElseBranch)
                {
                    sb.AppendLine($"内容: {content}");
                }
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// 命令节点
    /// </summary>
    public abstract class CommandNode : ContentNode
    {
        protected CommandNode(int line, int column) : base(line, column) { }
    }

    /// <summary>
    /// 变量命令节点
    /// </summary>
    public class VarCommandNode : CommandNode
    {
        /// <summary>
        /// 变量
        /// </summary>
        public string Variable { get; }

        /// <summary>
        /// 值
        /// </summary>
        public ExpressionNode Value { get; }

        /// <summary>
        /// 操作
        /// </summary>
        public string Operation { get; } // set, add, sub, mul, div, mod
        public VarCommandNode(string variable, ExpressionNode value, string operation, int line, int column)
            : base(line, column)
        {
            Variable = variable;
            Value = value;
            Operation = operation;
        }

        public override string ToString()
        {
            return $"{Variable} {Operation} {Value}";
        }
    }

    /// <summary>
    /// 调用命令节点
    /// </summary>
    public class CallCommandNode : CommandNode
    {
        /// <summary>
        /// 函数名称
        /// </summary>
        public string FunctionName { get; }

        /// <summary>
        /// 参数列表
        /// </summary>
        public List<ExpressionNode> Parameters { get; }

        public CallCommandNode(string functionName, List<ExpressionNode> parameters, int line, int column)
            : base(line, column)
        {
            FunctionName = functionName;
            Parameters = parameters;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"调用命令节点: {FunctionName}(");
            if (Parameters is {Count: > 0})
            {
                sb.Append(string.Join(", ", Parameters));
            }
            sb.Append(")");
            return sb.ToString();
        }
    }

    /// <summary>
    /// 等待命令节点
    /// </summary>
    public class WaitCommandNode : CommandNode
    {
        /// <summary>
        /// 持续时间
        /// </summary>
        public ExpressionNode Duration { get; }

        public WaitCommandNode(ExpressionNode duration, int line, int column)
            : base(line, column)
        {
            Duration = duration;
        }

        public override string ToString()
        {
            return $"{Duration}";
        }
    }

    /// <summary>
    /// 跳转命令节点
    /// </summary>
    public class JumpCommandNode : CommandNode
    {
        /// <summary>
        /// 目标节点
        /// </summary>
        public string TargetNode { get; }

        public JumpCommandNode(string targetNode, int line, int column)
            : base(line, column)
        {
            TargetNode = targetNode;
        }

        public override string ToString()
        {
            return TargetNode;
        }
    }

    /// <summary>
    /// 表达式节点
    /// </summary>
    public abstract class ExpressionNode : ASTNode
    {
        protected ExpressionNode(int line, int column) : base(line, column) { }
    }

    /// <summary>
    /// 二元运算符节点
    /// </summary>
    public class BinaryOpNode : ExpressionNode
    {
        /// <summary>
        /// 左操作数
        /// </summary>
        public ExpressionNode Left { get; }

        /// <summary>
        /// 运算符
        /// </summary>
        public string Operator { get; }

        /// <summary>
        /// 右操作数
        /// </summary>
        public ExpressionNode Right { get; }

        public BinaryOpNode(ExpressionNode left, string op, ExpressionNode right, int line, int column)
            : base(line, column)
        {
            Left = left;
            Operator = op;
            Right = right;
        }

        public override string ToString()
        {
            return $"{Left} {Operator} {Right}";
        }
    }

    /// <summary>
    /// 一元运算符节点
    /// </summary>
    public class UnaryOpNode : ExpressionNode
    {
        /// <summary>
        /// 运算符
        /// </summary>
        public string Operator { get; }

        /// <summary>
        /// 操作数
        /// </summary>
        public ExpressionNode Operand { get; }

        public UnaryOpNode(string op, ExpressionNode operand, int line, int column)
            : base(line, column)
        {
            Operator = op;
            Operand = operand;
        }

        public override string ToString()
        {
            return $"{Operator} {Operand}";
        }
    }

    /// <summary>
    /// 数字节点
    /// </summary>
    public class NumberNode : ExpressionNode
    {
        /// <summary>
        /// 值
        /// </summary>
        public double Value { get; }

        public NumberNode(double value, int line, int column)
            : base(line, column)
        {
            Value = value;
        }

        public override string ToString()
        {
            return $"{Value}";
        }
    }

    /// <summary>
    /// 字符串和字符串插值表达式节点
    /// </summary>
    public class StringInterpolationExpressionNode : ExpressionNode
    {
        /// <summary>
        /// 文本段列表
        /// </summary>
        public List<TextSegmentNode> Segments { get; }

        public StringInterpolationExpressionNode(List<TextSegmentNode> segments, int line, int column)
            : base(line, column)
        {
            Segments = segments;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"{string.Join("", Segments)}");
            return sb.ToString();
        }
    }

    /// <summary>
    /// 布尔节点
    /// </summary>
    public class BooleanNode : ExpressionNode
    {
        /// <summary>
        /// 值
        /// </summary>
        public bool Value { get; }

        public BooleanNode(bool value, int line, int column)
            : base(line, column)
        {
            Value = value;
        }

        public override string ToString()
        {
            return Value.ToString().ToLower();
        }
    }

    /// <summary>
    /// 变量节点
    /// </summary>
    public class VariableNode : ExpressionNode
    {
        /// <summary>
        /// 变量名（不包含$符号，$符号在词法分析阶段已被处理）
        /// </summary>
        public string Name { get; }

        public VariableNode(string name, int line, int column)
            : base(line, column)
        {
            Name = name;
        }

        public override string ToString()
        {
            return Name;
        }
    }

    /// <summary>
    /// 标识符节点
    /// </summary>
    public class IdentifierNode : ExpressionNode
    {
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; }

        public IdentifierNode(string name, int line, int column)
            : base(line, column)
        {
            Name = name;
        }

        public override string ToString()
        {
            return Name;
        }
    }

    // FunctionCallNode 已被删除 - 使用 CallExpressionNode 替代

    /// <summary>
    /// 调用表达式节点，支持任意表达式的调用
    /// </summary>
    public class CallExpressionNode : ExpressionNode
    {
        /// <summary>
        /// 被调用者表达式
        /// </summary>
        public ExpressionNode Callee { get; }

        /// <summary>
        /// 参数列表
        /// </summary>
        public List<ExpressionNode> Arguments { get; }

        public CallExpressionNode(ExpressionNode callee, List<ExpressionNode> arguments, int line, int column)
            : base(line, column)
        {
            Callee = callee;
            Arguments = arguments ?? new List<ExpressionNode>();
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"调用表达式: {Callee}(");
            if (Arguments is {Count: > 0})
            {
                sb.Append(string.Join(", ", Arguments));
            }
            sb.Append(")");
            return sb.ToString();
        }
    }

    /// <summary>
    /// 成员访问节点
    /// </summary>
    public class MemberAccessNode : ExpressionNode
    {
        /// <summary>
        /// 目标表达式
        /// </summary>
        public ExpressionNode Target { get; }

        /// <summary>
        /// 成员名称
        /// </summary>
        public string Member { get; }

        public MemberAccessNode(ExpressionNode target, string member, int line, int column)
            : base(line, column)
        {
            Target = target;
            Member = member;
        }

        public override string ToString()
        {
            return $"{Target}.{Member}";
        }
    }

    /// <summary>
    /// 索引访问节点
    /// </summary>
    public class IndexAccessNode : ExpressionNode
    {
        /// <summary>
        /// 目标表达式
        /// </summary>
        public ExpressionNode Target { get; }

        /// <summary>
        /// 索引表达式
        /// </summary>
        public ExpressionNode Index { get; }

        public IndexAccessNode(ExpressionNode target, ExpressionNode index, int line, int column)
            : base(line, column)
        {
            Target = target;
            Index = index;
        }

        public override string ToString()
        {
            return $"{Target}[{Index}]";
        }
    }

}
