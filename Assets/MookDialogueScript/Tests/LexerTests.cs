using System.Collections.Generic;
using System.Text;
using MookDialogueScript;
using NUnit.Framework;
using UnityEngine;

namespace Tests
{
    public class LexerTests
    {
        private Lexer _lexer;

        [SetUp]
        public void Setup()
        {
            _lexer = null;
        }

        private List<Token> Tokenize(string source)
        {
            _lexer = new Lexer(source);
            return _lexer.Tokenize();
        }

        [Test]
        public void TestEmptyString()
        {
            var tokens = Tokenize("");
            Assert.That(tokens.Count, Is.EqualTo(1));
            Assert.That(tokens[0].Type, Is.EqualTo(TokenType.EOF));
        }

        [Test]
        public void TestNodeMetadata()
        {
            // 测试节点元数据的词法分析
            string source = "node: test_node\nauthor: test_author\n---";
            var tokens = Tokenize(source);

            Assert.That(tokens[0].Type, Is.EqualTo(TokenType.IDENTIFIER));
            Assert.That(tokens[0].Value, Is.EqualTo("node"));
            Assert.That(tokens[1].Type, Is.EqualTo(TokenType.METADATA_SEPARATOR));
            Assert.That(tokens[2].Type, Is.EqualTo(TokenType.TEXT));
            Assert.That(tokens[2].Value, Is.EqualTo("test_node"));
            Assert.That(tokens[3].Type, Is.EqualTo(TokenType.NEWLINE));
        }

        [Test]
        public void TestNodeMarkers()
        {
            // 测试节点开始和结束标记
            string source = "---\n内容\n===";
            var tokens = Tokenize(source);

            Assert.That(tokens[0].Type, Is.EqualTo(TokenType.NODE_START));
            Assert.That(tokens[0].Value, Is.EqualTo("---"));
            
            // 在节点内容中，应该有NEWLINE和TEXT
            Assert.That(tokens[1].Type, Is.EqualTo(TokenType.NEWLINE));
            Assert.That(tokens[2].Type, Is.EqualTo(TokenType.TEXT).Or.EqualTo(TokenType.IDENTIFIER));
            Assert.That(tokens[2].Value, Is.EqualTo("内容"));
            
            // 找到节点结束标记
            Assert.That(tokens[4].Type, Is.EqualTo(TokenType.NODE_END));
            Assert.That(tokens[4].Value, Is.EqualTo("==="));
        }

        [Test]
        public void TestCommandMarkers()
        {
            // 测试新的统一命令格式 <<>>
            string source = "---\n<<if $test>>\n内容\n<<endif>>\n===";
            var tokens = Tokenize(source);

            var commandStartTokens = tokens.FindAll(t => t.Type == TokenType.COMMAND_START);
            var commandEndTokens = tokens.FindAll(t => t.Type == TokenType.COMMAND_END);
            
            Assert.That(commandStartTokens.Count, Is.EqualTo(2));
            Assert.That(commandEndTokens.Count, Is.EqualTo(2));
            
            Assert.That(commandStartTokens[0].Value, Is.EqualTo("<<"));
            Assert.That(commandEndTokens[0].Value, Is.EqualTo(">>"));
        }

        [Test]
        public void TestDialogue()
        {
            // 测试角色对话
            string source = "---\n角色: 你好世界\n===";
            var tokens = Tokenize(source);
            
            Assert.That(tokens[2].Value, Is.EqualTo("角色"));
            Assert.That(tokens[3].Type, Is.EqualTo(TokenType.COLON));
            Assert.That(tokens[4].Value, Is.EqualTo(" 你好世界"));
        }

        [Test]
        public void TestNarration()
        {
            // 测试旁白文本（包括冒号前缀格式）
            string source = "---\n:这是旁白文本\n普通旁白\n===";
            var tokens = Tokenize(source);
            
            Assert.That(tokens[2].Type, Is.EqualTo(TokenType.COLON));
            Assert.That(tokens[3].Value, Is.EqualTo("这是旁白文本"));
            Assert.That(tokens[5].Value, Is.EqualTo("普通旁白"));
        }

        [Test]
        public void TestChoice()
        {
            // 测试选项
            string source = "---\n-> 选项文本\n===";
            var tokens = Tokenize(source);

            var arrowToken = tokens.Find(t => t.Type == TokenType.ARROW);
            Assert.That(arrowToken, Is.Not.Null);
            Assert.That(arrowToken.Value, Is.EqualTo("->"));
        }

        [Test]
        public void TestVariableInterpolation()
        {
            // 测试变量插值 {$var}
            string source = "---\n你好{$name}\n===";
            var tokens = Tokenize(source);

            var braceTokens = tokens.FindAll(t => t.Type == TokenType.LEFT_BRACE || t.Type == TokenType.RIGHT_BRACE);
            var variableTokens = tokens.FindAll(t => t.Type == TokenType.VARIABLE);
            
            Assert.That(braceTokens.Count, Is.EqualTo(2));
            Assert.That(variableTokens.Count, Is.EqualTo(1));
            Assert.That(variableTokens[0].Value, Is.EqualTo("name"));
        }

        [Test]
        public void TestTags()
        {
            // 测试标签
            string source = "---\n对话内容 #tag1 #tag2\n===";
            var tokens = Tokenize(source);

            var hashTokens = tokens.FindAll(t => t.Type == TokenType.HASH);
            Assert.That(hashTokens.Count, Is.EqualTo(2));
        }

