using System.Collections.Generic;
using MookDialogueScript;
using NUnit.Framework;

namespace Tests
{
    public class LexerTests
    {
        private Lexer _lexer;
        private List<Token> _tokens;

        [SetUp]
        public void Setup()
        {
            _lexer = null;
            _tokens = null;
        }

        private void Tokenize(string source)
        {
            _lexer = new Lexer(source);
            _tokens = _lexer.Tokenize();
        }

        [Test]
        public void TestEmptySource()
        {
            Tokenize("");
            Assert.That(_tokens.Count, Is.EqualTo(1));
            Assert.That(_tokens[0].Type, Is.EqualTo(TokenType.EOF));
        }

        [Test]
        public void TestKeywords()
        {
            Tokenize("if elif else endif true false var set add sub mul div mod jump call wait eq is neq gt lt gte lte and or not xor");
            Assert.That(_tokens.Count, Is.EqualTo(28));
            Assert.That(_tokens[0].Type, Is.EqualTo(TokenType.IF));
            Assert.That(_tokens[1].Type, Is.EqualTo(TokenType.ELIF));
            Assert.That(_tokens[2].Type, Is.EqualTo(TokenType.ELSE));
            Assert.That(_tokens[3].Type, Is.EqualTo(TokenType.ENDIF));
            Assert.That(_tokens[4].Type, Is.EqualTo(TokenType.TRUE));
            Assert.That(_tokens[5].Type, Is.EqualTo(TokenType.FALSE));
            Assert.That(_tokens[6].Type, Is.EqualTo(TokenType.VAR));
            Assert.That(_tokens[7].Type, Is.EqualTo(TokenType.SET));
            Assert.That(_tokens[8].Type, Is.EqualTo(TokenType.ADD));
            Assert.That(_tokens[9].Type, Is.EqualTo(TokenType.SUB));
            Assert.That(_tokens[10].Type, Is.EqualTo(TokenType.MUL));
            Assert.That(_tokens[11].Type, Is.EqualTo(TokenType.DIV));
            Assert.That(_tokens[12].Type, Is.EqualTo(TokenType.MOD));
            Assert.That(_tokens[13].Type, Is.EqualTo(TokenType.JUMP));
            Assert.That(_tokens[14].Type, Is.EqualTo(TokenType.CALL));
            Assert.That(_tokens[15].Type, Is.EqualTo(TokenType.WAIT));
            Assert.That(_tokens[16].Type, Is.EqualTo(TokenType.EQUALS));
            Assert.That(_tokens[17].Type, Is.EqualTo(TokenType.EQUALS));
            Assert.That(_tokens[18].Type, Is.EqualTo(TokenType.NOT_EQUALS));
            Assert.That(_tokens[19].Type, Is.EqualTo(TokenType.GREATER));
            Assert.That(_tokens[20].Type, Is.EqualTo(TokenType.LESS));
            Assert.That(_tokens[21].Type, Is.EqualTo(TokenType.GREATER_EQUALS));
            Assert.That(_tokens[22].Type, Is.EqualTo(TokenType.LESS_EQUALS));
            Assert.That(_tokens[23].Type, Is.EqualTo(TokenType.AND));
            Assert.That(_tokens[24].Type, Is.EqualTo(TokenType.OR));
            Assert.That(_tokens[25].Type, Is.EqualTo(TokenType.NOT));
            Assert.That(_tokens[26].Type, Is.EqualTo(TokenType.XOR));
            Assert.That(_tokens[27].Type, Is.EqualTo(TokenType.EOF));
        }

        [Test]
        public void TestNumbers()
        {
            Tokenize("123 45.67");
            Assert.That(_tokens.Count, Is.EqualTo(3));
            Assert.That(_tokens[0].Type, Is.EqualTo(TokenType.NUMBER));
            Assert.That(_tokens[0].Value, Is.EqualTo("123"));
            Assert.That(_tokens[1].Type, Is.EqualTo(TokenType.NUMBER));
            Assert.That(_tokens[1].Value, Is.EqualTo("45.67"));
            Assert.That(_tokens[2].Type, Is.EqualTo(TokenType.EOF));
        }

