using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// XianYu Fixer 工具注册表 — 定义所有可供 AI 调用的 function calling 工具
/// </summary>
public static class FixerToolRegistry
{
    /// <summary>
    /// 获取所有工具定义
    /// </summary>
    public static List<AiToolDefinition> GetAllTools()
    {
        return
        [
            // ===== 查询类工具（只读，零风险）=====

            ListInstalledMods,
            GetVersionConfig,
            CheckJavaVersions,
            SearchKnowledgeBase,
            ReadModInfo,

            // ===== 操作类工具（需要用户确认）=====

            SearchModrinthProject,
            DeleteMod,
            ToggleMod,
            SwitchJavaForVersion,
        ];
    }

    // ---------- 查询类 ----------

    public static AiToolDefinition ListInstalledMods => AiToolDefinition.Create(
        "listInstalledMods",
        "列出当前游戏版本已安装的所有 Mod，返回名称、版本、文件名、是否启用",
        new
        {
            type = "object",
            properties = new { },
            required = Array.Empty<string>()
        });

    public static AiToolDefinition GetVersionConfig => AiToolDefinition.Create(
        "getVersionConfig",
        "获取当前游戏版本的配置信息，包括 Minecraft 版本号、ModLoader 类型和版本、Java 路径、内存设置等",
        new
        {
            type = "object",
            properties = new { },
            required = Array.Empty<string>()
        });

    public static AiToolDefinition CheckJavaVersions => AiToolDefinition.Create(
        "checkJavaVersions",
        "列出本机已安装的所有 Java 版本，返回版本号和路径",
        new
        {
            type = "object",
            properties = new { },
            required = Array.Empty<string>()
        });

    public static AiToolDefinition SearchKnowledgeBase => AiToolDefinition.Create(
        "searchKnowledgeBase",
        "在内置错误知识库中搜索匹配的错误规则，返回匹配的错误标题、分析和修复建议",
        new
        {
            type = "object",
            properties = new
            {
                keyword = new { type = "string", description = "搜索关键词，如错误类名或关键日志片段" }
            },
            required = new[] { "keyword" }
        });

    public static AiToolDefinition ReadModInfo => AiToolDefinition.Create(
        "readModInfo",
        "读取指定 Mod 文件的元数据（fabric.mod.json 或 mods.toml），返回 modId、名称、版本、依赖列表",
        new
        {
            type = "object",
            properties = new
            {
                fileName = new { type = "string", description = "Mod 的 jar 文件名" }
            },
            required = new[] { "fileName" }
        });

    // ---------- 操作类 ----------

    public static AiToolDefinition SearchModrinthProject => AiToolDefinition.Create(
        "searchModrinthProject",
        "在 Modrinth 搜索 Mod/资源包/光影，用于查找缺失的依赖或推荐替代品",
        new
        {
            type = "object",
            properties = new
            {
                query = new { type = "string", description = "搜索关键词" },
                projectType = new { type = "string", description = "资源类型: mod, resourcepack, shader", @enum = new[] { "mod", "resourcepack", "shader" } },
                loader = new { type = "string", description = "ModLoader 类型: fabric, forge, neoforge, quilt" }
            },
            required = new[] { "query" }
        });

    public static AiToolDefinition DeleteMod => AiToolDefinition.Create(
        "deleteMod",
        "删除指定的 Mod 文件（会弹窗让用户确认）",
        new
        {
            type = "object",
            properties = new
            {
                modId = new { type = "string", description = "Mod ID 或文件名" }
            },
            required = new[] { "modId" }
        });

    public static AiToolDefinition ToggleMod => AiToolDefinition.Create(
        "toggleMod",
        "启用或禁用指定 Mod（通过重命名 .jar <-> .jar.disabled），比删除更安全",
        new
        {
            type = "object",
            properties = new
            {
                fileName = new { type = "string", description = "Mod 的 jar 文件名" },
                enabled = new { type = "boolean", description = "true=启用, false=禁用" }
            },
            required = new[] { "fileName", "enabled" }
        });

    public static AiToolDefinition SwitchJavaForVersion => AiToolDefinition.Create(
        "switchJavaForVersion",
        "自动检测当前版本所需的 Java 版本并切换到最合适的已安装 Java",
        new
        {
            type = "object",
            properties = new { },
            required = Array.Empty<string>()
        });
}
