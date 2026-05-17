using System.Globalization;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;
using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Services;

/// <summary>
/// 在日志落盘前对文本进行统一脱敏。
/// </summary>
public sealed class SensitiveLogTextFormatter : ITextFormatter
{
    private readonly MessageTemplateTextFormatter _innerFormatter;

    public SensitiveLogTextFormatter(string outputTemplate)
    {
        _innerFormatter = new MessageTemplateTextFormatter(outputTemplate, CultureInfo.InvariantCulture);
    }

    public void Format(LogEvent logEvent, TextWriter output)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        _innerFormatter.Format(logEvent, writer);
        output.Write(SensitiveDataSanitizer.Sanitize(writer.ToString()));
    }
}