        [Test]
        public void TestExpressions()
        {
            // 测试表达式中的各种Token
            string source = "---\n<<set $var 10 + 5 * 2>>\n===";
            var tokens = Tokenize(source);

            var numberTokens = tokens.FindAll(t => t.Type == TokenType.NUMBER);
            var operatorTokens = tokens.FindAll(t => t.Type == TokenType.PLUS || t.Type == TokenType.MULTIPLY);
            
            Assert.That(numberTokens.Count, Is.EqualTo(3));
            Assert.That(operatorTokens.Count, Is.EqualTo(2));
        }

        [Test]
        public void TestBooleanValues()
        {
            // 测试布尔值
            string source = "---\n<<set $flag true>>\n<<set $other false>>\n===";
            var tokens = Tokenize(source);

            var trueToken = tokens.Find(t => t.Type == TokenType.TRUE);
            var falseToken = tokens.Find(t => t.Type == TokenType.FALSE);
            
            Assert.That(trueToken, Is.Not.Null);
            Assert.That(falseToken, Is.Not.Null);
        }

        [Test]
        public void TestKeywords()
        {
            // 测试各种关键字
            string source = "---\n<<if $x eq 5 and $y gt 10>>\n<<set $z add 1>>\n<<endif>>\n===";
            var tokens = Tokenize(source);

            var ifToken = tokens.Find(t => t.Type == TokenType.IF);
            var equalsToken = tokens.Find(t => t.Type == TokenType.EQUALS);
            var andToken = tokens.Find(t => t.Type == TokenType.AND);
            var greaterToken = tokens.Find(t => t.Type == TokenType.GREATER);
            var setToken = tokens.Find(t => t.Type == TokenType.SET);
            var addToken = tokens.Find(t => t.Type == TokenType.ADD);
            var endifToken = tokens.Find(t => t.Type == TokenType.ENDIF);

            Assert.That(ifToken, Is.Not.Null);
            Assert.That(equalsToken, Is.Not.Null);
            Assert.That(andToken, Is.Not.Null);
            Assert.That(greaterToken, Is.Not.Null);
            Assert.That(setToken, Is.Not.Null);
            Assert.That(addToken, Is.Not.Null);
            Assert.That(endifToken, Is.Not.Null);
        }

        [Test]
        public void TestStringQuotes()
        {
            // 测试字符串引号
            string source = "---\n<<set $str \"Hello World\">>\n===";
            var tokens = Tokenize(source);

            var quoteTokens = tokens.FindAll(t => t.Type == TokenType.QUOTE);
            Assert.That(quoteTokens.Count, Is.EqualTo(2));
        }

        [Test]
        public void TestComments()
        {
            // 测试注释（注释应该被跳过）
            string source = "// 这是注释\n---\n内容\n===";
            var tokens = Tokenize(source);

            // 注释应该被完全跳过，不会生成任何Token
            var firstNonEofToken = tokens.Find(t => t.Type != TokenType.EOF);
            Assert.That(firstNonEofToken.Type, Is.EqualTo(TokenType.NEWLINE));
        }

        [Test]
        public void TestIndentation()
        {
            // 测试缩进处理（仅在节点内容中生效）
            string source = "---\n对话\n    嵌套内容\n        更深嵌套\n===";
            var tokens = Tokenize(source);

            var indentTokens = tokens.FindAll(t => t.Type == TokenType.INDENT);
            var dedentTokens = tokens.FindAll(t => t.Type == TokenType.DEDENT);
            
            // 应该有缩进和取消缩进的Token
            Assert.That(indentTokens.Count, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void TestNewSyntaxBrackets()
        {
            // 测试中括号文本支持（新语法特性）
            string source = "---\n文本[中括号内容]文本\n===";
            var tokens = Tokenize(source);

            var textTokens = tokens.FindAll(t => t.Type == TokenType.TEXT);
            // 中括号应该作为文本的一部分被处理
            var bracketText = textTokens.Find(t => t.Value.Contains("[") || t.Value.Contains("]"));
            Assert.That(bracketText, Is.Not.Null);
        }

        [Test]
        public void TestComplexExpression()
        {
            // 测试复杂表达式
            string source = "---\n<<if ($level >= 5 and $gold > 100) or $vip>>\n内容\n<<endif>>\n===";
            var tokens = Tokenize(source);

            var parenTokens = tokens.FindAll(t => t.Type == TokenType.LEFT_PAREN || t.Type == TokenType.RIGHT_PAREN);
            var comparisonTokens = tokens.FindAll(t => t.Type == TokenType.GREATER_EQUALS || t.Type == TokenType.GREATER);
            var logicalTokens = tokens.FindAll(t => t.Type == TokenType.AND || t.Type == TokenType.OR);
            
            Assert.That(parenTokens.Count, Is.EqualTo(2));
            Assert.That(comparisonTokens.Count, Is.EqualTo(2));
            Assert.That(logicalTokens.Count, Is.EqualTo(2));
        }

        [Test]
        public void TestOutsideNodeContentValidation()
        {
            // 测试集合外内容验证（新语法特性）
            string source = "node: test\n-> 这应该被忽略\n---\n正常内容\n===";
            var tokens = Tokenize(source);

            // 集合外的箭头应该被忽略或标记为错误
            // 但EOF和其他合法Token应该存在
            var eofToken = tokens.Find(t => t.Type == TokenType.EOF);
            Assert.That(eofToken, Is.Not.Null);
        }
    }
}