using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

using XianYuLauncher.Contracts.ViewModels;

namespace XianYuLauncher.Helpers;

public sealed class HostedLocalPageCoordinator
{
    private readonly Action<IPageHeaderAware> _applyHostedPageHeaderState;
    private readonly EventHandler _hostedLocalPageCloseRequested;
    private readonly Action<IHostedLocalPage>? _onHostedPageAttached;
    private readonly Action<IHostedLocalPage>? _onHostedPageDetaching;
    private IHostedLocalPage? _activeHostedLocalPage;

    public HostedLocalPageCoordinator(
        Action<IPageHeaderAware> applyHostedPageHeaderState,
        EventHandler hostedLocalPageCloseRequested,
        Action<IHostedLocalPage>? onHostedPageAttached = null,
        Action<IHostedLocalPage>? onHostedPageDetaching = null)
    {
        _applyHostedPageHeaderState = applyHostedPageHeaderState;
        _hostedLocalPageCloseRequested = hostedLocalPageCloseRequested;
        _onHostedPageAttached = onHostedPageAttached;
        _onHostedPageDetaching = onHostedPageDetaching;
    }

    public IHostedLocalPage? ActiveHostedLocalPage => _activeHostedLocalPage;

    public void Attach(IHostedLocalPage hostedLocalPage)
    {
        ArgumentNullException.ThrowIfNull(hostedLocalPage);

        Detach();

        _activeHostedLocalPage = hostedLocalPage;
        hostedLocalPage.ResetEmbeddedVisualState();
        hostedLocalPage.CloseRequested += _hostedLocalPageCloseRequested;
        hostedLocalPage.HeaderSource.HeaderMetadata.PropertyChanged += ActiveHostedHeaderMetadata_PropertyChanged;
        _onHostedPageAttached?.Invoke(hostedLocalPage);
        _applyHostedPageHeaderState(hostedLocalPage.HeaderSource);
    }

    public void Detach()
    {
        if (_activeHostedLocalPage is null)
        {
            return;
        }

        var hostedLocalPage = _activeHostedLocalPage;
        _onHostedPageDetaching?.Invoke(hostedLocalPage);
        hostedLocalPage.CloseRequested -= _hostedLocalPageCloseRequested;
        hostedLocalPage.HeaderSource.HeaderMetadata.PropertyChanged -= ActiveHostedHeaderMetadata_PropertyChanged;
        _activeHostedLocalPage = null;
    }

    public bool TryGetActiveHostedLocalPage([NotNullWhen(true)] out IHostedLocalPage? hostedLocalPage)
    {
        hostedLocalPage = _activeHostedLocalPage;
        return hostedLocalPage is not null;
    }

    private void ActiveHostedHeaderMetadata_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_activeHostedLocalPage is null)
        {
            return;
        }

        _applyHostedPageHeaderState(_activeHostedLocalPage.HeaderSource);
    }
}