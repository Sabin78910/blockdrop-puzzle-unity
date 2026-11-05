using System.IO;
using NUnit.Framework;

/// <summary>Unity runs tests from the project root and some tests read Assets/ by relative path,
/// so start from the same place.</summary>
[SetUpFixture]
public class ProjectRoot
{
    [OneTimeSetUp]
    public void UseProjectRoot()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets"))) dir = dir.Parent;
        if (dir != null) Directory.SetCurrentDirectory(dir.FullName);
    }
}
