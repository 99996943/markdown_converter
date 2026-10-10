using System.Text.Json;
using LegalAgent.Downloads.Tests.Fakes;

namespace MBank.FaqGenerator.Tests.Fakes;

/// <summary>Result of one application run.</summary>
/// <param name="Code">Exit code.</param>
/// <param name="Out">Standard output.</param>
/// <param name="Err">Standard error.</param>
internal sealed record AppRun(int Code, string Out, string Err);

/// <summary>
/// Runs <see cref="Program.RunAsync"/> against a temporary working directory with its own <c>appsettings.json</c>
/// (allowed host <c>example.test</c>, output in <c>downloads</c> under the temporary directory) and a fake HTTP handler.
/// </summary>
internal sealed class AppHarness : IDisposable
{
    private readonly TempDirectory root = new();

    /// <summary>Creates the harness with the default settings.</summary>
    public AppHarness()
    {
        WriteSettings(new Dictionary<string, object?>());
    }

    /// <summary>The fake HTTP handler.</summary>
    public FakeHttpHandler Http { get; } = new();

    /// <summary>Environment passed to the application.</summary>
    public Dictionary<string, string?> Environment { get; } = new(StringComparer.Ordinal);

    /// <summary>Directory with the test <c>appsettings.json</c>.</summary>
    public string ConfigDirectory => root.Combine("config");

    /// <summary>Output directory configured by default.</summary>
    public string OutputDirectory => root.Combine("downloads");

    /// <summary>The temporary root.</summary>
    public TempDirectory Root => root;

    /// <summary>Writes <c>appsettings.json</c> with the defaults overridden by <paramref name="overrides"/> (section Download).</summary>
    public void WriteSettings(IReadOnlyDictionary<string, object?> overrides)
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
        foreach ((string key, object? value) in overrides)
        {
            download[key] = value;
        }

        Directory.CreateDirectory(ConfigDirectory);
        File.WriteAllText(
            Path.Combine(ConfigDirectory, "appsettings.json"),
            JsonSerializer.Serialize(new Dictionary<string, object?> { ["Download"] = download }));
    }

    /// <summary>Runs the application.</summary>
    public async Task<AppRun> RunAsync(string[] args, string stdin = "", CancellationToken cancellationToken = default)
    {
        using var input = new StringReader(stdin);
        using var output = new StringWriter();
        using var error = new StringWriter();
        int code = await Program.RunAsync(args, input, output, error, Environment, ConfigDirectory, Http, cancellationToken);
        return new AppRun(code, output.ToString(), error.ToString());
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Http.Dispose();
        root.Dispose();
    }
}
