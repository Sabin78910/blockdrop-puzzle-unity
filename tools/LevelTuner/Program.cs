using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BlockDrop.Core;

namespace BlockDrop.Core
{
    // The tuner builds without the baked table so every level is tuned fresh.
    public static class LevelTable
    {
        public static bool Has(int number) => false;
        public static LevelDefinition Build(int number) => throw new InvalidOperationException();
    }
}

public static class Program
{
    public static int Main(string[] args)
    {
        string path = args.Length > 0 ? args[0] : "Assets/Scripts/Core/LevelTable.cs";
        var results = new Playtester.Result[LevelLibrary.Count];
        Parallel.For(1, LevelLibrary.Count + 1, n => results[n - 1] = Playtester.Tune(n));
        var rows = new StringBuilder();
        foreach (var r in results)
        {
            rows.AppendLine("            " + Playtester.TableRow(r));
            Console.WriteLine(Playtester.TableRow(r));
        }
        var src = File.ReadAllText(path);
        int start = src.IndexOf("        private static readonly int[,] Rows =", StringComparison.Ordinal);
        int open = src.IndexOf('{', start), close = src.IndexOf("        };", open, StringComparison.Ordinal);
        src = src.Substring(0, open + 1) + "\n" + rows + src.Substring(close);
        File.WriteAllText(path, src.Replace("\r\n", "\n"));
        Console.WriteLine($"Mean simulated first-try win rate {results.Average(r => r.WinRate):P0}; wrote {path}");
        return 0;
    }
}
