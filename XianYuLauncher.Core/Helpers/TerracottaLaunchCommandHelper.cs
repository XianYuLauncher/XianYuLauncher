using System;

namespace XianYuLauncher.Core.Helpers;

public static class TerracottaLaunchCommandHelper
{
    public static string BuildHmclStartupCommandArguments(string workingDirectory, string executableName, string hmclFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(executableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(hmclFilePath);

        return $"/c cd /d \"{workingDirectory}\" && \"{executableName}\" --hmcl \"{hmclFilePath}\"";
    }
}