using Banned.CodeDiff.Services;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Banned.CodeDiff.Tests;

/// <summary>
/// Replays the golden fastDiff cases: the C# port vs the real fast-diff@1.3.0 run in
/// tests/js-harness. Cases flagged pathological make fast-diff infinite-recurse (JS
/// dies with RangeError); the C# port depth-guards those and must throw a catchable
/// exception instead of a process-fatal StackOverflowException.
/// </summary>
public class FastDiffGoldenTests
{
    public static IEnumerable<object[]> Cases =>
        GoldenSupport.Golden["fastDiff"]!.AsArray().Select((c, i) => new object[] { i });

    [Theory]
    [MemberData(nameof(Cases))]
    public void MatchesFastDiffJs(int index)
    {
        var c = GoldenSupport.Golden["fastDiff"]![index]!.AsObject();
        var a = c["a"]!.GetValue<string>();
        var b = c["b"]!.GetValue<string>();

        if (c["pathological"]?.GetValue<bool>() == true)
        {
            // JS died before producing a result. When it was the fast-diff infinite
            // recursion (RangeError) the C# port must throw via the depth guard; when
            // it was a fast-diff argument bug (e.g. {oldRange} without newRange reads
            // undefined.length) there is no reference behavior to match, so skip.
            var jsError = c["error"]?.GetValue<string>() ?? "";
            if (jsError.Contains("Maximum call stack"))
            {
                Assert.ThrowsAny<Exception>(() => RunCase(c));
            }

            return;
        }

        var actual = RunCase(c);

        var diffs = GoldenSupport.Compare(c["out"], GoldenSupport.DumpFastDiffResult(actual));
        Assert.True(diffs.Count == 0,
                    $"case #{index}: {string.Join("\n", diffs)}\ninput a={Truncate(a)} b={Truncate(b)}");
    }

    private static List<DiffTuple> RunCase(JsonObject c)
    {
        var a       = c["a"]!.GetValue<string>();
        var b       = c["b"]!.GetValue<string>();
        var cleanup = c["cleanup"]!.GetValue<bool>();

        var cursorNode = c["cursor"];
        if (cursorNode is null || cursorNode.GetValueKind() == JsonValueKind.Null)
        {
            return FastDiff.Diff(a, b, (int?)null, cleanup);
        }

        if (cursorNode.GetValueKind() == JsonValueKind.Number)
        {
            return FastDiff.Diff(a, b, cursorNode.GetValue<int>(), cleanup);
        }

        var cursorObj = cursorNode!.AsObject();
        var info = new CursorInfo
        {
            OldRange = new CursorRange(cursorObj["oldRange"]!["index"]!.GetValue<int>(),
                                       cursorObj["oldRange"]!["length"]!.GetValue<int>()),
            NewRange = null,
        };
        if (cursorObj["newRange"] is not null && cursorObj["newRange"]!.GetValueKind() != JsonValueKind.Null)
        {
            info.NewRange = new CursorRange(cursorObj["newRange"]!["index"]!.GetValue<int>(),
                                            cursorObj["newRange"]!["length"]!.GetValue<int>());
        }

        return FastDiff.Diff(a, b, info, cleanup);
    }

    private static string Truncate(string s)
    {
        return s.Length <= 60 ? s : s[..60] + $"...({s.Length})";
    }
}