using Banned.CodeDiff.Services;
using NUnit.Framework;
using System.Text.Json.Nodes;

namespace Banned.CodeDiff.Tests;

/// <summary>
///     Golden replay of the DiffFile pipeline: initRaw → buildSplitDiffLines →
///     buildUnifiedDiffLines → expansion steps, against the original TS sources run on
///     the repository's own demo data (ui/vue-example/src/data.ts).
/// </summary>
public class DiffFileGoldenTests
{
    public static IEnumerable<object[]> Cases =>
        GoldenSupport.Golden["diffFile"]!.AsArray().Select((c, i) => new object[] { i });

    [Test]
    [TestCaseSource(nameof(Cases))]
    public void MatchesDiffFilePipeline(int index)
    {
        var c      = GoldenSupport.Golden["diffFile"]![index]!.AsObject();
        var models = c["models"]!.AsArray();

        var df = BuildInitial(c);

        for (var m = 0; m < models.Count; m++)
        {
            if (m > 0) ApplyStep(df, models[m]!["step"]!.AsObject());

            var expected = models[m]!["model"]!.AsObject();
            var actual   = GoldenSupport.DumpModel(df);
            var diffs    = GoldenSupport.Compare(expected, actual);
            Assert.That(diffs.Count == 0,
                        Is.True,
                        $"diffFile #{index} ({c["name"]}) model step {m}: {string.Join("\n", diffs)}");
        }
    }

    private static DiffFile BuildInitial(JsonObject c)
    {
        // The JS generator runs DiffFile.createInstance(data) with demo data of the
        // shape { oldFile?: { fileName, content }, newFile?: { fileName, content }, hunks?: string[] }.
        // The golden dump does not carry the raw input, so replay the demo data by name.
        var name = c["name"]!.GetValue<string>();
        var data = DemoData.ByName(name);
        var df = new DiffFile(data.OldFileName,
                              data.OldFileContent,
                              data.NewFileName,
                              data.NewFileContent,
                              data.Hunks);
        df.InitRaw();
        df.BuildSplitDiffLines();
        df.BuildUnifiedDiffLines();
        return df;
    }

    private static void ApplyStep(DiffFile df, JsonObject step)
    {
        var op = step["op"]!.GetValue<string>();
        switch (op)
        {
            case "split-expand" :
                df.OnSplitHunkExpand(ParseDir(step), step["index"]!.GetValue<int>());
                break;
            case "unified-expand" :
                df.OnUnifiedHunkExpand(ParseDir(step), step["index"]!.GetValue<int>());
                break;
            case "all-expand" :
                df.OnAllExpand(step["mode"]!.GetValue<string>() == "split"
                                   ? ExpandViewMode.Split
                                   : ExpandViewMode.Unified);
                break;
            case "all-collapse" :
                df.OnAllCollapse(step["mode"]!.GetValue<string>() == "split"
                                     ? ExpandViewMode.Split
                                     : ExpandViewMode.Unified);
                break;
            default :
                throw new InvalidOperationException($"unknown step op {op}");
        }
    }

    private static HunkExpandDirection ParseDir(JsonObject step)
    {
        return step["dir"]!.GetValue<string>() switch
        {
            "up"       => HunkExpandDirection.Up,
            "down"     => HunkExpandDirection.Down,
            "all"      => HunkExpandDirection.All,
            "up-all"   => HunkExpandDirection.UpAll,
            "down-all" => HunkExpandDirection.DownAll,
            var d      => throw new InvalidOperationException($"unknown dir {d}")
        };
    }
}

/// <summary>
///     Mirrors the handcrafted cases from gen.ts. Demo-xxx cases load the repository's
///     ui/vue-example/src/data.ts fixture (extracted verbatim into DemoData.cs).
/// </summary>
public static class DemoData
{
    private static readonly DemoCase _expandCase = BuildExpandCase();

    private static readonly DemoCase _singleCount = new("one.txt",
                                                        "only line\n",
                                                        "one.txt",
                                                        "only line changed\n",
                                                        [
                                                            "--- a/one.txt\n+++ b/one.txt\n@@ -1 +1 @@\n-only line\n+only line changed\n"
                                                        ]);

    public static DemoCase ByName(string name)
    {
        if (name.StartsWith("demo-", StringComparison.Ordinal)) return DemoDataStore.Get(name["demo-".Length..]);

        return name switch
        {
            "expand-split" or "expand-unified" => ExpandCase(),
            "single-count-omitted"             => SingleCountCase(),
            _                                  => throw new InvalidOperationException($"unknown golden case {name}")
        };
    }

    private static DemoCase ExpandCase()
    {
        return _expandCase;
    }

    private static DemoCase BuildExpandCase()
    {
        static string MkLine(int i, string tag)
        {
            return $"line {i} {tag}\n";
        }

        var oldLines = new string[120];
        var newLines = new string[120];
        for (var i = 1; i <= 120; i++)
        {
            oldLines[i - 1] = MkLine(i, "old");
            newLines[i - 1] = MkLine(i, "old");
        }

        for (var i = 5; i <= 8; i++) newLines[i - 1] = MkLine(i, "new");

        for (var i = 100; i <= 103; i++) newLines[i - 1] = MkLine(i, "new2");

        static string BuildHunk(string[] oldLines, string[] newLines, int oldStart, int oldEnd, int newStart)
        {
            var oldCount = oldEnd - oldStart + 1;
            var body     = "";
            var oc       = 0;
            var nc       = 0;
            for (var i = oldStart; i <= oldEnd; i++)
            {
                var o = oldLines[i - 1];
                var n = newLines[i - 1];
                if (o == n)
                {
                    body += " " + o;
                    oc++;
                    nc++;
                }
                else
                {
                    body += "-" + o;
                    body += "+" + n;
                    oc++;
                    nc++;
                }
            }

            return $"@@ -{oldStart},{oc} +{newStart},{nc} @@\n" + body;
        }

        var hunk1    = BuildHunk(oldLines, newLines, 1, 12, 1);
        var hunk2    = BuildHunk(oldLines, newLines, 96, 107, 96);
        var diffText = $"--- a/big.txt\n+++ b/big.txt\n{hunk1}{hunk2}";
        return new DemoCase(
                            "big.txt",
                            string.Join("", oldLines),
                            "big.txt",
                            string.Join("", newLines),
                            [diffText]
                           );
    }

    private static DemoCase SingleCountCase()
    {
        return _singleCount;
    }

    public sealed record DemoCase(
        string   OldFileName,
        string   OldFileContent,
        string   NewFileName,
        string   NewFileContent,
        string[] Hunks);
}
