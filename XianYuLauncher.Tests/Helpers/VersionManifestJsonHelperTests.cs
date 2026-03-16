using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Tests.Helpers;

public class VersionManifestJsonHelperTests
{
    [Fact]
    public void SerializeVersionJson_ShouldStripInheritsFrom_AndKeepManifestPropertyNames()
    {
        var versionInfo = new VersionInfo
        {
            Id = "fabric-1.20.4",
            InheritsFrom = "1.20.4",
            MainClass = "net.fabricmc.loader.impl.launch.knot.KnotClient"
        };

        string json = VersionManifestJsonHelper.SerializeVersionJson(versionInfo);
        var jsonObject = JObject.Parse(json);

        jsonObject.Property("inheritsFrom").Should().BeNull();
        jsonObject["id"]!.Value<string>().Should().Be("fabric-1.20.4");
        jsonObject["mainClass"]!.Value<string>().Should().Be("net.fabricmc.loader.impl.launch.knot.KnotClient");
        jsonObject.Property(nameof(VersionInfo.MainClass)).Should().BeNull();
    }

    [Fact]
    public void DeserializeVersionInfo_ShouldReadNewtonsoftManifestContract()
    {
        const string json = """
        {
          "id": "forge-1.20.1",
          "mainClass": "cpw.mods.bootstraplauncher.BootstrapLauncher",
          "inheritsFrom": "1.20.1",
          "libraries": [
            { "name": "net.minecraftforge:forge:47.2.0" },
            { "name": "com.mojang:brigadier:1.0.18" }
          ]
        }
        """;

        VersionInfo? versionInfo = VersionManifestJsonHelper.DeserializeVersionInfo(json);

        versionInfo.Should().NotBeNull();
        versionInfo!.Id.Should().Be("forge-1.20.1");
        versionInfo.MainClass.Should().Be("cpw.mods.bootstraplauncher.BootstrapLauncher");
        versionInfo.InheritsFrom.Should().Be("1.20.1");
        versionInfo.Libraries.Should().NotBeNull();
        versionInfo.Libraries!.ConvertAll(library => library.Name).Should().Equal(
            "net.minecraftforge:forge:47.2.0",
            "com.mojang:brigadier:1.0.18");
    }
}