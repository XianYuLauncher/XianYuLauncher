using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Features.ResourceDownload.ViewModels.Tabs;

public abstract class CommunityResourceTabViewModel : ObservableObject
{
    protected const int PageSize = 20;

    protected static List<ModrinthProject> InterleaveLists(
        List<ModrinthProject> modrinthList,
        List<ModrinthProject> curseForgeList)
    {
        var result = new List<ModrinthProject>();
        int maxCount = Math.Max(modrinthList.Count, curseForgeList.Count);
        for (int i = 0; i < maxCount; i++)
        {
            if (i < modrinthList.Count) result.Add(modrinthList[i]);
            if (i < curseForgeList.Count) result.Add(curseForgeList[i]);
        }
        return result;
    }
}
