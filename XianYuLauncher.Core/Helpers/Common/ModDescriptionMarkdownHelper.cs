using System;
using System.Net;
using System.Text.RegularExpressions;

namespace XianYuLauncher.Core.Helpers;

public static class ModDescriptionMarkdownHelper
{
    public static string Preprocess(string description)
    {
        if (string.IsNullOrEmpty(description))
        {
            return string.Empty;
        }

        try
        {
            description = WebUtility.HtmlDecode(description);

            description = Regex.Replace(
                description,
                @"<h([1-6])(?: [^>]*)?>(.*?)</h\1>",
                m => "\n" + new string('#', int.Parse(m.Groups[1].Value)) + " " + m.Groups[2].Value + "\n",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            description = Regex.Replace(
                description,
                @"<(?:strong|b)(?: [^>]*)?>(.*?)</(?:strong|b)>",
                "**$1**",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            description = Regex.Replace(
                description,
                @"<(?:em|i)(?: [^>]*)?>(.*?)</(?:em|i)>",
                "*$1*",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            description = Regex.Replace(
                description,
                @"<a\s+(?:[^>]*?\s+)?href\s*=\s*([""'])(.*?)\1[^>]*>(.*?)</a>",
                "[$3]($2)",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            description = Regex.Replace(
                description,
                @"<img\s+(?:[^>]*?\s+)?src\s*=\s*([""'])(.*?)\1.*?>",
                "![]($2)",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            description = description.Replace("</img>", "");
            description = description.Replace("<li>", "\n- ").Replace("</li>", "");
            description = description.Replace("<ul>", "\n").Replace("</ul>", "\n");
            description = description.Replace("<ol>", "\n").Replace("</ol>", "\n");
            description = description.Replace("<p>", "").Replace("</p>", "\n\n");
            description = description.Replace("<br>", "\n").Replace("<br/>", "\n").Replace("<br />", "\n");

            description = Regex.Replace(description, @"<blockquote(?: [^>]*)?>", "\n> ", RegexOptions.IgnoreCase);
            description = description.Replace("</blockquote>", "\n\n");
            description = Regex.Replace(description, @"<hr(?: [^>]*)?/?>", "\n---\n", RegexOptions.IgnoreCase);

            description = Regex.Replace(
                description,
                @"</?(?:div|span|center|font|tbody|tr|td|table|thead|th)[^>]*>",
                string.Empty,
                RegexOptions.IgnoreCase);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Markdown Preprocess] Error: {ex.Message}");
        }

        return description;
    }
}
