using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            foreach (var token in tokens)
            {
                Debug.Log(token.ToString());
            }
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
            Assert.AreEqual(4, dialogue.Text.Count, "应该有5个文本段：文本+插值+文本+插值");
            
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
            Assert.AreEqual("showmessage", callCommand.FunctionName, "函数名应该是showMessage");
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
            var leftFunction = expression.Left as CallExpressionNode;
            Assert.IsNotNull(leftFunction, "左侧应该是函数调用");
            
            // 检查被调用者是标识符
            var leftCallee = leftFunction.Callee as IdentifierNode;
            Assert.IsNotNull(leftCallee, "被调用者应该是标识符");
            Assert.AreEqual("visited", leftCallee.Name, "函数名应该是visited");
            
            // 右侧是比较表达式
            var rightComparison = expression.Right as BinaryOpNode;
            Assert.IsNotNull(rightComparison, "右侧应该是比较表达式");
            Assert.AreEqual(">", rightComparison.Operator, "比较运算符应该是>");
            
            // 比较左侧是random函数调用
            var randomFunction = rightComparison.Left as CallExpressionNode;
            Assert.IsNotNull(randomFunction, "应该是random函数调用");
            
            // 检查被调用者是标识符
            var randomCallee = randomFunction.Callee as IdentifierNode;
            Assert.IsNotNull(randomCallee, "被调用者应该是标识符");
            Assert.AreEqual("random", randomCallee.Name, "函数名应该是random");
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
            Assert.AreEqual("结束节点描述", secondNode.Metadata["desc"], "描述元数据值应该匹配");
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

        [Test]
        public void TestComplexExpressionPrecedenceAndAssociativity()
        {
            string script = @"---
<<set $result = $a + $b * $c - $d / $e + $f>>
===";

            var ast = ParseScript(script);
            
            var setCommand = ast.Nodes[0].Content[0] as VarCommandNode;
            var expression = setCommand.Value as BinaryOpNode;
            
            // 应该解析为: (($a + ($b * $c)) - ($d / $e)) + $f
            Assert.IsNotNull(expression, "应该是二元运算符节点");
            Assert.AreEqual("+", expression.Operator, "顶层运算符应该是+");
        }

        [Test]
        public void TestNestedConditionalDepth()
        {
            string script = @"---
<<if $level1>>
    <<if $level2>>
        <<if $level3>>
            <<if $level4>>
                深层嵌套内容
            <<endif>>
        <<endif>>
    <<endif>>
<<endif>>
===";

            var ast = ParseScript(script);
            
            var level1 = ast.Nodes[0].Content[0] as ConditionNode;
            Assert.IsNotNull(level1, "第1层应该是条件节点");
            
            var level2 = level1.ThenBranch[0] as ConditionNode;
            Assert.IsNotNull(level2, "第2层应该是条件节点");
            
            var level3 = level2.ThenBranch[0] as ConditionNode;
            Assert.IsNotNull(level3, "第3层应该是条件节点");
            
            var level4 = level3.ThenBranch[0] as ConditionNode;
            Assert.IsNotNull(level4, "第4层应该是条件节点");
            
            Assert.AreEqual(1, level4.ThenBranch.Count, "最深层应该有内容");
        }

        [Test]
        public void TestComplexChoiceWithNestedConditions()
        {
            string script = @"---
-> 复杂选项 <<if $hp > 50 && visited(""node1"") || $level >= 10>>
    <<if $gold >= 100>>
        花费100金币
    <<else>>
        没有足够金币
    <<endif>>
===";

            var ast = ParseScript(script);
            
            var choice = ast.Nodes[0].Content[0] as ChoiceNode;
            Assert.IsNotNull(choice, "应该是选择节点");
            Assert.IsNotNull(choice.Condition, "选择应该有条件");
            Assert.AreEqual(1, choice.Content.Count, "选择应该有嵌套内容");
            
            var nestedCondition = choice.Content[0] as ConditionNode;
            Assert.IsNotNull(nestedCondition, "嵌套内容应该是条件节点");
        }

        [Test]
        public void TestMixedContentTypes()
        {
            string script = @"---
:旁白开场
角色A: 普通对话
<<var $test 123>>
-> 选项1
    嵌套旁白
<<if $test > 100>>
    条件对话
<<endif>>
<<wait 1.5>>
角色B: 结束对话 #ending
===";

            var ast = ParseScript(script);
            
            var node = ast.Nodes[0];
            Assert.AreEqual(7, node.Content.Count, "节点应该有7个不同类型的内容");
            
            Assert.IsInstanceOf<DialogueNode>(node.Content[0], "第1个应该是对话节点（旁白）");
            Assert.IsInstanceOf<DialogueNode>(node.Content[1], "第2个应该是对话节点");
            Assert.IsInstanceOf<VarCommandNode>(node.Content[2], "第3个应该是变量命令");
            Assert.IsInstanceOf<ChoiceNode>(node.Content[3], "第4个应该是选择节点");
            Assert.IsInstanceOf<ConditionNode>(node.Content[4], "第5个应该是条件节点");
            Assert.IsInstanceOf<WaitCommandNode>(node.Content[5], "第6个应该是等待命令");
            Assert.IsInstanceOf<DialogueNode>(node.Content[6], "第7个应该是对话节点");
        }

        [Test]
        public void TestStringInterpolationWithComplexExpressions()
        {
            string script = @"---
<<set $msg = ""玩家{$name}等级{$level + 1}，经验{($exp * 100) / $maxExp}%"">>
===";

            var ast = ParseScript(script);
            
            var setCommand = ast.Nodes[0].Content[0] as VarCommandNode;
            var stringExpr = setCommand.Value as StringInterpolationExpressionNode;
            
            Assert.IsNotNull(stringExpr, "应该是字符串插值表达式");
            Assert.IsTrue(stringExpr.Segments.Count >= 6, "应该有多个段（文本+插值混合）");
            
            // 检查复杂表达式插值
            var interpolations = stringExpr.Segments.OfType<InterpolationNode>().ToList();
            Assert.AreEqual(3, interpolations.Count, "应该有3个插值");
            
            // 第二个插值应该是加法表达式
            var addExpr = interpolations[1].Expression as BinaryOpNode;
            Assert.IsNotNull(addExpr, "第2个插值应该是加法表达式");
            Assert.AreEqual("+", addExpr.Operator, "应该是加法运算符");
        }

        [Test]
        public void TestUnaryOperatorChaining()
        {
            string script = @"---
<<set $result = !!$bool && !($flag || $state)>>
===";

            var ast = ParseScript(script);
            
            var setCommand = ast.Nodes[0].Content[0] as VarCommandNode;
            var expression = setCommand.Value as BinaryOpNode;
            
            Assert.IsNotNull(expression, "应该是二元运算符节点");
            Assert.AreEqual("&&", expression.Operator, "应该是逻辑与运算符");
            
            // 左侧应该是连续的一元运算符
            var leftUnary = expression.Left as UnaryOpNode;
            Assert.IsNotNull(leftUnary, "左侧应该是一元运算符");
            Assert.AreEqual("!", leftUnary.Operator, "外层应该是逻辑非");
            
            var innerUnary = leftUnary.Operand as UnaryOpNode;
            Assert.IsNotNull(innerUnary, "内层也应该是一元运算符");
            Assert.AreEqual("!", innerUnary.Operator, "内层也应该是逻辑非");
        }

        [Test]
        public void TestParenthesesPrecedenceOverride()
        {
            string script = @"---
<<set $result = ($a + $b) * ($c - $d) / ($e + $f)>>
===";

            var ast = ParseScript(script);
            
            var setCommand = ast.Nodes[0].Content[0] as VarCommandNode;
            var expression = setCommand.Value as BinaryOpNode;
            
            // 验证括号优先级覆盖
            Assert.IsNotNull(expression, "应该是二元运算符节点");
            Assert.AreEqual("/", expression.Operator, "顶层应该是除法");
            
            var leftMul = expression.Left as BinaryOpNode;
            Assert.IsNotNull(leftMul, "左侧应该是乘法表达式");
            Assert.AreEqual("*", leftMul.Operator, "应该是乘法运算符");
        }

        [Test]
        public void TestFunctionCallWithComplexParameters()
        {
            string script = @"---
<<if complexFunc($a + $b, random(1, 10) * 2, ""string with {$var}"", $obj__method($param))>>
测试复杂函数调用
<<endif>>
===";

            var ast = ParseScript(script);
            
            var condition = ast.Nodes[0].Content[0] as ConditionNode;
            var funcCall = condition.Condition as CallExpressionNode;
            
            Assert.IsNotNull(funcCall, "条件应该是函数调用");
            
            // 检查被调用者
            var callee = funcCall.Callee as IdentifierNode;
            Assert.IsNotNull(callee, "被调用者应该是标识符");
            Assert.AreEqual("complexFunc", callee.Name, "函数名应该是complexFunc");
            Assert.AreEqual(4, funcCall.Arguments.Count, "应该有4个参数");
            
            // 验证复杂参数类型
            Assert.IsInstanceOf<BinaryOpNode>(funcCall.Arguments[0], "第1个参数应该是表达式");
            Assert.IsInstanceOf<BinaryOpNode>(funcCall.Arguments[1], "第2个参数应该是表达式");
            Assert.IsInstanceOf<StringInterpolationExpressionNode>(funcCall.Arguments[2], "第3个参数应该是字符串插值");
            Assert.IsInstanceOf<CallExpressionNode>(funcCall.Arguments[3], "第4个参数应该是函数调用");
        }

        [Test]
        public void TestMemberAccessExpression()
        {
            string script = @"---
<<if $obj.property == 5>>
测试成员访问
<<endif>>
===";

            var ast = ParseScript(script);
            
            var condition = ast.Nodes[0].Content[0] as ConditionNode;
            var comparison = condition.Condition as BinaryOpNode;
            
            Assert.IsNotNull(comparison, "条件应该是比较表达式");
            Assert.AreEqual("==", comparison.Operator, "应该是等于运算符");
            
            var memberAccess = comparison.Left as MemberAccessNode;
            Assert.IsNotNull(memberAccess, "左侧应该是成员访问");
            Assert.AreEqual("property", memberAccess.Member, "成员名应该是property");
            
            var targetVar = memberAccess.Target as VariableNode;
            Assert.IsNotNull(targetVar, "目标应该是变量");
            Assert.AreEqual("obj", targetVar.Name, "变量名应该是obj");
        }

        [Test]
        public void TestIndexAccessExpression()
        {
            string script = @"---
<<if $arr[0] > 10>>
测试索引访问
<<endif>>
===";

            var ast = ParseScript(script);
            
            var condition = ast.Nodes[0].Content[0] as ConditionNode;
            var comparison = condition.Condition as BinaryOpNode;
            
            Assert.IsNotNull(comparison, "条件应该是比较表达式");
            Assert.AreEqual(">", comparison.Operator, "应该是大于运算符");
            
            var indexAccess = comparison.Left as IndexAccessNode;
            Assert.IsNotNull(indexAccess, "左侧应该是索引访问");
            
            var targetVar = indexAccess.Target as VariableNode;
            Assert.IsNotNull(targetVar, "目标应该是变量");
            Assert.AreEqual("arr", targetVar.Name, "变量名应该是arr");
            
            var indexExpr = indexAccess.Index as NumberNode;
            Assert.IsNotNull(indexExpr, "索引应该是数字");
            Assert.AreEqual(0, indexExpr.Value, "索引值应该是0");
        }

        [Test]
        public void TestChainedPostfixExpressions()
        {
            string script = @"---
<<if $obj.method(1, 2).result[0] == ""test"">>
测试链式后缀表达式
<<endif>>
===";

            var ast = ParseScript(script);
            
            var condition = ast.Nodes[0].Content[0] as ConditionNode;
            var comparison = condition.Condition as BinaryOpNode;
            
            Assert.IsNotNull(comparison, "条件应该是比较表达式");
            Assert.AreEqual("==", comparison.Operator, "应该是等于运算符");
            
            // 左侧应该是 $obj.method(1, 2).result[0]
            var indexAccess = comparison.Left as IndexAccessNode;
            Assert.IsNotNull(indexAccess, "最外层应该是索引访问");
            
            // 索引访问的目标应该是成员访问
            var memberAccess = indexAccess.Target as MemberAccessNode;
            Assert.IsNotNull(memberAccess, "目标应该是成员访问");
            Assert.AreEqual("result", memberAccess.Member, "成员名应该是result");
            
            // 成员访问的目标应该是函数调用
            var functionCall = memberAccess.Target as CallExpressionNode;
            Assert.IsNotNull(functionCall, "目标应该是函数调用");
            Assert.AreEqual(2, functionCall.Arguments.Count, "应该有2个参数");
            
            // 函数调用的目标应该是成员访问
            var objectMemberAccess = functionCall.Callee as MemberAccessNode;
            Assert.IsNotNull(objectMemberAccess, "被调用者应该是成员访问");
            Assert.AreEqual("method", objectMemberAccess.Member, "方法名应该是method");
            
            // 最深层的目标应该是变量
            var objectVar = objectMemberAccess.Target as VariableNode;
            Assert.IsNotNull(objectVar, "最终目标应该是变量");
            Assert.AreEqual("obj", objectVar.Name, "变量名应该是obj");
        }

        [Test]
        public void TestErrorRecoveryMissingEndif()
        {
            string script = @"---
<<if $condition>>
    内容1
<<elif $other>>
    内容2
// 缺少 <<endif>>
===";

            // 测试错误恢复机制
            Assert.Throws<InvalidOperationException>(() => ParseScript(script), 
                "缺少endif应该抛出异常");
        }

        [Test]
        public void TestDeepNestedChoicesWithConditions()
        {
            string script = @"---
-> 第一层选择 <<if $level1>>
    -> 第二层选择 <<if $level2>>
        -> 第三层选择 <<if $level3>>
            最终内容
===";

            var ast = ParseScript(script);
            
            var choice1 = ast.Nodes[0].Content[0] as ChoiceNode;
            Assert.IsNotNull(choice1, "第1层应该是选择节点");
            Assert.IsNotNull(choice1.Condition, "第1层应该有条件");
            Assert.AreEqual(1, choice1.Content.Count, "第1层应该有嵌套内容");
            
            var choice2 = choice1.Content[0] as ChoiceNode;
            Assert.IsNotNull(choice2, "第2层应该是选择节点");
            Assert.IsNotNull(choice2.Condition, "第2层应该有条件");
            
            var choice3 = choice2.Content[0] as ChoiceNode;
            Assert.IsNotNull(choice3, "第3层应该是选择节点");
            Assert.IsNotNull(choice3.Condition, "第3层应该有条件");
        }

        [Test]
        public void TestVariousCommandTypes()
        {
            string script = @"---
<<var $hp 100>>
<<set $mp = $maxMp>>
<<add $exp = calculateExp(10)>>
<<sub $gold ($item.price * $quantity)>>
<<mul $damage = $baseDamage>>
<<div $result $total>>
<<mod $remainder ($value % 10)>>
===";

            var ast = ParseScript(script);
            
            var node = ast.Nodes[0];
            Assert.AreEqual(7, node.Content.Count, "应该有7个命令");
            
            var commands = node.Content.Cast<VarCommandNode>().ToList();
            var expectedOps = new[] { "var", "set", "add", "sub", "mul", "div", "mod" };
            
            for (int i = 0; i < expectedOps.Length; i++)
            {
                Assert.AreEqual(expectedOps[i], commands[i].Operation, $"第{i+1}个命令应该是{expectedOps[i]}");
            }
        }

        [Test]
        public void TestComplexMetadataHandling()
        {
            string script = @"node: complex_test_node
description: 这是一个复杂的测试节点
author: 测试作者
version: 1.0.0
tags: test, complex, metadata
empty_value: 
unicode_desc: 🌟复杂的Unicode描述🌟
---
节点内容
===";

            var ast = ParseScript(script);
            
            var node = ast.Nodes[0];
            Assert.AreEqual("complex_test_node", node.NodeName, "节点名称应该正确");
            Assert.AreEqual(7, node.Metadata.Count, "应该有7个元数据项");
            
            Assert.AreEqual("这是一个复杂的测试节点", node.Metadata["description"], "描述元数据应该正确");
            Assert.AreEqual("测试作者", node.Metadata["author"], "作者元数据应该正确");
            Assert.AreEqual("1.0.0", node.Metadata["version"], "版本元数据应该正确");
            Assert.AreEqual("test, complex, metadata", node.Metadata["tags"], "标签元数据应该正确");
            Assert.AreEqual("", node.Metadata["empty_value"], "空值元数据应该正确");
            Assert.AreEqual("🌟复杂的Unicode描述🌟", node.Metadata["unicode_desc"], "Unicode元数据应该正确");
        }

        [Test]
        public void TestElifChaining()
        {
            string script = @"---
<<if $score >= 90>>
    优秀
<<elif $score >= 80>>
    良好
<<elif $score >= 70>>
    中等
<<elif $score >= 60>>
    及格
<<else>>
    不及格
<<endif>>
===";

            var ast = ParseScript(script);
            
            var condition = ast.Nodes[0].Content[0] as ConditionNode;
            Assert.IsNotNull(condition, "应该是条件节点");
            Assert.IsNotNull(condition.ThenBranch, "应该有then分支");
            Assert.AreEqual(3, condition.ElifBranches.Count, "应该有3个elif分支");
            Assert.IsNotNull(condition.ElseBranch, "应该有else分支");
            
            // 验证每个elif条件
            for (int i = 0; i < condition.ElifBranches.Count; i++)
            {
                var elifBranch = condition.ElifBranches[i];
                Assert.IsNotNull(elifBranch.Condition, $"第{i+1}个elif应该有条件");
                Assert.IsTrue(elifBranch.Content.Count > 0, $"第{i+1}个elif应该有内容");
            }
        }

        [Test]
        public void TestAutoLineTagGeneration()
        {
            string script = @"node: tag_test
---
角色A: 第一句话
-> 选项1
    嵌套内容
角色B: 第二句话
:旁白内容
<<if $condition>>
    条件内容
<<endif>>
===";

            var ast = ParseScript(script);
            
            var node = ast.Nodes[0];
            var dialogueNodes = new List<DialogueNode>();
            
            // 收集所有对话节点（包括嵌套的）
            CollectDialogueNodes(node.Content, dialogueNodes);
            
            // 验证每个对话节点都有自动行标签
            foreach (var dialogue in dialogueNodes)
            {
                var lineTags = dialogue.Tags.Where(tag => tag.StartsWith("line:tag_test")).ToList();
                Assert.AreEqual(1, lineTags.Count, "每个对话节点应该有且仅有一个自动行标签");
            }
        }

        private void CollectDialogueNodes(List<ContentNode> content, List<DialogueNode> dialogues)
        {
            foreach (var node in content)
            {
                if (node is DialogueNode dialogue)
                {
                    dialogues.Add(dialogue);
                    if (dialogue.Content?.Count > 0)
                    {
                        CollectDialogueNodes(dialogue.Content, dialogues);
                    }
                }
                else if (node is ChoiceNode choice && choice.Content?.Count > 0)
                {
                    CollectDialogueNodes(choice.Content, dialogues);
                }
                else if (node is ConditionNode condition)
                {
                    if (condition.ThenBranch?.Count > 0)
                        CollectDialogueNodes(condition.ThenBranch, dialogues);
                    
                    foreach (var elif in condition.ElifBranches ?? new())
                    {
                        if (elif.Content?.Count > 0)
                            CollectDialogueNodes(elif.Content, dialogues);
                    }
                    
                    if (condition.ElseBranch?.Count > 0)
                        CollectDialogueNodes(condition.ElseBranch, dialogues);
                }
            }
        }

        [Test]
        public void TestMaxNestingLevelWarning()
        {
            // 创建一个超过10层嵌套的脚本
            var script = new StringBuilder();
            script.AppendLine("---");
            
            for (int i = 0; i < 12; i++)
            {
                script.Append(new string(' ', i * 4));
                script.AppendLine($"-> 层级{i + 1}");
            }
            
            script.Append(new string(' ', 12 * 4));
            script.AppendLine("最深层内容");
            script.AppendLine("===");

            // 这个测试主要确保深层嵌套不会导致崩溃
            var ast = ParseScript(script.ToString());
            Assert.IsNotNull(ast, "深层嵌套不应导致解析失败");
            Assert.AreEqual(1, ast.Nodes.Count, "应该有1个节点");
        }

        [Test]
        public void TestEmptyExpressionHandling()
        {
            string script = @"---
<<set $empty = >>
===";

            // 测试空表达式的处理
            Assert.Throws<InvalidOperationException>(() => ParseScript(script), 
                "空表达式应该抛出异常");
        }

        [Test]
        public void TestInvalidCommandHandling()
        {
            string script = @"---
<<unknownCommand $param>>
===";

            // 测试未知命令的处理
            Assert.Throws<InvalidOperationException>(() => ParseScript(script), 
                "未知命令应该抛出异常");
        }

        [Test]
        public void TestComplexStringEscaping()
        {
            string script = @"---
<<set $msg = ""转义测试: \""引号\"" \\ 反斜杠 \{ 大括号 \: 冒号 \# 井号"">>
===";

            var ast = ParseScript(script);
            
            var setCommand = ast.Nodes[0].Content[0] as VarCommandNode;
            var stringExpr = setCommand.Value as StringInterpolationExpressionNode;
            
            Assert.IsNotNull(stringExpr, "应该是字符串插值表达式");
            
            // 验证转义字符被正确处理
            var textSegments = stringExpr.Segments.OfType<TextNode>().ToList();
            var combinedText = string.Join("", textSegments.Select(t => t.Text));
            
            Assert.IsTrue(combinedText.Contains("\"引号\""), "应该正确处理转义的引号");
            Assert.IsTrue(combinedText.Contains("\\ 反斜杠"), "应该正确处理转义的反斜杠");
        }

        [Test]
        public void TestLineEndingVariations()
        {
            // 测试不同的换行符组合
            string scriptWindows = "node: test\r\n---\r\n内容\r\n===";
            string scriptUnix = "node: test\n---\n内容\n===";
            string scriptMac = "node: test\r---\r内容\r===";

            var astWindows = ParseScript(scriptWindows);
            var astUnix = ParseScript(scriptUnix);
            var astMac = ParseScript(scriptMac);

            Assert.AreEqual(1, astWindows.Nodes.Count, "Windows换行符应该正确解析");
            Assert.AreEqual(1, astUnix.Nodes.Count, "Unix换行符应该正确解析");
            Assert.AreEqual(1, astMac.Nodes.Count, "Mac换行符应该正确解析");
        }
    }
}