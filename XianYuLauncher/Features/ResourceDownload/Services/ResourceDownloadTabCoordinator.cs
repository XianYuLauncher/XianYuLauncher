using System.Threading.Tasks;
using XianYuLauncher.Features.ResourceDownload.ViewModels;

namespace XianYuLauncher.Features.ResourceDownload.Services;

public sealed class ResourceDownloadTabCoordinator : IResourceDownloadTabCoordinator
{
    private bool _versionsLoaded;
    private bool _modsLoaded;
    private bool _resourcePacksLoaded;
    private bool _shaderPacksLoaded;
    private bool _modpacksLoaded;
    private bool _datapacksLoaded;
    private bool _worldsLoaded;

    private bool _isVersionInitialLoadPending;
    private bool _isModInitialLoadPending;
    private bool _isShaderPackInitialLoadPending;
    private bool _isResourcePackInitialLoadPending;
    private bool _isDatapackInitialLoadPending;
    private bool _isModpackInitialLoadPending;
    private bool _isWorldInitialLoadPending;

    public bool IsTabLoaded(int tabIndex) => tabIndex switch
    {
        0 => _versionsLoaded,
        1 => _modsLoaded,
        2 => _shaderPacksLoaded,
        3 => _resourcePacksLoaded,
        4 => _datapacksLoaded,
        5 => _modpacksLoaded,
        6 => _worldsLoaded,
        _ => false
    };

    public async Task EnsureSelectedTabLoadedAsync(ResourceDownloadHostViewModel viewModel, int selectedIndex)
    {
        switch (selectedIndex)
        {
            case 0:
                if (_versionsLoaded || _isVersionInitialLoadPending)
                {
                    break;
                }

                _isVersionInitialLoadPending = true;
                try
                {
                    await viewModel.SearchVersionsCommand.ExecuteAsync(null);
                    _versionsLoaded = true;
                }
                finally
                {
                    _isVersionInitialLoadPending = false;
                }

                break;

            case 1:
                if (!_modsLoaded && !_isModInitialLoadPending)
                {
                    _isModInitialLoadPending = true;
                    try
                    {
                        await viewModel.LoadCategoriesAsync("mod");
                        await viewModel.SearchModsCommand.ExecuteAsync(null);
                        _modsLoaded = true;
                    }
                    finally
                    {
                        _isModInitialLoadPending = false;
                    }
                }

                break;

            case 2:
                if (!_shaderPacksLoaded && !_isShaderPackInitialLoadPending)
                {
                    _isShaderPackInitialLoadPending = true;
                    try
                    {
                        await viewModel.LoadCategoriesAsync("shader");
                        await viewModel.SearchShaderPacksCommand.ExecuteAsync(null);
                        _shaderPacksLoaded = true;
                    }
                    finally
                    {
                        _isShaderPackInitialLoadPending = false;
                    }
                }

                break;

            case 3:
                if (!_resourcePacksLoaded && !_isResourcePackInitialLoadPending)
                {
                    _isResourcePackInitialLoadPending = true;
                    try
                    {
                        await viewModel.LoadCategoriesAsync("resourcepack");
                        await viewModel.SearchResourcePacksCommand.ExecuteAsync(null);
                        _resourcePacksLoaded = true;
                    }
                    finally
                    {
                        _isResourcePackInitialLoadPending = false;
                    }
                }

                break;

            case 4:
                if (!_datapacksLoaded && !_isDatapackInitialLoadPending)
                {
                    _isDatapackInitialLoadPending = true;
                    try
                    {
                        await viewModel.LoadCategoriesAsync("datapack");
                        await viewModel.SearchDatapacksCommand.ExecuteAsync(null);
                        _datapacksLoaded = true;
                    }
                    finally
                    {
                        _isDatapackInitialLoadPending = false;
                    }
                }

                break;

            case 5:
                if (!_modpacksLoaded && !_isModpackInitialLoadPending)
                {
                    _isModpackInitialLoadPending = true;
                    try
                    {
                        await viewModel.LoadCategoriesAsync("modpack");
                        await viewModel.SearchModpacksCommand.ExecuteAsync(null);
                        _modpacksLoaded = true;
                    }
                    finally
                    {
                        _isModpackInitialLoadPending = false;
                    }
                }

                break;

            case 6:
                if (!_worldsLoaded && !_isWorldInitialLoadPending)
                {
                    _isWorldInitialLoadPending = true;
                    try
                    {
                        await viewModel.LoadCategoriesAsync("world");
                        await viewModel.SearchWorldsCommand.ExecuteAsync(null);
                        _worldsLoaded = true;
                    }
                    finally
                    {
                        _isWorldInitialLoadPending = false;
                    }
                }

                break;
        }
    }
}
