# CHANGELOG v0.8.1

## 版本 0.8.1 - 表达式解析器架构修复 (2025-01-30)

### 🔧 架构修复

#### **表达式解析器重构**
- **修复核心问题**：解决 `ArgumentException: Invalid tokens or start index` 异常
- **简化接口设计**：`IExpressionParser` 移除复杂的 tokens 列表参数依赖
- **统一数据源**：完全依赖注入的 `TokenBuffer`，实现单一职责原则
- **消除设计冲突**：移除双重依赖导致的架构混乱

#### **接口重构详情**
```csharp
// 修复前：复杂的参数传递
(ExpressionNode expression, int tokensConsumed) ParseExpression(
    List<Token> tokens, int startIndex, int endIndex = -1);

// 修复后：简洁的单一职责接口
(ExpressionNode expression, int tokensConsumed) ParseExpression();
```

#### **解析器调用优化**
- **修复范围**：更新 Parser.cs 中所有8处表达式解析调用
- **涵盖功能**：变量声明、条件语句、函数调用、文本插值、等待命令等
- **提升可靠性**：消除边界检查异常的根本原因

### 🐛 Bug修复

- **[CRITICAL]** 修复变量声明解析时的 `ArgumentException` 崩溃
- **[HIGH]** 修复所有表达式解析场景的架构不一致问题
- **[MEDIUM]** 优化接口设计，减少调用复杂性

### 📈 性能优化

- **减少参数传递开销**：简化接口调用，减少不必要的列表创建
- **保持缓存优化**：内部高性能缓存和内联方法完全保留
- **提升解析速度**：移除冗余的边界检查和位置管理逻辑

### ✅ 兼容性

- **API兼容**：内部重构，不影响外部使用者
- **功能完整**：所有表达式解析功能正常工作
- **向后兼容**：现有脚本和配置无需修改

### 🔬 技术细节

#### 修改的文件
- `Assets/MookDialogueScript/Runtime/Parsing/Interfaces/IExpressionParser.cs`
- `Assets/MookDialogueScript/Runtime/Parsing/Components/ExpressionParser.cs`  
- `Assets/MookDialogueScript/Runtime/Parsing/Parser.cs`

#### 架构原则实施
- **单一职责原则**：表达式解析器专注于解析逻辑，不管理Token列表
- **依赖倒置原则**：完全依赖抽象的TokenBuffer接口
- **接口隔离原则**：简化接口，移除不必要的参数
- **组合优于继承**：保持现有的组合设计模式

### 🧪 测试验证

- ✅ 编译成功：无编译错误
- ✅ 运行验证：对话系统正常初始化
- ✅ 功能测试：变量声明、表达式解析正常工作
- ✅ 错误消除：原始异常完全解决

### 📝 后续计划

- **v0.8.2**: 进一步优化词法分析器性能
- **v0.9.0**: 语义分析器增强和错误报告改进
- **v1.0.0**: 完整稳定版本发布

---

**重要提示**：此版本修复了影响所有变量声明和表达式解析的严重架构问题，强烈建议从v0.8.0升级。

**开发者**：通过Claude Code (claude.ai/code) 智能重构完成
**提交者**：Claude <noreply@anthropic.com>