        [Test]
        public void TestStrings()
        {
            Tokenize("\"Hello World\" 'Single Quote'");
            Assert.That(_tokens.Count, Is.EqualTo(7));
            Assert.That(_tokens[0].Type, Is.EqualTo(TokenType.QUOTE));
            Assert.That(_tokens[1].Type, Is.EqualTo(TokenType.TEXT));
            Assert.That(_tokens[1].Value, Is.EqualTo("Hello World"));
            Assert.That(_tokens[2].Type, Is.EqualTo(TokenType.QUOTE));
            Assert.That(_tokens[3].Type, Is.EqualTo(TokenType.QUOTE));
            Assert.That(_tokens[4].Type, Is.EqualTo(TokenType.TEXT));
            Assert.That(_tokens[4].Value, Is.EqualTo("Single Quote"));
            Assert.That(_tokens[5].Type, Is.EqualTo(TokenType.QUOTE));
            Assert.That(_tokens[6].Type, Is.EqualTo(TokenType.EOF));
        }

        [Test]
        public void TestIndentation()
        {
            Tokenize("line1\n    line2\n        line3");
            Assert.That(_tokens.Count, Is.GreaterThan(1));
            Assert.That(_tokens[0].Type, Is.EqualTo(TokenType.TEXT).Or.EqualTo(TokenType.IDENTIFIER));
            Assert.That(_tokens[0].Value, Is.EqualTo("line1"));
            Assert.That(_tokens[1].Type, Is.EqualTo(TokenType.NEWLINE));
            Assert.That(_tokens[2].Type, Is.EqualTo(TokenType.INDENT));
            Assert.That(_tokens[3].Type, Is.EqualTo(TokenType.TEXT).Or.EqualTo(TokenType.IDENTIFIER));
            Assert.That(_tokens[3].Value, Is.EqualTo("line2"));
        }

        [Test]
        public void TestComments()
        {
            Tokenize("line1 // comment\nline2");
            Assert.That(_tokens.Count, Is.GreaterThan(1));
            Assert.That(_tokens[0].Type, Is.EqualTo(TokenType.TEXT).Or.EqualTo(TokenType.IDENTIFIER));
            Assert.That(_tokens[0].Value, Is.EqualTo("line1"));
            Assert.That(_tokens[1].Type, Is.EqualTo(TokenType.NEWLINE));
            Assert.That(_tokens[2].Type, Is.EqualTo(TokenType.TEXT).Or.EqualTo(TokenType.IDENTIFIER));
            Assert.That(_tokens[2].Value, Is.EqualTo("line2"));
        }

        [Test]
        public void TestOperators()
        {
            Tokenize("+ - * / % = == != > < >= <= && ||");
            Assert.That(_tokens.Count, Is.EqualTo(15));
            Assert.That(_tokens[0].Type, Is.EqualTo(TokenType.PLUS));
            Assert.That(_tokens[1].Type, Is.EqualTo(TokenType.MINUS));
            Assert.That(_tokens[2].Type, Is.EqualTo(TokenType.MULTIPLY));
            Assert.That(_tokens[3].Type, Is.EqualTo(TokenType.DIVIDE));
            Assert.That(_tokens[4].Type, Is.EqualTo(TokenType.MODULO));
            Assert.That(_tokens[5].Type, Is.EqualTo(TokenType.ASSIGN));
            Assert.That(_tokens[6].Type, Is.EqualTo(TokenType.EQUALS));
            Assert.That(_tokens[7].Type, Is.EqualTo(TokenType.NOT_EQUALS));
            Assert.That(_tokens[8].Type, Is.EqualTo(TokenType.GREATER));
            Assert.That(_tokens[9].Type, Is.EqualTo(TokenType.LESS));
            Assert.That(_tokens[10].Type, Is.EqualTo(TokenType.GREATER_EQUALS));
            Assert.That(_tokens[11].Type, Is.EqualTo(TokenType.LESS_EQUALS));
            Assert.That(_tokens[12].Type, Is.EqualTo(TokenType.AND));
            Assert.That(_tokens[13].Type, Is.EqualTo(TokenType.OR));
            Assert.That(_tokens[14].Type, Is.EqualTo(TokenType.EOF));
        }

