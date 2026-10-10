using System.Text;
using LegalAgent.Faq;
using LegalAgent.Faq.Conversion.Model;
using LegalAgent.Faq.Model;
using Microsoft.SemanticKernel.ChatCompletion;

namespace MBank.FaqGenerator;

/// <summary>The FAQ stage: size check → key → generation → rendering → <c>FAQ_mBank.md</c> (contracts/cli.md).</summary>
internal static class FaqStage
{
    /// <summary>Name of the FAQ file.</summary>
    public const string FileName = "FAQ_mBank.md";

    /// <summary>Title in the front matter.</summary>
    public const string Title = "FAQ — regulaminy mBanku";

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Runs the stage for converted documents.</summary>
    /// <returns>Exit code.</returns>
    public static async Task<int> RunAsync(
        IReadOnlyList<ConvertedDocument> documents,
        AppConfiguration configuration,
        string faqDirectory,
        IKeyInput keys,
        TextWriter stdout,
        TextWriter stderr,
        AppHost host,
        CancellationToken cancellationToken)
    {
        AzureOpenAiSettings azure = configuration.AzureOpenAI;
        var options = new FaqGeneratorOptions
        {
            CandidatesPerDocument = configuration.Faq.CandidatesPerDocument,
            MaxDocumentTokens = configuration.Faq.MaxDocumentTokens,
        };
        FaqDocumentInput[] inputs =
        [
            .. documents.Select(d => new FaqDocumentInput(
                string.IsNullOrWhiteSpace(d.Title) ? d.MarkdownFileName : d.Title,
                d.Address,
                d.Markdown,
                d.Units)),
        ];
        LegalAgent.Faq.FaqGenerator.CheckInput(inputs, options);

        stdout.WriteLine();
        string? key = KeyPrompt.Read(keys, stdout);
        if (key is null)
        {
            return 2;
        }

        IChatCompletionService chat = host.ChatFactory?.Invoke(key) ?? ChatServiceFactory.Create(azure, key, host.ModelHandler);
        var generator = new LegalAgent.Faq.FaqGenerator(chat, options, (step, schema) => ChatServiceFactory.ExecutionSettings(azure, step, schema));
        var report = new FaqConsoleReport(
            stdout,
            documents.Select((d, i) => (Id: $"D{i + 1}", d.MarkdownFileName)).ToDictionary(p => p.Id, p => p.MarkdownFileName, StringComparer.Ordinal));
        report.Start(azure.Model, azure.Deployment);
        FaqResult result = await generator.GenerateAsync(inputs, report, cancellationToken).ConfigureAwait(false);

        string text = FaqMarkdownRenderer.Render(
            result,
            new FaqFileHeader(Title, Description(result), host.TimeProvider.GetUtcNow(), azure.Model, azure.Deployment));
        string path = Path.Combine(faqDirectory, FileName);
        try
        {
            await WriteAtomicAsync(faqDirectory, path, text, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            await stderr.WriteLineAsync($"Nie można zapisać {FileName} w {faqDirectory}: {e.Message}").ConfigureAwait(false);
            return 4;
        }

        report.Saved(path, result);
        return 0;
    }

    /// <summary>Writes <c>FAQ_mBank.md.tmp</c> and moves it over the file; the previous file stays unless the move succeeds.</summary>
    private static async Task WriteAtomicAsync(string directory, string path, string text, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(directory);
        string temporary = path + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, text, Utf8NoBom, cancellationToken).ConfigureAwait(false);
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

    private static string Description(FaqResult result) =>
        $"{PolishText.Count(result.Items.Count, "najważniejsze pytanie", "najważniejsze pytania", "najważniejszych pytań")} i odpowiedzi "
        + $"na podstawie {PolishText.Count(result.Documents.Count, "regulaminu", "regulaminów", "regulaminów")} mBanku.";
}
