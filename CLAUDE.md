# CLAUDE.md

本文件为 Claude Code (claude.ai/code) 在此代码库中工作时提供指导。

## 项目概述

MookDialogueScript 是为 Unity 游戏开发设计的轻量级对话脚本系统。它提供了一种自定义脚本语言，用于创建复杂的对话系统和分支叙事。

当前版本：**0.8.0**，完成架构重构，实现高性能模块化设计。

## 版本 0.8.0 新功能

### 🏗️ 架构重构升级
- **语义分析器架构重构**：采用5层架构设计（Contracts → TypeSystem → Symbols → Diagnostics → Core）
- **对象池系统重构**：统一池管理架构，消除重复代码，提升复用性
- **解析器组件化**：模块化解析器设计，支持缓存管理和上下文处理
- **完整SOLID原则**：遵循单一职责、依赖倒置、接口隔离等设计原则
- **组合优于继承**：全面采用组合模式，提升代码可维护性

### 🎯 语义分析系统
- **分层架构设计**：清晰的职责分离，支持插件化规则扩展
- **符号表管理**：完整的作用域管理和符号解析系统
- **类型系统优化**：强类型检查和兼容性验证
- **诊断收集器**：统一的错误收集和报告生成机制
- **缓存优化**：分析结果缓存，避免重复计算

### 🚀 对象池优化
- **通用池架构**：`UniversalObjectPool<T>` 支持任意类型对象池化
- **全局池管理器**：`GlobalPoolManager` 统一管理所有对象池
- **作用域管理**：`ScopedPoolable<T>` 泛型类消除重复代码
- **专用池优化**：词法分析器和解析器的专用高性能池
- **统计监控**：完整的池使用统计和性能监控

### ⚡ 性能优化亮点
- **无锁并发设计**：使用 `ConcurrentBag` 和原子操作，避免锁竞争
- **线程本地存储**：每个线程维护独立的小池，减少跨线程开销
- **智能对象复用**：基于使用模式的自适应池大小调整
- **零分配优化**：关键路径使用 `AggressiveInlining`，减少调用开销
- **共享组件复用**：线程安全组件在线程间共享，减少内存占用

## 版本 0.6.0 功能

### 🎯 函数签名系统 (FunctionSignature)
- **完整的类型安全**：函数注册时自动构建详细签名信息，包含参数名、类型、默认值
- **严格参数校验**：语义分析阶段进行参数数量和类型的严格验证
- **智能错误提示**：提供中文错误消息和具体的修复建议
- **重载检测拒绝**：系统不支持函数重载，自动检测并报错

### 🔍 语义分析增强  
- **优化类型推断**：符号表未命中时返回 `Any` 类型，允许更宽松的分析
- **统一诊断系统**：完整的 SEM 错误编码，涵盖所有语义错误情况
- **性能优化**：TypeInfo 单例化，减少内存分配和 GC 压力

## 架构

### 核心组件

对话系统遵循经典的解释器模式，采用现代化模块架构，包含以下关键组件：

- **词法分析器** (`Assets/MookDialogueScript/Runtime/Lexers/`): 高性能组件化词法分析架构
  - `Lexer.cs`: 统一的词法分析器接口和核心实现
  - 支持无锁并发、线程本地缓存和自适应调整
  - 完整资源管理和Unity Profiler集成
  
- **语法解析器** (`Assets/MookDialogueScript/Runtime/Parsing/`): 模块化解析器系统
  - `Parser.cs`: 主解析器实现，从标记构建抽象语法树 (AST)
  - `Components/`: 解析器组件（表达式解析、缓存管理、上下文处理、缓冲管理）
  - `Interfaces/`: 解析器接口定义，支持组合模式设计
  
- **语义分析器** (`Assets/MookDialogueScript/Runtime/Semantic/`): 5层架构语义分析系统
  - `Contracts/`: 核心接口层（分析器、规则、诊断、符号解析等）
  - `TypeSystem/`: 类型系统（类型推断、兼容性检查、类型信息）
  - `Symbols/`: 符号管理（符号表、作用域管理、符号解析）
  - `Diagnostics/`: 诊断系统（错误收集、诊断代码、报告生成）
  - `Core/`: 核心实现（组合分析器、分析上下文、符号表工厂）
  
- **对象池系统** (`Assets/MookDialogueScript/Runtime/Pooling/`): 统一对象池架构
  - `Core/`: 核心池实现（通用对象池、全局管理器、作用域管理）
  - `Specialized/`: 专用对象池（词法分析器池、解析器池）
  - `Interfaces/`: 对象池接口定义
  - `PoolOptions.cs` & `PoolStatistics.cs`: 池配置和统计监控

- **集成系统** (`Assets/MookDialogueScript/Runtime/Integration/`): Unity集成层
  - `Integration.cs`: Unity特定的集成逻辑和生命周期管理

