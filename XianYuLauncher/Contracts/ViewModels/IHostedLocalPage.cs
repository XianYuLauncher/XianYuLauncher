namespace XianYuLauncher.Contracts.ViewModels;

public interface IHostedLocalPage
{
    IPageHeaderAware HeaderSource { get; }

    event EventHandler? CloseRequested;

    void ResetEmbeddedVisualState();
}