        [Test]
        public void TestVariables()
        {
            Tokenize("$var1 $var2");
            Assert.That(_tokens.Count, Is.EqualTo(3));
            Assert.That(_tokens[0].Type, Is.EqualTo(TokenType.VARIABLE));
            Assert.That(_tokens[0].Value, Is.EqualTo("var1"));
            Assert.That(_tokens[1].Type, Is.EqualTo(TokenType.VARIABLE));
            Assert.That(_tokens[1].Value, Is.EqualTo("var2"));
            Assert.That(_tokens[2].Type, Is.EqualTo(TokenType.EOF));
        }

        [Test]
        public void TestComplexExpression()
        {
            Tokenize("if $var > 10\n    set $result = true\n}");
            Assert.That(_tokens.Count, Is.GreaterThan(1));
            Assert.That(_tokens[0].Type, Is.EqualTo(TokenType.IF));
            Assert.That(_tokens[1].Type, Is.EqualTo(TokenType.VARIABLE));
            Assert.That(_tokens[1].Value, Is.EqualTo("var"));
            Assert.That(_tokens[2].Type, Is.EqualTo(TokenType.GREATER));
            Assert.That(_tokens[3].Type, Is.EqualTo(TokenType.NUMBER));
            Assert.That(_tokens[3].Value, Is.EqualTo("10"));
            Assert.That(_tokens[4].Type, Is.EqualTo(TokenType.NEWLINE));
        }

        [Test]
        public void TestPlainTextNarration()
        {
            Tokenize("这是一段旁白描述，讲述了故事的背景。");
            Assert.That(_tokens.Count, Is.GreaterThan(1));
            Assert.That(_tokens[0].Type, Is.EqualTo(TokenType.TEXT).Or.EqualTo(TokenType.IDENTIFIER));
            Assert.That(_tokens[0].Value, Is.EqualTo("这是一段旁白描述"));
            Assert.That(_tokens[1].Type, Is.EqualTo(TokenType.COMMA));
            Assert.That(_tokens[1].Value, Is.EqualTo("，"));
            Assert.That(_tokens[2].Type, Is.EqualTo(TokenType.TEXT).Or.EqualTo(TokenType.IDENTIFIER));
            Assert.That(_tokens[2].Value, Is.EqualTo("讲述了故事的背景"));
            Assert.That(_tokens[3].Type, Is.EqualTo(TokenType.TEXT).Or.EqualTo(TokenType.IDENTIFIER));
            Assert.That(_tokens[3].Value, Is.EqualTo("。"));
            Assert.That(_tokens[4].Type, Is.EqualTo(TokenType.EOF));
        }

        [Test]
        public void TestColonPrefixedNarration()
        {
            Tokenize(":这是一段旁白描述，讲述了故事的背景。");
            Assert.That(_tokens.Count, Is.EqualTo(3));
            Assert.That(_tokens[0].Type, Is.EqualTo(TokenType.COLON));
            Assert.That(_tokens[1].Type, Is.EqualTo(TokenType.TEXT).Or.EqualTo(TokenType.IDENTIFIER));
            Assert.That(_tokens[1].Value, Is.EqualTo("这是一段旁白描述，讲述了故事的背景。"));
            Assert.That(_tokens[2].Type, Is.EqualTo(TokenType.EOF));
        }

