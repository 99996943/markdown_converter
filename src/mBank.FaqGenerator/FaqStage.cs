using System.Globalization;
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
        try
        {
            LegalAgent.Faq.FaqGenerator.CheckInput(inputs, options);
        }
        catch (FaqInputTooLongException e)
        {
            await stderr.WriteLineAsync(TooLong(e, documents, inputs)).ConfigureAwait(false);
            return 6;
        }

        stdout.WriteLine();
        string? key = KeyPrompt.Read(keys, stdout);
        if (key is null)
        {
            await stderr.WriteLineAsync(
                "Brak klucza API: wejście jest przekierowane, ale nie zawiera klucza. Przekaż klucz potokiem jako kolejny wiersz, "
                + "np. `… keys list … -o tsv | mBank.FaqGenerator --url …`, albo uruchom w konsoli.").ConfigureAwait(false);
            return 2;
        }

        // From here on every message may echo the key (e.g. an excerpt of a service response): redact it (FR-411).
        try
        {
            return await GenerateAndWriteAsync(documents, inputs, options, azure, faqDirectory, key, stdout, stderr, host, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (FaqServiceException e)
        {
            await stderr.WriteLineAsync(SecretRedactor.Redact(ServiceMessage(e, azure), key)).ConfigureAwait(false);
            return 6;
        }
        catch (FaqInputTooLongException e)
        {
            await stderr.WriteLineAsync(SecretRedactor.Redact(TooLong(e, documents, inputs), key)).ConfigureAwait(false);
            return 6;
        }
        catch (FaqResponseException e)
        {
            await stderr.WriteLineAsync(SecretRedactor.Redact(e.Message, key)).ConfigureAwait(false);
            return 7;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            await stderr.WriteLineAsync(SecretRedactor.Redact($"Błąd nieoczekiwany: {e.Message}", key)).ConfigureAwait(false);
            return 1;
        }
    }

    /// <summary>The message of contracts/cli.md for a service failure.</summary>
    private static string ServiceMessage(FaqServiceException e, AzureOpenAiSettings azure)
    {
        string step = e.DocumentId is { } id ? $"krok kandydatów, {id}" : "krok wyboru";
        string status = e.StatusCode?.ToString(CultureInfo.InvariantCulture) ?? "?";
        return e.Kind switch
        {
            FaqServiceErrorKind.Authentication =>
                $"Usługa Azure OpenAI odrzuciła klucz ({status}): klucz jest nieprawidłowy lub nie ma dostępu do zasobu.",
            FaqServiceErrorKind.DeploymentNotFound => $"Nie znaleziono wdrożenia „{azure.Deployment}” w zasobie {azure.Endpoint} ({status}).",
            FaqServiceErrorKind.RateLimited =>
                $"Przekroczono limit zapytań wdrożenia ({status}) {(e.DocumentId is { } d ? "przy dokumencie " + d : "w kroku wyboru")}. "
                + "Spróbuj później lub zwiększ przepustowość wdrożenia.",
            FaqServiceErrorKind.ContentFiltered => $"Usługa zablokowała zapytanie filtrem treści ({e.DocumentId ?? "krok wyboru"}).",
            FaqServiceErrorKind.Timeout =>
                $"Brak odpowiedzi usługi w ciągu {azure.TimeoutSeconds.ToString("0.###", PolishText.Culture)} s ({step}).",
            FaqServiceErrorKind.Network => $"Błąd połączenia z usługą Azure OpenAI: {Cause(e).TrimEnd('.')}.",
            _ => e.Message,
        };
    }

    /// <summary>The message of the innermost transport exception, e.g. a name resolution failure.</summary>
    private static string Cause(Exception e)
    {
        string message = e.Message;
        for (Exception? inner = e.InnerException; inner is not null; inner = inner.InnerException)
        {
            if (inner is HttpRequestException or System.Net.Sockets.SocketException)
            {
                message = inner.Message;
            }
        }

        return message;
    }

    private static string TooLong(FaqInputTooLongException e, IReadOnlyList<ConvertedDocument> documents, FaqDocumentInput[] inputs)
    {
        int index = Array.FindIndex(inputs, i => string.Equals(i.Name, e.DocumentName, StringComparison.Ordinal));
        string name = index >= 0 ? documents[index].MarkdownFileName : e.DocumentName;
        return $"Dokument {name} jest za długi dla modelu: {PolishText.Count(e.Characters, "znak", "znaki", "znaków")} "
            + $"(~{PolishText.Number(e.EstimatedTokens)} tokenów), limit {PolishText.Number(e.Limit)} tokenów (Faq:MaxDocumentTokens).";
    }

    private static async Task<int> GenerateAndWriteAsync(
        IReadOnlyList<ConvertedDocument> documents,
        FaqDocumentInput[] inputs,
        FaqGeneratorOptions options,
        AzureOpenAiSettings azure,
        string faqDirectory,
        string key,
        TextWriter stdout,
        TextWriter stderr,
        AppHost host,
        CancellationToken cancellationToken)
    {
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
            await stderr.WriteLineAsync(SecretRedactor.Redact($"Nie można zapisać {FileName} w {faqDirectory}: {e.Message}", key))
                .ConfigureAwait(false);
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
