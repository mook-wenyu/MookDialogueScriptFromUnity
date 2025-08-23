using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MookDialogueScript.Tests
{
    /// <summary>
    /// 词法分析器测试
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
                Debug.Log(token.ToString());
            }
            return tokens;
        }

        /// <summary>
        /// 验证Token序列
        /// </summary>
        private void AssertTokens(List<Token> tokens, params (TokenType type, string value)[] expectedTokens)
        {
            // 过滤掉NEWLINE token以简化测试
            var filteredTokens = tokens.Where(t => t.Type != TokenType.NEWLINE).ToList();

            Assert.AreEqual(expectedTokens.Length + 1, filteredTokens.Count, "Token数量不匹配（包括EOF）");

            for (int i = 0; i < expectedTokens.Length; i++)
            {
                var expected = expectedTokens[i];
                var actual = filteredTokens[i];
                Assert.AreEqual(expected.type, actual.Type, $"第{i}个Token类型不匹配");
                Assert.AreEqual(expected.value, actual.Value, $"第{i}个Token值不匹配");
            }

            // 最后一个应该是EOF
            Assert.AreEqual(TokenType.EOF, filteredTokens.Last().Type, "最后一个Token应该是EOF");
        }

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
                (TokenType.IDENTIFIER, "角色"),
                (TokenType.COLON, ":"),
                (TokenType.TEXT, " 你好世界"),
                (TokenType.NODE_END, "===")
            );
        }

        [Test]
        public void TestVariablesAndInterpolation()
        {
            string script = @"---
角色: 你好{$name}，欢迎来到{$place}
===";

            var tokens = TokenizeScript(script);

            AssertTokens(tokens,
                (TokenType.NODE_START, "---"),
                (TokenType.IDENTIFIER, "角色"),
                (TokenType.COLON, ":"),
                (TokenType.IDENTIFIER, "你好"),
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
        public void TestCommands()
        {
            string script = @"---
<<set $hp = 100>>
<<var $gold 50>>
<<wait 2.0>>
<<jump ending>>
===";

            var tokens = TokenizeScript(script);

            var expectedTokens = new List<(TokenType type, string value)>
            {
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
            };

            AssertTokens(tokens, expectedTokens.ToArray());
        }

        [Test]
        public void TestChoices()
        {
            string script = @"---
选择一个选项：
-> 选项1 #tag1
-> 选项2 <<if $hp > 50>> #tag2
===";

            var tokens = TokenizeScript(script);
            

            var expectedTokens = new List<(TokenType type, string value)>
            {
                (TokenType.NODE_START, "---"),
                (TokenType.IDENTIFIER, "选择一个选项"),
                (TokenType.TEXT, "："),
                (TokenType.ARROW, "->"),
                (TokenType.TEXT, " 选项1 "),
                (TokenType.HASH, "#"),
                (TokenType.IDENTIFIER, "tag1"),
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
                (TokenType.IDENTIFIER, "tag2"),
                (TokenType.NODE_END, "===")
            };

            AssertTokens(tokens, expectedTokens.ToArray());
        }

        [Test]
        public void TestConditions()
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

            var expectedTokens = new List<(TokenType type, string value)>
            {
                (TokenType.NODE_START, "---"),
                (TokenType.COMMAND_START, "<<"),
                (TokenType.IF, "if"),
                (TokenType.VARIABLE, "hp"),
                (TokenType.GREATER, ">"),
                (TokenType.NUMBER, "0"),
                (TokenType.COMMAND_END, ">>"),
                (TokenType.IDENTIFIER, "你还活着"),
                (TokenType.COMMAND_START, "<<"),
                (TokenType.ELIF, "elif"),
                (TokenType.VARIABLE, "hp"),
                (TokenType.EQUALS, "=="),
                (TokenType.NUMBER, "0"),
                (TokenType.COMMAND_END, ">>"),
                (TokenType.IDENTIFIER, "你死了"),
                (TokenType.COMMAND_START, "<<"),
                (TokenType.ELSE, "else"),
                (TokenType.COMMAND_END, ">>"),
                (TokenType.IDENTIFIER, "状态未知"),
                (TokenType.COMMAND_START, "<<"),
                (TokenType.ENDIF, "endif"),
                (TokenType.COMMAND_END, ">>"),
                (TokenType.NODE_END, "===")
            };

            AssertTokens(tokens, expectedTokens.ToArray());
        }

        [Test]
        public void TestStringLiterals()
        {
            string script = @"---
<<set $message = ""Hello World"">>
<<set $name = 'John Doe'>>
===";

            var tokens = TokenizeScript(script);

            var expectedTokens = new List<(TokenType type, string value)>
            {
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
            };

            AssertTokens(tokens, expectedTokens.ToArray());
        }

        [Test]
        public void TestStringWithInterpolation()
        {
            string script = @"---
<<set $greeting = ""Hello {$name}, welcome to {$place}!"">>
===";

            var tokens = TokenizeScript(script);

            var expectedTokens = new List<(TokenType type, string value)>
            {
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
            };

            AssertTokens(tokens, expectedTokens.ToArray());
        }

        [Test]
        public void TestOperators()
        {
            string script = @"---
<<if $a + $b * $c >= $d && $e or not $f>>
测试
<<endif>>
===";

            var tokens = TokenizeScript(script);

            var expectedTokens = new List<(TokenType type, string value)>
            {
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
            };

            AssertTokens(tokens, expectedTokens.ToArray());
        }

        [Test]
        public void TestFunctionCalls()
        {
            string script = @"---
<<if visited(""node1"") && random(0, 10) > 5>>
测试函数调用
<<endif>>
===";

            var tokens = TokenizeScript(script);

            var expectedTokens = new List<(TokenType type, string value)>
            {
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
            };

            AssertTokens(tokens, expectedTokens.ToArray());
        }

        [Test]
        public void TestNarrationWithColon()
        {
            string script = @"---
:这是旁白文本
普通文本
===";

            var tokens = TokenizeScript(script);

            AssertTokens(tokens,
                (TokenType.NODE_START, "---"),
                (TokenType.TEXT, ":这是旁白文本"),
                (TokenType.TEXT, "普通文本"),
                (TokenType.NODE_END, "===")
            );
        }

        [Test]
        public void TestComments()
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
                (TokenType.TEXT, " test"),
                (TokenType.NODE_START, "---"),
                (TokenType.IDENTIFIER, "角色"),
                (TokenType.COLON, ":"),
                (TokenType.TEXT, " 对话内容"),
                (TokenType.NODE_END, "===")
            );
        }

        [Test]
        public void TestEscapedCharacters()
        {
            string script = @"---
角色: 这是\:转义冒号，这是\#转义标签，这是\\转义反斜杠
===";

            var tokens = TokenizeScript(script);

            AssertTokens(tokens,
                (TokenType.NODE_START, "---"),
                (TokenType.IDENTIFIER, "角色"),
                (TokenType.COLON, ":"),
                (TokenType.TEXT, " 这是:转义冒号，这是#转义标签，这是\\转义反斜杠"),
                (TokenType.NODE_END, "===")
            );
        }

        [Test]
        public void TestComplexExpression()
        {
            string script = @"---
<<set $result = ($a + $b) * ($c - $d) / (2 + 3.14)>>
===";

            var tokens = TokenizeScript(script);

            var expectedTokens = new List<(TokenType type, string value)>
            {
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
            };

            AssertTokens(tokens, expectedTokens.ToArray());
        }

        [Test]
        public void TestBooleanLiterals()
        {
            string script = @"---
<<set $isAlive = true>>
<<set $isDead = false>>
===";

            var tokens = TokenizeScript(script);

            var expectedTokens = new List<(TokenType type, string value)>
            {
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
            };

            AssertTokens(tokens, expectedTokens.ToArray());
        }

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

            var expectedTokens = new List<(TokenType type, string value)>
            {
                (TokenType.IDENTIFIER, "node"),
                (TokenType.METADATA_SEPARATOR, ":"),
                (TokenType.TEXT, " first"),
                (TokenType.NODE_START, "---"),
                (TokenType.TEXT, "第一个节点"),
                (TokenType.NODE_END, "==="),
                (TokenType.IDENTIFIER, "node"),
                (TokenType.METADATA_SEPARATOR, ":"),
                (TokenType.TEXT, " second  "),
                (TokenType.NODE_START, "---"),
                (TokenType.TEXT, "第二个节点"),
                (TokenType.NODE_END, "===")
            };

            AssertTokens(tokens, expectedTokens.ToArray());
        }

        [Test]
        public void TestIndentationTokens()
        {
            string script = @"---
-> 选项1
    嵌套内容1
    嵌套内容2
-> 选项2
===";

            var tokens = TokenizeScript(script);

            // 这个测试专门检查缩进Token
            var indentTokens = tokens.Where(t => t.Type == TokenType.INDENT || t.Type == TokenType.DEDENT).ToList();

            // 应该有一个INDENT和一个DEDENT
            Assert.AreEqual(2, indentTokens.Count, "应该有2个缩进相关的Token");
            Assert.AreEqual(TokenType.INDENT, indentTokens[0].Type, "第一个应该是INDENT");
            Assert.AreEqual(TokenType.DEDENT, indentTokens[1].Type, "第二个应该是DEDENT");
        }

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
    }
}