        [Test]
        public void TestDialogueAndPunctuation()
        {
            Tokenize("小明[害羞地]：你好，我叫小明。");
            Assert.That(_tokens.Count, Is.GreaterThan(1));
            Assert.That(_tokens[0].Type, Is.EqualTo(TokenType.TEXT).Or.EqualTo(TokenType.IDENTIFIER));
            Assert.That(_tokens[0].Value, Is.EqualTo("小明"));
            Assert.That(_tokens[1].Type, Is.EqualTo(TokenType.LEFT_BRACKET));
            Assert.That(_tokens[2].Type, Is.EqualTo(TokenType.TEXT).Or.EqualTo(TokenType.IDENTIFIER));
            Assert.That(_tokens[2].Value, Is.EqualTo("害羞地"));
            Assert.That(_tokens[3].Type, Is.EqualTo(TokenType.RIGHT_BRACKET));
            Assert.That(_tokens[4].Type, Is.EqualTo(TokenType.COLON));

            // 冒号后面的内容应该是一个整体字符串
            Assert.That(_tokens[5].Type, Is.EqualTo(TokenType.TEXT));
            Assert.That(_tokens[5].Value, Is.EqualTo("你好，我叫小明。"));
            Assert.That(_tokens[6].Type, Is.EqualTo(TokenType.EOF));
        }

        [Test]
        public void TestQuotes()
        {
            Tokenize("'单引号' \"双引号\" '中文单引号' \"中文双引号\"");
            Assert.That(_tokens.Count, Is.GreaterThan(1));
            Assert.That(_tokens[0].Type, Is.EqualTo(TokenType.QUOTE));
            Assert.That(_tokens[1].Type, Is.EqualTo(TokenType.TEXT));
            Assert.That(_tokens[1].Value, Is.EqualTo("单引号"));
            Assert.That(_tokens[2].Type, Is.EqualTo(TokenType.QUOTE));
            Assert.That(_tokens[3].Type, Is.EqualTo(TokenType.QUOTE));
            Assert.That(_tokens[4].Type, Is.EqualTo(TokenType.TEXT));
            Assert.That(_tokens[4].Value, Is.EqualTo("双引号"));
            Assert.That(_tokens[5].Type, Is.EqualTo(TokenType.QUOTE));
            Assert.That(_tokens[6].Type, Is.EqualTo(TokenType.QUOTE));
            Assert.That(_tokens[7].Type, Is.EqualTo(TokenType.TEXT));
            Assert.That(_tokens[7].Value, Is.EqualTo("中文单引号"));
            Assert.That(_tokens[8].Type, Is.EqualTo(TokenType.QUOTE));
            Assert.That(_tokens[9].Type, Is.EqualTo(TokenType.QUOTE));
            Assert.That(_tokens[10].Type, Is.EqualTo(TokenType.TEXT));
            Assert.That(_tokens[10].Value, Is.EqualTo("中文双引号"));
            Assert.That(_tokens[11].Type, Is.EqualTo(TokenType.QUOTE));
        }

        [Test]
        public void TestEscapeCharacters()
        {
            Tokenize("这是一个\\{转义\\}字符测试");
            Assert.That(_tokens.Count, Is.EqualTo(3));
            Assert.That(_tokens[0].Type, Is.EqualTo(TokenType.TEXT).Or.EqualTo(TokenType.IDENTIFIER));
            Assert.That(_tokens[0].Value, Is.EqualTo("这是一个"));
            Assert.That(_tokens[1].Type, Is.EqualTo(TokenType.TEXT).Or.EqualTo(TokenType.IDENTIFIER));
            Assert.That(_tokens[1].Value, Is.EqualTo("{转义}字符测试"));
            Assert.That(_tokens[2].Type, Is.EqualTo(TokenType.EOF));
        }

        [Test]
        public void TestSpecialCommands()
        {
            Tokenize("---节点开始\n===");
            Assert.That(_tokens.Count, Is.GreaterThan(1));
            Assert.That(_tokens[0].Type, Is.EqualTo(TokenType.NODE_START));
            Assert.That(_tokens[1].Type, Is.EqualTo(TokenType.IDENTIFIER));
            Assert.That(_tokens[1].Value, Is.EqualTo("节点开始"));
            Assert.That(_tokens[2].Type, Is.EqualTo(TokenType.NEWLINE));
            Assert.That(_tokens[3].Type, Is.EqualTo(TokenType.NODE_END));
        }

