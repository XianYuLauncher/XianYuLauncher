using System;

namespace XianYuLauncher.Contracts.ViewModels;

/// <summary>
/// 允许一级页把自己内部的 detail 导航暴露给全局导航服务，用于统一仲裁返回键行为。
/// </summary>
public interface ILocalNavigationHost
{
    event EventHandler? LocalNavigationStateChanged;

    bool CanGoBackLocally { get; }

    bool TryGoBackLocally();

    void ResetLocalNavigation(bool useReturnTransition = false);
}