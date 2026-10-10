using System.Globalization;
using LegalAgent.Downloads;
using LegalAgent.Downloads.Model;
using LegalAgent.Faq.Conversion;
using LegalAgent.Faq.Conversion.Model;
using LegalAgent.PdfParser;
using Microsoft.Extensions.DependencyInjection;

namespace MBank.FaqGenerator;

/// <summary>Entry point of mBank.FaqGenerator — a thin layer over LegalAgent.Downloads (contracts/cli.md).</summary>
public static class Program
{
    /// <summary>Number of regulations the application downloads.</summary>
    internal const int RequiredCount = 5;

    /// <summary>Process entry point.</summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Exit code.</returns>
    public static async Task<int> Main(string[] args)
    {
        // Polish messages and pasted addresses: the Windows console defaults to an OEM code page.
        Console.OutputEncoding = new System.Text.UTF8Encoding(false);
        Console.InputEncoding = new System.Text.UTF8Encoding(false);

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellation.Cancel();
        };

        var environment = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            environment[(string)entry.Key] = entry.Value as string;
        }

        return await RunAsync(args, Console.In, Console.Out, Console.Error, environment, AppContext.BaseDirectory, null, cancellation.Token)
            .ConfigureAwait(false);
    }

    /// <summary>Runs the application without touching the console, the process environment or the network.</summary>
    /// <param name="args">Command-line arguments.</param>
    /// <param name="stdin">Standard input (address prompts).</param>
    /// <param name="stdout">Standard output.</param>
    /// <param name="stderr">Standard error.</param>
    /// <param name="environment">Environment variables (<c>FAQGEN__…</c>).</param>
    /// <param name="configDirectory">Directory with <c>appsettings*.json</c>.</param>
    /// <param name="host">External dependencies (tests); <c>null</c> uses the network, the console and the system clock.</param>
    /// <param name="cancellationToken">Cancellation (Ctrl+C).</param>
    /// <returns>Exit code (contracts/cli.md).</returns>
    public static async Task<int> RunAsync(
        string[] args,
        TextReader stdin,
        TextWriter stdout,
        TextWriter stderr,
        IReadOnlyDictionary<string, string?> environment,
        string configDirectory,
        AppHost? host = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdin);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(configDirectory);
        host ??= new AppHost();

        AppArguments arguments = AppArguments.Parse(args, RequiredCount);
        if (arguments.Error is not null)
        {
            await stderr.WriteLineAsync($"Błąd: {arguments.Error}").ConfigureAwait(false);
            await stderr.WriteLineAsync().ConfigureAwait(false);
            await stderr.WriteLineAsync(AppArguments.Usage).ConfigureAwait(false);
            return 2;
        }

        if (arguments.Help)
        {
            await stdout.WriteLineAsync(AppArguments.Usage).ConfigureAwait(false);
            return 0;
        }

        if (arguments.Version)
        {
            await stdout.WriteLineAsync($"mBank.FaqGenerator {typeof(Program).Assembly.GetName().Version?.ToString(3)}").ConfigureAwait(false);
            return 0;
        }

        try
        {
            return await RunCoreAsync(arguments, stdin, stdout, stderr, environment, configDirectory, host, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await stderr.WriteLineAsync("Przerwano.").ConfigureAwait(false);
            return 130;
        }
        catch (DownloadDirectoryException e)
        {
            await stderr.WriteLineAsync($"Błąd: {e.Message}").ConfigureAwait(false);
            return 4;
        }
        catch (ConfigurationException e)
        {
            await stderr.WriteLineAsync($"Błąd konfiguracji: {e.Message}").ConfigureAwait(false);
            return 2;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            await stderr.WriteLineAsync($"Błąd nieoczekiwany: {e.Message}").ConfigureAwait(false);
            return 1;
        }
    }

    private static async Task<int> RunCoreAsync(
        AppArguments arguments,
        TextReader stdin,
        TextWriter stdout,
        TextWriter stderr,
        IReadOnlyDictionary<string, string?> environment,
        string configDirectory,
        AppHost host,
        CancellationToken cancellationToken)
    {
        AppConfiguration configuration = AppSettings.LoadAll(configDirectory, environment);
        AppSettings.Validate(configuration);
        DownloadSettings settings = configuration.Download;
        DownloadOptions options = settings.ToOptions() with { OutputDirectory = arguments.Output ?? settings.OutputDirectory };
        using HttpClient httpClient = CreateHttpClient(host.DownloadHandler);
        DocumentDownloader downloader;
        try
        {
            downloader = new DocumentDownloader(httpClient, options);
        }
        catch (ArgumentException e)
        {
            throw new ConfigurationException(e.Message, e);
        }

        IReadOnlyList<Uri>? addresses;
        if (arguments.Urls.Count > 0 || settings.Urls.Length > 0)
        {
            // Arguments win over configuration; neither source asks the user (FR-303).
            addresses = await CheckListAsync(arguments.Urls.Count > 0 ? arguments.Urls : settings.Urls, options, stderr).ConfigureAwait(false);
        }
        else
        {
            addresses = await AddressPrompt.AskAsync(stdin, stdout, options, RequiredCount, cancellationToken).ConfigureAwait(false);
            if (addresses is null)
            {
                await stderr.WriteLineAsync(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Brak adresów: wejście zostało zamknięte. Podaj {RequiredCount} adresów opcją --url albo w konfiguracji (Download:Urls).")).ConfigureAwait(false);
            }
        }

        if (addresses is null)
        {
            return 2;
        }

        var report = new ConsoleReport(stdout, stderr, RequiredCount);
        DownloadRun run = await downloader.DownloadAllAsync(addresses, report, cancellationToken).ConfigureAwait(false);
        string downloads = Path.GetFullPath(options.OutputDirectory);
        report.Summary(run, downloads);
        if (!run.AllSucceeded)
        {
            return 3;
        }

        ConversionRun conversion = await ConvertAsync(run, downloads, stdout, stderr, cancellationToken).ConfigureAwait(false);
        if (!conversion.AllSucceeded)
        {
            return 5;
        }

        string faqDirectory = Path.GetFullPath(arguments.FaqOutput ?? configuration.Faq.OutputDirectory);
        return await FaqStage.RunAsync(
            conversion.Documents,
            configuration,
            faqDirectory,
            host.KeyInput ?? new ConsoleKeyInput(stdin),
            stdout,
            stderr,
            host,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Converts the downloaded files with the parser's default options (as <c>legalagent-pdf convert</c>).</summary>
    private static async Task<ConversionRun> ConvertAsync(
        DownloadRun run,
        string downloads,
        TextWriter stdout,
        TextWriter stderr,
        CancellationToken cancellationToken)
    {
        using ServiceProvider services = new ServiceCollection().AddLegalAgentPdfParser().BuildServiceProvider();
        var converter = new DocumentSetConverter(services.GetRequiredService<IPdfMarkdownConverter>());
        PdfSource[] sources = [.. run.Results.Select(r => new PdfSource(r.Index, r.Address, Path.Combine(downloads, r.FileName)))];
        var report = new ConversionConsoleReport(stdout, stderr, RequiredCount);
        report.Start();
        ConversionRun conversion = await converter.ConvertAllAsync(sources, report, cancellationToken).ConfigureAwait(false);
        report.Summary(conversion);
        return conversion;
    }

    /// <summary>Checks a list from the arguments or the configuration; reports the first wrong position.</summary>
    private static async Task<IReadOnlyList<Uri>?> CheckListAsync(IReadOnlyList<string> urls, DownloadOptions options, TextWriter stderr)
    {
        if (urls.Count != RequiredCount)
        {
            await stderr.WriteLineAsync(string.Create(
                CultureInfo.InvariantCulture,
                $"Błąd: lista adresów w konfiguracji (Download:Urls) musi mieć {RequiredCount} pozycji, ma {urls.Count}.")).ConfigureAwait(false);
            return null;
        }

        IReadOnlyList<AddressCheck> checks = AddressValidator.CheckAll(urls, options);
        for (int i = 0; i < checks.Count; i++)
        {
            if (checks[i].Address is null)
            {
                await stderr.WriteLineAsync(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Błąd: Adres {i + 1}: {checks[i].Message}")).ConfigureAwait(false);
                return null;
            }
        }

        return [.. checks.Select(c => c.Address!)];
    }

    private static HttpClient CreateHttpClient(HttpMessageHandler? handler)
    {
        HttpMessageHandler effective = handler ?? new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(15),
        };

        return new HttpClient(effective, disposeHandler: handler is null) { Timeout = Timeout.InfiniteTimeSpan };
    }
}
