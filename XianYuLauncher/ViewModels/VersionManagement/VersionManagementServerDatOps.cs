using System;
using System.Collections.Generic;
using System.IO;
using fNbt;
using XianYuLauncher.Models.VersionManagement;

namespace XianYuLauncher.ViewModels;

internal static class VersionManagementServerDatOps
{
    public static void AddServer(string serversDatPath, string name, string address)
    {
        string? directoryPath = Path.GetDirectoryName(serversDatPath);
        if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        NbtFile file = new();
        if (File.Exists(serversDatPath))
        {
            try
            {
                file.LoadFromFile(serversDatPath);
            }
            catch
            {
            }
        }

        if (file.RootTag == null)
        {
            file.RootTag = new NbtCompound(string.Empty);
        }

        NbtList? serversList = file.RootTag.Get<NbtList>("servers");
        if (serversList == null)
        {
            serversList = new NbtList("servers", NbtTagType.Compound);
            file.RootTag.Add(serversList);
        }

        NbtCompound newServer = new();
        newServer.Add(new NbtString("name", name));
        newServer.Add(new NbtString("ip", address));
        newServer.Add(new NbtByte("hidden", 0));
        serversList.Add(newServer);

        file.SaveToFile(serversDatPath, NbtCompression.None);
    }

    public static List<ServerItem> LoadServers(string serversDatPath)
    {
        List<ServerItem> servers = new();

        if (!File.Exists(serversDatPath))
        {
            return servers;
        }

        try
        {
            NbtFile file = new();
            file.LoadFromFile(serversDatPath);
            NbtList? serversTag = file.RootTag.Get<NbtList>("servers");
            if (serversTag == null)
            {
                return servers;
            }

            foreach (NbtCompound serverTag in serversTag)
            {
                bool isHidden = serverTag["hidden"]?.ByteValue == 1;
                if (isHidden)
                {
                    continue;
                }

                ServerItem serverItem = new()
                {
                    Name = serverTag["name"]?.StringValue ?? "Minecraft Server",
                    Address = serverTag["ip"]?.StringValue ?? string.Empty,
                    IconBase64 = serverTag["icon"]?.StringValue
                };
                servers.Add(serverItem);
            }
        }
        catch
        {
        }

        return servers;
    }

    public static bool RemoveServer(string serversDatPath, string name, string address)
    {
        if (!File.Exists(serversDatPath))
        {
            return false;
        }

        NbtFile file = new();
        file.LoadFromFile(serversDatPath);
        NbtList? list = file.RootTag.Get<NbtList>("servers");
        if (list == null)
        {
            return false;
        }

        string expectedName = name ?? string.Empty;
        string expectedAddress = address ?? string.Empty;

        NbtCompound? target = null;
        foreach (NbtCompound tag in list)
        {
            string tagName = tag["name"]?.StringValue ?? string.Empty;
            string tagIp = tag["ip"]?.StringValue ?? string.Empty;
            if (tagName == expectedName && tagIp == expectedAddress)
            {
                target = tag;
                break;
            }
        }

        if (target == null)
        {
            return false;
        }

        list.Remove(target);
        file.SaveToFile(serversDatPath, NbtCompression.None);
        return true;
    }
}
