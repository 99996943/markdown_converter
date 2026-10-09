namespace LegalAgent.Corpus.Manifest;

/// <summary>Writes and reads <c>manifest.json</c>.</summary>
public static class ManifestWriter
{
    /// <summary>Serialises the manifest with the default type order.</summary>
    /// <param name="manifest">Manifest.</param>
    /// <returns>JSON text.</returns>
    public static string Write(Manifest manifest) => throw new NotImplementedException();

    /// <summary>Serialises the manifest.</summary>
    /// <param name="manifest">Manifest.</param>
    /// <param name="typeOrder">Order of base types.</param>
    /// <returns>JSON text.</returns>
    public static string Write(Manifest manifest, IReadOnlyList<string> typeOrder) => throw new NotImplementedException();

    /// <summary>Parses manifest JSON.</summary>
    /// <param name="json">JSON text.</param>
    /// <returns>Manifest.</returns>
    public static Manifest Read(string json) => throw new NotImplementedException();
}
