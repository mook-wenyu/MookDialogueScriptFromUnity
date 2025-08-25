# MookDialogueScript

MookDialogueScript 是一个轻量级的对话脚本系统，专为 Unity 游戏开发设计。它提供了一个简单而强大的脚本语言，用于创建复杂的对话系统和分支剧情。

## 功能特性

- 简单易用的脚本语法
- 支持变量系统和条件判断
- 支持函数调用和标签系统
- 支持节点跳转和分支剧情
- 支持与 C# 代码的深度集成
- 支持中文和英文混合使用

## VSCode 语法高亮支持

为了提供更好的编辑体验，我们提供了 VSCode 语法高亮插件：[MookDialogueScript Language](https://github.com/mook-wenyu/mookdialoguescript-lang)

插件特性：
- 完整的语法高亮支持（节点、元数据、条件语句、系统命令等）
- 智能代码片段（快速插入常用语法结构）
- 便捷的编辑功能（自动缩进、代码折叠、括号匹配）
- 支持中英文符号混用

使用方法：
1. 在 VSCode 扩展市场中搜索 "MookDialogueScript Lang"
2. 安装插件
3. 创建 `.mds` 文件即可享受语法高亮支持

## 快速开始

### 安装

#### 方法一：通过 Unity 包管理器从 Git URL 安装

1. 打开 Unity 编辑器
2. 选择菜单：Window > Package Manager
3. 点击左上角的"+"按钮
4. 选择"Add package from git URL..."
5. 输入以下 URL：
   ```
   https://github.com/mook-wenyu/MookDialogueScriptFromUnity.git?path=Assets/MookDialogueScript
   ```
6. 点击"Add"按钮

#### 方法二：手动安装

1. 克隆或下载本项目
2. 将 `Assets/Scripts/MookDialogueScript` 文件夹复制到你的 Unity 项目中
3. 在 Unity 中导入必要的依赖项

### 基本用法

1. 创建对话脚本文件（.mds 后缀）
2. 在 Unity 中加载并运行对话脚本
3. 使用 C# 代码与对话系统交互

### 示例代码

```csharp
// 创建对话管理器单例
public class DialogueMgr : MonoBehaviour
{
    public static DialogueMgr Instance { get; private set; }
    public Runner RunMgrs { get; private set; }

    void Awake()
    {
        Instance = this;
        Initialize();
    }

    public void Initialize()
    {
        Debug.Log("开始初始化对话系统");
        RunMgrs = new Runner();

        // 注册玩家对象（同时注册其属性、字段和方法，使它们在脚本中可访问）
        var player = new Player("二狗");
        RunMgrs.RegisterObject("player", player);  // 会自动注册所有公共属性、字段和方法

        // 注册游戏变量
        RunMgrs.RegisterVariable("gold", new RuntimeValue(100));
        RunMgrs.RegisterVariable("has_key", new RuntimeValue(false));

        // 注册C#(内置)变量
        RunMgrs.RegisterVariable("game_difficulty",
            () => GameSystem.Difficulty,
            (value) => GameSystem.Difficulty = (int)value
        );

        // 注册委托函数
        RunMgrs.RegisterFunction("show_message", (string message) => {
            Debug.Log($"[消息] {message}");
        });
        RunMgrs.RegisterFunction("calculate_damage", (int base_damage, float multiplier) => {
            return (int)(base_damage * multiplier);
        });
    }
}

// 玩家类示例
public class Player
{
    public string Name { get; set; }
    public int Level { get; set; }
    public int Health { get; set; }
    public bool IsAlive { get; set; }

    public Player(string name, int level = 1)
    {
        Name = name;
        Level = level;
        Health = level * 10;
        IsAlive = true;
    }

    public string GetStatus()
    {
        return $"{Name} (Lv.{Level}) - HP: {Health}";
    }

    public void TakeDamage(int amount)
    {
        Health -= amount;
        if (Health <= 0)
        {
            Health = 0;
            IsAlive = false;
        }
    }

    public void Heal(int amount)
    {
        if (IsAlive)
        {
            Health += amount;
            Health = Math.Min(Health, Level * 10);
        }
    }
}

// 游戏系统类示例（静态类）
public static class GameSystem
{
    [ScriptVar("game_version")]
    public static string GameVersion { get; } = "1.0.0";

    [ScriptVar("game_difficulty")]
    public static int Difficulty { get; set; } = 1;

    [ScriptVar("is_debug_mode")]
    public static bool IsDebugMode { get; set; } = false;

    [ScriptFunc]
    public static void ShowNotification(string title, string content)
    {
        Debug.Log($"[{title}] {content}");
    }

    [ScriptFunc]
    public static int GetMaxHealth(int level)
    {
        return level * 10;
    }
}
```

## 脚本语法

### 基本语法

```mds
// 节点元数据（位于节点定义前）
node: start
priority: 1
enabled: true
---

// 定义变量
<<var $player_name "冒险者">>
<<set $player_level 1>>
<<set $gold 100>>

// 对话
商人: 欢迎来到我的商店，{$player_name}。 #welcome
    这是补充说明。
    <<set $visited true>>

// 旁白文本（冒号开头）
:这是一个宁静的小镇，阳光明媚。

// 条件判断
<<if $player_level < 5>>
    商人: 看起来你是个新手冒险者。
<<else>>
    商人: 欢迎回来，经验丰富的冒险者！
<<endif>>

// 选项分支
-> 购买武器
    商人: 这是我们的武器清单。

-> 离开 <<if $gold > 0>>
    商人: 再见，欢迎下次光临。

// 节点结束
===
```

### 完整语法规则

以下是 MookDialogueScript 的完整语法规则和使用方法：

#### 注释

```mds
// 这是一行注释
```

- 所有注释必须单独一行
- 注释以 `//` 开头
- 注释可以放在任何位置
- 注释不会影响脚本执行

#### 节点系统

```mds
// 新语法：节点元数据在 --- 之前定义
node: start
priority: 1
enabled: true
---
// 节点内容
===

// 无需元数据的节点
---
// 节点内容
===
```

节点系统说明：

- **元数据必须在 `---` 之前定义**，使用 `key: value` 格式
- 必须至少有 `node: 节点名` 元数据来定义节点名称
- 节点结束标记 `===` 是必需的
- 节点内容只能包含四种类型：对话/旁白、选项(`->`)、命令(`<<>>`)、注释(`//`)

#### 变量系统

```mds
// 定义和操作变量（必须在命令块内）
<<var $name "玩家名称">>
<<set $level 1>>
<<add $gold 50>>
<<sub $gold 30>>
<<mul $experience 1.5>>
<<div $gold 2>>

// 对象成员访问（属性和字段）
player.name
player.health
player.level

// 变量插值
这是{player.name}的属性，等级为{player.level}
```

变量访问说明：
- 变量以 `$` 开头
- 所有变量操作必须在 `<<>>` 命令块内
- 对象成员使用 `objectName.propertyName` 格式访问
- 在文本中可以使用 `{$variable}` 进行插值

#### 函数系统

```mds
// 函数调用（必须在命令块内）
<<call log("这是一条日志")>>
<<call player.take_damage(10)>>

// 在表达式中调用函数（不需要call关键字）
<<if random() > 0.5>>
    // 做某事
<<endif>>

// 在插值中调用函数
商人: 你的状态是：{player.get_status()}
```

函数调用说明：
- 直接调用函数时必须在 `<<call>>` 命令块内
- 对象函数使用 `objectName.methodName()` 格式调用
- 在表达式或插值中使用函数时，不需要 `call` 关键字

#### 等待控制

```mds
// 等待控制（必须在命令块内）
<<wait 1>>
<<wait 0.01>>
```

#### 对话系统

```mds
// 基本对话
商人: 欢迎光临！ #标签1 #标签2
    这是嵌套内容。
    <<set $visited true>>

// 旁白文本（冒号开头，冒号会作为文本显示）
:这是一个宁静的小镇，阳光明媚。
```

对话格式说明：
- 基本对话：`角色名: 对话内容`
- 旁白文本：`:旁白内容` （冒号会作为文本的一部分显示）
- 支持嵌套内容（缩进表示）
- 每行对话会自动生成行号标签（如 `#line:start1`）
- 标签使用 `#` 开头

#### 选择分支

```mds
-> 购买武器 <<if $gold >= 100>>
    商人: 这是我们的武器清单。
    <<set $gold $gold - 100>>

-> 离开
    商人: 再见，欢迎下次光临。
    <<jump town>>
```

选择分支说明：
- 选项使用 `->` 标记
- 选项可以添加条件，使用 `<<if>>` 格式
- 选项内容必须缩进，表示属于该选项
- 支持嵌套选项

#### 条件语句

```mds
<<if $level >= 10>>
    商人: 你已经是高级冒险者了！
<<elif $level >= 5>>
    商人: 你已经有一定经验了。
<<else>>
    商人: 你看起来是个新手。
<<endif>>
```

条件语句说明：
- 使用 `<<if>>` `<<elif>>` `<<else>>` `<<endif>>` 结构
- 条件表达式支持比较运算符和逻辑运算符
- 条件语句内容必须缩进
- 支持嵌套条件语句

#### 节点跳转

```mds
// 跳转命令（必须在命令块内）
<<jump shop>>
```

### 关键字

1. 条件控制：`<<if>>`, `<<elif>>`, `<<else>>`, `<<endif>>`
2. 布尔值：`true`, `false`
3. 变量操作：`<<var>>`, `<<set>>`, `<<add>>`, `<<sub>>`, `<<mul>>`, `<<div>>`, `<<mod>>`
4. 跳转控制：`<<jump>>`
5. 函数调用：`<<call>>`
6. 等待控制：`<<wait>>`（单位：秒，可精确到毫秒，如 `<<wait 0.01>>`）
7. 比较运算：
   - 等于：`==` 或 `eq` 或 `is`
   - 不等于：`!=` 或 `neq`
   - 大于：`>` 或 `gt`
   - 小于：`<` 或 `lt`
   - 大于等于：`>=` 或 `gte`
   - 小于等于：`<=` 或 `lte`
8. 逻辑运算：
   - 与：`&&` 或 `and`
   - 或：`||` 或 `or`
   - 非：`!` 或 `not`
   - 异或：`^` 或 `xor`

## 与 C# 集成

### 创建 Runner

Runner 是对话系统的核心组件，负责加载和执行对话脚本。创建 Runner 是使用 MookDialogueScript 的第一步。

```csharp
// 方法一：使用默认加载器（从 Resources 文件夹加载脚本）
Runner runner = new Runner();

// 方法二：指定脚本根目录
Runner runner = new Runner("DialogueScripts");

// 方法三：使用自定义加载器
Runner runner = new Runner(new CustomDialogueLoader());
```

在 MonoBehaviour 中创建和初始化 Runner：
```csharp
public class DialogueMgr : MonoBehaviour
{
    public static DialogueMgr Instance { get; private set; }
    public Runner RunMgrs { get; private set; }

    void Awake()
    {
        Instance = this;
        Initialize();
    }

    public void Initialize()
    {
        Debug.Log("开始初始化对话系统");
        RunMgrs = new Runner();

        // 注册对象、变量和函数
        // ...
    }
}
```

### 对话状态存储 (DialogueStorage)

DialogueStorage 类用于记录对话状态和变量，支持保存和加载游戏进度，是实现游戏存档功能的关键组件。

在 Runner 中使用：
```csharp
// 获取当前存储
DialogueStorage storage = runner.Storage;

// 设置自定义存储
runner.SetStorage(customStorage);

// 获取并更新当前存储（用于保存游戏）
DialogueStorage updatedStorage = runner.GetCurrentStorage();
```

### 注册变量

变量注册提供了多种方式，可以注册静态变量、脚本变量和C#变量，使它们在脚本中可用。所有注册的变量都支持在脚本中读取和修改。

```csharp
// 1. 使用 ScriptVar 特性标记静态变量
// 这种方式适用于全局静态变量，会自动注册到对话系统中
[ScriptVar("game_version")]
public static string GameVersion { get; } = "1.0.0";

// 2. 注册脚本变量
// 这种方式用于注册简单的值类型变量
RunMgrs.RegisterVariable("gold", new RuntimeValue(100));

// 3. 注册C#变量
// 这种方式可以将C#变量与脚本变量双向绑定，支持getter和setter
RunMgrs.RegisterVariable("game_difficulty",
    () => GameSystem.Difficulty,    // getter：从C#读取值
    (value) => GameSystem.Difficulty = (int)value    // setter：将值写回C#
);
```

在脚本中使用变量：
```mds
// 访问变量
商人: 你有{$gold}金币。
商人: 游戏版本：{$game_version}
商人: 当前难度：{$game_difficulty}

// 修改变量（包括C#变量）
set $gold 200
set $game_difficulty 2  // 这会通过setter修改C#中的 GameSystem.Difficulty
add $gold 50
sub $gold 30
```

### 注册函数

函数注册支持多种方式，可以注册静态函数、委托函数，使它们在脚本中可调用。

```csharp
// 1. 使用 ScriptFunc 特性标记静态函数
// 这种方式适用于全局静态函数，会自动注册到对话系统中
[ScriptFunc]
public static void ShowNotification(string title, string content)
{
    Debug.Log($"[{title}] {content}");
}

// 2. 注册委托函数
// 这种方式可以直接注册lambda表达式或委托
RunMgrs.RegisterFunction("calculate_damage", (int base_damage, float multiplier) => {
    return (int)(base_damage * multiplier);
});
```

在脚本中调用函数：
```mds
// 直接调用函数
call ShowNotification("提示", "这是一条通知")

// 在表达式中使用函数
var $damage calculate_damage(10, 1.5)

// 在插值中使用函数
商人: 计算伤害：{calculate_damage(10, 1.5)}
```

### 注册对象

对象注册会同时注册对象的属性、字段和方法，使它们在脚本中可用。属性和字段会以 `objectName.propertyName` 或 `objectName.fieldName` 的形式注册。注册的对象属性和字段都支持双向绑定，可以在脚本中修改C#对象的值。

```csharp
// 创建对象实例
var player = new Player("二狗");

// 注册对象（同时注册其属性、字段和方法）
RunMgrs.RegisterObject("player", player);

// 在脚本中访问对象成员
// 属性访问：player.name, player.level
// 字段访问：player.health, player.isAlive
// 方法调用：player.get_status(), player.take_damage(10)
```

示例：
```mds
// 访问对象属性和字段
商人: 欢迎你，{player.name}！你的生命值是{player.health}。

// 修改对象属性或字段（会直接修改C#对象的值）
<<set player.name "张三">>
<<set player.health 100>>

// 调用对象方法
商人: 让我看看你的状态... {player.get_status()}

// 在命令中调用方法
<<call player.take_damage(10)>>
```

### 创建自定义加载器

MookDialogueScript 提供了 `IDialogueLoader` 接口，允许你创建自定义的脚本加载器。默认的Unity加载器会从 Resources 文件夹加载脚本，但你可以根据需要实现自己的加载逻辑。

```csharp
// 1. 实现 IDialogueLoader 接口
public class CustomDialogueLoader : IDialogueLoader
{
    private readonly string _rootDir;
    private readonly string[] _extensions = new[] {".txt", ".mds"};

    public CustomDialogueLoader(string rootDir = "DialogueScripts")
    {
        _rootDir = rootDir;
    }

    public void LoadScripts(Runner runner)
    {
        // 自定义加载逻辑
        // 例如：从网络加载、从本地文件加载、从数据库加载等

        // 示例：从 StreamingAssets 加载
        string scriptPath = Path.Combine(Application.streamingAssetsPath, _rootDir);
        if (Directory.Exists(scriptPath))
        {
            foreach (string file in Directory.GetFiles(scriptPath, "*.*", SearchOption.AllDirectories))
            {
                string extension = Path.GetExtension(file).ToLower();
                if (_extensions.Contains(extension))
                {
                    try
                    {
                        string scriptContent = File.ReadAllText(file);
                        string scriptName = Path.GetFileNameWithoutExtension(file);
                        LoadScriptContent(scriptContent, runner, scriptName);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"加载脚本文件 {file} 时出错: {ex}");
                    }
                }
            }
        }
        else
        {
            Debug.LogWarning($"StreamingAssets 目录 {scriptPath} 不存在，将创建该目录");
            Directory.CreateDirectory(scriptPath);
        }
    }

    private void LoadScriptContent(string scriptContent, Runner runner, string filePath)
    {
        try
        {
            // 创建词法分析器
            var lexer = new Lexer(scriptContent);

            // 创建语法分析器
            var parser = new Parser(lexer.Tokenize());
            var scriptNode = parser.Parse();

            // 注册脚本节点
            runner.RegisterScript(scriptNode);
        }
        catch (Exception ex)
        {
            Debug.LogError($"解析脚本内容时出错 (文件: {filePath}): {ex}");
        }
    }
}

// 2. 使用自定义加载器创建 Runner
public class DialogueMgr : MonoBehaviour
{
    public static DialogueMgr Instance { get; private set; }
    public Runner RunMgrs { get; private set; }

    void Awake()
    {
        Instance = this;
        Initialize();
    }

    public void Initialize()
    {
        Debug.Log("开始初始化对话系统");

        // 使用自定义加载器，从 StreamingAssets 加载脚本
        RunMgrs = new Runner(new CustomDialogueLoader());
    }
}

```

### 内置函数

MookDialogueScript 提供了一系列内置函数，可以直接在脚本中使用，无需额外注册。

#### visited(string node_name)

检查指定节点是否已被访问过。
- 参数: 
  - `node_name`: 节点名称
- 返回值: 布尔值，表示指定节点是否已访问

```mds
// 检查节点 "shop" 是否已被访问
if visited("shop")
    商人: 欢迎再次光临！
else
    商人: 这是你第一次来我的商店吧？
endif
```

#### visit_count(string node_name)

返回指定节点被访问的次数。
- 参数:
  - `node_name`: 节点名称
- 返回值: 整数，表示节点被访问的次数

```mds
// 获取节点 "shop" 的访问次数
var $shop_visits visit_count("shop")
商人: 这是你第{$shop_visits}次来我的商店。

// 在条件判断中使用
if visit_count("shop") > 3
    商人: 看来你很喜欢我的商店！
endif
```

#### concat(string str1, string str2, ...)

连接多个字符串。
- 参数:
  - `str1, str2, ...`: 要连接的字符串参数，可以是变量或字面量
- 返回值: 字符串，表示连接后的结果

```mds
// 连接字符串和变量
var $full_name concat($first_name, " ", $last_name)

// 在插值中使用
商人: 你好，{concat($title, " ", player.name)}！
```

#### random(int digits = 2)

返回 0 到 1 之间的随机浮点数，可以指定小数位数。
- 参数:
  - `digits`: 小数点后的位数，默认为2位
- 返回值: 浮点数，范围在 [0.0, 1.0) 之间

```mds
// 获取随机数（默认2位小数）
var $chance random()

// 获取随机数（4位小数）
var $precise_chance random(4)

// 在条件判断中使用
if random() > 0.7
    商人: 今天运气不错，给你打个折！
endif
```

#### random_range(float min, float max, int digits = 2)

返回指定范围内的随机浮点数，可以指定小数位数。
- 参数:
  - `min`: 最小值（包含）
  - `max`: 最大值（包含）
  - `digits`: 小数点后的位数，默认为2位
- 返回值: 浮点数，范围在 [min, max] 之间

```mds
// 获取 1 到 10 之间的随机整数
var $random_number random_range(1, 10)

// 获取 0.5 到 1.5 之间的随机浮点数（3位小数）
var $random_float random_range(0.5, 1.5, 3)

// 在对话中使用
商人: 今天的汇率是...{random_range(6.8, 7.2, 4)}！
```

#### dice(int sides, int count = 1)

模拟掷骰子，返回指定数量和面数骰子的总和。
- 参数:
  - `sides`: 骰子面数
  - `count`: 骰子数量，默认为1
- 返回值: 整数，表示所有骰子点数的总和

```mds
// 掷一个六面骰子 (1d6)
var $roll dice(6)

// 掷三个八面骰子 (3d8)
var $damage dice(8, 3)

// 在对话中使用
战士: 我攻击敌人，造成了{dice(6, 2)}点伤害！
```

## 更新说明

### 版本 0.5.2 - 解析器性能优化

#### 性能优化
- **StringBuilder 对象池**：新增线程不安全的 StringBuilder 池，减少内存分配和GC压力
- **内联方法优化**：为关键路径方法添加 `MethodImpl(AggressiveInlining)` 特性
- **静态缓存优化**：优化运算符优先级和文本终止条件判断，使用静态方法替代字典查找
- **LINQ替换**：用高性能的循环替代LINQ操作，减少运行时开销

#### 代码结构优化
- **去除 System.Linq 依赖**：使用自定义轻量级方法替代LINQ操作
- **错误处理增强**：优化错误恢复机制，增加前置检查避免死循环
- **字符串处理优化**：仅去除末尾空白，保留有意义的前导空格
- **内存管理改进**：统一使用对象池管理临时对象，避免大对象回池

#### 技术改进
- **运算符判定优化**：使用 switch 语句替代字典查找，提升判断性能
- **文本解析优化**：统一文本终止条件判断，减少重复代码分支
- **容器初始化优化**：为集合类型指定合适的初始容量，减少扩容开销
- **前进保护机制**：增加解析过程中的死循环检测和防护

### 版本 0.5.1 - 代码质量优化与性能改进

#### 代码格式化与优化
- **代码格式统一**：全面格式化所有源代码文件，统一缩进和空白符使用
- **现代C#语法优化**：进一步优化代码结构，使用表达式体成员和模式匹配
- **性能微调**：优化字符串格式化和集合操作，减少不必要的内存分配
- **错误处理改进**：统一异常处理机制，提供更清晰的错误信息

#### 技术改进
- **RuntimeValue结构优化**：进一步完善值语义比较性能
- **Helper类重构**：优化类型转换逻辑，提升运行效率
- **DialogueContext重构**：简化对象注册逻辑，优化内存使用
- **FunctionManager改进**：优化函数注册和调用性能

#### 测试与文档
- **测试覆盖率提升**：增强词法分析器和解析器测试用例
- **文档更新**：完善API文档和使用示例

### 版本 0.5.0 - 架构重构与性能优化

#### 核心架构优化
- **RuntimeValue性能优化**：重构为只读结构体，实现高性能值语义比较和优化的哈希计算
- **Helper工具类完善**：统一类型转换逻辑，优化性能，减少重复代码
- **现代C#语法应用**：全面采用模式匹配、表达式体方法等现代语法特性
- **内存管理优化**：减少GC压力，优化对象分配策略

#### 功能增强
- **错误处理机制完善**：改进解析错误恢复机制，提供更友好的错误信息
- **代码剥离支持**：添加Preserve特性支持Unity代码剥离优化
- **测试覆盖率提升**：完善单元测试，提高代码质量保证
- **API设计优化**：简化接口使用，提升开发者体验

#### 性能提升
- **解析器性能优化**：优化词法分析和语法解析算法
- **执行效率提升**：优化解释器执行逻辑，减少运行时开销
- **缓存机制改进**：优化变量和函数查找缓存策略
- **集合操作优化**：使用现代C#集合操作方法，提升性能

#### 代码质量改进
- **统一编码规范**：严格遵循项目编码标准
- **注释文档完善**：增加详细的中文注释说明
- **架构设计优化**：改进组件间职责分离，提升可维护性
- **单元测试扩展**：增加更全面的测试用例覆盖

### 版本 0.3.0 - 语法重构与性能优化

#### 新语法特性
- **统一的命令语法**：所有命令现在都使用 `<<命令>>` 格式，包括变量操作、函数调用、条件语句等
- **元数据前置**：节点元数据现在在 `---` 之前定义，使用 `key: value` 格式
- **自动行标签**：每行对话会自动生成唯一的行号标签（如 `#line:start1`）
- **嵌套内容支持**：对话和选项支持嵌套内容，使用缩进表示层级关系
- **中括号文本支持**：文本中的中括号（如 `[动作描述]`）不再被截断，可以正常显示

#### 性能优化
- **RuntimeValue优化**：重构为只读结构体，实现了高性能的值语义相等比较
- **类型转换优化**：重构类型转换逻辑到Helper类，提升转换性能
- **内存优化**：减少不必要的内存分配，优化GC压力
- **代码重构**：使用现代C#语法模式匹配，提升代码可读性和执行效率

#### 语法变更
- 变量操作：`var $name "value"` → `<<var $name "value">>`
- 条件语句：`if condition` → `<<if condition>>`
- 函数调用：`call function()` → `<<call function()>>`
- 节点跳转：`=> node` → `<<jump node>>`
- 等待命令：`wait 1` → `<<wait 1>>`

#### 向后兼容性
- **破坏性更新**：此版本包含语法变更，旧版本脚本需要手动更新语法格式
- 建议使用新的语法格式编写脚本以获得更好的解析性能和错误处理能力


本项目采用 Apache License 2.0 开源协议。详情请参阅 [LICENSE](LICENSE.txt) 文件。

## 联系方式

如有任何问题或建议，请通过以下方式联系我们：

- 提交 Issue
- 发送邮件至 [1317578863@qq.com]
