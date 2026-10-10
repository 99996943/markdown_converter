namespace LegalAgent.Faq.Tests.Fixtures;

/// <summary>A fresh directory under the system temp path, removed on dispose.</summary>
internal sealed class TestDirectory : IDisposable
{
    /// <summary>Creates the directory.</summary>
    public TestDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "faq-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    /// <summary>Full path of the directory.</summary>
    public string Path { get; }

    /// <summary>Path of a file in the directory.</summary>
    public string Combine(string name) => System.IO.Path.Combine(Path, name);

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
