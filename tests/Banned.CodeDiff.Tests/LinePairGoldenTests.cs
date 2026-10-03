using Banned.CodeDiff.Models;
using Banned.CodeDiff.Services;
using System.Text.Json.Nodes;
using NUnit.Framework;

namespace Banned.CodeDiff.Tests;

/// <summary>Golden replay of relativeChanges / diffChanges (change-range.ts).</summary>
public class LinePairGoldenTests
{
    public static IEnumerable<object[]> Cases =>
        GoldenSupport.Golden["linePairs"]!.AsArray().Select((c, i) => new object[] { i });

    [Test]
    [TestCaseSource(nameof(Cases))]
    public void MatchesRelativeChangesAndDiffChanges(int index)
    {
        var c          = GoldenSupport.Golden["linePairs"]![index]!.AsObject();
        var input      = c["input"]!.AsObject();
        var additionIn = input["addition"]!.AsObject();
        var deletionIn = input["deletion"]!.AsObject();

        var addition = new DiffLine(additionIn["text"]!.GetValue<string>(), DiffLineType.Add, null, null, 1,
                                    additionIn["noTrailingNewLine"]!.GetValue<bool>());
        var deletion = new DiffLine(deletionIn["text"]!.GetValue<string>(), DiffLineType.Delete, null, 1, null,
                                    deletionIn["noTrailingNewLine"]!.GetValue<bool>());

        var (relAdd, relDel)   = ChangeRange.RelativeChanges(addition, deletion);
        var (fastAdd, fastDel) = ChangeRange.DiffChanges(addition, deletion);

        var actual = new JsonObject
        {
            ["input"] = c["input"]!.DeepClone(),
            ["relative"] = new JsonObject
            {
                ["addRange"] = GoldenSupport.DumpLineRange(relAdd),
                ["delRange"] = GoldenSupport.DumpLineRange(relDel),
            },
            ["fastDiff"] = new JsonObject
            {
                ["addRange"] = GoldenSupport.DumpDiffRange(fastAdd),
                ["delRange"] = GoldenSupport.DumpDiffRange(fastDel),
            },
        };

        var diffs = GoldenSupport.Compare(c, actual);
        Assert.That(diffs.Count == 0, Is.True, $"linePair #{index}: {string.Join("\n", diffs)}");
    }
}

/// <summary>Golden replay of DiffParser (diff-parse.ts).</summary>
public class ParseGoldenTests
{
    public static IEnumerable<object[]> Cases =>
        GoldenSupport.Golden["parse"]!.AsArray().Select((c, i) => new object[] { i });

    [Test]
    [TestCaseSource(nameof(Cases))]
    public void MatchesDiffParserJs(int index)
    {
        var c     = GoldenSupport.Golden["parse"]![index]!.AsObject();
        var text  = c["text"]!.GetValue<string>();
        var error = c["error"];

        if (error is not null)
        {
            // JS threw; the C# port must throw as well
            Assert.Catch<Exception>(() => DiffParser.Shared.Parse(text));
            return;
        }

        var actual = GoldenSupport.DumpRawDiff(DiffParser.Shared.Parse(text));
        var diffs  = GoldenSupport.Compare(c["out"], actual);
        Assert.That(diffs.Count == 0, Is.True, $"parse #{index} ({c["name"]}): {string.Join("\n", diffs)}");
    }
}

/// <summary>Golden replay of getLang (diff-tool.ts).</summary>
public class LangGoldenTests
{
    public static IEnumerable<object[]> Cases =>
        GoldenSupport.Golden["lang"]!.AsArray().Select((c, i) => new object[] { i });

    [Test]
    [TestCaseSource(nameof(Cases))]
    public void MatchesGetLangJs(int index)
    {
        var c        = GoldenSupport.Golden["lang"]![index]!.AsObject();
        var input    = c["input"]!.GetValue<string>();
        var expected = c["output"]!.GetValue<string>();
        Assert.That(DiffTool.GetLang(input), Is.EqualTo(expected));
    }
}