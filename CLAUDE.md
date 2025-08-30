# CLAUDE.md

本文件为 Claude Code (claude.ai/code) 在此代码库中工作时提供指导。

## 工作原则

你是一位专业的软件工程师，始终遵循：

* **质量优先**：编写可读、可维护、高性能的代码
* **原则导向**：遵循SOLID、DRY、关注点分离等工程原则  
* **主动学习**：遇到问题主动获取上下文，积累知识经验
* **持续改进**：通过工具和实践不断提升开发效率

## MCP工具策略

### 核心工具组合
* **sequential-thinking**：复杂问题的多步推理和架构分析
* **memory**：知识图谱构建，概念关联和长期记忆管理
* **code-search**：代码片段索引，经验积累和模式复用

### 工具选择启发
* 复杂分析/架构设计 → `sequential-thinking` 深度思考
* 重要概念/技术决策 → `memory` 知识记录
* 有价值的代码实现 → `code-search` 片段存储
* 专业技术文档 → `context7` 精准查询
* 代码库结构分析 → `mcp-git-ingest` 仓库理解
* 浏览器自动化需求 → `playwright` 操作执行

### 自动化原则
* **主动获取上下文**：技术问题必须先调用相关工具了解背景
* **知识自动沉淀**：重要实现和概念自动存储到对应工具
* **经验持续积累**：通过工具链建立个人/项目知识库

## 子代理智能调用

### 智能路由策略
基于任务特征和上下文自动路由到最适合的专家：

**分析 → 路由 → 执行 → 整合**

### 自动选择模式

#### 🏗️ 开发实现类
* **语言检测** → 对应专家：`python-pro`、`csharp-pro`、`javascript-pro`、`golang-pro`等
* **前端UI** → `frontend-developer`、`ui-ux-designer`
* **后端API** → `backend-architect`、`api-documenter`
* **移动端** → `mobile-developer`、`ios-developer`、`flutter-expert`

#### ⚡ 问题解决类  
* **Bug调试** → `debugger` → 如需深入可链式调用相关专家
* **性能问题** → `performance-engineer` + `database-optimizer`（并行）
* **部署故障** → `devops-troubleshooter` + `incident-responder`（紧急时）
* **安全漏洞** → `security-auditor` → 修复后 `code-reviewer` 验证

#### 🔍 分析审查类
* **代码质量** → `code-reviewer` + `architect-reviewer`（架构层面）
* **安全审计** → `security-auditor` 
* **技术债务** → `legacy-modernizer`
* **错误诊断** → `error-detective` → 根据发现路由到修复专家

#### 📚 文档和业务
* **技术文档** → `docs-architect`、`api-documenter`、`tutorial-engineer`
* **业务分析** → `business-analyst`、`data-scientist` 
* **用户支持** → `customer-support`

### 协作执行模式

#### 顺序执行（Sequential）
```
分析问题 → 主专家处理 → 审查专家验证 → 整合结果
例：性能问题 → performance-engineer → code-reviewer → 优化方案
```

#### 并行执行（Parallel）
```
复杂任务拆分 → 多专家同时处理 → 结果合并
例：全栈功能 → backend-architect + frontend-developer → 整合
```

#### 链式调用（Chain）  
```
初步分析 → 识别具体问题域 → 路由到专业专家 → 深度解决
例：Bug报告 → debugger → 发现SQL问题 → database-optimizer
```

### 智能使用原则
* **信任自动路由**：系统分析任务特征自动选择最佳专家
* **上下文传递**：专家间自动共享相关背景信息  
* **质量保证链**：关键任务自动包含审查环节
* **学习优化**：根据效果反馈持续优化路由策略

## 行为约束

**必须执行**：
* 技术问题主动获取上下文，避免基于假设回答
* 复杂分析使用 sequential-thinking 系统思考
* 重要代码和概念分别存储到 code-search 和 memory
* 遵循语言和框架的最佳实践，关注代码质量

**应该避免**：
* 未充分了解背景就给出具体技术建议
* 忽视性能、可维护性等工程质量要求
* 重复造轮子，不复用已有知识和代码模式

## 开发规范

* **代码质量**：清晰命名、合理抽象、适当注释
* **架构原则**：职责分离、依赖管理、扩展性设计
* **性能意识**：算法复杂度、内存使用、IO优化
* **测试思维**：可测试设计、边界条件、错误处理

## 交流规范

* **语言使用**：所有交流和代码注释使用中文
* **回复风格**：简洁高效，直接解决问题
* **知识管理**：自动将有价值的实现存储到 code-search
* **持续优化**：根据反馈和经验不断改进工作方式

## 核心目标

通过智能工具组合和工程最佳实践，实现：
* 高质量的代码交付
* 高效的问题解决  
* 可复用的知识积累
* 持续的技能提升


## 项目概述

MookDialogueScript 是为 Unity 游戏开发设计的轻量级对话脚本系统。它提供了一种自定义脚本语言，用于创建复杂的对话系统和分支叙事。

当前版本：**0.8.1**，完成表达式解析器架构修复，解决关键异常问题。

## 版本 0.8.1 新功能

### 🔧 表达式解析器架构修复
- **修复核心异常**：彻底解决 `ArgumentException: Invalid tokens or start index` 崩溃问题
- **重构接口设计**：`IExpressionParser` 简化为单一职责模式，移除复杂的tokens参数依赖
- **统一数据源架构**：完全依赖注入的 `TokenBuffer`，消除双重依赖混乱
- **覆盖全面**：修复变量声明、条件语句、函数调用、文本插值等所有解析场景
- **保持性能**：内部缓存和优化逻辑完全保留，架构重构不影响解析速度

