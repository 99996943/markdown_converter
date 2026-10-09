using System.Globalization;
using LegalAgent.Corpus.Content;

namespace LegalAgent.Corpus.Tests.Unit.Content;

/// <summary>Helpers over the minimal valid content set in <c>TestData/zrodla-mini</c>.</summary>
internal static class MiniContent
{
    /// <summary>Directory of the pristine mini set.</summary>
    public static string Path => System.IO.Path.Combine(AppContext.BaseDirectory, "TestData", "zrodla-mini");

    /// <summary>Loads the pristine mini set.</summary>
    public static ContentLibrary Load() => ContentLoader.Load(Path);

    /// <summary>Copies the mini set to a fresh temp directory, lets <paramref name="modify"/> change it, and loads it.</summary>
    public static ContentLibrary LoadModified(Action<string> modify)
    {
        var dir = Copy();
        try
        {
            modify(dir);
            return ContentLoader.Load(dir);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>Replaces <paramref name="oldText"/> with <paramref name="newText"/> in a relative file of <paramref name="dir"/>.</summary>
    public static void Replace(string dir, string relativeFile, string oldText, string newText)
    {
        var path = System.IO.Path.Combine(dir, relativeFile);
        var text = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
        if (!text.Contains(oldText, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"'{oldText}' not found in {relativeFile}.");
        }

        File.WriteAllText(path, text.Replace(oldText, newText, StringComparison.Ordinal));
    }

    /// <summary>Copies the mini set to a new temp directory.</summary>
    public static string Copy()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mini-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        foreach (var file in Directory.GetFiles(Path, "*", SearchOption.AllDirectories))
        {
            var target = System.IO.Path.Combine(dir, System.IO.Path.GetRelativePath(Path, file));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }

        return dir;
    }
}
