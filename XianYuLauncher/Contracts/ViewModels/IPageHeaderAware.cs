using XianYuLauncher.Shared.Models;

namespace XianYuLauncher.Contracts.ViewModels;

public interface IPageHeaderAware
{
    PageHeaderMetadata HeaderMetadata { get; }

    PageHeaderPresentationMode HeaderPresentationMode { get; }
}