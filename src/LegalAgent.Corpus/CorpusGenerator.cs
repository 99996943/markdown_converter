using LegalAgent.Corpus.Output;
using LegalAgent.Corpus.Planning;
using LegalAgent.PdfParser;

namespace LegalAgent.Corpus;

/// <summary>Settings of a generator run that are not part of the recorded run parameters.</summary>
public sealed class CorpusGeneratorOptions
{
    /// <summary>Directory against which relative paths of the run parameters are resolved (the repository root).</summary>
    public string BaseDirectory { get; init; } = Directory.GetCurrentDirectory();

    /// <summary>Converter used for Markdown; null = <see cref="PdfMarkdownConverter.CreateDefault"/> with the run's parser options.</summary>
    public IPdfMarkdownConverter? Converter { get; init; }

    /// <summary>Progress messages ("dokument N/M …").</summary>
    public IProgress<string>? Progress { get; init; }

    /// <summary>When set, the reference truth of every document is written there as JSON (diagnostics).</summary>
    public string? TruthDirectory { get; init; }
}

/// <summary>One generated document.</summary>
/// <param name="Plan">Its plan.</param>
/// <param name="Pdf">PDF bytes.</param>
/// <param name="Markdown">Markdown from the library.</param>
/// <param name="Entry">Its manifest entry.</param>
public sealed record GeneratedDocument(DocumentPlan Plan, byte[] Pdf, string Markdown, Manifest.ManifestDocument Entry);

/// <summary>Result of <see cref="CorpusGenerator.GenerateAsync"/>.</summary>
/// <param name="Documents">Generated documents in manifest order.</param>
/// <param name="Manifest">The manifest written.</param>
/// <param name="Deleted">Stale files removed from the managed directories.</param>
public sealed record GenerationResult(IReadOnlyList<GeneratedDocument> Documents, Manifest.Manifest Manifest, IReadOnlyList<string> Deleted);

/// <summary>A conversion failed or was incomplete (FR-160; CLI exit code 5).</summary>
public sealed class ConversionFailedException : Exception
{
    /// <summary>Creates the exception.</summary>
    public ConversionFailedException()
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    public ConversionFailedException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and an inner exception.</summary>
    public ConversionFailedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Generated text contains a forbidden name (FR-105; CLI exit code 4).</summary>
public sealed class ForbiddenNameException : Exception
{
    /// <summary>Creates the exception.</summary>
    public ForbiddenNameException()
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    public ForbiddenNameException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and an inner exception.</summary>
    public ForbiddenNameException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Facade of the corpus generator: <c>generate</c>, <c>verify</c> (contracts/cli.md).</summary>
public static class CorpusGenerator
{
    /// <summary>Plans, typesets, converts and writes the corpus; removes stale managed files after a successful run.</summary>
    public static Task<GenerationResult> GenerateAsync(RunParameters parameters, CorpusGeneratorOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    /// <summary>Rebuilds the corpus in memory and compares it with the files on disk; writes nothing.</summary>
    public static Task<CorpusDiff> VerifyAsync(RunParameters parameters, CorpusGeneratorOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
