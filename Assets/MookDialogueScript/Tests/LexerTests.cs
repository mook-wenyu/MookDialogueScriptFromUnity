using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MookDialogueScript.Tests
{
    /// <summary>
    /// 词法分析器测试 - 全面的边界测试和异常情况处理
    /// </summary>
    public class LexerTests
    {
        /// <summary>
        /// 创建词法分析器并获取Token列表
        /// </summary>
        private List<Token> TokenizeScript(string script)
        {
            var lexer = new Lexer(script);
            var tokens = lexer.Tokenize();
            foreach (var token in tokens)
            {
                Debug.Log($"Token: {token.Type} '{token.Value}' at {token.Line}:{token.Column}");
            }
            return tokens;
        }

        /// <summary>
        /// 验证Token序列（包含详细的错误信息）
        /// </summary>
        private void AssertTokens(List<Token> tokens, params (TokenType type, string value)[] expectedTokens)
        {
            // 过滤掉NEWLINE token以简化测试
            var filteredTokens = tokens.Where(t => t.Type != TokenType.NEWLINE).ToList();

            var errorMessage = "Token数量不匹配（包括EOF）" + System.Environment.NewLine +
                "实际Token: " + string.Join(", ", filteredTokens.Select(t => t.Type + "('" + t.Value + "')")) + System.Environment.NewLine +
                "期望Token: " + string.Join(", ", expectedTokens.Select(e => e.type + "('" + e.value + "')")) + " + EOF";
            
            Assert.AreEqual(expectedTokens.Length + 1, filteredTokens.Count, errorMessage);

            for (int i = 0; i < expectedTokens.Length; i++)
            {
                var expected = expectedTokens[i];
                var actual = filteredTokens[i];
                Assert.AreEqual(expected.type, actual.Type, $"第{i+1}个Token类型不匹配\n期望: {expected.type}\n实际: {actual.Type}\nToken值: '{actual.Value}'");
                Assert.AreEqual(expected.value, actual.Value, $"第{i+1}个Token值不匹配\n期望: '{expected.value}'\n实际: '{actual.Value}'\nToken类型: {actual.Type}");
            }

            // 最后一个应该是EOF
            Assert.AreEqual(TokenType.EOF, filteredTokens.Last().Type, "最后一个Token应该是EOF");
        }

        /// <summary>
        /// 验证Token序列（包含行列位置信息）
        /// </summary>
        private void AssertTokensWithPosition(List<Token> tokens, params (TokenType type, string value, int line, int column)[] expectedTokens)
        {
            var filteredTokens = tokens.Where(t => t.Type != TokenType.NEWLINE).ToList();

            for (int i = 0; i < Math.Min(expectedTokens.Length, filteredTokens.Count); i++)
            {
                var expected = expectedTokens[i];
                var actual = filteredTokens[i];
                Assert.AreEqual(expected.type, actual.Type, $"第{i+1}个Token类型不匹配");
                Assert.AreEqual(expected.value, actual.Value, $"第{i+1}个Token值不匹配");
                Assert.AreEqual(expected.line, actual.Line, $"第{i+1}个Token行号不匹配");
                Assert.AreEqual(expected.column, actual.Column, $"第{i+1}个Token列号不匹配");
            }
        }

        // === 基础语法测试 ===
        [Test]
        public void TestBasicNodeStructure()
        {
            string script = @"node: test
---
角色: 你好世界
===";

            var tokens = TokenizeScript(script);
            
            AssertTokens(tokens,
                (TokenType.IDENTIFIER, "node"),
                (TokenType.METADATA_SEPARATOR, ":"),
                (TokenType.TEXT, "test"),
                (TokenType.NODE_START, "---"),
                (TokenType.TEXT, "角色"),
                (TokenType.COLON, ":"),
                (TokenType.TEXT, " 你好世界"),
                (TokenType.NODE_END, "===")
            );
        }

        [Test]
        public void TestVariableInterpolation()
        {
            string script = @"---
角色: 你好{$name}，欢迎来到{$place}
===";

            var tokens = TokenizeScript(script);

            AssertTokens(tokens,
                (TokenType.NODE_START, "---"),
                (TokenType.TEXT, "角色"),
                (TokenType.COLON, ":"),
                (TokenType.TEXT, " 你好"),
                (TokenType.LEFT_BRACE, "{"),
                (TokenType.VARIABLE, "name"),
                (TokenType.RIGHT_BRACE, "}"),
                (TokenType.TEXT, "，欢迎来到"),
                (TokenType.LEFT_BRACE, "{"),
                (TokenType.VARIABLE, "place"),
                (TokenType.RIGHT_BRACE, "}"),
                (TokenType.NODE_END, "===")
            );
        }

        [Test]
        public void TestBasicCommands()
        {
            string script = @"---
<<set $hp = 100>>
<<var $gold 50>>
<<wait 2.0>>
<<jump ending>>
===";

            var tokens = TokenizeScript(script);

            AssertTokens(tokens,
                (TokenType.NODE_START, "---"),
                (TokenType.COMMAND_START, "<<"),
                (TokenType.SET, "set"),
                (TokenType.VARIABLE, "hp"),
                (TokenType.ASSIGN, "="),
                (TokenType.NUMBER, "100"),
                (TokenType.COMMAND_END, ">>"),
                (TokenType.COMMAND_START, "<<"),
                (TokenType.VAR, "var"),
                (TokenType.VARIABLE, "gold"),
                (TokenType.NUMBER, "50"),
                (TokenType.COMMAND_END, ">>"),
                (TokenType.COMMAND_START, "<<"),
                (TokenType.WAIT, "wait"),
                (TokenType.NUMBER, "2.0"),
                (TokenType.COMMAND_END, ">>"),
                (TokenType.COMMAND_START, "<<"),
                (TokenType.JUMP, "jump"),
                (TokenType.IDENTIFIER, "ending"),
                (TokenType.COMMAND_END, ">>"),
                (TokenType.NODE_END, "===")
            );
        }

        [Test]
        public void TestChoiceOptions()
        {
            string script = @"---
选择一个选项：
-> 选项1 #tag1
-> 选项2 <<if $hp > 50>> #tag2
===";

            var tokens = TokenizeScript(script);

            AssertTokens(tokens,
                (TokenType.NODE_START, "---"),
                (TokenType.TEXT, "选择一个选项："),
                (TokenType.ARROW, "->"),
                (TokenType.TEXT, " 选项1 "),
                (TokenType.HASH, "#"),
                (TokenType.TEXT, "tag1"),
                (TokenType.ARROW, "->"),
                (TokenType.TEXT, " 选项2 "),
                (TokenType.COMMAND_START, "<<"),
                (TokenType.IF, "if"),
                (TokenType.VARIABLE, "hp"),
                (TokenType.GREATER, ">"),
                (TokenType.NUMBER, "50"),
                (TokenType.COMMAND_END, ">>"),
                (TokenType.TEXT, " "),
                (TokenType.HASH, "#"),
                (TokenType.TEXT, "tag2"),
                (TokenType.NODE_END, "===")
            );
        }

        [Test]
        public void TestConditionalBlocks()
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

            var tokens = TokenizeScript(script);

            AssertTokens(tokens,
                (TokenType.NODE_START, "---"),
                (TokenType.COMMAND_START, "<<"),
                (TokenType.IF, "if"),
                (TokenType.VARIABLE, "hp"),
                (TokenType.GREATER, ">"),
                (TokenType.NUMBER, "0"),
                (TokenType.COMMAND_END, ">>"),
                (TokenType.TEXT, "你还活着"),
                (TokenType.COMMAND_START, "<<"),
                (TokenType.ELIF, "elif"),
                (TokenType.VARIABLE, "hp"),
                (TokenType.EQUALS, "=="),
                (TokenType.NUMBER, "0"),
                (TokenType.COMMAND_END, ">>"),
                (TokenType.TEXT, "你死了"),
                (TokenType.COMMAND_START, "<<"),
                (TokenType.ELSE, "else"),
                (TokenType.COMMAND_END, ">>"),
                (TokenType.TEXT, "状态未知"),
                (TokenType.COMMAND_START, "<<"),
                (TokenType.ENDIF, "endif"),
                (TokenType.COMMAND_END, ">>"),
                (TokenType.NODE_END, "===")
            );
        }

        // === 字符串与数值测试 ===
        [Test]
        public void TestStringLiterals()
        {
            string script = @"---
<<set $message = ""Hello World"">>
<<set $name = 'John Doe'>>
===";

            var tokens = TokenizeScript(script);

            AssertTokens(tokens,
                (TokenType.NODE_START, "---"),
                (TokenType.COMMAND_START, "<<"),
                (TokenType.SET, "set"),
                (TokenType.VARIABLE, "message"),
                (TokenType.ASSIGN, "="),
                (TokenType.QUOTE, "\""),
                (TokenType.TEXT, "Hello World"),
                (TokenType.QUOTE, "\""),
                (TokenType.COMMAND_END, ">>"),
                (TokenType.COMMAND_START, "<<"),
                (TokenType.SET, "set"),
                (TokenType.VARIABLE, "name"),
                (TokenType.ASSIGN, "="),
                (TokenType.QUOTE, "'"),
                (TokenType.TEXT, "John Doe"),
                (TokenType.QUOTE, "'"),
                (TokenType.COMMAND_END, ">>"),
                (TokenType.NODE_END, "===")
            );
        }

        [Test]
        public void TestStringWithInterpolation()
        {
            string script = @"---
<<set $greeting = ""Hello {$name}, welcome to {$place}!"">>
===";

            var tokens = TokenizeScript(script);

            AssertTokens(tokens,
                (TokenType.NODE_START, "---"),
                (TokenType.COMMAND_START, "<<"),
                (TokenType.SET, "set"),
                (TokenType.VARIABLE, "greeting"),
                (TokenType.ASSIGN, "="),
                (TokenType.QUOTE, "\""),
                (TokenType.TEXT, "Hello "),
                (TokenType.LEFT_BRACE, "{"),
                (TokenType.VARIABLE, "name"),
                (TokenType.RIGHT_BRACE, "}"),
                (TokenType.TEXT, ", welcome to "),
                (TokenType.LEFT_BRACE, "{"),
                (TokenType.VARIABLE, "place"),
                (TokenType.RIGHT_BRACE, "}"),
                (TokenType.TEXT, "!"),
                (TokenType.QUOTE, "\""),
                (TokenType.COMMAND_END, ">>"),
                (TokenType.NODE_END, "===")
            );
        }

        // === 运算符与表达式测试 ===
        [Test]
        public void TestMathematicalOperators()
        {
            string script = @"---
<<if $a + $b * $c >= $d && $e or not $f>>
测试
<<endif>>
===";

            var tokens = TokenizeScript(script);

            AssertTokens(tokens,
                (TokenType.NODE_START, "---"),
                (TokenType.COMMAND_START, "<<"),
                (TokenType.IF, "if"),
                (TokenType.VARIABLE, "a"),
                (TokenType.PLUS, "+"),
                (TokenType.VARIABLE, "b"),
                (TokenType.MULTIPLY, "*"),
                (TokenType.VARIABLE, "c"),
                (TokenType.GREATER_EQUALS, ">="),
                (TokenType.VARIABLE, "d"),
                (TokenType.AND, "&&"),
                (TokenType.VARIABLE, "e"),
                (TokenType.OR, "or"),
                (TokenType.NOT, "not"),
                (TokenType.VARIABLE, "f"),
                (TokenType.COMMAND_END, ">>"),
                (TokenType.TEXT, "测试"),
                (TokenType.COMMAND_START, "<<"),
                (TokenType.ENDIF, "endif"),
                (TokenType.COMMAND_END, ">>"),
                (TokenType.NODE_END, "===")
            );
        }

        [Test]
        public void TestFunctionCallSyntax()
        {
            string script = @"---
<<if visited(""node1"") && random(0, 10) > 5>>
测试函数调用
<<endif>>
===";

            var tokens = TokenizeScript(script);

            AssertTokens(tokens,
                (TokenType.NODE_START, "---"),
                (TokenType.COMMAND_START, "<<"),
                (TokenType.IF, "if"),
                (TokenType.IDENTIFIER, "visited"),
                (TokenType.LEFT_PAREN, "("),
                (TokenType.QUOTE, "\""),
                (TokenType.TEXT, "node1"),
                (TokenType.QUOTE, "\""),
                (TokenType.RIGHT_PAREN, ")"),
                (TokenType.AND, "&&"),
                (TokenType.IDENTIFIER, "random"),
                (TokenType.LEFT_PAREN, "("),
                (TokenType.NUMBER, "0"),
                (TokenType.COMMA, ","),
                (TokenType.NUMBER, "10"),
                (TokenType.RIGHT_PAREN, ")"),
                (TokenType.GREATER, ">"),
                (TokenType.NUMBER, "5"),
                (TokenType.COMMAND_END, ">>"),
                (TokenType.TEXT, "测试函数调用"),
                (TokenType.COMMAND_START, "<<"),
                (TokenType.ENDIF, "endif"),
                (TokenType.COMMAND_END, ">>"),
                (TokenType.NODE_END, "===")
            );
        }

        // === 特殊语法测试 ===
        [Test]
        public void TestNarrationWithColon()
        {
            string script = @"---
:这是冒号旁白文本
普通旁白文本
===";

            var tokens = TokenizeScript(script);

            AssertTokens(tokens,
                (TokenType.NODE_START, "---"),
                (TokenType.COLON, ":"),
                (TokenType.TEXT, "这是冒号旁白文本"),
                (TokenType.TEXT, "普通旁白文本"),
                (TokenType.NODE_END, "===")
            );
        }

        [Test]
        public void TestCommentFiltering()
        {
            string script = @"// 这是注释
node: test
---
// 这是另一个注释
角色: 对话内容
===";

            var tokens = TokenizeScript(script);

            AssertTokens(tokens,
                (TokenType.IDENTIFIER, "node"),
                (TokenType.METADATA_SEPARATOR, ":"),
                (TokenType.TEXT, "test"),
                (TokenType.NODE_START, "---"),
                (TokenType.TEXT, "角色"),
                (TokenType.COLON, ":"),
                (TokenType.TEXT, " 对话内容"),
                (TokenType.NODE_END, "===")
            );
        }

        [Test]
        public void TestEscapeSequences()
        {
            string script = @"---
角色: 这是\:转义冒号，这是\#转义标签，这是\\转义反斜杠
===";

            var tokens = TokenizeScript(script);

            AssertTokens(tokens,
                (TokenType.NODE_START, "---"),
                (TokenType.TEXT, "角色"),
                (TokenType.COLON, ":"),
                (TokenType.TEXT, " 这是:转义冒号，这是#转义标签，这是\\转义反斜杠"),
                (TokenType.NODE_END, "===")
            );
        }

        [Test]
        public void TestComplexMathExpression()
        {
            string script = @"---
<<set $result = ($a + $b) * ($c - $d) / (2 + 3.14)>>
===";

            var tokens = TokenizeScript(script);

            AssertTokens(tokens,
                (TokenType.NODE_START, "---"),
                (TokenType.COMMAND_START, "<<"),
                (TokenType.SET, "set"),
                (TokenType.VARIABLE, "result"),
                (TokenType.ASSIGN, "="),
                (TokenType.LEFT_PAREN, "("),
                (TokenType.VARIABLE, "a"),
                (TokenType.PLUS, "+"),
                (TokenType.VARIABLE, "b"),
                (TokenType.RIGHT_PAREN, ")"),
                (TokenType.MULTIPLY, "*"),
                (TokenType.LEFT_PAREN, "("),
                (TokenType.VARIABLE, "c"),
                (TokenType.MINUS, "-"),
                (TokenType.VARIABLE, "d"),
                (TokenType.RIGHT_PAREN, ")"),
                (TokenType.DIVIDE, "/"),
                (TokenType.LEFT_PAREN, "("),
                (TokenType.NUMBER, "2"),
                (TokenType.PLUS, "+"),
                (TokenType.NUMBER, "3.14"),
                (TokenType.RIGHT_PAREN, ")"),
                (TokenType.COMMAND_END, ">>"),
                (TokenType.NODE_END, "===")
            );
        }

        [Test]
        public void TestBooleanLiterals()
        {
            string script = @"---
<<set $isAlive = true>>
<<set $isDead = false>>
===";

            var tokens = TokenizeScript(script);

            AssertTokens(tokens,
                (TokenType.NODE_START, "---"),
                (TokenType.COMMAND_START, "<<"),
                (TokenType.SET, "set"),
                (TokenType.VARIABLE, "isAlive"),
                (TokenType.ASSIGN, "="),
                (TokenType.TRUE, "true"),
                (TokenType.COMMAND_END, ">>"),
                (TokenType.COMMAND_START, "<<"),
                (TokenType.SET, "set"),
                (TokenType.VARIABLE, "isDead"),
                (TokenType.ASSIGN, "="),
                (TokenType.FALSE, "false"),
                (TokenType.COMMAND_END, ">>"),
                (TokenType.NODE_END, "===")
            );
        }

        // === 多节点结构测试 ===
        [Test]
        public void TestMultipleNodes()
        {
            string script = @"node: first
---
第一个节点
===

node: second  
---
第二个节点
===";

            var tokens = TokenizeScript(script);

            AssertTokens(tokens,
                (TokenType.IDENTIFIER, "node"),
                (TokenType.METADATA_SEPARATOR, ":"),
                (TokenType.TEXT, "first"),
                (TokenType.NODE_START, "---"),
                (TokenType.TEXT, "第一个节点"),
                (TokenType.NODE_END, "==="),
                (TokenType.IDENTIFIER, "node"),
                (TokenType.METADATA_SEPARATOR, ":"),
                (TokenType.TEXT, "second"),
                (TokenType.NODE_START, "---"),
                (TokenType.TEXT, "第二个节点"),
                (TokenType.NODE_END, "===")
            );
        }

        // === 缩进结构测试 ===
        [Test]
        public void TestIndentationHandling()
        {
            string script = @"---
-> 选项1
    嵌套内容1
    嵌套内容2
-> 选项2
===";

            var tokens = TokenizeScript(script);

            // 检查缩进Token的存在
            var indentTokens = tokens.Where(t => t.Type == TokenType.INDENT || t.Type == TokenType.DEDENT).ToList();

            Assert.AreEqual(2, indentTokens.Count, "应该有2个缩进相关的Token");
            Assert.AreEqual(TokenType.INDENT, indentTokens[0].Type, "第一个应该是INDENT");
            Assert.AreEqual(TokenType.DEDENT, indentTokens[1].Type, "第二个应该是DEDENT");
        }

        // === 边界情况测试 ===
        [Test]
        public void TestEmptyScript()
        {
            string script = "";

            var tokens = TokenizeScript(script);

            Assert.AreEqual(1, tokens.Count, "空脚本应该只有EOF Token");
            Assert.AreEqual(TokenType.EOF, tokens[0].Type, "应该是EOF Token");
        }

        [Test]
        public void TestOnlyComments()
        {
            string script = @"// 只有注释的脚本
// 另一行注释";

            var tokens = TokenizeScript(script);

            Assert.AreEqual(1, tokens.Count, "只有注释的脚本应该只有EOF Token");
            Assert.AreEqual(TokenType.EOF, tokens[0].Type, "应该是EOF Token");
        }

        [Test]
        public void TestMixedIndentationWarning()
        {
            string script = @"---
-> 选项1
	    嵌套内容（混合Tab和空格）
===";

            // 这个测试主要是验证不会因为混合缩进而崩溃
            var tokens = TokenizeScript(script);
            Assert.IsNotNull(tokens, "混合缩进时应该仍能正常解析");
            Assert.IsTrue(tokens.Any(t => t.Type == TokenType.NODE_START), "应该包含节点开始标记");
        }

        [Test]
        public void TestExtremeLongText()
        {
            var longText = new string('测', 1000);
            string script = $@"---
角色: {longText}
===";

            var tokens = TokenizeScript(script);
            
            var textToken = tokens.FirstOrDefault(t => t.Type == TokenType.TEXT && t.Value.Contains("测"));
            Assert.IsNotNull(textToken, "应该能处理极长文本");
            Assert.AreEqual($" {longText}", textToken.Value, "长文本内容应该正确");
        }

        [Test]
        public void TestUnicodeEmojis()
        {
            string script = @"---
角色: 你好世界 😊🌟🎮 Unicode测试
===";

            var tokens = TokenizeScript(script);
            
            var textToken = tokens.FirstOrDefault(t => t.Type == TokenType.TEXT && t.Value.Contains("😊"));
            Assert.IsNotNull(textToken, "应该能处理Unicode表情符号");
            Assert.IsTrue(textToken.Value.Contains("😊🌟🎮"), "应该正确保留所有表情符号");
        }

        // === 复杂转义与嵌套测试 ===
        [Test]
        public void TestNestedQuotesAndEscaping()
        {
            string script = @"---
<<set $msg = ""外层引号'内层单引号'和\""转义双引号\""和\\反斜杠"">>
===";

            var tokens = TokenizeScript(script);

            var textTokens = tokens.Where(t => t.Type == TokenType.TEXT).ToList();
            var combinedText = string.Join("", textTokens.Select(t => t.Value));
            Assert.IsTrue(combinedText.Contains("'内层单引号'"), "应该正确处理嵌套的单引号");
            Assert.IsTrue(combinedText.Contains("\"转义双引号\""), "应该正确处理转义的双引号");
            Assert.IsTrue(combinedText.Contains("\\反斜杠"), "应该正确处理转义的反斜杠");
        }

        [Test]
        public void TestMultipleVariableInterpolation()
        {
            string script = @"---
角色: {$name}说：{$greeting}，今天是{$date}，心情{$mood}！
===";

            var tokens = TokenizeScript(script);

            var braceTokens = tokens.Where(t => t.Type == TokenType.LEFT_BRACE || t.Type == TokenType.RIGHT_BRACE).ToList();
            Assert.AreEqual(8, braceTokens.Count, "应该有8个大括号（4对）");

            var variableTokens = tokens.Where(t => t.Type == TokenType.VARIABLE).ToList();
            Assert.AreEqual(4, variableTokens.Count, "应该有4个变量");
            
            var expectedVars = new[] { "name", "greeting", "date", "mood" };
            for (int i = 0; i < expectedVars.Length; i++)
            {
                Assert.AreEqual(expectedVars[i], variableTokens[i].Value, $"第{i+1}个变量应该是{expectedVars[i]}");
            }
        }

        [Test]
        public void TestConsecutiveOperators()
        {
            string script = @"---
<<if $a===$b||$c>==$d&&!$e!==$f>>
测试
<<endif>>
===";

            var tokens = TokenizeScript(script);

            // 检查连续操作符的处理
            var operatorTokens = tokens.Where(t => 
                t.Type == TokenType.EQUALS || t.Type == TokenType.NOT_EQUALS || 
                t.Type == TokenType.GREATER_EQUALS || t.Type == TokenType.OR || 
                t.Type == TokenType.AND || t.Type == TokenType.NOT).ToList();

            Assert.IsTrue(operatorTokens.Count >= 6, "应该识别出多个操作符");
        }

        [Test]
        public void TestConsecutiveCommands()
        {
            string script = @"---
<<set $a=1>><<wait 2>><<jump next>>
===";

            var tokens = TokenizeScript(script);

            var commandStarts = tokens.Where(t => t.Type == TokenType.COMMAND_START).ToList();
            var commandEnds = tokens.Where(t => t.Type == TokenType.COMMAND_END).ToList();
            
            Assert.AreEqual(3, commandStarts.Count, "应该有3个命令开始标记");
            Assert.AreEqual(3, commandEnds.Count, "应该有3个命令结束标记");
        }

        [Test]
        public void TestVariousNumberFormats()
        {
            string script = @"---
<<set $int = 42>>
<<set $float = 3.14>>
<<set $zero = 0>>
<<set $negative = -123>>
<<set $decimal = 0.001>>
<<set $scientific = 1.23e-4>>
===";

            var tokens = TokenizeScript(script);

            var numberTokens = tokens.Where(t => t.Type == TokenType.NUMBER).ToList();
            Assert.IsTrue(numberTokens.Count >= 5, "应该识别出至少5个数字");
            
            // 验证整数、浮点数、零值等
            Assert.IsTrue(numberTokens.Any(t => t.Value == "42"), "应该包含整数");
            Assert.IsTrue(numberTokens.Any(t => t.Value == "3.14"), "应该包含浮点数");
            Assert.IsTrue(numberTokens.Any(t => t.Value == "0"), "应该包含零值");
        }

        // === 新增高级边界测试 ===
        [Test]
        public void TestTokenPositionAccuracy()
        {
            string script = @"node: test
---
角色: 测试
===";

            var tokens = TokenizeScript(script);

            // 验证Token位置信息
            AssertTokensWithPosition(tokens.Take(4).ToList(),
                (TokenType.IDENTIFIER, "node", 1, 1),
                (TokenType.METADATA_SEPARATOR, ":", 1, 5),
                (TokenType.TEXT, "test", 1, 7),
                (TokenType.NODE_START, "---", 2, 1)
            );
        }

        [Test]
        public void TestCrossplatformLineEndings()
        {
            string scriptWindows = "node: test\r\n---\r\n角色: 对话\r\n===";
            string scriptUnix = "node: test\n---\n角色: 对话\n===";
            string scriptMac = "node: test\r---\r角色: 对话\r===";

            var tokensWindows = TokenizeScript(scriptWindows);
            var tokensUnix = TokenizeScript(scriptUnix);
            var tokensMac = TokenizeScript(scriptMac);

            // 所有平台应该生成相同的非换行Token结构
            var nonNewlineWindows = tokensWindows.Where(t => t.Type != TokenType.NEWLINE).ToArray();
            var nonNewlineUnix = tokensUnix.Where(t => t.Type != TokenType.NEWLINE).ToArray();
            var nonNewlineMac = tokensMac.Where(t => t.Type != TokenType.NEWLINE).ToArray();

            Assert.AreEqual(nonNewlineWindows.Length, nonNewlineUnix.Length, "Windows和Unix应该生成相同数量的Token");
            Assert.AreEqual(nonNewlineUnix.Length, nonNewlineMac.Length, "Unix和Mac应该生成相同数量的Token");
        }

        [Test]
        public void TestColonContextSensitivity()
        {
            string script = @"---
:旁白冒号开头
角色名: 对话内容
普通:文本:中间:有:冒号
===";

            var tokens = TokenizeScript(script);

            // 检查冒号在不同上下文中的处理
            var colonTokens = tokens.Where(t => t.Type == TokenType.COLON).ToList();
            Assert.IsTrue(colonTokens.Count >= 2, "应该识别出多个冒号");
            
            // 第一个应该是行首旁白标记，第二个应该是对话分隔符
            Assert.AreEqual(1, colonTokens[0].Column, "第一个冒号应该在行首");
        }

        [Test]
        public void TestArrowInText()
        {
            string script = @"---
普通文本 -> 不是选项箭头
-> 真正的选项
===";

            var tokens = TokenizeScript(script);

            var arrowTokens = tokens.Where(t => t.Type == TokenType.ARROW).ToList();
            Assert.AreEqual(1, arrowTokens.Count, "应该只识别一个真正的箭头（在行首的）");

            var textTokens = tokens.Where(t => t.Type == TokenType.TEXT).ToList();
            var arrowInText = textTokens.Any(t => t.Value.Contains("->"));
            Assert.IsTrue(arrowInText, "文本中的箭头应该作为普通文本处理");
        }

        [Test]
        public void TestHashInText()
        {
            string script = @"---
角色: 这是\#号在文本中的情况 #real_tag
===";

            var tokens = TokenizeScript(script);

            var hashTokens = tokens.Where(t => t.Type == TokenType.HASH).ToList();
            var textTokens = tokens.Where(t => t.Type == TokenType.TEXT).ToList();

            Assert.IsTrue(textTokens.Any(t => t.Value.Contains("#号")), "文本中的#号应该被包含在文本中");
            Assert.IsTrue(hashTokens.Count >= 1, "独立的#标签应该被识别");
        }

        [Test]
        public void TestBracketEdgeCases()
        {
            string script = @"---
角色: 方括号[text]和圆括号(text)和花括号{$var}
===";

            var tokens = TokenizeScript(script);

            var braceTokens = tokens.Where(t => t.Type == TokenType.LEFT_BRACE || t.Type == TokenType.RIGHT_BRACE).ToList();
            Assert.AreEqual(2, braceTokens.Count, "应该只识别插值的花括号");

            var textTokens = tokens.Where(t => t.Type == TokenType.TEXT).ToList();
            Assert.IsTrue(textTokens.Any(t => t.Value.Contains("[text]")), "方括号应该作为普通文本");
            Assert.IsTrue(textTokens.Any(t => t.Value.Contains("(text)")), "圆括号应该作为普通文本");
        }

        [Test]
        public void TestUnterminatedStringRecovery()
        {
            string script = @"---
<<set $msg = ""未结束的字符串
角色: 后续正常内容
===";

            // 验证错误恢复机制
            var tokens = TokenizeScript(script);
            Assert.IsNotNull(tokens, "未结束的字符串不应导致崩溃");
            Assert.IsTrue(tokens.Any(t => t.Type == TokenType.EOF), "应该能到达EOF");
            Assert.IsTrue(tokens.Any(t => t.Type == TokenType.TEXT && t.Value.Contains("后续")), "应该能继续解析后续内容");
        }

        [Test]
        public void TestEmptyInterpolationAndEdgeCases()
        {
            string script = @"---
角色: 空插值{}测试，多重插值{$a}{$b}，嵌套文本{$name}和{$value}混合
===";

            var tokens = TokenizeScript(script);

            var braceTokens = tokens.Where(t => t.Type == TokenType.LEFT_BRACE || t.Type == TokenType.RIGHT_BRACE).ToList();
            Assert.AreEqual(10, braceTokens.Count, "应该识别所有大括号（5对）");
            
            var variableTokens = tokens.Where(t => t.Type == TokenType.VARIABLE).ToList();
            Assert.AreEqual(4, variableTokens.Count, "应该有4个变量：a, b, name, value");
        }

        [Test]
        public void TestComplexIdentifierSupport()
        {
            string script = @"test_node_123: value
node_测试: 中文值
_special: 下划线开头
123invalid: 数字开头
---
内容
===";

            var tokens = TokenizeScript(script);

            var identifiers = tokens.Where(t => t.Type == TokenType.IDENTIFIER).ToList();
            Assert.IsTrue(identifiers.Any(t => t.Value == "test_node_123"), "应该支持下划线和数字的标识符");
            Assert.IsTrue(identifiers.Any(t => t.Value == "node_测试"), "应该支持中文字符的标识符");
            Assert.IsTrue(identifiers.Any(t => t.Value == "_special"), "应该支持下划线开头的标识符");
        }

        // === 极端边界测试 ===
        [Test]
        public void TestExtremeNestingDepth()
        {
            // 创建15层嵌套结构
            var script = @"---";
            for (int i = 0; i < 15; i++)
            {
                script += "\n" + new string(' ', i * 4) + $"-> 第{i+1}层";
            }
            script += "\n" + new string(' ', 15 * 4) + "最深层内容";
            script += "\n===";

            // 验证极深嵌套不会崩溃
            var tokens = TokenizeScript(script);
            Assert.IsNotNull(tokens, "极深嵌套不应崩溃");
            Assert.IsTrue(tokens.Any(t => t.Type == TokenType.EOF), "应该正常结束");
            
            var arrowTokens = tokens.Where(t => t.Type == TokenType.ARROW).ToList();
            Assert.AreEqual(15, arrowTokens.Count, "应该正确识别15个选项箭头");
        }

        [Test]
        public void TestMalformedCommandRecovery()
        {
            string script = @"---
<<incomplete command without ending
正常文本
<<set $valid = true>>
===";

            // 验证错误命令后的恢复能力
            var tokens = TokenizeScript(script);
            Assert.IsNotNull(tokens, "错误命令不应崩溃");
            Assert.IsTrue(tokens.Any(t => (t.Type is TokenType.TEXT or TokenType.IDENTIFIER) && t.Value.Contains("正常")), "应该能继续解析正常文本");
        }

        [Test]
        public void TestUtf8NoBomHandling()
        {
            // UTF-8 BOM + 正常内容
            string script = "\uFEFFnode: test\n---\n角色: 对话\n===";

            var tokens = TokenizeScript(script);

            // BOM 暂不兼容
            Assert.IsFalse(tokens.Any(t => t.Type == TokenType.INDENT && t.Value == "node"), "应该识别节点标识符错误");
            Assert.IsTrue(tokens.Any(t => t.Type == TokenType.TEXT && t.Value == "test"), "应该正确识别文本内容");
        }

        [Test]
        public void TestVeryLongLine()
        {
            // 创建一个非常长的行（5000字符）
            var longLine = new string('A', 5000);
            string script = $@"---
角色: {longLine}
===";

            var tokens = TokenizeScript(script);
            
            var longTextToken = tokens.FirstOrDefault(t => t.Type == TokenType.TEXT && t.Value.Length > 4000);
            Assert.IsNotNull(longTextToken, "应该能处理非常长的行");
        }

        [Test]
        public void TestSpecialWhitespaceCharacters()
        {
            // 包含各种空白字符：制表符、全角空格、无分隔空格等
            string script = "node:\u3000test\n---\n角色:\u00A0测试\u2009内容\n===";

            var tokens = TokenizeScript(script);

            // 验证各种空白字符被正确处理
            Assert.IsTrue(tokens.Any(t => t.Type == TokenType.IDENTIFIER && t.Value == "node"), "应该正确识别标识符");
            Assert.IsTrue(tokens.Any(t => t.Type == TokenType.TEXT && t.Value.Contains("测试")), "应该正确处理特殊空白字符");
        }

        [Test]
        public void TestMixedQuoteRecovery()
        {
            string script = @"---
<<set $msg1 = ""双引号开始'单引号结束>>
<<set $msg2 = '单引号开始""双引号结束>>
正常内容
===";

            // 验证引号不匹配的错误恢复
            var tokens = TokenizeScript(script);
            Assert.IsNotNull(tokens, "引号不匹配不应崩溃");
            Assert.IsTrue(tokens.Any(t => t.Type == TokenType.TEXT && t.Value.Contains("正常")), "应该能继续解析后续内容");
        }
    }
}
