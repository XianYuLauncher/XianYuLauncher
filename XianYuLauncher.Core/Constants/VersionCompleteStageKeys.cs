namespace XianYuLauncher.Core.Constants;

public static class VersionCompleteStageKeys
{
    public const string CheckingDeps = "Dialog_VersionComplete_Stage_CheckingDeps";
    public const string ProcessingModLoader = "Dialog_VersionComplete_Stage_ProcessingModLoader";
    public const string DownloadingLibraries = "Dialog_VersionComplete_Stage_DownloadingLibraries";
    public const string ExtractingNatives = "Dialog_VersionComplete_Stage_ExtractingNatives";
    public const string ProcessingAssetIndex = "Dialog_VersionComplete_Stage_ProcessingAssetIndex";
    public const string DownloadingAssets = "Dialog_VersionComplete_Stage_DownloadingAssets";
    public const string RetryAssetFormat = "Dialog_VersionComplete_RetryAsset_Format";
    public const string SecondPassAssetFormat = "Dialog_VersionComplete_SecondPassAsset_Format";

    public static bool IsResourceKey(string? value) =>
        !string.IsNullOrEmpty(value) &&
        value.StartsWith("Dialog_VersionComplete_", StringComparison.Ordinal);
}