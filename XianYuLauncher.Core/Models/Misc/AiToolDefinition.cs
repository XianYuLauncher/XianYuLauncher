namespace XianYuLauncher.Core.Models;

/// <summary>
/// OpenAI function calling 工具定义
/// </summary>
public class AiToolDefinition
{
    public string Type { get; set; } = "function";
    public AiFunctionDefinition Function { get; set; } = new();

    public static AiToolDefinition Create(string name, string description, object parameters)
    {
        return new AiToolDefinition
        {
            Type = "function",
            Function = new AiFunctionDefinition
            {
                Name = name,
                Description = description,
                Parameters = parameters
            }
        };
    }
}

public class AiFunctionDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public object Parameters { get; set; } = new { type = "object", properties = new { }, required = Array.Empty<string>() };
}
