using System.Text.Json;
using LegalAgent.Downloads.Tests.Fakes;
using LegalAgent.Faq.Tests.Fakes;

namespace MBank.FaqGenerator.Tests.Fakes;

/// <summary>Result of one application run.</summary>
/// <param name="Code">Exit code.</param>
/// <param name="Out">Standard output.</param>
/// <param name="Err">Standard error.</param>
internal sealed record AppRun(int Code, string Out, string Err);

/// <summary>
/// Runs <see cref="Program.RunAsync"/> against a temporary working directory with its own <c>appsettings.json</c>
/// (allowed host <c>example.test</c>, output in <c>downloads</c> and <c>faq</c> under the temporary directory), a fake
/// HTTP handler, a fake model, a fake keyboard and a fixed clock.
/// </summary>
internal sealed class AppHarness : IDisposable
{
    /// <summary>The fixed moment of <see cref="Clock"/>.</summary>
    public static readonly DateTimeOffset Now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    private readonly TempDirectory root = new();

    /// <summary>Creates the harness with the default settings.</summary>
    public AppHarness()
    {
        WriteSettings(new Dictionary<string, object?>());
    }

    /// <summary>The fake HTTP handler for downloads.</summary>
    public FakeHttpHandler Http { get; } = new();

    /// <summary>The fake HTTP handler for the Azure OpenAI connector (used when <see cref="UseConnector"/> is set).</summary>
    public FakeHttpHandler ModelHttp { get; } = new();

    /// <summary>The fake model (used unless <see cref="UseConnector"/> is set).</summary>
    public FakeChatCompletionService Model { get; } = new();

    /// <summary>The fake keyboard; by default a console that types „test-key” and Enter.</summary>
    public FakeKeyInput Keys { get; } = new();

    /// <summary>The fixed clock.</summary>
    public FakeTimeProvider Clock { get; } = new(Now);

    /// <summary>Whether the real connector is used with <see cref="ModelHttp"/> instead of <see cref="Model"/>.</summary>
    public bool UseConnector { get; set; }

    /// <summary>Environment passed to the application.</summary>
    public Dictionary<string, string?> Environment { get; } = new(StringComparer.Ordinal);

    /// <summary>Directory with the test <c>appsettings.json</c>.</summary>
    public string ConfigDirectory => root.Combine("config");

    /// <summary>Output directory configured by default.</summary>
    public string OutputDirectory => root.Combine("downloads");

    /// <summary>FAQ directory configured by default.</summary>
    public string FaqDirectory => root.Combine("faq");

    /// <summary>The FAQ file in <see cref="FaqDirectory"/>.</summary>
    public string FaqFile => Path.Combine(FaqDirectory, "FAQ_mBank.md");

    /// <summary>Endpoint configured by default.</summary>
    public const string Endpoint = "https://faq.example.test/";

    /// <summary>The temporary root.</summary>
    public TempDirectory Root => root;

    /// <summary>Writes <c>appsettings.json</c> with the defaults overridden by <paramref name="overrides"/> (section Download).</summary>
    public void WriteSettings(
        IReadOnlyDictionary<string, object?> overrides,
        IReadOnlyDictionary<string, object?>? azure = null,
        IReadOnlyDictionary<string, object?>? faq = null)
    {
        var download = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["Urls"] = Array.Empty<string>(),
            ["AllowedHosts"] = new[] { "example.test" },
            ["AllowHttp"] = false,
            ["OutputDirectory"] = OutputDirectory,
            ["TimeoutSeconds"] = 10,
            ["MaxFileSizeMegabytes"] = 50,
            ["MaxRedirects"] = 5,
            ["UserAgent"] = "test-agent/1.0",
        };
        Merge(download, overrides);

