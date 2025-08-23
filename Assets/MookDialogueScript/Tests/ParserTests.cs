using System.Collections.Generic;
using MookDialogueScript;
using NUnit.Framework;
using System.Linq;
using UnityEngine;

namespace Tests
{
    public class ParserTests
    {
        private Parser _parser;

        [SetUp]
        public void Setup()
        {
            _parser = null;
        }

        private Parser SetupParser(string source)
        {
            Lexer lexer = new Lexer(source);
            List<Token> tokens = lexer.Tokenize();
            foreach (var token in tokens)
            {
                Debug.Log(token.ToString());
            }
            return new Parser(tokens);
        }

        [Test]
        public void TestEmptyScript()
        {
            _parser = SetupParser("");
            ScriptNode script = _parser.Parse();
            Assert.That(script, Is.Not.Null);
            Assert.That(script.Nodes, Is.Not.Null);
            Assert.That(script.Nodes.Count, Is.EqualTo(0));
        }

        [Test]
        public void TestNodeWithMetadata()
        {
            // 测试新语法：元数据前置
            string source = "node: start_node\nauthor: test_author\n---\n这是旁白内容\n===";
            _parser = SetupParser(source);
            ScriptNode script = _parser.Parse();

            Assert.That(script.Nodes.Count, Is.EqualTo(1));
            NodeDefinitionNode node = script.Nodes[0];
            
            // 检查节点名和元数据
            Assert.That(node.NodeName, Is.EqualTo("start_node"));
            Assert.That(node.Metadata.ContainsKey("node"), Is.True);
            Assert.That(node.Metadata.ContainsKey("author"), Is.True);
            Assert.That(node.Metadata["node"], Is.EqualTo("start_node"));
            Assert.That(node.Metadata["author"], Is.EqualTo("test_author"));

            // 检查内容
            Assert.That(node.Content.Count, Is.EqualTo(1));
            Assert.That(node.Content[0], Is.TypeOf<DialogueNode>());
            
            DialogueNode dialogue = node.Content[0] as DialogueNode;
            Assert.That(dialogue.Speaker, Is.Null); // 旁白没有说话者
            Assert.That(dialogue.Tags.Count, Is.GreaterThan(0)); // 应该有自动生成的行标签
        }

        [Test]
        public void TestBasicNodeWithoutMetadata()
        {
            // 测试没有元数据的基本节点
            string source = "---\n旁白文本内容\n===";
            _parser = SetupParser(source);
            ScriptNode script = _parser.Parse();

            Assert.That(script.Nodes.Count, Is.EqualTo(1));
            NodeDefinitionNode node = script.Nodes[0];
            Assert.That(node.NodeName, Is.EqualTo("unnamed")); // 默认节点名
            Assert.That(node.Content.Count, Is.EqualTo(1));
        }

        [Test]
        public void TestDialogueWithAutoTags()
        {
            // 测试角色对话和自动标签生成
            string source = "node: dialogue_test\n---\n角色: 你好，世界！ #manual_tag\n===";
            _parser = SetupParser(source);
            ScriptNode script = _parser.Parse();

            DialogueNode dialogue = script.Nodes[0].Content[0] as DialogueNode;
            Assert.That(dialogue.Speaker, Is.EqualTo("角色"));
            Assert.That(dialogue.Text.Count, Is.GreaterThan(0));
            
            // 检查自动生成的标签和手动标签
            Assert.That(dialogue.Tags.Count, Is.EqualTo(2)); // manual_tag + 自动生成的行标签
            Assert.That(dialogue.Tags.Contains("manual_tag"), Is.True);
            
            // 检查自动生成的行标签格式
            var lineTag = dialogue.Tags.Find(t => t.StartsWith("line:"));
            Assert.That(lineTag, Is.Not.Null);
            Assert.That(lineTag, Is.EqualTo("line:dialogue_test1")); // 第一行
        }

        [Test]
        public void TestUnifiedCommandSyntax()
        {
            // 测试新的统一命令语法 <<>>
            string source = "---\n<<set $var 100>>\n<<if $var > 50>>\n内容\n<<endif>>\n===";
            _parser = SetupParser(source);
            ScriptNode script = _parser.Parse();

            NodeDefinitionNode node = script.Nodes[0];
            Assert.That(node.Content.Count, Is.EqualTo(2));
            
            // 检查set命令
            Assert.That(node.Content[0], Is.TypeOf<VarCommandNode>());
            VarCommandNode setCmd = node.Content[0] as VarCommandNode;
            Assert.That(setCmd.Operation, Is.EqualTo("set"));
            Assert.That(setCmd.Variable, Is.EqualTo("var"));
            
            // 检查条件语句
            Assert.That(node.Content[1], Is.TypeOf<ConditionNode>());
            ConditionNode condition = node.Content[1] as ConditionNode;
            Assert.That(condition.Condition, Is.TypeOf<BinaryOpNode>());
        }