        [Test]
        public void TestArrows()
        {
            Tokenize("->选项内容1\n-》选项内容2\n=> 跳转1\n=》跳转2\njump 跳转3");
            Assert.That(_tokens.Count, Is.GreaterThan(1));
            Assert.That(_tokens[0].Type, Is.EqualTo(TokenType.ARROW));
            Assert.That(_tokens[1].Type, Is.EqualTo(TokenType.TEXT));
            Assert.That(_tokens[1].Value, Is.EqualTo("选项内容1"));
            Assert.That(_tokens[2].Type, Is.EqualTo(TokenType.NEWLINE));
            Assert.That(_tokens[3].Type, Is.EqualTo(TokenType.ARROW));
            Assert.That(_tokens[4].Type, Is.EqualTo(TokenType.TEXT));
            Assert.That(_tokens[4].Value, Is.EqualTo("选项内容2"));
            Assert.That(_tokens[5].Type, Is.EqualTo(TokenType.NEWLINE));
            Assert.That(_tokens[6].Type, Is.EqualTo(TokenType.JUMP));
            Assert.That(_tokens[7].Type, Is.EqualTo(TokenType.IDENTIFIER));
            Assert.That(_tokens[7].Value, Is.EqualTo("跳转1"));
            Assert.That(_tokens[8].Type, Is.EqualTo(TokenType.NEWLINE));
            Assert.That(_tokens[9].Type, Is.EqualTo(TokenType.JUMP));
            Assert.That(_tokens[10].Type, Is.EqualTo(TokenType.IDENTIFIER));
            Assert.That(_tokens[10].Value, Is.EqualTo("跳转2"));
            Assert.That(_tokens[11].Type, Is.EqualTo(TokenType.NEWLINE));
            Assert.That(_tokens[12].Type, Is.EqualTo(TokenType.JUMP));
            Assert.That(_tokens[13].Type, Is.EqualTo(TokenType.IDENTIFIER));
            Assert.That(_tokens[13].Value, Is.EqualTo("跳转3"));
        }

        [Test]
        public void TestMultilineText()
        {
            Tokenize("第一行\n第二行\n第三行");
            Assert.That(_tokens.Count, Is.GreaterThan(1));
            Assert.That(_tokens[0].Type, Is.EqualTo(TokenType.TEXT).Or.EqualTo(TokenType.IDENTIFIER));
            Assert.That(_tokens[0].Value, Is.EqualTo("第一行"));
            Assert.That(_tokens[1].Type, Is.EqualTo(TokenType.NEWLINE));
            Assert.That(_tokens[2].Type, Is.EqualTo(TokenType.TEXT).Or.EqualTo(TokenType.IDENTIFIER));
            Assert.That(_tokens[2].Value, Is.EqualTo("第二行"));
            Assert.That(_tokens[3].Type, Is.EqualTo(TokenType.NEWLINE));
            Assert.That(_tokens[4].Type, Is.EqualTo(TokenType.TEXT).Or.EqualTo(TokenType.IDENTIFIER));
            Assert.That(_tokens[4].Value, Is.EqualTo("第三行"));
        }

        [Test]
        public void TestInterpolatedString()
        {
            Tokenize("\"Hello {$name}!\"");
            Assert.That(_tokens.Count, Is.GreaterThan(1));
            Assert.That(_tokens[0].Type, Is.EqualTo(TokenType.QUOTE));
            Assert.That(_tokens[1].Type, Is.EqualTo(TokenType.TEXT));
            Assert.That(_tokens[1].Value, Is.EqualTo("Hello "));
            Assert.That(_tokens[2].Type, Is.EqualTo(TokenType.LEFT_BRACE));
            Assert.That(_tokens[3].Type, Is.EqualTo(TokenType.VARIABLE));
            Assert.That(_tokens[3].Value, Is.EqualTo("name"));
            Assert.That(_tokens[4].Type, Is.EqualTo(TokenType.RIGHT_BRACE));
            Assert.That(_tokens[5].Type, Is.EqualTo(TokenType.TEXT));
            Assert.That(_tokens[5].Value, Is.EqualTo("!"));
            Assert.That(_tokens[6].Type, Is.EqualTo(TokenType.QUOTE));
        }

