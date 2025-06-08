using System.Collections.Generic;
using MookDialogueScript;
using NUnit.Framework;
using System.Linq;

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
        public void TestBasicNodeDefinition()
        {
            string source = "--- 开始节点\n这是一段旁白描述。\n===";
            _parser = SetupParser(source);
            ScriptNode script = _parser.Parse();

            Assert.That(script.Nodes.Count, Is.EqualTo(1));
            NodeDefinitionNode node = script.Nodes[0];
            Assert.That(node.NodeName, Is.EqualTo("开始节点"));
            Assert.That(node.Content.Count, Is.EqualTo(1));
            Assert.That(node.Content[0], Is.TypeOf<DialogueNode>());

            DialogueNode dialogue = node.Content[0] as DialogueNode;
            Assert.That(dialogue.Speaker, Is.Null); // 旁白没有说话者
            Assert.That(dialogue.Text.Count, Is.EqualTo(2));
            Assert.That(dialogue.Text[0], Is.TypeOf<TextNode>());
            Assert.That((dialogue.Text[0] as TextNode).Text, Is.EqualTo("这是一段旁白描述"));
            Assert.That(dialogue.Text[1], Is.TypeOf<TextNode>());
            Assert.That((dialogue.Text[1] as TextNode).Text, Is.EqualTo("。"));
        }

        [Test]
        public void TestMultipleNodes()
        {
            string source = "--- 节点1\n旁白1\n===\n--- 节点2\n旁白2\n===";
            _parser = SetupParser(source);
            ScriptNode script = _parser.Parse();

            Assert.That(script.Nodes.Count, Is.EqualTo(2));
            Assert.That(script.Nodes[0].NodeName, Is.EqualTo("节点1"));
            Assert.That(script.Nodes[1].NodeName, Is.EqualTo("节点2"));
        }

        [Test]
        public void TestDialogue()
        {
            string source = "--- 对话节点\n小明：你好，世界！\n===";
            _parser = SetupParser(source);
            ScriptNode script = _parser.Parse();

            Assert.That(script.Nodes.Count, Is.EqualTo(1));
            NodeDefinitionNode node = script.Nodes[0];
            Assert.That(node.Content.Count, Is.EqualTo(1));
            Assert.That(node.Content[0], Is.TypeOf<DialogueNode>());

            DialogueNode dialogue = node.Content[0] as DialogueNode;
            Assert.That(dialogue.Speaker, Is.EqualTo("小明"));
            Assert.That(dialogue.Text.Count, Is.EqualTo(1));
            Assert.That(dialogue.Text[0], Is.TypeOf<TextNode>());
            Assert.That((dialogue.Text[0] as TextNode).Text, Is.EqualTo("你好，世界！"));
        }

        [Test]
        public void TestChoice()
        {
            string source = "--- 选择节点\n-> 选项1\n    选项1的内容\n-> 选项2\n    选项2的内容\n===";
            _parser = SetupParser(source);
            ScriptNode script = _parser.Parse();

            NodeDefinitionNode node = script.Nodes[0];
            Assert.That(node.Content.Count, Is.EqualTo(2));
            Assert.That(node.Content[0], Is.TypeOf<ChoiceNode>());
            Assert.That(node.Content[1], Is.TypeOf<ChoiceNode>());

            ChoiceNode choice1 = node.Content[0] as ChoiceNode;
            ChoiceNode choice2 = node.Content[1] as ChoiceNode;

            Assert.That(choice1.Text.Count, Is.EqualTo(1));
            Assert.That((choice1.Text[0] as TextNode).Text, Is.EqualTo(" 选项1"));
            Assert.That(choice1.Content.Count, Is.EqualTo(1));
            Assert.That(choice1.Content[0], Is.TypeOf<DialogueNode>());

            Assert.That(choice2.Text.Count, Is.EqualTo(1));
            Assert.That((choice2.Text[0] as TextNode).Text, Is.EqualTo(" 选项2"));
            Assert.That(choice2.Content.Count, Is.EqualTo(1));
            Assert.That(choice2.Content[0], Is.TypeOf<DialogueNode>());
        }

        [Test]
        public void TestConditionalChoice()
        {
            string source = "--- 条件选择\n-> 选项1 [if $condition]\n    选择了选项1\n===";
            _parser = SetupParser(source);
            ScriptNode script = _parser.Parse();

            ChoiceNode choice = script.Nodes[0].Content[0] as ChoiceNode;
            Assert.That(choice.Condition, Is.Not.Null);
            Assert.That(choice.Condition, Is.TypeOf<VariableNode>());
            Assert.That((choice.Condition as VariableNode).Name, Is.EqualTo("condition"));
        }

        [Test]
        public void TestCondition()
        {
            string source = "--- 条件节点\nif $flag\n    这是满足条件的内容\nelse\n    这是不满足条件的内容\nendif\n===";
            _parser = SetupParser(source);
            ScriptNode script = _parser.Parse();

            Assert.That(script.Nodes[0].Content[0], Is.TypeOf<ConditionNode>());
            ConditionNode condition = script.Nodes[0].Content[0] as ConditionNode;

            Assert.That(condition.Condition, Is.TypeOf<VariableNode>());
            Assert.That((condition.Condition as VariableNode).Name, Is.EqualTo("flag"));
            Assert.That(condition.ThenBranch.Count, Is.EqualTo(1));
            Assert.That(condition.ElseBranch.Count, Is.EqualTo(1));
            Assert.That(condition.ElifBranches.Count, Is.EqualTo(0));
        }

        [Test]
        public void TestElifCondition()
        {
            string source = "--- 多条件\nif $age < 18\n    未成年\nelif $age < 60\n    成年人\nelse\n    老年人\nendif\n===";
            _parser = SetupParser(source);
            ScriptNode script = _parser.Parse();

            ConditionNode condition = script.Nodes[0].Content[0] as ConditionNode;
            Assert.That(condition.Condition, Is.TypeOf<BinaryOpNode>());
            Assert.That(condition.ThenBranch.Count, Is.EqualTo(1));
            Assert.That(condition.ElifBranches.Count, Is.EqualTo(1));
            Assert.That(condition.ElseBranch.Count, Is.EqualTo(1));

            var elifCondition = condition.ElifBranches[0].Condition;
            Assert.That(elifCondition, Is.TypeOf<BinaryOpNode>());
        }

        [Test]
        public void TestMetadata()
        {
            string source = "--- 测试节点\n[title:真正的标题]\n[author:测试作者]\n[version:1.0]\n这是一段旁白。\n===";
            _parser = SetupParser(source);
            ScriptNode script = _parser.Parse();

            Assert.That(script.Nodes.Count, Is.EqualTo(1));
            NodeDefinitionNode node = script.Nodes[0];

            // 检查节点名称是否被title元数据替换
            Assert.That(node.NodeName, Is.EqualTo("真正的标题"));

            // 检查元数据字典
            Assert.That(node.Metadata.Count, Is.EqualTo(3));
            Assert.That(node.Metadata["title"], Is.EqualTo("真正的标题"));
            Assert.That(node.Metadata["author"], Is.EqualTo("测试作者"));
            Assert.That(node.Metadata["version"], Is.EqualTo("1.0"));

            // 检查节点内容
            Assert.That(node.Content.Count, Is.EqualTo(1));
            Assert.That(node.Content[0], Is.TypeOf<DialogueNode>());
        }

        [Test]
        public void TestMetadataWithoutTitle()
        {
            string source = "--- 测试节点\n[category:开场]\n[tags:\"重要,教程\"]\n这是没有title元数据的节点。\n===";
            _parser = SetupParser(source);
            ScriptNode script = _parser.Parse();

            NodeDefinitionNode node = script.Nodes[0];

            // 检查节点名称是否保持原样
            Assert.That(node.NodeName, Is.EqualTo("测试节点"));

            // 检查元数据字典
            Assert.That(node.Metadata.Count, Is.EqualTo(2));
            Assert.That(node.Metadata["category"], Is.EqualTo("开场"));
            Assert.That(node.Metadata["tags"], Is.EqualTo("重要,教程"));
        }

        [Test]
        public void TestExpressions()
        {
            string source = "--- 表达式测试\nset $result = 10 + 5 * 2\n===";
            _parser = SetupParser(source);
            ScriptNode script = _parser.Parse();

            Assert.That(script.Nodes[0].Content[0], Is.TypeOf<VarCommandNode>());
            VarCommandNode varCmd = script.Nodes[0].Content[0] as VarCommandNode;

            Assert.That(varCmd.Variable, Is.EqualTo("result"));
            Assert.That(varCmd.Operation, Is.EqualTo("set"));
            Assert.That(varCmd.Value, Is.TypeOf<BinaryOpNode>());

            BinaryOpNode expr = varCmd.Value as BinaryOpNode;
            Assert.That(expr.Operator, Is.EqualTo("+"));
            Assert.That(expr.Left, Is.TypeOf<NumberNode>());
            Assert.That(expr.Right, Is.TypeOf<BinaryOpNode>());

            Assert.That((expr.Left as NumberNode).Value, Is.EqualTo(10));

            BinaryOpNode rightExpr = expr.Right as BinaryOpNode;
            Assert.That(rightExpr.Operator, Is.EqualTo("*"));
            Assert.That((rightExpr.Left as NumberNode).Value, Is.EqualTo(5));
            Assert.That((rightExpr.Right as NumberNode).Value, Is.EqualTo(2));
        }

        [Test]
        public void TestBooleanExpressions()
        {
            string source = "--- 布尔表达式\nif $a > 5 and $b < 10\n    条件满足\nendif\n===";
            _parser = SetupParser(source);
            ScriptNode script = _parser.Parse();

            ConditionNode condition = script.Nodes[0].Content[0] as ConditionNode;
            Assert.That(condition.Condition, Is.TypeOf<BinaryOpNode>());

            BinaryOpNode expr = condition.Condition as BinaryOpNode;
            Assert.That(expr.Operator, Is.EqualTo("and"));
            Assert.That(expr.Left, Is.TypeOf<BinaryOpNode>());
            Assert.That(expr.Right, Is.TypeOf<BinaryOpNode>());
        }

        [Test]
        public void TestVarCommand()
        {
            string source = "--- 变量命令\nvar $newVar = 100\nset $existingVar = 200\nadd $counter = 1\nsub $counter = 1\n===";
            _parser = SetupParser(source);
            ScriptNode script = _parser.Parse();

            NodeDefinitionNode node = script.Nodes[0];
            Assert.That(node.Content.Count, Is.EqualTo(4));

            // 检查var命令
            VarCommandNode varCmd = node.Content[0] as VarCommandNode;
            Assert.That(varCmd.Operation, Is.EqualTo("var"));
            Assert.That(varCmd.Variable, Is.EqualTo("newVar"));
            Assert.That((varCmd.Value as NumberNode).Value, Is.EqualTo(100));

            // 检查set命令
            VarCommandNode setCmd = node.Content[1] as VarCommandNode;
            Assert.That(setCmd.Operation, Is.EqualTo("set"));
            Assert.That(setCmd.Variable, Is.EqualTo("existingVar"));
            Assert.That((setCmd.Value as NumberNode).Value, Is.EqualTo(200));

            // 检查add命令
            VarCommandNode addCmd = node.Content[2] as VarCommandNode;
            Assert.That(addCmd.Operation, Is.EqualTo("add"));
            Assert.That(addCmd.Variable, Is.EqualTo("counter"));
            Assert.That((addCmd.Value as NumberNode).Value, Is.EqualTo(1));

            // 检查sub命令
            VarCommandNode subCmd = node.Content[3] as VarCommandNode;
            Assert.That(subCmd.Operation, Is.EqualTo("sub"));
            Assert.That(subCmd.Variable, Is.EqualTo("counter"));
            Assert.That((subCmd.Value as NumberNode).Value, Is.EqualTo(1));
        }

        [Test]
        public void TestCallCommand()
        {
            string source = "--- 函数调用\ncall PlaySound(\"explosion\", 1.0)\n===";
            _parser = SetupParser(source);
            ScriptNode script = _parser.Parse();

            CallCommandNode callCmd = script.Nodes[0].Content[0] as CallCommandNode;
            Assert.That(callCmd.FunctionName, Is.EqualTo("PlaySound"));
            Assert.That(callCmd.Parameters.Count, Is.EqualTo(2));
            Assert.That(callCmd.Parameters[0], Is.TypeOf<StringInterpolationExpressionNode>());
            Assert.That(callCmd.Parameters[1], Is.TypeOf<NumberNode>());
            Assert.That(((callCmd.Parameters[0] as StringInterpolationExpressionNode).Segments[0] as TextNode).Text, Is.EqualTo("explosion"));
            Assert.That((callCmd.Parameters[1] as NumberNode).Value, Is.EqualTo(1.0));
        }

        [Test]
        public void TestJumpCommand()
        {
            string source = "--- 开始\n=> 结束\n===\n--- 结束\n这是结束节点\n===";
            _parser = SetupParser(source);
            ScriptNode script = _parser.Parse();

            JumpCommandNode jumpCmd = script.Nodes[0].Content[0] as JumpCommandNode;
            Assert.That(jumpCmd.TargetNode, Is.EqualTo("结束"));
        }

        [Test]
        public void TestWaitCommand()
        {
            string source = "--- 等待测试\nwait 2.5\n===";
            _parser = SetupParser(source);
            ScriptNode script = _parser.Parse();

            WaitCommandNode waitCmd = script.Nodes[0].Content[0] as WaitCommandNode;
            Assert.That(waitCmd.Duration, Is.TypeOf<NumberNode>());
            Assert.That((waitCmd.Duration as NumberNode).Value, Is.EqualTo(2.5));
        }

        [Test]
        public void TestStringInterpolation()
        {
            string source = "--- 字符串插值\n小明：你好，{$name}！\n===";
            _parser = SetupParser(source);
            ScriptNode script = _parser.Parse();

            DialogueNode dialogue = script.Nodes[0].Content[0] as DialogueNode;
            Assert.That(dialogue.Text.Count, Is.EqualTo(3));
            Assert.That(dialogue.Text[0], Is.TypeOf<TextNode>());
            Assert.That(dialogue.Text[1], Is.TypeOf<InterpolationNode>());
            Assert.That(dialogue.Text[2], Is.TypeOf<TextNode>());

            Assert.That((dialogue.Text[0] as TextNode).Text, Is.EqualTo("你好，"));
            Assert.That(((dialogue.Text[1] as InterpolationNode).Expression as VariableNode).Name, Is.EqualTo("name"));
            Assert.That((dialogue.Text[2] as TextNode).Text, Is.EqualTo("！"));
        }

        [Test]
        public void TestNestedContent()
        {
            string source = "--- 嵌套内容\n小明：你好\n    if $flag\n        这是条件内容\n    endif\n===";
            _parser = SetupParser(source);
            ScriptNode script = _parser.Parse();

            DialogueNode dialogue = script.Nodes[0].Content[0] as DialogueNode;
            Assert.That(dialogue.Content.Count, Is.EqualTo(1));
            Assert.That(dialogue.Content[0], Is.TypeOf<ConditionNode>());

            ConditionNode condition = dialogue.Content[0] as ConditionNode;
            Assert.That(condition.ThenBranch.Count, Is.EqualTo(1));
            Assert.That(condition.ThenBranch[0], Is.TypeOf<DialogueNode>());
        }

        [Test]
        public void TestDialogueWithHashTags()
        {
            // 注意：词法分析器会将冒号和引号分割为独立的标记，语法分析器需要正确处理
            string source = "--- 带标签的对话\n小明：你好！ #角色 #情绪:高兴 #ID:\"NPC001\"\n===";
            _parser = SetupParser(source);
            ScriptNode script = _parser.Parse();

            Assert.That(script.Nodes.Count, Is.EqualTo(1));
            NodeDefinitionNode node = script.Nodes[0];
            Assert.That(node.Content.Count, Is.EqualTo(1));

            DialogueNode dialogue = node.Content[0] as DialogueNode;
            Assert.That(dialogue.Speaker, Is.EqualTo("小明"));
            Assert.That(dialogue.Text.Count, Is.GreaterThan(0));
            Assert.That((dialogue.Text[0] as TextNode).Text, Is.EqualTo("你好！ "));

            // 测试标签是否被正确解析 - 语法分析器应该能够合并被分割的标记
            Assert.That(dialogue.Labels.Count, Is.EqualTo(3), "标签数量不正确");
            Assert.That(dialogue.Labels[0], Is.EqualTo("角色"), "第一个标签不正确");
            Assert.That(dialogue.Labels[1], Is.EqualTo("情绪:高兴"), "第二个标签不正确（带冒号的标签）");
            Assert.That(dialogue.Labels[2], Is.EqualTo("ID:\"NPC001\""), "第三个标签不正确（带引号的标签）");
        }

        [Test]
        public void TestDialogueWithMultipleQuotedHashTags()
        {
            // 词法分析器会将引号等特殊字符分割为独立的标记
            // 但语法分析器ParseDialogue方法应该能够正确合并这些标记
            string source = "--- 多标签引号测试\n旁白：这是一段有\"引号\"的文本。 #标签1 #标签\"引号内容\"标签 #标签'单引号'标签\n===";
            _parser = SetupParser(source);
            ScriptNode script = _parser.Parse();

            DialogueNode dialogue = script.Nodes[0].Content[0] as DialogueNode;

            // 测试带有不同引号类型的标签
            Assert.That(dialogue.Labels.Count, Is.EqualTo(3), "标签数量不正确");
            Assert.That(dialogue.Labels[0], Is.EqualTo("标签1"));
            Assert.That(dialogue.Labels[1], Is.EqualTo("标签\"引号内容\"标签"), "带双引号的标签解析不正确");
            Assert.That(dialogue.Labels[2], Is.EqualTo("标签'单引号'标签"), "带单引号的标签解析不正确");
        }

        [Test]
        public void TestHashInString()
        {
            // 词法分析器会将字符串中的内容单独分割，但在字符串模式下不会进入标签模式
            // 语法分析器需要正确区分字符串中的#号和实际标签
            string source = "--- 字符串中的井号\n\"这里有一个#号符号，不是标签\" #这才是标签\n===";
            _parser = SetupParser(source);
            ScriptNode script = _parser.Parse();

            DialogueNode dialogue = script.Nodes[0].Content[0] as DialogueNode;

            // 检查文本中的#号被正确处理为普通字符
            string combinedText = string.Join("", dialogue.Text.Where(t => t is TextNode).Select(t => (t as TextNode).Text));
            Assert.That(combinedText, Does.Contain("#号符号"));

            // 检查#标签被正确识别
            Assert.That(dialogue.Labels.Count, Is.EqualTo(1));
            Assert.That(dialogue.Labels[0], Is.EqualTo("这才是标签"));
        }

        [Test]
        public void TestHashInDialogueText()
        {
            // 在对话文本中，#号会被词法分析器作为标签起始标记处理
            // 语法分析器需要正确将这些标记组合为完整的标签
            string source = "--- 对话文本中的井号\n小明：我的手机号码是#123456，记得保存哦 #联系人 #重要\n===";
            _parser = SetupParser(source);
            ScriptNode script = _parser.Parse();

            DialogueNode dialogue = script.Nodes[0].Content[0] as DialogueNode;

            // 检查对话文本中的#号（没有被引号包裹）会被识别为标签开始
            string combinedText = string.Join("", dialogue.Text.Where(t => t is TextNode).Select(t => (t as TextNode).Text));
            Assert.That(combinedText, Is.EqualTo("我的手机号码是"));

            // 三个标签应该被正确识别
            Assert.That(dialogue.Labels.Count, Is.EqualTo(3));
            Assert.That(dialogue.Labels[0], Is.EqualTo("123456，记得保存哦"));
            Assert.That(dialogue.Labels[1], Is.EqualTo("联系人"));
            Assert.That(dialogue.Labels[2], Is.EqualTo("重要"));
        }
    }
}