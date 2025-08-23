using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MookDialogueScript.Tests
{
    /// <summary>
    /// 语法分析器测试
    /// </summary>
    public class ParserTests
    {
        /// <summary>
        /// 解析脚本并返回AST
        /// </summary>
        private ScriptNode ParseScript(string script)
        {
            var lexer = new Lexer(script);
            var tokens = lexer.Tokenize();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Test]
        public void TestSimpleNodeDefinition()
        {
            string script = @"node: test
---
角色: 你好世界
===";

            var ast = ParseScript(script);
            
            Assert.AreEqual(1, ast.Nodes.Count, "应该有1个节点");
            
            var node = ast.Nodes[0];
            Assert.AreEqual("test", node.NodeName, "节点名称应该是test");
            Assert.AreEqual(1, node.Content.Count, "节点应该有1个内容项");
            
            var dialogue = node.Content[0] as DialogueNode;
            Assert.IsNotNull(dialogue, "内容应该是对话节点");
            Assert.AreEqual("角色", dialogue.Speaker, "说话者应该是'角色'");
            Assert.AreEqual(1, dialogue.Text.Count, "对话应该有1个文本段");
            
            var textSegment = dialogue.Text[0] as TextNode;
            Assert.IsNotNull(textSegment, "应该是文本节点");
            Assert.AreEqual(" 你好世界", textSegment.Text, "文本内容应该匹配");
        }

        [Test]
        public void TestNarrationNode()
        {
            string script = @"---
:这是旁白文本
普通文本
===";

            var ast = ParseScript(script);
            
            Assert.AreEqual(1, ast.Nodes.Count, "应该有1个节点");
            
            var node = ast.Nodes[0];
            Assert.AreEqual(2, node.Content.Count, "节点应该有2个内容项");
            
            // 第一个是旁白（冒号开头）
            var narration1 = node.Content[0] as DialogueNode;
            Assert.IsNotNull(narration1, "第一个内容应该是对话节点");
            Assert.IsNull(narration1.Speaker, "旁白的说话者应该为null");
            
            // 第二个是普通文本
            var narration2 = node.Content[1] as DialogueNode;
            Assert.IsNotNull(narration2, "第二个内容应该是对话节点");
            Assert.IsNull(narration2.Speaker, "旁白的说话者应该为null");
        }

        [Test]
        public void TestVariableInterpolation()
        {
            string script = @"---
角色: 你好{$name}，欢迎来到{$place}
===";

            var ast = ParseScript(script);
            
            var dialogue = ast.Nodes[0].Content[0] as DialogueNode;
            Assert.AreEqual(5, dialogue.Text.Count, "应该有5个文本段：文本+插值+文本+插值+空");
            
            // 检查插值节点
            var interpolation1 = dialogue.Text[1] as InterpolationNode;
            Assert.IsNotNull(interpolation1, "第2个段应该是插值节点");
            
            var variable1 = interpolation1.Expression as VariableNode;
            Assert.IsNotNull(variable1, "插值表达式应该是变量节点");
            Assert.AreEqual("name", variable1.Name, "变量名应该是name");
            
            var interpolation2 = dialogue.Text[3] as InterpolationNode;
            Assert.IsNotNull(interpolation2, "第4个段应该是插值节点");
            
            var variable2 = interpolation2.Expression as VariableNode;
            Assert.IsNotNull(variable2, "插值表达式应该是变量节点");
            Assert.AreEqual("place", variable2.Name, "变量名应该是place");
        }

        [Test]
        public void TestChoiceNodes()
        {
            string script = @"---
选择一个选项：
-> 选项1 #tag1
-> 选项2 <<if $hp > 50>> #tag2
===";

            var ast = ParseScript(script);
            
            var node = ast.Nodes[0];
            Assert.AreEqual(3, node.Content.Count, "节点应该有3个内容项");
            
            // 第一个是提示文本
            var prompt = node.Content[0] as DialogueNode;
            Assert.IsNotNull(prompt, "第一个应该是对话节点");
            
            // 第二个是选项1
            var choice1 = node.Content[1] as ChoiceNode;
            Assert.IsNotNull(choice1, "第二个应该是选择节点");
            Assert.IsNull(choice1.Condition, "选项1不应该有条件");
            Assert.IsTrue(choice1.Tags.Any(t => t.Contains("tag1")), "选项1应该有tag1标签");
            
            // 第三个是选项2
            var choice2 = node.Content[2] as ChoiceNode;
            Assert.IsNotNull(choice2, "第三个应该是选择节点");
            Assert.IsNotNull(choice2.Condition, "选项2应该有条件");
            Assert.IsTrue(choice2.Tags.Any(t => t.Contains("tag2")), "选项2应该有tag2标签");
            
            // 检查条件表达式
            var condition = choice2.Condition as BinaryOpNode;
            Assert.IsNotNull(condition, "条件应该是二元运算符节点");
            Assert.AreEqual(">", condition.Operator, "运算符应该是>");
        }

        [Test]
        public void TestConditionalNodes()
        {
            string script = @"---
<<if $hp > 0>>
你还活着
<<elif $hp == 0>>
你死了
<<else>>
状态未知
<<endif>>
===";

            var ast = ParseScript(script);
            
            var condition = ast.Nodes[0].Content[0] as ConditionNode;
            Assert.IsNotNull(condition, "应该是条件节点");
            
            // 检查if条件
            var ifCondition = condition.Condition as BinaryOpNode;
            Assert.IsNotNull(ifCondition, "if条件应该是二元运算符");
            Assert.AreEqual(">", ifCondition.Operator, "if条件运算符应该是>");
            
            // 检查then分支
            Assert.AreEqual(1, condition.ThenBranch.Count, "then分支应该有1个内容");
            var thenContent = condition.ThenBranch[0] as DialogueNode;
            Assert.IsNotNull(thenContent, "then内容应该是对话节点");
            
            // 检查elif分支
            Assert.AreEqual(1, condition.ElifBranches.Count, "应该有1个elif分支");
            var elifBranch = condition.ElifBranches[0];
            var elifCondition = elifBranch.Condition as BinaryOpNode;
            Assert.AreEqual("==", elifCondition.Operator, "elif条件运算符应该是==");
            
            // 检查else分支
            Assert.IsNotNull(condition.ElseBranch, "应该有else分支");
            Assert.AreEqual(1, condition.ElseBranch.Count, "else分支应该有1个内容");
        }

        [Test]
        public void TestVariableCommands()
        {
            string script = @"---
<<var $hp 100>>
<<set $mp = 50>>
<<add $exp 10>>
<<sub $gold 5>>
===";

            var ast = ParseScript(script);
            
            var node = ast.Nodes[0];
            Assert.AreEqual(4, node.Content.Count, "节点应该有4个命令");
            
            // 检查var命令
            var varCommand = node.Content[0] as VarCommandNode;
            Assert.IsNotNull(varCommand, "第1个应该是变量命令");
            Assert.AreEqual("var", varCommand.Operation, "操作应该是var");
            Assert.AreEqual("hp", varCommand.Variable, "变量名应该是hp");
            
            // 检查set命令
            var setCommand = node.Content[1] as VarCommandNode;
            Assert.IsNotNull(setCommand, "第2个应该是变量命令");
            Assert.AreEqual("set", setCommand.Operation, "操作应该是set");
            Assert.AreEqual("mp", setCommand.Variable, "变量名应该是mp");
            
            // 检查add命令
            var addCommand = node.Content[2] as VarCommandNode;
            Assert.IsNotNull(addCommand, "第3个应该是变量命令");
            Assert.AreEqual("add", addCommand.Operation, "操作应该是add");
            Assert.AreEqual("exp", addCommand.Variable, "变量名应该是exp");
            
            // 检查sub命令
            var subCommand = node.Content[3] as VarCommandNode;
            Assert.IsNotNull(subCommand, "第4个应该是变量命令");
            Assert.AreEqual("sub", subCommand.Operation, "操作应该是sub");
            Assert.AreEqual("gold", subCommand.Variable, "变量名应该是gold");
        }

        [Test]
        public void TestJumpAndWaitCommands()
        {
            string script = @"---
<<wait 2.5>>
<<jump ending>>
===";

            var ast = ParseScript(script);
            
            var node = ast.Nodes[0];
            Assert.AreEqual(2, node.Content.Count, "节点应该有2个命令");
            
            // 检查wait命令
            var waitCommand = node.Content[0] as WaitCommandNode;
            Assert.IsNotNull(waitCommand, "第1个应该是等待命令");
            
            var duration = waitCommand.Duration as NumberNode;
            Assert.IsNotNull(duration, "等待时长应该是数字节点");
            Assert.AreEqual(2.5, duration.Value, 0.001, "等待时长应该是2.5");
            
            // 检查jump命令
            var jumpCommand = node.Content[1] as JumpCommandNode;
            Assert.IsNotNull(jumpCommand, "第2个应该是跳转命令");
            Assert.AreEqual("ending", jumpCommand.TargetNode, "目标节点应该是ending");
        }

        [Test]
        public void TestFunctionCallCommand()
        {
            string script = @"---
<<showMessage(""Hello"", 3.14)>>
===";

            var ast = ParseScript(script);
            
            var callCommand = ast.Nodes[0].Content[0] as CallCommandNode;
            Assert.IsNotNull(callCommand, "应该是函数调用命令");
            Assert.AreEqual("showMessage", callCommand.FunctionName, "函数名应该是showMessage");
            Assert.AreEqual(2, callCommand.Parameters.Count, "应该有2个参数");
            
            // 检查第一个参数（字符串）
            var param1 = callCommand.Parameters[0] as StringInterpolationExpressionNode;
            Assert.IsNotNull(param1, "第1个参数应该是字符串插值表达式");
            
            // 检查第二个参数（数字）
            var param2 = callCommand.Parameters[1] as NumberNode;
            Assert.IsNotNull(param2, "第2个参数应该是数字节点");
            Assert.AreEqual(3.14, param2.Value, 0.001, "数字值应该是3.14");
        }

        [Test]
        public void TestExpressionPrecedence()
        {
            string script = @"---
<<set $result = $a + $b * $c - $d>>
===";

            var ast = ParseScript(script);
            
            var setCommand = ast.Nodes[0].Content[0] as VarCommandNode;
            var expression = setCommand.Value as BinaryOpNode;
            
            // 应该解析为: ($a + ($b * $c)) - $d
            Assert.IsNotNull(expression, "应该是二元运算符节点");
            Assert.AreEqual("-", expression.Operator, "顶层运算符应该是-");
            
            var leftSide = expression.Left as BinaryOpNode;
            Assert.IsNotNull(leftSide, "左侧应该是二元运算符节点");
            Assert.AreEqual("+", leftSide.Operator, "左侧运算符应该是+");
            
            var multiply = leftSide.Right as BinaryOpNode;
            Assert.IsNotNull(multiply, "右侧应该是乘法运算符节点");
            Assert.AreEqual("*", multiply.Operator, "应该是*运算符");
        }

        [Test]
        public void TestBooleanExpressions()
        {
            string script = @"---
<<if $a && $b || !$c>>
测试
<<endif>>
===";

            var ast = ParseScript(script);
            
            var condition = ast.Nodes[0].Content[0] as ConditionNode;
            var expression = condition.Condition as BinaryOpNode;
            
            // 应该解析为: ($a && $b) || (!$c)
            Assert.IsNotNull(expression, "应该是二元运算符节点");
            Assert.AreEqual("||", expression.Operator, "顶层运算符应该是||");
            
            var leftSide = expression.Left as BinaryOpNode;
            Assert.IsNotNull(leftSide, "左侧应该是二元运算符节点");
            Assert.AreEqual("&&", leftSide.Operator, "左侧运算符应该是&&");
            
            var rightSide = expression.Right as UnaryOpNode;
            Assert.IsNotNull(rightSide, "右侧应该是一元运算符节点");
            Assert.AreEqual("!", rightSide.Operator, "右侧运算符应该是!");
        }

        [Test]
        public void TestFunctionCallInExpression()
        {
            string script = @"---
<<if visited(""node1"") && random(1, 10) > 5>>
测试函数调用
<<endif>>
===";

            var ast = ParseScript(script);
            
            var condition = ast.Nodes[0].Content[0] as ConditionNode;
            var expression = condition.Condition as BinaryOpNode;
            
            Assert.AreEqual("&&", expression.Operator, "应该是&&运算符");
            
            // 左侧是visited函数调用
            var leftFunction = expression.Left as FunctionCallNode;
            Assert.IsNotNull(leftFunction, "左侧应该是函数调用");
            Assert.AreEqual("visited", leftFunction.Name, "函数名应该是visited");
            
            // 右侧是比较表达式
            var rightComparison = expression.Right as BinaryOpNode;
            Assert.IsNotNull(rightComparison, "右侧应该是比较表达式");
            Assert.AreEqual(">", rightComparison.Operator, "比较运算符应该是>");
            
            // 比较左侧是random函数调用
            var randomFunction = rightComparison.Left as FunctionCallNode;
            Assert.IsNotNull(randomFunction, "应该是random函数调用");
            Assert.AreEqual("random", randomFunction.Name, "函数名应该是random");
            Assert.AreEqual(2, randomFunction.Arguments.Count, "random应该有2个参数");
        }

        [Test]
        public void TestNestedContent()
        {
            string script = @"---
-> 主选项
    这是嵌套的内容
    角色: 嵌套的对话
===";

            var ast = ParseScript(script);
            
            var choice = ast.Nodes[0].Content[0] as ChoiceNode;
            Assert.IsNotNull(choice, "应该是选择节点");
            Assert.AreEqual(2, choice.Content.Count, "选择应该有2个嵌套内容");
            
            // 第一个嵌套内容是旁白
            var nestedNarration = choice.Content[0] as DialogueNode;
            Assert.IsNotNull(nestedNarration, "第一个嵌套内容应该是对话节点");
            Assert.IsNull(nestedNarration.Speaker, "应该是旁白（无说话者）");
            
            // 第二个嵌套内容是角色对话
            var nestedDialogue = choice.Content[1] as DialogueNode;
            Assert.IsNotNull(nestedDialogue, "第二个嵌套内容应该是对话节点");
            Assert.AreEqual("角色", nestedDialogue.Speaker, "说话者应该是'角色'");
        }

        [Test]
        public void TestComplexNestedCondition()
        {
            string script = @"---
<<if $level > 5>>
    -> 高级选项
        你选择了高级选项
    <<if $experience > 100>>
        获得特殊奖励
    <<endif>>
<<endif>>
===";

            var ast = ParseScript(script);
            
            var outerCondition = ast.Nodes[0].Content[0] as ConditionNode;
            Assert.IsNotNull(outerCondition, "应该是条件节点");
            Assert.AreEqual(2, outerCondition.ThenBranch.Count, "外层条件应该有2个内容");
            
            // 第一个是选择
            var choice = outerCondition.ThenBranch[0] as ChoiceNode;
            Assert.IsNotNull(choice, "第一个应该是选择节点");
            Assert.AreEqual(1, choice.Content.Count, "选择应该有1个嵌套内容");
            
            // 第二个是内层条件
            var innerCondition = outerCondition.ThenBranch[1] as ConditionNode;
            Assert.IsNotNull(innerCondition, "第二个应该是条件节点");
            Assert.AreEqual(1, innerCondition.ThenBranch.Count, "内层条件应该有1个内容");
        }

        [Test]
        public void TestMultipleNodes()
        {
            string script = @"node: start
---
开始节点
===

node: end
desc: 结束节点描述
---
结束节点
===";

            var ast = ParseScript(script);
            
            Assert.AreEqual(2, ast.Nodes.Count, "应该有2个节点");
            
            // 第一个节点
            var firstNode = ast.Nodes[0];
            Assert.AreEqual("start", firstNode.NodeName, "第一个节点名应该是start");
            Assert.AreEqual(1, firstNode.Metadata.Count, "第一个节点应该有1个元数据");
            Assert.IsTrue(firstNode.Metadata.ContainsKey("node"), "应该包含node元数据");
            
            // 第二个节点
            var secondNode = ast.Nodes[1];
            Assert.AreEqual("end", secondNode.NodeName, "第二个节点名应该是end");
            Assert.AreEqual(2, secondNode.Metadata.Count, "第二个节点应该有2个元数据");
            Assert.IsTrue(secondNode.Metadata.ContainsKey("node"), "应该包含node元数据");
            Assert.IsTrue(secondNode.Metadata.ContainsKey("desc"), "应该包含desc元数据");
            Assert.AreEqual(" 结束节点描述", secondNode.Metadata["desc"], "描述元数据值应该匹配");
        }

        [Test]
        public void TestStringWithInterpolation()
        {
            string script = @"---
<<set $message = ""Hello {$name}, you have {$gold} gold!"">>
===";

            var ast = ParseScript(script);
            
            var setCommand = ast.Nodes[0].Content[0] as VarCommandNode;
            var stringExpr = setCommand.Value as StringInterpolationExpressionNode;
            
            Assert.IsNotNull(stringExpr, "应该是字符串插值表达式");
            Assert.AreEqual(5, stringExpr.Segments.Count, "应该有5个段：文本+插值+文本+插值+文本");
            
            // 检查各个段
            Assert.IsInstanceOf<TextNode>(stringExpr.Segments[0], "第1段应该是文本");
            Assert.IsInstanceOf<InterpolationNode>(stringExpr.Segments[1], "第2段应该是插值");
            Assert.IsInstanceOf<TextNode>(stringExpr.Segments[2], "第3段应该是文本");
            Assert.IsInstanceOf<InterpolationNode>(stringExpr.Segments[3], "第4段应该是插值");
            Assert.IsInstanceOf<TextNode>(stringExpr.Segments[4], "第5段应该是文本");
            
            // 检查插值变量
            var interp1 = stringExpr.Segments[1] as InterpolationNode;
            var var1 = interp1.Expression as VariableNode;
            Assert.AreEqual("name", var1.Name, "第1个变量应该是name");
            
            var interp2 = stringExpr.Segments[3] as InterpolationNode;
            var var2 = interp2.Expression as VariableNode;
            Assert.AreEqual("gold", var2.Name, "第2个变量应该是gold");
        }

        [Test]
        public void TestAutoGeneratedLineTags()
        {
            string script = @"node: test
---
角色1: 第一句话
角色2: 第二句话
旁白文本
===";

            var ast = ParseScript(script);
            
            var node = ast.Nodes[0];
            Assert.AreEqual(3, node.Content.Count, "节点应该有3个内容");
            
            // 检查每个内容都有自动生成的行号标签
            for (int i = 0; i < node.Content.Count; i++)
            {
                var dialogue = node.Content[i] as DialogueNode;
                Assert.IsNotNull(dialogue, $"第{i+1}个内容应该是对话节点");
                Assert.IsTrue(dialogue.Tags.Any(tag => tag.StartsWith("line:test")), 
                    $"第{i+1}个对话应该有自动生成的行号标签");
            }
            
            // 验证标签内容
            var firstDialogue = node.Content[0] as DialogueNode;
            Assert.IsTrue(firstDialogue.Tags.Any(tag => tag == "line:test1"), "第1个对话应该有line:test1标签");
            
            var secondDialogue = node.Content[1] as DialogueNode;
            Assert.IsTrue(secondDialogue.Tags.Any(tag => tag == "line:test2"), "第2个对话应该有line:test2标签");
            
            var thirdDialogue = node.Content[2] as DialogueNode;
            Assert.IsTrue(thirdDialogue.Tags.Any(tag => tag == "line:test3"), "第3个对话应该有line:test3标签");
        }

        [Test]
        public void TestParsingErrors()
        {
            // 测试缺少节点结束标记
            string incompleteScript = @"---
角色: 对话内容";

            Assert.Throws<InvalidOperationException>(() => ParseScript(incompleteScript), 
                "缺少节点结束标记应该抛出异常");
        }

        [Test]
        public void TestEmptyNode()
        {
            string script = @"---
===";

            var ast = ParseScript(script);
            
            Assert.AreEqual(1, ast.Nodes.Count, "应该有1个节点");
            var node = ast.Nodes[0];
            Assert.AreEqual(0, node.Content.Count, "空节点应该没有内容");
        }

        [Test]
        public void TestNodeWithOnlyComments()
        {
            string script = @"---
// 这是注释
// 另一个注释
===";

            var ast = ParseScript(script);
            
            Assert.AreEqual(1, ast.Nodes.Count, "应该有1个节点");
            var node = ast.Nodes[0];
            Assert.AreEqual(0, node.Content.Count, "只有注释的节点应该没有内容");
        }
    }
}