# MookDialogueScript 版本 0.5.0 更新日志

## 发布日期
2024年12月25日

## 主要更新内容

### 🏗️ 架构重构与优化

#### AST节点系统优化
- **删除废弃的 FunctionCallNode 类**：完全移除了标记为 `@Obsolete` 的 `FunctionCallNode`
- **统一调用表达式处理**：所有函数调用现在统一使用 `CallExpressionNode`，提升代码一致性和可维护性
- **简化AST结构**：减少冗余的节点类型，优化内存占用

#### VariableManager 功能增强
- **新增 MethodReference 类**：专门处理对象方法引用的封装类
- **完善对象成员访问**：支持更复杂的对象属性和方法访问模式
- **优化变量解析性能**：改进变量查找算法，减少反射调用开销

#### DialogueContext API 重构
- **统一对象注册接口**：`RegisterObject()` 方法现在自动处理属性、字段和方法的注册
- **重构内部方法命名**：将 `RegisterObjectPropertiesAndFields()` 重命名为 `RegisterObjectOnlyPropertiesAndFields()`
- **增强方法解析**：新增 `TryGetFunction()` 方法，提供更灵活的函数查找机制

### 🛠️ 解析器改进

#### Lexer 转义功能增强
- **新增节点标记转义**：支持 `\---` 和 `\===` 转义，避免与实际节点标记冲突
- **完善转义字符支持**：扩展支持的转义字符集合，包括所有特殊标记符
- **改进转义检测算法**：优化连续反斜杠的处理逻辑

#### Parser 错误恢复优化
- **增强语法错误恢复**：改进 `SynchronizeToTokens()` 方法，提供更好的错误恢复能力
- **统一表达式解析**：重构命令解析逻辑，统一使用 `ParseExpression()` 处理复杂表达式
- **优化函数调用解析**：简化 `CallExpressionNode` 到 `CallCommandNode` 的转换逻辑

#### Interpreter 代码简化
- **移除冗余代码**：删除所有与 `FunctionCallNode` 相关的处理逻辑
- **优化表达式求值**：简化 `EvaluateExpression()` 方法，提升执行效率
- **改进错误处理**：统一异常处理机制，减少代码重复

### 🧪 测试与质量保证

#### 新增测试覆盖
- **VariableManagerTest**：针对变量管理器的专门测试类
- **扩展现有测试**：更新 `LexerTests` 和 `ParserTests` 以覆盖新功能

#### 修复报告文档
- 生成多个修复报告文档，记录重构过程中的关键决策和变更

### 🔧 开发体验优化

#### Claude 设置更新
- **扩展自动化权限**：增加 Git 提交和推送的自动化权限
- **改进开发工具流程**：优化持续集成和部署流程

## 破坏性变更

### API 变更
- **FunctionCallNode 已删除**：所有使用 `FunctionCallNode` 的代码需要迁移到 `CallExpressionNode`
- **DialogueContext 方法重命名**：
  - `RegisterObjectPropertiesAndFields()` → `RegisterObjectOnlyPropertiesAndFields()`
  - `RegisterObjectFunctions()` → `RegisterObjectOnlyFunctions()`

### 迁移建议
```csharp
// 旧代码
context.RegisterObjectPropertiesAndFields("player", playerInstance);
context.RegisterObjectFunctions("player", playerInstance);

// 新代码 - 推荐使用统一方法
context.RegisterObject("player", playerInstance);

// 或者使用新的命名方法
context.RegisterObjectOnlyPropertiesAndFields("player", playerInstance);
context.RegisterObjectOnlyFunctions("player", playerInstance);
```

## 性能改进

- **减少AST节点类型**：简化节点继承层次，降低多态调用开销
- **优化对象成员访问**：改进反射缓存机制，提升属性和方法访问性能
- **简化表达式求值**：移除冗余的类型检查和转换逻辑

## 文档更新

- **版本号更新至 0.5.0**
- **CLAUDE.md 文档同步**：更新项目描述和API文档
- **新增转义语法说明**：完善脚本语法文档

## 向后兼容性

此版本包含破坏性变更，主要影响：
- 直接使用 `FunctionCallNode` 的扩展代码
- 依赖旧 `DialogueContext` 方法名的集成代码

建议在升级前仔细检查和测试现有集成代码。

---

**完整变更集**：此版本包含 13 个文件的修改，重点关注代码质量提升和架构优化。