# MookDialogueScript

轻量级的 Unity 对话脚本系统。提供简洁但强大的脚本语言与完整运行时，以便在 Unity 中快速实现分支对话、条件逻辑、变量/函数交互与节点跳转等。

当前版本：0.9.0（以 `Assets/MookDialogueScript/package.json` 为准）

## 主要特性

- 简洁可读的脚本语法（节点/元数据/对话/旁白/选项/命令）。
- 变量与对象访问：支持 `$var` 与 `object.property`/`object.method()`。
- 条件与分支：`if/elif/else/endif` 与 `->` 选项分支，支持条件选项。
- 运行时 API：注册变量/对象/函数，获取与设置存储，事件回调齐全。
- 内置函数：`log`、`concat`、`random`、`random_range`、`dice`、`visited`、`visit_count`。
- 高性能词法/语法解析与缓存优化；对象池化与性能统计接口。
- Unity 编辑器集成：`.mds` 导入器与脚本验证器（菜单：Tools/MookDialogue/Validate All Scripts）。

## 目录结构

- `Assets/MookDialogueScript/Runtime/`：运行时代码（Lexers/Parsing/Interpreter/Pooling/Unity）。
- `Assets/MookDialogueScript/Editor/`：编辑器导入器与验证器。
- `Assets/MookDialogueScript/Tests/`：EditMode 测试（Unity Test Framework）。
- `Assets/Resources/DialogueScripts/`：示例脚本放置目录（运行时默认示例路径）。

## 安装

- Package Manager（推荐）
  1) Unity 打开 Window > Package Manager。
  2) “+” > Add package from git URL...
  3) 填写：`https://github.com/mook-wenyu/MookDialogueScriptFromUnity.git?path=Assets/MookDialogueScript`

- 手动安装
  1) 克隆或下载本仓库。
  2) 将 `Assets/MookDialogueScript` 拷贝到你的项目 `Assets/` 下。
  3) 若使用示例，确保 `.mds` 脚本在 `Assets/Resources/DialogueScripts/`。

环境建议：Unity 2021.4+（推荐 2022.3 LTS）。

## 快速开始

1) 创建脚本：在 `Assets/Resources/DialogueScripts/` 下新建 `example.mds`
```
node: start
---
商人: 欢迎光临！
    -> 购买
        商人: 谢谢惠顾
    -> 离开
        商人: 欢迎下次再来
===
```

2) 初始化 Runner 并启动对话（MonoBehaviour 示例）
```csharp
using MookDialogueScript;
using UnityEngine;
using UnityEngine.UI;

public class DialogueBoot : MonoBehaviour
{
    public Text speaker;
    public Text content;

    private Runner runner;

    void Awake()
    {
        // 从 Resources 根目录加载所有 TextAsset（推荐使用子目录：DialogueScripts）
        runner = new Runner("DialogueScripts");

        // 订阅事件：UI 展示
        runner.OnDialogueDisplayed += async dialogue =>
        {
            speaker.text = string.IsNullOrEmpty(dialogue.Speaker) ? "" : dialogue.Speaker;
            content.text = await runner.BuildDialogueText(dialogue);
        };

        runner.OnChoicesDisplayed += async choices =>
        {
            for (int i = 0; i < choices.Count; i++)
            {
                var text = await runner.BuildChoiceText(choices[i]);
                Debug.Log($"选项 {i + 1}: {text}");
            }
        };

        runner.OnOptionSelected += async (choice, index) =>
        {
            Debug.Log($"选择了: {index + 1}");
        };

        runner.OnDialogueCompleted += () => Debug.Log("对话结束");
    }

    async void Start()
    {
        await runner.StartDialogue("start");
    }
}
```

## 脚本语法速览

- 节点与元数据
```
node: start
title: 开始
---
...内容...
===
```

- 对话与旁白
```
商人: 欢迎光临 #欢迎
:这是旁白文本
```

- 变量与命令（命令必须写在 `<< >>` 内）
```
<<var $gold 100>>
<<set $gold $gold + 50>>
<<wait 0.2>>
<<jump next_node>>
```