- **核心运行时**:
  - `Runner.cs`: 对话执行的主入口点，集成所有子系统
  - `Interpreter.cs`: 执行 AST 节点的解释器
  - `AST.cs`: 定义所有 AST 节点类型
  - `SemanticAnalyzer.cs`: 向后兼容的语义分析器外观接口

### 变量和函数系统

- **变量管理器** (`Assets/MookDialogueScript/Runtime/VariableManager.cs`): 管理脚本变量和 C# 变量绑定
- **函数管理器** (`Assets/MookDialogueScript/Runtime/FunctionManager.cs`): 函数注册、签名构建和调用管理，支持严格的类型检查
- **对话存储** (`Assets/MookDialogueScript/Runtime/DialogueStorage.cs`): 持久化对话状态以支持保存/加载

### Unity 集成

- **Unity对话加载器** (`Assets/MookDialogueScript/Runtime/Unity/UnityDialogueLoader.cs`): 从 Resources 文件夹加载脚本
- **Unity日志器** (`Assets/MookDialogueScript/Runtime/Unity/UnityLogger.cs`): Unity 特定的日志实现
- **对话管理器** (`Assets/Scripts/DialogueMgr.cs`): 管理对话系统的示例 Unity MonoBehaviour

## 开发命令

### 测试
```bash
# 在 Unity Test Runner 中运行测试
# 在 Unity 编辑器中导航到 Window > General > Test Runner
# 测试位于 Assets/MookDialogueScript/Tests/
```

### 构建
这是一个 Unity 包项目。通过 Unity 编辑器进行构建：
1. 在 Unity 编辑器中打开项目 (2021.4+)
2. 使用 Unity 的 Package Manager 导出为 `.unitypackage`
3. 或者在其他 Unity 项目中作为 Git 子模块包含

## 脚本语言 (.mds 文件)

### 基本语法
- **注释**: `// 注释文本` (必须独占一行)
- **节点**: `--- 节点名` 或 `:: 节点名` (以 `===` 结束)
- **变量**: `var $名称 值`，在文本中以 `{$名称}` 形式访问
- **对话**: `角色: 文本内容 #标签1 #标签2`
- **旁白**: `:旁白文本` (冒号前缀保留格式)
- **条件**: `if 表达式`、`elif 表达式`、`else`、`endif`
- **选择**: `-> 选项文本 [if 条件]`
- **函数**: `call 函数名(参数)` 或在表达式中直接使用 `函数名()`
- **跳转**: `=> 节点名` 或 `jump 节点名`
- **转义**: 使用反斜杠转义特殊字符，支持 `\:`、`\#`、`\{`、`\}`、`\<`、`\>`、`\'`、`\"`、`\\`、`\---`、`\===`

### 关键语言特性
- 变量插值: `{$变量名}`
- 对象属性: `对象.属性名`
- 条件选择: `-> 文本 [if $条件]`
- 内置函数: `visited()`、`visit_count()`、`random()`、`concat()`
- 通过 `[ScriptVar]` 和 `[ScriptFunc]` 特性与 C# 集成

## 集成模式

### 注册 C# 对象
```csharp
// 注册完整对象（属性、字段、方法）
runner.RegisterObject("player", playerInstance);
// 访问方式：player.name、player.health、player.method()
```

### 变量绑定
```csharp
// 静态注册
[ScriptVar("variable_name")]
public static string MyVariable { get; set; }

// 动态注册
runner.RegisterVariable("gold", new RuntimeValue(100));

// 带 getter/setter 的 C# 变量绑定
runner.RegisterVariable("difficulty", 
    () => GameSystem.Difficulty,
    (value) => GameSystem.Difficulty = (int)value);
```

### 函数注册与签名
```csharp
// 静态注册（自动构建签名）
[ScriptFunc]
public static void ShowMessage(string text) { /* 实现 */ }

// 动态注册（自动类型推断）
runner.RegisterFunction("calculate", (int a, int b) => a + b);

// 获取函数签名信息
var signature = functionManager.GetFunctionSignature("calculate");
if (signature != null)
{
    Debug.Log($"函数签名: {signature.FormatSignature()}");
    // 输出: Number calculate(Number a, Number b)
    Debug.Log($"参数范围: {signature.MinRequiredParameters}-{signature.MaxParameters}");
    Debug.Log($"来源类型: {signature.SourceType}");
}

// 获取所有已注册的函数签名
foreach (var kvp in functionManager.GetAllFunctionSignatures())
{
    Debug.Log($"{kvp.Key}: {kvp.Value.FormatSignature()}");
}
```