        [Test]
        public void TestChoiceWithCondition()
        {
            // 测试带条件的选项（新语法）
            string source = "---\n-> 选项1 <<if $flag>>\n    选项内容\n===";
            _parser = SetupParser(source);
            ScriptNode script = _parser.Parse();

            ChoiceNode choice = script.Nodes[0].Content[0] as ChoiceNode;
            Assert.That(choice.Condition, Is.Not.Null);
            Assert.That(choice.Condition, Is.TypeOf<VariableNode>());
            Assert.That((choice.Condition as VariableNode).Name, Is.EqualTo("flag"));
            Assert.That(choice.Content.Count, Is.EqualTo(1)); // 嵌套内容
        }

        [Test]
        public void TestNestedContent()
        {
            // 测试嵌套内容解析
            string source = "---\n角色: 主对话\n    嵌套对话1\n    嵌套对话2\n        更深嵌套\n===";
            _parser = SetupParser(source);
            ScriptNode script = _parser.Parse();

            DialogueNode dialogue = script.Nodes[0].Content[0] as DialogueNode;
            Assert.That(dialogue.Speaker, Is.EqualTo("角色"));
            Assert.That(dialogue.Content, Is.Not.Null);
            Assert.That(dialogue.Content.Count, Is.GreaterThan(0)); // 应该有嵌套内容
            
            // 检查嵌套的对话节点
            Assert.That(dialogue.Content[0], Is.TypeOf<DialogueNode>());
            DialogueNode nestedDialogue = dialogue.Content[1] as DialogueNode;
            Assert.That(nestedDialogue.Content, Is.Not.Null);
            Assert.That(nestedDialogue.Content.Count, Is.GreaterThan(0)); // 更深层嵌套
        }

        [Test]
        public void TestMultipleConditions()
        {
            // 测试elif分支
            string source = "---\n<<if $age < 18>>\n    未成年\n<<elif $age < 60>>\n    成年\n<<else>>\n    老年\n<<endif>>\n===";
            _parser = SetupParser(source);
            ScriptNode script = _parser.Parse();

            ConditionNode condition = script.Nodes[0].Content[0] as ConditionNode;
            Assert.That(condition.ThenBranch.Count, Is.EqualTo(1));
            Assert.That(condition.ElifBranches.Count, Is.EqualTo(1));
            Assert.That(condition.ElseBranch.Count, Is.EqualTo(1));
            
            // 检查elif条件
            var elifCondition = condition.ElifBranches[0].Condition;
            Assert.That(elifCondition, Is.TypeOf<BinaryOpNode>());
        }

        [Test]
        public void TestVariableInterpolation()
        {
            // 测试变量插值
            string source = "---\n角色: 你好，{$name}！你有{$gold}金币。\n===";
            _parser = SetupParser(source);
            ScriptNode script = _parser.Parse();

            DialogueNode dialogue = script.Nodes[0].Content[0] as DialogueNode;
            Assert.That(dialogue.Text.Count, Is.EqualTo(6)); // "你好，" + {$name} + "！你有" + {$gold} + "金币。"
            Assert.That(dialogue.Text[1], Is.TypeOf<InterpolationNode>());
            Assert.That(dialogue.Text[3], Is.TypeOf<InterpolationNode>());
            
            InterpolationNode nameInterp = dialogue.Text[1] as InterpolationNode;
            InterpolationNode goldInterp = dialogue.Text[3] as InterpolationNode;
            
            Assert.That(nameInterp.Expression, Is.TypeOf<VariableNode>());
            Assert.That((nameInterp.Expression as VariableNode).Name, Is.EqualTo("name"));
            
            Assert.That(goldInterp.Expression, Is.TypeOf<VariableNode>());
            Assert.That((goldInterp.Expression as VariableNode).Name, Is.EqualTo("gold"));
        }

        [Test]
        public void TestFunctionCall()
        {
            // 测试函数调用
            string source = "---\n<<PlaySound(\"bell\", 1.0)>>\n===";
            _parser = SetupParser(source);
            ScriptNode script = _parser.Parse();

            CallCommandNode callCmd = script.Nodes[0].Content[0] as CallCommandNode;
            Assert.That(callCmd.FunctionName, Is.EqualTo("playsound"));
            Assert.That(callCmd.Parameters.Count, Is.EqualTo(2));
            
            // 检查参数类型
            Assert.That(callCmd.Parameters[0], Is.TypeOf<StringInterpolationExpressionNode>());
            Assert.That(callCmd.Parameters[1], Is.TypeOf<NumberNode>());
        }

        [Test]
        public void TestJumpCommand()
        {
            // 测试跳转命令
            string source = "---\n<<jump target_node>>\n===";
            _parser = SetupParser(source);
            ScriptNode script = _parser.Parse();

            JumpCommandNode jumpCmd = script.Nodes[0].Content[0] as JumpCommandNode;
            Assert.That(jumpCmd.TargetNode, Is.EqualTo("target_node"));
        }

        [Test]
        public void TestWaitCommand()
        {
            // 测试等待命令
            string source = "---\n<<wait 2.5>>\n===";
            _parser = SetupParser(source);
            ScriptNode script = _parser.Parse();

            WaitCommandNode waitCmd = script.Nodes[0].Content[0] as WaitCommandNode;
            Assert.That(waitCmd.Duration, Is.TypeOf<NumberNode>());
            Assert.That((waitCmd.Duration as NumberNode).Value, Is.EqualTo(2.5));
        }