        [Test]
        public void TestIndentationLevels()
        {
            Tokenize("level0\n    level1\n        level2\n    level1again\nlevel0again\n    level1\n        level2\nlevel0again");
            Assert.That(_tokens.Count, Is.GreaterThan(1));

            // 第一行
            Assert.That(_tokens[0].Type, Is.EqualTo(TokenType.TEXT).Or.EqualTo(TokenType.IDENTIFIER));
            Assert.That(_tokens[0].Value, Is.EqualTo("level0"));
            Assert.That(_tokens[1].Type, Is.EqualTo(TokenType.NEWLINE));

            // 第二行缩进
            Assert.That(_tokens[2].Type, Is.EqualTo(TokenType.INDENT));
            Assert.That(_tokens[3].Type, Is.EqualTo(TokenType.TEXT).Or.EqualTo(TokenType.IDENTIFIER));
            Assert.That(_tokens[3].Value, Is.EqualTo("level1"));
            Assert.That(_tokens[4].Type, Is.EqualTo(TokenType.NEWLINE));

            // 第三行缩进
            Assert.That(_tokens[5].Type, Is.EqualTo(TokenType.INDENT));
            Assert.That(_tokens[6].Type, Is.EqualTo(TokenType.TEXT).Or.EqualTo(TokenType.IDENTIFIER));
            Assert.That(_tokens[6].Value, Is.EqualTo("level2"));
            Assert.That(_tokens[7].Type, Is.EqualTo(TokenType.NEWLINE));

            // 第四行缩进减少
            Assert.That(_tokens[8].Type, Is.EqualTo(TokenType.DEDENT));
            Assert.That(_tokens[9].Type, Is.EqualTo(TokenType.TEXT).Or.EqualTo(TokenType.IDENTIFIER));
            Assert.That(_tokens[9].Value, Is.EqualTo("level1again"));
            Assert.That(_tokens[10].Type, Is.EqualTo(TokenType.NEWLINE));

            // 第五行缩进减少
            Assert.That(_tokens[11].Type, Is.EqualTo(TokenType.DEDENT));
            Assert.That(_tokens[12].Type, Is.EqualTo(TokenType.TEXT).Or.EqualTo(TokenType.IDENTIFIER));
            Assert.That(_tokens[12].Value, Is.EqualTo("level0again"));
            Assert.That(_tokens[13].Type, Is.EqualTo(TokenType.NEWLINE));

            // 第六行缩进
            Assert.That(_tokens[14].Type, Is.EqualTo(TokenType.INDENT));
            Assert.That(_tokens[15].Type, Is.EqualTo(TokenType.TEXT).Or.EqualTo(TokenType.IDENTIFIER));
            Assert.That(_tokens[15].Value, Is.EqualTo("level1"));
            Assert.That(_tokens[16].Type, Is.EqualTo(TokenType.NEWLINE));

            // 第七行缩进
            Assert.That(_tokens[17].Type, Is.EqualTo(TokenType.INDENT));
            Assert.That(_tokens[18].Type, Is.EqualTo(TokenType.TEXT).Or.EqualTo(TokenType.IDENTIFIER));
            Assert.That(_tokens[18].Value, Is.EqualTo("level2"));
            Assert.That(_tokens[19].Type, Is.EqualTo(TokenType.NEWLINE));

            // 第八行连续缩进减少
            Assert.That(_tokens[20].Type, Is.EqualTo(TokenType.DEDENT));
            Assert.That(_tokens[21].Type, Is.EqualTo(TokenType.DEDENT));
            Assert.That(_tokens[22].Type, Is.EqualTo(TokenType.TEXT).Or.EqualTo(TokenType.IDENTIFIER));
            Assert.That(_tokens[22].Value, Is.EqualTo("level0again"));

        }

