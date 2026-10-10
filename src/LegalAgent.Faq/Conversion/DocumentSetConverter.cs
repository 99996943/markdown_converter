using System.Text;
using LegalAgent.Faq.Conversion.Model;
using LegalAgent.PdfParser;
using LegalAgent.PdfParser.Model;

namespace LegalAgent.Faq.Conversion;

/// <summary>
/// Converts a set of PDF files to Markdown written next to them (research R9): files in index order, atomic writes,
/// a failed file does not stop the others, stale <c>*.md</c> files are removed only when all succeeded.
/// </summary>
public sealed class DocumentSetConverter
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly IPdfMarkdownConverter converter;

    /// <summary>Creates the converter.</summary>
    /// <param name="converter">The PDF parser.</param>
    public DocumentSetConverter(IPdfMarkdownConverter converter)
    {
        ArgumentNullException.ThrowIfNull(converter);
        this.converter = converter;
    }

    /// <summary>Converts the files in turn and writes <c>&lt;name&gt;.md</c> next to each PDF.</summary>
    /// <param name="sources">Files to convert.</param>
    /// <param name="progress">Receives progress events.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Converted documents, failures and removed files.</returns>
    /// <exception cref="IOException">Writing or removing a file failed.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<ConversionRun> ConvertAllAsync(
        IReadOnlyList<PdfSource> sources,
        IProgress<ConversionEvent>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var documents = new List<ConvertedDocument>();
        var failures = new List<ConversionFailure>();
        foreach (PdfSource source in sources.OrderBy(s => s.Index))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string pdfName = Path.GetFileName(source.PdfPath);
            progress?.Report(new ConversionEvent(source.Index, pdfName, ConversionEventKind.Started, null, null));

            PdfConversionResult result;
            await using (FileStream pdf = File.OpenRead(source.PdfPath))
            {
                result = await converter.ConvertAsync(pdf, new PdfConversionRequest { SourceId = pdfName }, cancellationToken)
                    .ConfigureAwait(false);
            }

            string markdownName = Path.GetFileNameWithoutExtension(pdfName) + ".md";
            string markdownPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(source.PdfPath))!, markdownName);
            await WriteAtomicAsync(markdownPath, result.Markdown, cancellationToken).ConfigureAwait(false);

            var document = new ConvertedDocument(
                source.Index,
                source.Address,
                pdfName,
                markdownName,
                result.Document.Title,
                result.Markdown,
                UnitExtractor.FromDocument(result.Document),
                result.Report.PageCount,
                result.Report.Warnings);
            documents.Add(document);
            progress?.Report(new ConversionEvent(source.Index, pdfName, ConversionEventKind.Converted, document, null));
        }

        IReadOnlyList<string> removed = failures.Count == 0 ? RemoveStaleMarkdown(sources, documents) : [];
        return new ConversionRun(documents, failures, removed);
    }

    /// <summary>Removes <c>*.md</c> files of the PDF directories (not subdirectories) that are not part of the set (FR-403).</summary>
    private static List<string> RemoveStaleMarkdown(IReadOnlyList<PdfSource> sources, List<ConvertedDocument> documents)
    {
        var current = new HashSet<string>(documents.Select(d => d.MarkdownFileName), StringComparer.OrdinalIgnoreCase);
        var removed = new List<string>();
        foreach (string directory in sources.Select(s => Path.GetDirectoryName(Path.GetFullPath(s.PdfPath))!).Distinct(StringComparer.Ordinal))
        {
            foreach (string file in Directory.EnumerateFiles(directory, "*.md").Order(StringComparer.Ordinal))
            {
                string name = Path.GetFileName(file);
                if (!current.Contains(name) && string.Equals(Path.GetExtension(name), ".md", StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(file);
                    removed.Add(name);
                }
            }
        }

        return removed;
    }

    /// <summary>Writes <c>&lt;path&gt;.tmp</c> and moves it over the target; the temporary file never stays behind.</summary>
    private static async Task WriteAtomicAsync(string path, string content, CancellationToken cancellationToken)
    {
        string temporary = path + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, content, Utf8NoBom, cancellationToken).ConfigureAwait(false);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
