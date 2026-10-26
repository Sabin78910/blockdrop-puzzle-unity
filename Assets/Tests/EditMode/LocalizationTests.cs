using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BlockDrop.Core;
using NUnit.Framework;

public class LocalizationTests
{
    // Not user-facing: brand words, object names, IDs, preference keys, log prefixes.
    private static readonly Regex NotUi = new Regex(
        @"^([0-9A-Fa-f]{6}|[a-z_]+|[a-z]+[A-Z][A-Za-z]*|[A-Z][a-z]+[A-Z][A-Za-z]*|.*Loc\..*|.*_versus.*|\).*|Main Camera|Map/|Background|Board|Panel|Rim|MenuDeco|MiniBoard|MiniPanel|BlockDrop|World|Classic|Daily|Levels|BLOCK|DROP|K7Q2MX|SwiftFox#1234|\(prefers.*|\^.*|.*: |.*://.*)$", RegexOptions.Singleline);

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
            .Concat(LevelLibrary.Chapters.SelectMany(c => new[] { c.Name, c.Intro }))
            .Concat(PlayerMeta.Themes.SelectMany(t => new[] { t.Name, t.Tagline }).Where(t => t != ""))
            .Concat(Store.Products.SelectMany(p => new[] { p.Title, p.Badge }).Where(t => t != ""))
            .Concat(new[] { "{0} theme", "no ads", "{0} hammers", "{0} shuffles" })
            .Concat(UiLiterals("Assets/Scripts/Core/Levels.cs").Where(t => t.StartsWith("So close")))
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
            Assert.AreEqual(Lang.Ne, Loc.FromCode("ne-NP"));
            Assert.AreEqual(Lang.En, Loc.FromCode("fr"));
            Loc.Current = Lang.Pt;
            Assert.AreEqual("JOGAR", Loc.L("PLAY"));
            Assert.AreEqual("RECORDE 120", Loc.F("BEST {0}", 120));
            Assert.AreEqual("Some untranslated text", Loc.L("Some untranslated text"));
            Loc.Current = Lang.Es;
            Assert.AreEqual("Borra 10 líneas", new Mission { Kind = MissionKind.ClearLines, Target = 10 }.Text);
        }
        finally { Loc.Current = before; }
    }

    [Test] public void NepaliCoversEveryTranslatedString()
    {
        var missing = new List<string>();
        foreach (var k in Loc.Table.Keys) if (!Nepali.Table.ContainsKey(k)) missing.Add(k);
        foreach (var k in Coach.Table.Keys) if (!Nepali.Table.ContainsKey(k)) missing.Add(k);
        Assert.IsEmpty(missing, string.Join(" | ", missing));
        foreach (var kv in Nepali.Table)
            for (int i = 0; i < 4; i++)
                Assert.AreEqual(kv.Key.Contains("{" + i + "}"), kv.Value.Contains("{" + i + "}"), kv.Key);
    }

    [Test] public void NepaliSwitchesText()
    {
        var before = Loc.Current;
        try
        {
            Loc.Current = Lang.Ne;
            Assert.AreEqual("खेल्नुहोस्", Loc.L("PLAY"));
            Assert.AreEqual("Some untranslated text", Loc.L("Some untranslated text"));
            Assert.AreEqual("ne", Loc.Code(Lang.Ne));
            Assert.AreEqual(Enum.GetValues(typeof(Lang)).Length, Loc.Names.Length);
        }
        finally { Loc.Current = before; }
    }
}