        var azureSection = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["Endpoint"] = Endpoint,
            ["Deployment"] = "gpt-4o-mini",
            ["Model"] = "gpt-4o-mini",
            ["TimeoutSeconds"] = 30,
            ["Temperature"] = 0,
            ["Seed"] = 42,
            ["MaxOutputTokens"] = 4096,
        };
        Merge(azureSection, azure);

        var faqSection = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["OutputDirectory"] = FaqDirectory,
            ["CandidatesPerDocument"] = 10,
            ["MaxDocumentTokens"] = 100000,
        };
        Merge(faqSection, faq);

        Directory.CreateDirectory(ConfigDirectory);
        File.WriteAllText(
            Path.Combine(ConfigDirectory, "appsettings.json"),
            JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["Download"] = download,
                ["AzureOpenAI"] = azureSection,
                ["Faq"] = faqSection,
            }));
    }

    /// <summary>Runs the application.</summary>
    public async Task<AppRun> RunAsync(string[] args, string stdin = "", CancellationToken cancellationToken = default)
    {
        using var input = new StringReader(stdin);
        using var output = new StringWriter();
        using var error = new StringWriter();
        Keys.Reader = input;
        var host = new AppHost
        {
            DownloadHandler = Http,
            ModelHandler = ModelHttp,
            ChatFactory = UseConnector ? null : key =>
            {
                Model.ReceivedKey = key;
                return Model;
            },
            KeyInput = Keys,
            TimeProvider = Clock,
        };
        int code = await Program.RunAsync(args, input, output, error, Environment, ConfigDirectory, host, cancellationToken);
        return new AppRun(code, output.ToString(), error.ToString());
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Http.Dispose();
        ModelHttp.Dispose();
        root.Dispose();
    }

    private static void Merge(Dictionary<string, object?> target, IReadOnlyDictionary<string, object?>? overrides)
    {
        foreach ((string key, object? value) in overrides ?? new Dictionary<string, object?>())
        {
            target[key] = value;
        }
    }
}

/// <summary>A clock that always returns the same moment.</summary>
internal sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
{
    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => now;
}

/// <summary>
/// Scripted keyboard: keys typed in the console (<see cref="IsInputRedirected"/> = false) or lines of the redirected input
/// (read from the same reader as the addresses). Records every read and every Ctrl+C capture.
/// </summary>
internal sealed class FakeKeyInput : IKeyInput
{
    private readonly Queue<ConsoleKeyInfo> keys = new();
    private bool scripted;

    /// <inheritdoc />
    public bool IsInputRedirected { get; set; }

    /// <summary>The standard input of the current run; set by <see cref="AppHarness"/>.</summary>
    public TextReader? Reader { get; set; }

    /// <summary>Number of <see cref="ReadKey"/> and <see cref="ReadLine"/> calls.</summary>
    public int Reads { get; private set; }

    /// <summary>Number of <see cref="CaptureControlC"/> calls.</summary>
    public int Captures { get; private set; }

    /// <summary>Number of released captures.</summary>
    public int Releases { get; private set; }

    /// <summary>Whether a capture is active.</summary>
    public bool Capturing => Captures > Releases;

    /// <summary>Queues the characters of <paramref name="text"/> as key presses.</summary>
    public FakeKeyInput Type(string text)
    {
        foreach (char c in text)
        {
            keys.Enqueue(new ConsoleKeyInfo(c, ConsoleKey.NoName, false, false, false));
        }

        scripted = true;
        return this;
    }

    /// <summary>Queues a special key.</summary>
    public FakeKeyInput Press(ConsoleKey key, char c = '\0', bool control = false)
    {
        keys.Enqueue(new ConsoleKeyInfo(c, key, false, false, control));
        scripted = true;
        return this;
    }

    /// <summary>Queues Enter.</summary>
    public FakeKeyInput Enter() => Press(ConsoleKey.Enter, '\r');

    /// <inheritdoc />
    public ConsoleKeyInfo ReadKey()
    {
        Reads++;
        if (!scripted)
        {
            Type("test-key").Enter();
        }

        return keys.Count > 0
            ? keys.Dequeue()
            : throw new InvalidOperationException("Atrapa klawiatury: brak zaprogramowanych klawiszy.");
    }

    /// <inheritdoc />
    public string? ReadLine()
    {
        Reads++;
        return Reader?.ReadLine();
    }

    /// <inheritdoc />
    public IDisposable CaptureControlC()
    {
        Captures++;
        return new Release(this);
    }

    private sealed class Release(FakeKeyInput owner) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                owner.Releases++;
            }
        }
    }
}
