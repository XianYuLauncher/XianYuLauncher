using XianYuLauncher.Core.Contracts.Navigation;
using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Tests.Helpers;

public class LocalBreadcrumbNavigationPlannerTests
{
    [Fact]
    public void FindPreviousLocalBreadcrumb_WhenOnlyRootAndCurrent_ReturnsRoot()
    {
        var root = CreateLocalBreadcrumb("资源下载");
        var current = CreateCurrentBreadcrumb("版本 1.20.1");

        var result = LocalBreadcrumbNavigationPlanner.FindPreviousLocalBreadcrumb([root, current]);

        Assert.Same(root, result);
    }

    [Fact]
    public void FindLocalRootBreadcrumb_WhenGlobalBreadcrumbExists_SkipsGlobalBreadcrumb()
    {
        var global = CreateGlobalBreadcrumb("下载");
        var root = CreateLocalBreadcrumb("资源下载");
        var current = CreateCurrentBreadcrumb("版本 1.20.1");

        var result = LocalBreadcrumbNavigationPlanner.FindLocalRootBreadcrumb([global, root, current]);

        Assert.Same(root, result);
    }

    [Fact]
    public void TryCreateBackPlan_WhenReturningToMiddleLocalBreadcrumb_UsesSingleBackStep()
    {
        var global = CreateGlobalBreadcrumb("下载");
        var root = CreateLocalBreadcrumb("资源下载");
        var middle = CreateLocalBreadcrumb("加载器选择");
        var current = CreateCurrentBreadcrumb("安装确认");

        var success = LocalBreadcrumbNavigationPlanner.TryCreateBackPlan(
            [global, root, middle, current],
            middle,
            out var backSteps,
            out var destinationIsLocalRoot);

        Assert.True(success);
        Assert.Equal(1, backSteps);
        Assert.False(destinationIsLocalRoot);
    }

    [Fact]
    public void TryCreateBackPlan_WhenReturningToLocalRootFromThirdLevel_UsesTwoBackSteps()
    {
        var global = CreateGlobalBreadcrumb("下载");
        var root = CreateLocalBreadcrumb("资源下载");
        var middle = CreateLocalBreadcrumb("加载器选择");
        var current = CreateCurrentBreadcrumb("安装确认");

        var success = LocalBreadcrumbNavigationPlanner.TryCreateBackPlan(
            [global, root, middle, current],
            root,
            out var backSteps,
            out var destinationIsLocalRoot);

        Assert.True(success);
        Assert.Equal(2, backSteps);
        Assert.True(destinationIsLocalRoot);
    }

    [Fact]
    public void TryCreateBackPlan_WhenTargetIsNotLocalBreadcrumb_ReturnsFalse()
    {
        var global = CreateGlobalBreadcrumb("下载");
        var current = CreateCurrentBreadcrumb("版本 1.20.1");

        var success = LocalBreadcrumbNavigationPlanner.TryCreateBackPlan(
            [global, current],
            global,
            out var backSteps,
            out var destinationIsLocalRoot);

        Assert.False(success);
        Assert.Equal(0, backSteps);
        Assert.False(destinationIsLocalRoot);
    }

    private static NavigationBreadcrumbItem CreateGlobalBreadcrumb(string displayText)
    {
        return new NavigationBreadcrumbItem
        {
            DisplayText = displayText,
            HasLocalNavigationTarget = false,
        };
    }

    private static NavigationBreadcrumbItem CreateLocalBreadcrumb(string displayText)
    {
        return new NavigationBreadcrumbItem
        {
            DisplayText = displayText,
            HasLocalNavigationTarget = true,
        };
    }

    private static NavigationBreadcrumbItem CreateCurrentBreadcrumb(string displayText)
    {
        return new NavigationBreadcrumbItem
        {
            DisplayText = displayText,
            IsCurrent = true,
        };
    }

    private sealed class NavigationBreadcrumbItem : ILocalBreadcrumbNavigationItem
    {
        public string DisplayText { get; init; } = string.Empty;

        public bool HasLocalNavigationTarget { get; init; }

        public bool IsCurrent { get; init; }
    }
}