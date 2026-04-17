using Microsoft.UI.Xaml;

namespace XianYuLauncher.Contracts.ViewModels;

public sealed class PageHeaderHostConfiguration
{
    public static PageHeaderHostConfiguration Disabled { get; } = new();

    public bool UseShellHeader { get; init; }

    public bool ShowPrimaryHeading { get; init; } = true;

    public double BreadcrumbFontSize { get; init; } = 14d;

    public Thickness BreadcrumbMargin { get; init; } = new(0, 0, 0, 12);

    public PageHeaderBreadcrumbTemplateKind BreadcrumbTemplateKind { get; init; } = PageHeaderBreadcrumbTemplateKind.Default;

    public PageHeaderSupplementalContentKind SupplementalContentKind { get; init; } = PageHeaderSupplementalContentKind.None;

    public PageHeaderTrailingActionsKind TrailingActionsKind { get; init; } = PageHeaderTrailingActionsKind.None;
}