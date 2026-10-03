namespace Banned.CodeDiff.Avalonia.Demo.Models;

/// <summary>
/// Multi-language syntax samples (C# / TypeScript / JSON) with real old/new file
/// contents plus matching unified diffs — exercising keywords, strings, block
/// comments, template literals, regexes, and escaped JSON strings. The diff
/// bodies come from git diff --no-index; the headers are rewritten to the sample
/// file names.
/// </summary>
public static class SyntaxSample
{
    public static (string FileName, string OldContent, string NewContent, string Diff) CSharp() =>
        ("store.cs", """
        using System;
        using System.Collections.Generic;

        namespace Demo
        {
            // A simple order book.
            public class OrderBook
            {
                private readonly Dictionary<string, decimal> _prices = new();

                public decimal Total(int count)
                {
                    /* sum over the first N entries */
                    decimal total = 0;
                    for (int i = 0; i < count; i++)
                    {
                        total += i * 1.5m;
                    }
                    return total;
                }
            }
        }
        """, """
        using System;
        using System.Collections.Generic;

        namespace Demo
        {
            // A simple order book.
            public class OrderBook
            {
                private readonly Dictionary<string, decimal> _prices = new();

                public decimal Total(int count)
                {
                    /* sum over the first N entries */
                    decimal total = 0;
                    for (int i = 0; i < count; i++)
                    {
                        total += i * 1.5m;
                    }
                    return total;
                }

                public void Reset()
                {
                    _prices.Clear();
                    Console.WriteLine("order book reset");
                }
            }
        }
        """, """
        diff --git a/store.cs b/store.cs
        --- a/store.cs
        +++ b/store.cs
        @@ -18,5 +18,11 @@ namespace Demo
                     }
                     return total;
                 }
        +
        +        public void Reset()
        +        {
        +            _prices.Clear();
        +            Console.WriteLine("order book reset");
        +        }
             }
         }
        """);

    public static (string FileName, string OldContent, string NewContent, string Diff) TypeScript() =>
        ("api.ts", """
        export interface User {
          id: number;
          name: string;
          email?: string;
        }

        export function greet(user: User): string {
          const prefix = user.email ? "dear" : "hello";
          return `${prefix} ${user.name.toUpperCase()} (#${user.id})`;
        }

        const emailPattern = /^[a-z]+@[a-z]+\.[a-z]{2,}$/;
        """, """
        export interface User {
          id: number;
          name: string;
          email: string;
          roles: string[];
        }

        export function greet(user: User): string {
          const prefix = user.email ? "dear" : "hello";
          const suffix = user.roles.length > 0 ? ` [${user.roles.join(", ")}]` : "";
          return `${prefix} ${user.name.toUpperCase()} (#${user.id})${suffix}`;
        }

        const emailPattern = /^[a-z]+@[a-z]+\.[a-z]{2,}$/;
        """, """
        diff --git a/api.ts b/api.ts
        --- a/api.ts
        +++ b/api.ts
        @@ -1,12 +1,14 @@
         export interface User {
           id: number;
           name: string;
        -  email?: string;
        +  email: string;
        +  roles: string[];
         }

         export function greet(user: User): string {
           const prefix = user.email ? "dear" : "hello";
        -  return `${prefix} ${user.name.toUpperCase()} (#${user.id})`;
        +  const suffix = user.roles.length > 0 ? ` [${user.roles.join(", ")}]` : "";
        +  return `${prefix} ${user.name.toUpperCase()} (#${user.id})${suffix}`;
         }

         const emailPattern = /^[a-z]+@[a-z]+\.[a-z]{2,}$/;
        """);

    public static (string FileName, string OldContent, string NewContent, string Diff) Json() =>
        ("config.json", """
        {
          "app": "demo",
          "version": "1.2.0",
          "features": {
            "darkMode": true,
            "beta": false
          },
          "limits": [10, 20, 30],
          "note": "escaped \" quote"
        }
        """, """
        {
          "app": "demo",
          "version": "1.3.0",
          "features": {
            "darkMode": true,
            "beta": true,
            "syntax": true
          },
          "limits": [10, 20, 30, 40],
          "note": "escaped \" quote"
        }
        """, """
        diff --git a/config.json b/config.json
        --- a/config.json
        +++ b/config.json
        @@ -1,10 +1,11 @@
         {
           "app": "demo",
        -  "version": "1.2.0",
        +  "version": "1.3.0",
           "features": {
             "darkMode": true,
        -    "beta": false
        +    "beta": true,
        +    "syntax": true
           },
        -  "limits": [10, 20, 30],
        +  "limits": [10, 20, 30, 40],
           "note": "escaped \" quote"
         }
        """);
}
