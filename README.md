# MookDialogueScript

MookDialogueScript 是一个轻量级的对话脚本系统，专为 Unity 游戏开发设计。它提供了一个简单而强大的脚本语言，用于创建复杂的对话系统和分支剧情。

## 功能特性

- 简单易用的脚本语法
- 支持变量系统和条件判断
- 支持函数调用和异步操作
- 支持标签系统和情绪表达
- 支持节点跳转和分支剧情
- 支持与 C# 代码的深度集成
- 支持中文和英文混合使用

## 快速开始

### 安装

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
        
        // 注册玩家对象
        var player = new Player("二狗");
        RunMgrs.RegisterObject("player", player);
        RunMgrs.RegisterObjectFunction("player", player);

        // 注册游戏变量
        RunMgrs.RegisterVariable("gold", new RuntimeValue(100));
        RunMgrs.RegisterVariable("has_key", new RuntimeValue(false));

        // 注册C#变量
        RunMgrs.RegisterBuiltinVariable("game_difficulty", 
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
// 定义变量
var $player_name "冒险者"
var $player_level 1
var $gold 100

// 对话
商人: 欢迎来到我的商店，{$player_name}。

// 条件判断
if $player_level < 5
    商人[友好]: 看起来你是个新手冒险者。
else
    商人[热情]: 欢迎回来，经验丰富的冒险者！
endif

// 选项分支
商人: 你想买什么？
    -> 购买武器
        商人: 这是我们的武器清单。
    -> 离开
        商人: 再见，欢迎下次光临。
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

#### 变量系统

```mds
// 定义变量
var $name "玩家名称"    // 字符串变量
var $level 1           // 数字变量
var $has_key false     // 布尔变量

// 修改变量
set $name "新名字"      // 设置变量值
add $level 1           // 加法运算
sub $gold 50           // 减法运算
mul $gold 1.5          // 乘法运算
div $gold 2            // 除法运算
mod $count 5           // 取模运算

// 对象变量访问
$player__name          // 访问 player 对象的 name 属性
$player__health        // 访问 player 对象的 health 属性

// 变量插值
这是{$player__name}的属性，等级为{$player__level}
```

变量访问说明：
- 变量以 `$` 开头
- 对象变量使用 `$objectName__propertyName` 格式访问
- 在文本中可以使用 `{$variable}` 进行插值

#### 函数系统

```mds
// 函数调用
call log("这是一条日志")

// 对象函数调用
call player__get_status()
call player__take_damage(10)

// 在表达式中调用函数（不需要call关键字）
if random() > 0.5
    // 做某事
endif

// 在插值中调用函数（不需要call关键字）
商人: 你的状态是：{player__get_status()}
```

函数调用说明：
- 直接调用函数时必须使用 `call` 关键字
- 对象函数使用 `objectName__methodName()` 格式调用
- 在表达式或插值中使用函数时，不需要 `call` 关键字

#### 等待控制

```mds
// 等待控制
// 等待1秒
wait 1
// 等待0.01秒（10毫秒）
wait 0.01
```

等待控制说明：
- `wait` 是一个特殊关键字，直接使用 `wait 数字` 格式
- 等待时间单位为秒，可精确到毫秒
- 用于控制对话或事件的节奏和时序

#### 对话系统

```mds
// 基本对话
商人: 欢迎光临！

// 带情绪的对话
商人[高兴]: 今天有特价商品！
商人[生气]: 请不要乱碰商品！

// 带标签的对话
商人: 这是我的商店。 #商店 #介绍

// 旁白文本
这是一个宁静的小镇，阳光明媚。
```

对话格式说明：
- 基本对话：`角色名: 对话内容`
- 带情绪对话：`角色名[情绪]: 对话内容`
- 标签使用 `#` 开头，多个标签用空格分隔
- 标签必须放在对话后的同一行
- 旁白文本直接书写，无需特殊标记

#### 选择分支

```mds
商人: 你想买什么？
    -> 购买武器
        商人: 这是我们的武器清单。
    -> 购买防具
        商人: 这是最新的防具。
    -> 离开 [if $gold < 50]
        商人: 再见，欢迎下次光临。
```

选择分支说明：
- 选项使用 `->` 标记
- 选项可以添加条件，格式为 `-> 选项内容 [if 表达式]`
- 选项内容必须缩进，表示属于该选项
- 选项可以嵌套，使用缩进控制

#### 条件语句

```mds
if $level >= 10
    商人: 你已经是高级冒险者了！
elif $level >= 5
    商人: 你已经有一定经验了。
else
    商人: 你看起来是个新手。
endif

// 支持嵌套条件
if $has_sword
    if $has_shield
        商人: 你装备齐全！
    else
        商人: 你还需要一个盾牌。
    endif
endif
```

条件语句说明：
- 支持 `if`/`elif`/`else`/`endif` 结构
- 条件表达式支持比较运算符和逻辑运算符
- 条件语句内容必须缩进，表示属于该条件
- 支持嵌套条件语句

#### 节点系统

```mds
--- start              // 使用 --- 定义节点
// 节点内容
===                    // 节点结束标记（可选）

:: shop                // 使用 :: 定义节点
// 节点内容
===

// 跳转到其他节点
=> shop                // 跳转到 shop 节点
```

节点系统说明：
- 节点定义方式有两种：`--- 节点名` 或 `:: 节点名`
- 节点结束标记 `===` 是可选的，但建议写完整
- 使用 `=>` 或 `jump` 跳转到其他节点
- 节点内容可以包含变量定义、对话、条件、选项等元素

### 关键字

1. 条件控制：`if`, `elif`, `else`, `endif`
2. 布尔值：`true`, `false`
3. 变量操作：`var`, `set`, `add`, `sub`, `mul`, `div`, `mod`
4. 跳转控制：`=>`, `jump`
5. 函数调用：`call`
6. 等待控制：`wait 数字`（单位：秒，可精确到毫秒，如 `wait 0.01`）
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

### 注册变量

```csharp
// 1. 使用 ScriptVar 特性标记静态变量
[ScriptVar("game_version")]
public static string GameVersion { get; } = "1.0.0";

// 2. 注册对象变量
var player = new Player("二狗");
RunMgrs.RegisterObject("player", player);

// 3. 注册脚本变量
RunMgrs.RegisterVariable("gold", new RuntimeValue(100));

// 4. 注册C#变量
RunMgrs.RegisterBuiltinVariable("game_difficulty", 
    () => GameSystem.Difficulty,
    (value) => GameSystem.Difficulty = (int)value
);
```

### 注册函数

```csharp
// 1. 使用 ScriptFunc 特性标记静态函数
[ScriptFunc]
public static void ShowNotification(string title, string content)
{
    Debug.Log($"[{title}] {content}");
}

// 2. 注册对象函数
var player = new Player("二狗");
RunMgrs.RegisterObjectFunction("player", player);

// 3. 注册委托函数
RunMgrs.RegisterFunction("calculate_damage", (int base_damage, float multiplier) => {
    return (int)(base_damage * multiplier);
});
```

### 创建自定义加载器

MookDialogueScript 提供了 `IDialogueLoader` 接口，允许你创建自定义的脚本加载器。默认的加载器会从 Resources 文件夹加载脚本，但你可以根据需要实现自己的加载逻辑。

```csharp
// 1. 实现 IDialogueLoader 接口
public class CustomDialogueLoader : IDialogueLoader
{
    private readonly string _rootDir;
    private readonly string[] _extensions;

    public CustomDialogueLoader(string rootDir = "", string[] extensions = null)
    {
        _rootDir = string.IsNullOrEmpty(rootDir) ? "DialogueScripts" : rootDir;
        _extensions = extensions ?? new[] { ".txt", ".mds" };
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
                        Debug.Log($"从 StreamingAssets 加载脚本文件 {scriptName} 成功");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"加载脚本文件 {file} 时出错: {ex.Message}");
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

    private void LoadScriptContent(string scriptContent, Runner runner, string filePath = "")
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
            string fileInfo = string.IsNullOrEmpty(filePath) ? "" : $" (文件: {filePath})";
            Debug.LogError($"解析脚本内容时出错{fileInfo}: {ex.Message}");
            Debug.LogError(ex.StackTrace);
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

## 开源协议

本项目采用 Apache License 2.0 开源协议。详情请参阅 [LICENSE](LICENSE.txt) 文件。

## 联系方式

如有任何问题或建议，请通过以下方式联系我们：

- 提交 Issue
- 发送邮件至 [1317578863@qq.com]

## 致谢

感谢所有为这个项目做出贡献的人！