# 错误知识库使用说明

## 概述

错误知识库是一个本地化的崩溃分析系统，用于替代 AI 服务。它通过规则匹配来识别常见的游戏崩溃问题，并提供相应的解决方案。

## 文件结构

```
XianYuLauncher.Core/
├── Data/
│   └── ErrorKnowledgeBase.json    # 错误规则知识库
├── Models/
│   └── ErrorKnowledgeBase.cs      # 知识库数据模型
└── Services/
    ├── ErrorKnowledgeBaseService.cs  # 知识库服务
    └── CrashAnalyzer.cs              # 崩溃分析器（已重构）
```

## 使用方式

### 1. 基础分析（非流式）

```csharp
var crashAnalyzer = new CrashAnalyzer(fileService);
var result = await crashAnalyzer.AnalyzeCrashAsync(exitCode, outputLogs, errorLogs);

Console.WriteLine($"崩溃类型: {result.Type}");
Console.WriteLine($"分析: {result.Analysis}");
foreach (var suggestion in result.Suggestions)
{
    Console.WriteLine($"- {suggestion}");
}
```

### 2. 流式输出（仿 AI 效果）

```csharp
var crashAnalyzer = new CrashAnalyzer(fileService);

await foreach (var chunk in crashAnalyzer.GetStreamingAnalysisAsync(exitCode, outputLogs, errorLogs))
{
    Console.Write(chunk);  // 逐字输出
}
```

## 添加新规则

编辑 `ErrorKnowledgeBase.json` 文件，添加新的错误规则：

```json
{
  "id": "your_error_id",
  "type": "YourErrorType",
  "priority": 80,
  "patterns": [
    {
      "type": "contains",
      "value": "error keyword"
    },
    {
      "type": "regex",
      "value": "(?i)regex.*pattern"
    }
  ],
  "analysis": "问题的详细分析说明",
  "suggestions": [
    "解决建议 1",
    "解决建议 2"
  ]
}
```

### 规则字段说明

- **id**: 规则唯一标识符
- **type**: 崩溃类型（需要在 CrashType 枚举中定义）
- **priority**: 优先级（0-100，数值越大优先级越高）
- **patterns**: 匹配模式列表
  - **type**: `contains`（包含匹配）或 `regex`（正则表达式）
  - **value**: 匹配的值
- **analysis**: 问题分析文本
- **suggestions**: 解决建议列表

### 优先级建议

- 100: 特殊情况（如手动触发的崩溃）
- 90-95: 明确的错误（如 Java 版本不匹配）
- 80-89: 常见错误（如内存不足、Mod 冲突）
- 70-79: 一般错误（如网络问题、驱动问题）
- 60-69: 不太常见的错误

## 优势

1. **完全本地化** - 不依赖任何外部 API，无需网络连接
2. **无合规风险** - 不涉及 AI 服务，无需备案
3. **可扩展** - 轻松添加新的错误规则
4. **仿流式输出** - 提供类似 AI 的用户体验
5. **统一管理** - 所有错误规则集中在一个 JSON 文件中

## 维护建议

1. 定期收集用户反馈，补充新的错误规则
2. 优化现有规则的匹配模式，提高准确率
3. 更新解决建议，确保与最新版本游戏匹配
4. 考虑支持多语言（在 JSON 中添加语言字段）