        [Test]
        public void TestComplexExpressions()
        {
            // 测试复杂表达式的优先级
            string source = "---\n<<set $result 10 + 5 * 2>>\n===";
            _parser = SetupParser(source);
            ScriptNode script = _parser.Parse();

            VarCommandNode varCmd = script.Nodes[0].Content[0] as VarCommandNode;
            Assert.That(varCmd.Value, Is.TypeOf<BinaryOpNode>());
            
            BinaryOpNode expr = varCmd.Value as BinaryOpNode;
            Assert.That(expr.Operator, Is.EqualTo("+"));
            Assert.That(expr.Left, Is.TypeOf<NumberNode>());
            Assert.That(expr.Right, Is.TypeOf<BinaryOpNode>());
            
            // 检查乘法优先级
            BinaryOpNode rightExpr = expr.Right as BinaryOpNode;
            Assert.That(rightExpr.Operator, Is.EqualTo("*"));
        }

        [Test]
        public void TestStringWithInterpolation()
        {
            // 测试带插值的字符串
            string source = "---\n<<set $msg \"Hello {$name}!\">>\n===";
            _parser = SetupParser(source);
            ScriptNode script = _parser.Parse();

            VarCommandNode varCmd = script.Nodes[0].Content[0] as VarCommandNode;
            Assert.That(varCmd.Value, Is.TypeOf<StringInterpolationExpressionNode>());
            
            StringInterpolationExpressionNode strExpr = varCmd.Value as StringInterpolationExpressionNode;
            Assert.That(strExpr.Segments.Count, Is.EqualTo(3)); // "Hello " + {$name} + "!"
        }

        [Test]
        public void TestAutoLineTagGeneration()
        {
            // 测试自动行标签生成
            string source = "node: test_node\n---\n第一行对话\n第二行对话\n第三行对话\n===";
            _parser = SetupParser(source);
            ScriptNode script = _parser.Parse();

            NodeDefinitionNode node = script.Nodes[0];
            Assert.That(node.Content.Count, Is.EqualTo(3));
            
            // 检查每行的自动标签
            for (int i = 0; i < 3; i++)
            {
                DialogueNode dialogue = node.Content[i] as DialogueNode;
                var lineTag = dialogue.Tags.Find(t => t.StartsWith("line:"));
                Assert.That(lineTag, Is.Not.Null);
                Assert.That(lineTag, Is.EqualTo($"line:test_node{i + 1}"));
            }
        }

        [Test]
        public void TestNarrationFormats()
        {
            // 测试旁白的不同格式
            string source = "---\n:冒号前缀旁白\n普通旁白文本\n===";
            _parser = SetupParser(source);
            ScriptNode script = _parser.Parse();

            Assert.That(script.Nodes[0].Content.Count, Is.EqualTo(2));
            
            // 两种格式都应该被解析为DialogueNode（旁白）
            DialogueNode narration1 = script.Nodes[0].Content[0] as DialogueNode;
            DialogueNode narration2 = script.Nodes[0].Content[1] as DialogueNode;
            
            Assert.That(narration1.Speaker, Is.Null);
            Assert.That(narration2.Speaker, Is.Null);
        }

        [Test]
        public void TestBracketTextSupport()
        {
            // 测试中括号文本支持（新语法特性）
            string source = "---\n角色: 这里有[中括号文本]内容\n===";
            _parser = SetupParser(source);
            ScriptNode script = _parser.Parse();

            DialogueNode dialogue = script.Nodes[0].Content[0] as DialogueNode;
            Assert.That(dialogue.Text.Count, Is.GreaterThan(0));
            
            // 中括号内容应该作为普通文本被保留
            string fullText = string.Join("", dialogue.Text.Where(t => t is TextNode).Select(t => (t as TextNode).Text));
            Assert.That(fullText, Does.Contain("[中括号文本]"));
        }

        [Test]
        public void TestDeepNestedContent()
        {
            // 测试深层嵌套内容和嵌套层数警告
            string source = @"---
对话1
    嵌套1
        嵌套2
            嵌套3
                嵌套4
                    嵌套5
                        嵌套6
                            嵌套7
                                嵌套8
                                    嵌套9
                                        嵌套10
                                            嵌套11
===";
            _parser = SetupParser(source);
            ScriptNode script = _parser.Parse();

            // 应该能正常解析，但可能会有嵌套层数警告
            Assert.That(script.Nodes.Count, Is.EqualTo(1));
            Assert.That(script.Nodes[0].Content.Count, Is.EqualTo(1));
        }

        [Test]
        public void TestMultipleNodes()
        {
            // 测试多个节点的解析
            string source = @"node: first
---
第一个节点
===

node: second  
---
第二个节点
===";
            _parser = SetupParser(source);
            ScriptNode script = _parser.Parse();

            Assert.That(script.Nodes.Count, Is.EqualTo(2));
            Assert.That(script.Nodes[0].NodeName, Is.EqualTo("first"));
            Assert.That(script.Nodes[1].NodeName, Is.EqualTo("second"));
        }
    }
}