        [Test]
        public void TestHashTags()
        {
            Tokenize("小明：你好，世界！ #标签1 #包含:冒号 #包含\"引号\"");
            Assert.That(_tokens.Count, Is.GreaterThan(1));

            // 先找到COLON（冒号）的位置
            int colonIndex = -1;
            for (int i = 0; i < _tokens.Count; i++)
            {
                if (_tokens[i].Type == TokenType.COLON)
                {
                    colonIndex = i;
                    break;
                }
            }

            Assert.That(colonIndex, Is.GreaterThan(0), "未找到冒号标记");

            // 找到所有HASH标记
            var hashIndices = new List<int>();
            for (int i = colonIndex + 1; i < _tokens.Count - 1; i++)
            {
                if (_tokens[i].Type == TokenType.HASH)
                {
                    hashIndices.Add(i);
                }
            }

            // 确保找到了所有#标记
            Assert.That(hashIndices.Count, Is.EqualTo(3), "没有找到预期数量的标签标记");

            // 检查第一个标签
            Assert.That(_tokens[hashIndices[0] + 1].Type, Is.EqualTo(TokenType.TEXT).Or.EqualTo(TokenType.IDENTIFIER));
            Assert.That(_tokens[hashIndices[0] + 1].Value, Is.EqualTo("标签1"));

            // 检查第二个标签（包含冒号）
            Assert.That(_tokens[hashIndices[1] + 1].Type, Is.EqualTo(TokenType.TEXT).Or.EqualTo(TokenType.IDENTIFIER));
            Assert.That(_tokens[hashIndices[1] + 1].Value, Is.EqualTo("包含"));
            Assert.That(_tokens[hashIndices[1] + 2].Type, Is.EqualTo(TokenType.COLON));
            Assert.That(_tokens[hashIndices[1] + 3].Type, Is.EqualTo(TokenType.TEXT).Or.EqualTo(TokenType.IDENTIFIER));
            Assert.That(_tokens[hashIndices[1] + 3].Value, Is.EqualTo("冒号 "));

            // 检查第三个标签（包含引号）
            Assert.That(_tokens[hashIndices[2] + 1].Type, Is.EqualTo(TokenType.TEXT).Or.EqualTo(TokenType.IDENTIFIER));
            Assert.That(_tokens[hashIndices[2] + 1].Value, Is.EqualTo("包含"));
            // 引号在标签模式下应该被当作普通文本处理
            Assert.That(_tokens[hashIndices[2] + 2].Type, Is.EqualTo(TokenType.TEXT));
            Assert.That(_tokens[hashIndices[2] + 2].Value, Is.EqualTo("\"引号\""));
        }

        [Test]
        public void TestColonInDialogueText()
        {
            Tokenize("小明：我的时间是10:30，不要迟到哦！");
            Assert.That(_tokens.Count, Is.EqualTo(4));

            // 检查角色名
            Assert.That(_tokens[0].Type, Is.EqualTo(TokenType.TEXT).Or.EqualTo(TokenType.IDENTIFIER));
            Assert.That(_tokens[0].Value, Is.EqualTo("小明"));

            // 检查冒号
            Assert.That(_tokens[1].Type, Is.EqualTo(TokenType.COLON));

            // 检查冒号后的内容应该是一个整体文本，包括时间中的冒号
            Assert.That(_tokens[2].Type, Is.EqualTo(TokenType.TEXT));
            Assert.That(_tokens[2].Value, Is.EqualTo("我的时间是10:30，不要迟到哦！"));
        }