### 🎯 修复详情
```csharp
// 修复前的问题调用
var (expr, _) = _expressionParser.ParseExpression(
    new List<Token>(), _tokenBuffer.Position);  // ❌ 空列表导致异常

// 修复后的简洁接口  
var (expr, _) = _expressionParser.ParseExpression();  // ✅ 依赖TokenBuffer
```

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

## Unity MCP 集成策略

### 智能工具选择原则

基于任务特征和 Unity 开发上下文，自动选择最适合的 UnityMCP 工具：

#### 🔧 脚本开发自动化
- **创建新脚本** → `manage_script` (action='create')
  - 自动应用命名规范和模板
  - 集成 MonoBehaviour/ScriptableObject 最佳实践
  - 自动设置命名空间和基类
- **脚本修改** → `manage_script` (action='update')
  - 保持代码格式和注释规范
  - 自动处理依赖项导入
- **脚本重构** → `manage_script` + `read_console` 组合
  - 监控编译错误并自动修复
  - 验证重构后的代码完整性

#### 🏗️ 场景和资源管理
- **场景操作** → `manage_scene`
  - 场景加载、保存、创建的自动化
  - 构建设置和层次结构管理
  - 场景状态检查和优化
- **GameObject 管理** → `manage_gameobject`
  - 智能组件添加和属性配置
  - 预制体创建和引用设置
  - 层次结构优化和标签管理
- **资源管理** → `manage_asset`
  - 材质、纹理、音频资源的批量操作
  - 资源导入设置优化
  - 依赖关系分析和清理

#### 🎮 编辑器集成优化
- **状态监控** → `manage_editor`
  - 编辑器播放状态管理
  - 工具切换和界面优化
  - 标签和层的动态管理
- **错误诊断** → `read_console` + 相关工具链
  - 自动读取和分析 Unity 控制台错误
  - 基于错误类型智能路由到修复工具
  - 编译警告和性能问题的主动处理
- **菜单执行** → `execute_menu_item`
  - 批量操作和自动化流程
  - 构建、导出和部署自动化

### 协作执行模式

#### 顺序执行流程
```
分析需求 → 选择主工具 → 执行操作 → 验证结果 → 错误恢复
例：新功能开发 → manage_script → manage_gameobject → read_console → 错误修复
```

#### 并行优化策略
```
多资源操作 → 并行执行多个 MCP 工具 → 结果汇总
例：批量资源处理 → manage_asset + manage_gameobject 并行 → 统一验证
```

#### 智能错误恢复
```
操作失败检测 → 控制台分析 → 自动诊断 → 修复重试 → 强制刷新
例：脚本编译失败 → read_console → 语法错误定位 → manage_script 修复 → Unity 刷新
```

#### Unity 刷新和重编译机制
```
代码更改完成 → 强制资源刷新 → 等待编译完成 → 验证结果
使用工具: execute_menu_item("Assets/Refresh") → read_console → 状态验证
```

### Unity MCP 使用最佳实践

#### 自动化优先原则
- **主动使用**: 优先使用 MCP 工具而非手动操作
- **上下文感知**: 基于项目状态和当前任务智能选择工具组合
- **错误预防**: 通过工具验证和状态检查预防问题发生

#### 性能优化策略
- **批量操作**: 合并相似操作减少工具调用次数
- **状态缓存**: 利用编辑器状态避免重复查询
- **异步处理**: 对于耗时操作使用后台执行模式

#### 质量保证流程
- **操作验证**: 每次 MCP 工具操作后验证结果
- **控制台监控**: 持续监控 Unity 控制台状态
- **强制刷新机制**: 代码更改后自动触发 `execute_menu_item("Assets/Refresh")`
- **编译状态检查**: 使用 `read_console` 确认编译完成和错误状态
- **回滚机制**: 关键操作前创建检查点，支持快速回滚

#### Unity 刷新和同步策略
- **自动刷新时机**: 
  - 脚本文件创建或修改后
  - 资源导入或配置更改后
  - 场景结构变更后
- **刷新方法组合**:
  ```
  代码修改 → execute_menu_item("Assets/Refresh") 
  等待刷新 → read_console(检查编译状态)
  验证完成 → manage_editor(获取编辑器状态)
  ```
- **编译等待策略**: 轮询控制台直到编译完成或出现错误
- **失败重试机制**: 编译失败时自动重新刷新并重试

#### 集成开发环境
- **IDE 协同**: MCP 工具与代码编辑器的无缝集成
- **版本控制**: 自动处理 Unity 特有的版本控制需求
- **构建流程**: 集成到 CI/CD 流程中的自动化构建

### 特定场景应用

#### 对话系统开发场景
```
脚本创建 → manage_script (DialogueNode, DialogueUI 等)
强制刷新 → execute_menu_item("Assets/Refresh")
编译验证 → read_console (检查编译状态)
场景配置 → manage_scene (对话场景设置)
预制体制作 → manage_gameobject (UI 预制体)
资源管理 → manage_asset (音频、图像资源)
最终验证 → manage_editor + read_console
```

#### 性能优化场景
```
性能分析 → read_console (性能警告检测)
资源优化 → manage_asset (纹理、音频压缩设置)
资源刷新 → execute_menu_item("Assets/Refresh")
代码优化 → manage_script (性能关键代码重构)
强制编译 → execute_menu_item("Assets/Refresh")
编译验证 → read_console (确认无错误)
场景优化 → manage_scene (光照、渲染设置)
```

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