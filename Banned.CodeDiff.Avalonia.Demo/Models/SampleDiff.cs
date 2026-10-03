namespace Banned.CodeDiff.Avalonia.Demo.Models;

/// <summary>Sample unified diff used by the demo's "load sample" action.</summary>
public static class SampleDiff
{
    public const string ProgramCs = """
        diff --git a/Program.cs b/Program.cs
        --- a/Program.cs
        +++ b/Program.cs
        @@ -1,6 +1,7 @@
         using System;
         using System.Text;
         
        -Console.WriteLine("Hello");
        +Console.WriteLine("Hello, World!");
        +var name = Console.ReadLine();
         
         if (args.Length > 0)
        @@ -10,3 +11,4 @@
         
        -    return 1;
        +    return 2;
         }
        +// done
        """;
}