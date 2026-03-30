using System.Globalization;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

public sealed class CommunityResourceMetadataService : ICommunityResourceMetadataService
{
    private readonly ModInfoService _modInfoService;

    public CommunityResourceMetadataService(ModInfoService modInfoService)
    {
        _modInfoService = modInfoService;
    }

    public async Task<CommunityResourceResolvedMetadata?> GetMetadataAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ModMetadata? metadata = await _modInfoService.GetModInfoAsync(filePath, cancellationToken).ConfigureAwait(false);
        if (metadata == null)
        {
            return null;
        }

        string? projectId = metadata.Source == "CurseForge" && metadata.CurseForgeModId > 0
            ? metadata.CurseForgeModId.ToString(CultureInfo.InvariantCulture)
            : metadata.ProjectId;

        return new CommunityResourceResolvedMetadata
        {
            Description = metadata.Description,
            Source = metadata.Source,
            ProjectId = projectId
        };
    }
}