- 条件与选项
```
<<if $gold >= 50>>
    -> 购买
        商人: 谢谢惠顾
<<else>>
    商人: 金币不足
<<endif>>
```

- 对象与方法（注册对象后使用）
```
商人: {player.name}
<<player.take_damage(10)>>
商人: {player.get_status()}
```

## 运行时集成

- 注册变量
```csharp
runner.RegisterVariable("gold", new RuntimeValue(100));
runner.RegisterVariable("game_difficulty", () => Game.Difficulty, v => Game.Difficulty = (int)v);
```

- 注册对象与函数
```csharp
runner.RegisterObject("player", playerInstance);
runner.RegisterFunction("calculate_damage", (int baseVal, float mul) => (int)(baseVal * mul));
```

- 对话存储（存档/读档）
```csharp
// 获取可序列化的存储
var storage = runner.GetCurrentStorage();
// 恢复
runner.SetStorage(storage);
```

- 文本构建（用于 UI）
```csharp
string d = await runner.BuildDialogueText(dialogueNode);
string c = await runner.BuildChoiceText(choiceNode);
```

- 事件回调
```csharp
runner.OnDialogueStarted += () => { /* 开始时机（可存档）*/ return System.Threading.Tasks.Task.CompletedTask; };
runner.OnDialogueDisplayed += d => { /* 刷新对话 UI */ };
runner.OnChoicesDisplayed += cs => { /* 刷新选项 UI */ };
runner.OnOptionSelected += (c, i) => { /* 处理选择 */ };
runner.OnDialogueCompleted += () => { /* 结束时机（可存档）*/ };
```

## 内置函数

- `log(message, type="info")`：输出日志（info/warn/error）。
- `concat(a, b, ...)`：连接字符串。
- `random(digits=2)`：0..1 的随机数，保留指定位数。
- `random_range(min, max, digits=2)`：范围随机数。
- `dice(sides, count=1)`：掷骰返回总和。
- `visited(node)` / `visit_count(node)`：基于 `DialogueStorage` 的访问统计。

## Unity 编辑器集成

- `.mds` 导入：ScriptedImporter 自动将 `.mds` 以 `TextAsset` 导入。
- 校验工具：编辑器启动与资源变更时自动校验；菜单 `Tools/MookDialogue/Validate All Scripts` 可手动触发。

## 性能与缓存

- 解析与解释内部包含缓存与对象池优化；可获取统计或清理缓存。
```csharp
// 在示例中，通过 Runner.Context 暴露
var stats = runner.Context.GetPerformanceStatistics();
runner.Context.ClearAllCaches();
```

说明：README 不再描述未在代码中实现的“缓存预设/一键配置”等接口。

## 测试与打包（命令行）

- 运行 EditMode 测试（Windows 示例）
```
"<UnityPath>\\Unity.exe" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults .\\TestResults.xml -quit
```

- 运行 PlayMode 测试（如有）
```
"<UnityPath>\\Unity.exe" -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults .\\PlayResults.xml -quit
```

- 导出 UnityPackage（仅导出运行时代码与编辑器工具）
```
"<UnityPath>\\Unity.exe" -batchmode -quit -projectPath . -exportPackage Assets/MookDialogueScript MookDialogueScript.unitypackage
```

## VSCode 语法高亮

扩展：MookDialogueScript Language（https://github.com/mook-wenyu/mookdialoguescript-lang）

- 语法高亮、代码片段、自动缩进与折叠。
- 创建 `.mds` 文件即可生效。

## 许可证

Apache-2.0，详见 `LICENSE.txt`。

## 贡献

欢迎通过 Issue/PR 反馈问题与改进建议。提交前请遵循：
- 代码规范：C# 9，四空格缩进，UTF-8，命名与 `.asmdef` 分离规范。
- 不提交生成目录：`Library/`、`Temp/`、`Logs/`、`obj/`。
- 变更应附带清晰说明与最小可审计差异。
