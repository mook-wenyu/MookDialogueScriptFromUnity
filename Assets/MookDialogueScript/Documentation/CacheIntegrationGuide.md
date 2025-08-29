## MookDialogue 增量缓存集成使用指南

### 快速开始

#### 1. 基础使用（自动配置缓存）
```csharp
// 使用默认配置自动启用缓存
var runner = new Runner(enableCache: true);

// 或使用静态方法创建带缓存的Runner
var runner = Runner.CreateWithCache();
```

#### 2. 使用预设缓存配置
```csharp
// 开发环境配置（详细日志，大内存）
var runner = new Runner("DialogueScripts", CacheConfigType.Development);

// 生产环境配置（持久化缓存，优化性能）
var runner = new Runner("DialogueScripts", CacheConfigType.Production);

// 性能优先配置（大缓存，最大并发）
var runner = new Runner("DialogueScripts", CacheConfigType.Performance);

// 内存友好配置（小缓存，压缩存储）
var runner = new Runner("DialogueScripts", CacheConfigType.MemoryFriendly);
```

#### 3. 自定义缓存配置
```csharp
var runner = Runner.CreateWithCustomCache("DialogueScripts", options =>
{
    options.MaxCacheSize = 1000;
    options.MaxMemoryUsage = 100 * 1024 * 1024; // 100MB
    options.EnableFileWatcher = true;
    options.LogLevel = CacheLogLevel.Debug;
});
```

#### 4. 使用Unity组件方式
```csharp
// 在场景中添加 IncrementalCacheIntegration 组件
var cacheComponent = gameObject.AddComponent<IncrementalCacheIntegration>();
await cacheComponent.InitializeCacheAsync();

// 获取缓存统计
var stats = cacheComponent.GetCacheStatistics();
Debug.Log($"缓存命中率: {stats.HitRatio:P2}");
```

### 缓存管理

#### 在编辑器中管理缓存
1. 打开菜单：`Tools/MookDialogue/Cache Manager`
2. 查看缓存统计信息
3. 执行缓存操作（清空、预热、清理等）

#### 程序化管理缓存
```csharp
// 清理过期缓存
await runner.CleanupCacheAsync();

// 刷新特定文件缓存
await runner.RefreshFileCacheAsync("path/to/script.mds", forceRefresh: true);

// 预热缓存
string[] scriptPaths = { "script1.mds", "script2.mds" };
await runner.WarmupCacheAsync(scriptPaths);

// 获取缓存统计
var stats = runner.GetCacheStatistics();
Console.WriteLine($"总访问: {stats.TotalAccesses}, 命中率: {stats.HitRatio:P1}");

// 生成缓存报告
var report = await runner.GenerateCacheReportAsync();
Console.WriteLine($"健康状态: {report.HealthStatus}");
```

### 性能优化建议

#### 最佳实践
1. **开发阶段**：使用 `CacheConfigType.Development` 配置，启用详细日志和文件监控
2. **生产环境**：使用 `CacheConfigType.Production` 配置，启用持久化缓存
3. **内存受限**：使用 `CacheConfigType.MemoryFriendly` 配置，减少内存占用
4. **高频访问**：使用 `CacheConfigType.Performance` 配置，最大化缓存效率

#### 监控和调试
```csharp
// 获取详细的缓存报告
var report = await runner.GenerateCacheReportAsync();

// 检查推荐建议
foreach (var recommendation in report.Recommendations)
{
    Debug.Log($"建议: {recommendation}");
}

// 检查警告信息
foreach (var warning in report.Warnings)
{
    Debug.LogWarning($"警告: {warning}");
}

// 分析缓存效率
var efficiency = report.Statistics.CalculateEfficiencyScore();
Debug.Log($"缓存效率评分: {efficiency}/100");
```

### 错误处理

缓存系统具有完善的错误处理和回退机制：

```csharp
// 缓存初始化失败时会自动回退到无缓存模式
var runner = new Runner(enableCache: true);
// 即使缓存初始化失败，Runner仍然可以正常工作

// 检查缓存是否成功启用
if (runner.IsCacheEnabled)
{
    Debug.Log("缓存已启用");
}
else
{
    Debug.Log("缓存未启用，使用传统加载方式");
}
```

### Unity编辑器集成

在Unity编辑器中可以通过以下方式管理缓存：

1. **缓存管理器窗口**：`Tools/MookDialogue/Cache Manager`
   - 查看实时缓存状态
   - 执行缓存操作
   - 生成缓存报告

2. **快捷菜单**：
   - `Tools/MookDialogue/Clear All Cache` - 清空所有缓存
   - `Tools/MookDialogue/Warmup Cache` - 预热缓存
   - `Tools/MookDialogue/Generate Cache Report` - 生成报告

3. **Inspector集成**：
   - 在 `IncrementalCacheIntegration` 组件Inspector中查看实时状态
   - 调整缓存配置参数
   - 监控内存使用情况

### 常见问题

**Q: 缓存占用内存过多怎么办？**
A: 使用 `CacheConfigType.MemoryFriendly` 配置，或自定义更小的 `MaxMemoryUsage` 和 `MaxCacheSize`。

**Q: 缓存命中率低怎么优化？**
A: 检查文件是否频繁变动，考虑调整 `CacheExpiration` 时间，或使用预热功能。

**Q: 如何在运行时禁用缓存？**
A: 创建Runner时传入 `enableCache: false` 或使用 `CacheConfigType.Disabled`。

**Q: 缓存数据保存在哪里？**
A: 默认保存在 `Application.persistentDataPath/DialogueCache` 目录中（如果启用持久化缓存）。