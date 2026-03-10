using Newtonsoft.Json.Linq;
using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Tests.Helpers;

public class DefaultUserJvmArgumentsHelperTests
{
    [Fact]
    public void ResolveEffectiveArguments_ShouldExpandStringAndArrayValues_WhenRulesMatch()
    {
        var entries = new List<object>
        {
            JObject.Parse("""
            {
              "value": ["-Xms2G", "-Xmx4G"]
            }
            """),
            "-XX:+UseStringDeduplication",
            JObject.Parse("""
            {
              "rules": [{"action":"allow", "os":{"name":"windows"}}],
              "value": "-XX:+UseZGC"
            }
            """)
        };

        var result = DefaultUserJvmArgumentsHelper.ResolveEffectiveArguments(entries);

        result.Should().Contain("-Xms2G");
        result.Should().Contain("-Xmx4G");
        result.Should().Contain("-XX:+UseStringDeduplication");

        if (OperatingSystem.IsWindows())
        {
            result.Should().Contain("-XX:+UseZGC");
        }
        else
        {
            result.Should().NotContain("-XX:+UseZGC");
        }
    }

    [Fact]
    public void ResolveEffectiveArguments_ShouldApplyLastMatchingRule_WhenAllowAndDisallowAreBothPresent()
    {
        var currentOs = GetCurrentOsName();
        var entries = new List<object>
        {
            JObject.Parse($"""
            {{
              "rules": [
                {{"action":"allow", "os":{{"name":"{currentOs}"}}}},
                {{"action":"disallow", "os":{{"name":"{currentOs}"}}}}
              ],
              "value": ["-XX:+UseZGC"]
            }}
            """)
        };

        var result = DefaultUserJvmArgumentsHelper.ResolveEffectiveArguments(entries);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ResolveEffectiveArguments_ShouldRespectWindowsVersionRangeRules()
    {
        var entries = new List<object>
        {
            JObject.Parse("""
            {
              "rules": [
                {
                  "action": "allow",
                  "os": {
                    "name": "windows",
                    "versionRange": { "min": "99.0.0" }
                  }
                }
              ],
              "value": ["-XX:+UseZGC"]
            }
            """),
            JObject.Parse("""
            {
              "rules": [
                {
                  "action": "allow",
                  "os": {
                    "name": "windows",
                    "versionRange": { "max": "99.0.0" }
                  }
                }
              ],
              "value": ["-XX:+UseG1GC"]
            }
            """)
        };

        var result = DefaultUserJvmArgumentsHelper.ResolveEffectiveArguments(entries);

        if (OperatingSystem.IsWindows())
        {
            result.Should().NotContain("-XX:+UseZGC");
            result.Should().Contain("-XX:+UseG1GC");
        }
        else
        {
            result.Should().BeEmpty();
        }
    }

    private static string GetCurrentOsName()
    {
        if (OperatingSystem.IsWindows())
        {
            return "windows";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "osx";
        }

        return "linux";
    }
}