### 语义分析集成
```csharp
// 创建新架构的语义分析器
var analyzer = new SemanticAnalyzer(
    options: new AnalysisOptions(),
    nodeProvider: nodeProvider,
    functionManager: functionManager  // 注入函数管理器
);

// 或直接使用核心实现
var coreAnalyzer = new Semantic.Core.CompositeSemanticAnalyzer(
    options, nodeProvider);

// 执行严格的语义分析
var report = analyzer.Analyze(script, variableManager, functionManager);

// 检查分析结果
if (report.HasErrors)
{
    foreach (var diagnostic in report.Diagnostics)
    {
        if (diagnostic.Severity == DiagnosticSeverity.Error)
        {
            Debug.LogError($"{diagnostic.Code}: {diagnostic.Message} (第{diagnostic.Line}行)");
        }
    }
    
    // 示例输出: SEM013: 函数 'calculate' 参数不足：期望至少 2 个，实际 1 个 (第15行)
}

// 获取详细的分析统计
Debug.Log($"错误数: {report.ErrorCount}, 警告数: {report.WarningCount}");

// 使用符号表进行高级查询
var symbolResolver = analyzer.GetSymbolResolver();
var variableType = symbolResolver.ResolveVariable("playerHealth");
var functionInfo = symbolResolver.ResolveFunction("calculateDamage");
```

### 对象池集成
```csharp
// 使用全局池管理器
var lexerPool = GlobalPoolManager.GetPool<LexerRefactored>();
using var scopedLexer = lexerPool.GetScoped(); // 自动归还
var lexer = scopedLexer.Object;

// 获取池统计信息
var stats = GlobalPoolManager.GetStatistics<LexerRefactored>();
Debug.Log($"池大小: {stats.PoolSize}, 活跃对象: {stats.ActiveObjects}");

// 配置池选项
var poolOptions = new PoolOptions
{
    InitialSize = 10,
    MaxSize = 100,
    GrowthFactor = 2.0f
};
GlobalPoolManager.ConfigurePool<Parser>(poolOptions);
```

## 文件结构

- **`Assets/MookDialogueScript/`**: 核心包文件
  - **`Runtime/`**: 核心对话系统实现
  - **`Editor/`**: Unity 编辑器集成和脚本导入器
  - **`Tests/`**: 词法分析器和解析器单元测试
- **`Assets/Scripts/`**: 示例 Unity 集成脚本
- **`Assets/Resources/DialogueScripts/`**: 示例对话脚本 (.mds 文件)

## 包信息

- **版本**: 0.8.0 (在 `Assets/MookDialogueScript/package.json` 中定义)
- **Unity 版本**: 2021.4+
- **许可证**: Apache-2.0
- **包名**: `com.wenyu.dialoguescript`

## 使用示例

查看 `Assets/Resources/DialogueScripts/example_dialogue.mds` 了解全面的语法示例，查看 `Assets/Scripts/DialogueMgr.cs` 了解集成模式。

系统支持以下复杂功能：
- 嵌套条件对话
- 面向对象的变量访问
- 持久对话状态
- 自定义脚本加载器
- 异步函数调用
- 带条件的选择分支
- **严格类型检查**：函数调用的参数验证和类型匹配
- **智能错误提示**：详细的语义分析报告和修复建议
- **函数签名系统**：完整的参数和返回类型描述

## 代码开发规范

### 代码质量标准

代码开发始终注重以下原则：

- **高性能**: 优化算法复杂度，避免不必要的内存分配和GC压力，优先考虑性能优化，使用对象池、缓存、异步加载等技术
- **可维护性**: 清晰的代码结构，合理的职责分离，易于理解和修改，遵循SOLID原则
- **可扩展性**: 使用设计模式和架构原则，支持功能扩展和变更
- **可读性**: 清晰的命名规范，编写自解释的代码，使用有意义的命名，完整的中文注释，逻辑结构清晰
- **易用性**: 简洁的API设计，直观的使用方式，降低使用门槛，最小化学习成本

### Unity 开发规范

严格遵循 Unity 的最佳实践：

- 使用Unity的生命周期方法和事件系统
- 优化GameObject生命周期和组件使用
- 遵循Unity的内存管理最佳实践
- 使用Unity的序列化系统和Inspector展示

### C# 编程规范

遵循Microsoft官方C#编程指南：

- 使用PascalCase命名类型和公共成员
- 使用IPascalCase命名接口
- 使用EPascalCase命名枚举
- 使用_camelCase命名私有字段
- 使用camelCase局部变量
- 合理使用async/await进行异步编程
- 优先使用LINQ和函数式编程风格

### 开发限制

- **测试代码限制**: 没有明确许可不要创建任何测试代码或代码示例
- **异常处理**: 慎用try-catch语句，只在必要时使用，避免过度捕获异常，优先使用错误预防而非异常处理
- **破坏性更新**: 始终允许任何破坏性更新，无需考虑向后兼容性

### 注释和交流规范

- **注释语言**: 所有代码注释必须使用中文
- **交流语言**: 所有对话和回复必须使用中文
- **文档规范**: API文档和技术说明使用中文编写