namespace XianYuLauncher.Core.Contracts.Navigation;

public interface ILocalBreadcrumbNavigationItem
{
    bool HasLocalNavigationTarget { get; }

    bool IsCurrent { get; }
}