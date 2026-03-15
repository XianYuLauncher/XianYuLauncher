using System.Text.Json;
using Newtonsoft.Json.Linq;
using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.IntegrationTests;

/// <summary>
/// JVM 参数合并策略集成测试：
/// - 兼容现代 arguments/default-user-jvm + arguments.jvm
/// - 兼容旧版 minecraftArguments
/// - 仅覆盖用户可控域（Xms/Xmx/GC/-D），不破坏其它参数
/// </summary>
[Trait("Category", "Integration")]
public class JvmArgumentsMergeIntegrationTests
{
    [Fact]
    public void MergeAndDeduplicateArguments_ModernArguments_ShouldOnlyOverrideUserDomain()
    {
        const string modernJson = """
        {
          "arguments": {
            "default-user-jvm": [
              {
                "value": [
                  "-Xms2G",
                  "-Xmx4G",
                  "-XX:+UseCompactObjectHeaders",
                  "-XX:+AlwaysPreTouch",
                  "-XX:+UseStringDeduplication"
                ]
              },
              {
                "value": ["-XX:+UseZGC"]
              }
            ],
            "jvm": [
              "--sun-misc-unsafe-memory-access=allow",
              "--enable-native-access=ALL-UNNAMED",
              "-Djava.library.path=${natives_directory}",
              "-Dminecraft.launcher.brand=${launcher_name}",
              "-cp",
              "${classpath}",
              "--add-opens",
              "java.base/java.util.jar=cpw.mods.securejarhandler",
              "--add-opens",
              "java.base/java.lang.invoke=cpw.mods.securejarhandler"
            ]
          }
        }
        """;

        var launcherArgs = ExtractModernLauncherJvmArgs(modernJson);
        var customArgs = "-Xmx6G -XX:+UseG1GC -Dminecraft.launcher.brand=CustomBrand -Duser.custom.flag=true";

        var result = JvmArgumentsHelper.MergeAndDeduplicateArguments(launcherArgs, customArgs);

        // 覆盖域：用户参数应覆盖默认参数
        Assert.DoesNotContain("-Xmx4G", result);
        Assert.Contains("-Xmx6G", result);
        Assert.DoesNotContain("-XX:+UseZGC", result);
        Assert.Contains("-XX:+UseG1GC", result);
        Assert.Contains("-Dminecraft.launcher.brand=CustomBrand", result);

        // 非覆盖域：保持原样
        Assert.Contains("-cp", result);
        Assert.Contains("${classpath}", result);
        Assert.True(ContainsAdjacentPair(result, "--add-opens", "java.base/java.util.jar=cpw.mods.securejarhandler"));
        Assert.True(ContainsAdjacentPair(result, "--add-opens", "java.base/java.lang.invoke=cpw.mods.securejarhandler"));

        // 新增用户自定义属性应保留
        Assert.Contains("-Duser.custom.flag=true", result);
    }

    [Fact]
    public void MergeAndDeduplicateArguments_LegacyMinecraftArguments_ShouldNotBreakWhenNoManifestJvm()
    {
        const string legacyJson = """
        {
          "minecraftArguments": "--username ${auth_player_name} --version ${version_name} --gameDir ${game_directory} --assetsDir ${assets_root} --assetIndex ${assets_index_name} --uuid ${auth_uuid} --accessToken ${auth_access_token} --userType ${user_type} --tweakClass net.minecraftforge.fml.common.launcher.FMLTweaker --versionType Forge"
        }
        """;

        // 旧版 minecraftArguments 不提供 jvm 参数；launcherArgs 仅来自启动器默认项。
        var launcherArgs = new List<string>
        {
            "-Xms2G",
            "-Xmx4G",
            "-XX:+UseZGC",
            "-Dlauncher.default=true"
        };

        var customArgs = "-Xmx8G -XX:+UseG1GC -Dlauncher.default=false --add-opens java.base/java.util=ALL-UNNAMED";

        // 仅用于确认旧版字段存在且可解析，不参与 JVM 合并逻辑。
        var parsedLegacy = JsonDocument.Parse(legacyJson);
        Assert.True(parsedLegacy.RootElement.TryGetProperty("minecraftArguments", out var minecraftArgumentsProp));
        Assert.False(string.IsNullOrWhiteSpace(minecraftArgumentsProp.GetString()));

        var result = JvmArgumentsHelper.MergeAndDeduplicateArguments(launcherArgs, customArgs);

        // 覆盖域
        Assert.DoesNotContain("-Xmx4G", result);
        Assert.Contains("-Xmx8G", result);
        Assert.DoesNotContain("-XX:+UseZGC", result);
        Assert.Contains("-XX:+UseG1GC", result);
        Assert.Contains("-Dlauncher.default=false", result);

        // 非覆盖域（用户附加参数）不做全局去重
        Assert.True(ContainsAdjacentPair(result, "--add-opens", "java.base/java.util=ALL-UNNAMED"));
    }

    private static List<string> ExtractModernLauncherJvmArgs(string json)
    {
        var result = new List<string>();
        var root = JObject.Parse(json);
        var arguments = root["arguments"] as JObject;
        if (arguments == null)
        {
            return result;
        }

        // default-user-jvm 复用生产规则解析逻辑（含 rules）。
        if (arguments["default-user-jvm"] is JArray defaultUserJvm)
        {
            var entries = new List<object>(defaultUserJvm.Count);
            foreach (var token in defaultUserJvm)
            {
                if (token.Type == JTokenType.String)
                {
                    entries.Add(token.ToString());
                }
                else if (token is JObject obj)
                {
                    entries.Add(obj);
                }
            }

            result.AddRange(DefaultUserJvmArgumentsHelper.ResolveEffectiveArguments(entries));
        }

        if (arguments["jvm"] is JArray jvm)
        {
            foreach (var item in jvm)
            {
                AppendValueTokens(item, result);
            }
        }

        return result;
    }

    private static void AppendValueTokens(JToken item, List<string> target)
    {
        if (item.Type == JTokenType.String)
        {
            var value = item.ToString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                target.Add(value);
            }
            return;
        }

        if (item is JObject obj && obj.TryGetValue("value", out var valueElement))
        {
            if (valueElement.Type == JTokenType.String)
            {
                var value = valueElement.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    target.Add(value);
                }
            }
            else if (valueElement is JArray valueArray)
            {
                foreach (var token in valueArray)
                {
                    if (token.Type != JTokenType.String)
                    {
                        continue;
                    }

                    var text = token.ToString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        target.Add(text);
                    }
                }
            }
        }
    }

    private static bool ContainsAdjacentPair(List<string> args, string first, string second)
    {
        for (int i = 0; i < args.Count - 1; i++)
        {
            if (args[i] == first && args[i + 1] == second)
            {
                return true;
            }
        }

        return false;
    }
}
