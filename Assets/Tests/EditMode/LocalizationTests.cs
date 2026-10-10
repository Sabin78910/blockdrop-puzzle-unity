using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BlockDrop.Core;
using NUnit.Framework;

public class LocalizationTests
{
    // Not user-facing: brand words, object names, IDs, preference keys, log prefixes.
    private static readonly Regex NotUi = new Regex(
        @"^([0-9A-Fa-f]{6}|[a-z_]+|[a-z]+[A-Z][A-Za-z]*|[A-Z][a-z]+[A-Z][A-Za-z]*|.*Loc\..*|.*_versus.*|\).*|Main Camera|Background|Board|Panel|Rim|MenuDeco|MiniBoard|MiniPanel|BlockDrop|World|Classic|Daily|BLOCK|DROP|K7Q2MX|SwiftFox#1234|\(prefers.*|\^.*|.*: |.*://.*)$", RegexOptions.Singleline);

    private static string[] UiLiterals(params string[] files)
    {
        var all = files.Select(File.ReadAllText)
            .Select(s => Regex.Replace(s, @"^\s*//.*$", "", RegexOptions.Multiline))   // comments are not UI
            .Select(s => Regex.Replace(s, @"\$""(?:[^""\\]|\\.)*""", ""));
        return all.SelectMany(s => Regex.Matches(s, @"""((?:[^""\\]|\\.)*)""").Cast<Match>().Select(m => m.Groups[1].Value.Replace("\\n", "\n")))
                  .Where(t => Regex.IsMatch(t, "[A-Za-z]{2,}") && !NotUi.IsMatch(t)).Distinct().ToArray();
    }

    [Test] public void EveryOnScreenTextHasSpanishAndPortuguese()
    {
        var missing = UiLiterals("Assets/Scripts/Game/GameController.cs", "Assets/Scripts/Game/OnlineService.cs")
            .Concat(PlayerMeta.Achievements.SelectMany(a => new[] { a.Name, a.Text }))
            .Where(t => !Loc.Table.ContainsKey(t)).ToArray();
        Assert.IsEmpty(missing, "Add to Loc.Table: " + string.Join(" | ", missing));
    }

    [Test] public void TranslationsAreCompleteAndKeepPlaceholders()
    {
        foreach (var kv in Loc.Table)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(kv.Value.es) || string.IsNullOrWhiteSpace(kv.Value.pt), kv.Key);
            int n = Regex.Matches(kv.Key, @"\{\d\}").Count;
            Assert.AreEqual(n, Regex.Matches(kv.Value.es, @"\{\d\}").Count, "es " + kv.Key);
            Assert.AreEqual(n, Regex.Matches(kv.Value.pt, @"\{\d\}").Count, "pt " + kv.Key);
        }
    }

    [Test] public void LanguageSwitchAndFallback()
    {
        var before = Loc.Current;
        try
        {
            Assert.AreEqual(Lang.Es, Loc.FromCode("es-MX"));
            Assert.AreEqual(Lang.Pt, Loc.FromCode("pt_BR"));
            Assert.AreEqual(Lang.En, Loc.FromCode("ne"));
            Loc.Current = Lang.Pt;
            Assert.AreEqual("JOGAR", Loc.L("PLAY"));
            Assert.AreEqual("RECORDE 120", Loc.F("BEST {0}", 120));
            Assert.AreEqual("Some untranslated text", Loc.L("Some untranslated text"));
            Loc.Current = Lang.Es;
            Assert.AreEqual("Borra 10 líneas", new Mission { Kind = MissionKind.ClearLines, Target = 10 }.Text);
        }
        finally { Loc.Current = before; }
    }
}