        [Test]
        public void TestHashInQuotedString()
        {
            // 测试引号内的#号不会被当作标签处理
            Tokenize("\"这是一个引号内的#标签，不应该被当作标签\" #这才是真正的标签");

            // 检查引号
            Assert.That(_tokens[0].Type, Is.EqualTo(TokenType.QUOTE));
            Assert.That(_tokens[1].Type, Is.EqualTo(TokenType.TEXT));
            Assert.That(_tokens[1].Value, Is.EqualTo("这是一个引号内的#标签，不应该被当作标签"));
            Assert.That(_tokens[2].Type, Is.EqualTo(TokenType.QUOTE));

            // 检查#号
            Assert.That(_tokens[3].Type, Is.EqualTo(TokenType.HASH));
            Assert.That(_tokens[4].Type, Is.EqualTo(TokenType.TEXT).Or.EqualTo(TokenType.IDENTIFIER));
            Assert.That(_tokens[4].Value, Is.EqualTo("这才是真正的标签"));

        }

        [Test]
        public void TestNestedQuotesWithHash()
        {
            // 测试嵌套引号内的#号处理
            Tokenize("\"他说：'这里有个#符号'，很特别\" #外部标签");

            Assert.That(_tokens[0].Type, Is.EqualTo(TokenType.QUOTE));
            Assert.That(_tokens[1].Type, Is.EqualTo(TokenType.TEXT));
            Assert.That(_tokens[1].Value, Is.EqualTo("他说：'这里有个#符号'，很特别"));
            Assert.That(_tokens[2].Type, Is.EqualTo(TokenType.QUOTE));

            Assert.That(_tokens[3].Type, Is.EqualTo(TokenType.HASH));
            Assert.That(_tokens[4].Type, Is.EqualTo(TokenType.TEXT).Or.EqualTo(TokenType.IDENTIFIER));
            Assert.That(_tokens[4].Value, Is.EqualTo("外部标签"));
        }

        [Test]
        public void TestHashInStringInterpolation()
        {
            // 测试字符串插值中的#号处理
            Tokenize("\"这是{$count}个#符号\" #标签");

            Assert.That(_tokens[0].Type, Is.EqualTo(TokenType.QUOTE));
            Assert.That(_tokens[1].Type, Is.EqualTo(TokenType.TEXT));
            Assert.That(_tokens[1].Value, Is.EqualTo("这是"));
            Assert.That(_tokens[2].Type, Is.EqualTo(TokenType.LEFT_BRACE));
            Assert.That(_tokens[3].Type, Is.EqualTo(TokenType.VARIABLE));
            Assert.That(_tokens[3].Value, Is.EqualTo("count"));
            Assert.That(_tokens[4].Type, Is.EqualTo(TokenType.RIGHT_BRACE));
            Assert.That(_tokens[5].Type, Is.EqualTo(TokenType.TEXT));
            Assert.That(_tokens[5].Value, Is.EqualTo("个#符号"));
            Assert.That(_tokens[6].Type, Is.EqualTo(TokenType.QUOTE));

            Assert.That(_tokens[7].Type, Is.EqualTo(TokenType.HASH));
            Assert.That(_tokens[8].Type, Is.EqualTo(TokenType.TEXT).Or.EqualTo(TokenType.IDENTIFIER));
            Assert.That(_tokens[8].Value, Is.EqualTo("标签"));
        }

        [Test]
        public void TestColonInMetadata()
        {
            Tokenize("[title:测试标题]");
            Assert.That(_tokens.Count, Is.EqualTo(6));

            // 检查左括号
            Assert.That(_tokens[0].Type, Is.EqualTo(TokenType.LEFT_BRACKET));

            // 检查键名
            Assert.That(_tokens[1].Type, Is.EqualTo(TokenType.IDENTIFIER));
            Assert.That(_tokens[1].Value, Is.EqualTo("title"));

            // 检查冒号
            Assert.That(_tokens[2].Type, Is.EqualTo(TokenType.COLON));

            // 冒号后的值不应该被当作对话文本（一个完整的TOKEN），而是被当作标识符或文本
            Assert.That(_tokens[3].Type, Is.EqualTo(TokenType.IDENTIFIER).Or.EqualTo(TokenType.TEXT));
            Assert.That(_tokens[3].Value, Is.EqualTo("测试标题"));

            // 检查右括号
            Assert.That(_tokens[4].Type, Is.EqualTo(TokenType.RIGHT_BRACKET));
        }
    }
}
