using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace MBank.FaqGenerator.Tests;

public sealed class AzureScriptTests : IDisposable
{
    private const string ScriptPath = "scripts/azure/create-openai.sh";

    private static readonly string? Bash = FindBash();

    private readonly string workDir = Path.Combine(Path.GetTempPath(), "azscript-" + Guid.NewGuid().ToString("N"));
    private readonly string fakeBin;
    private readonly string logFile;
    private readonly string stateDir;

    public AzureScriptTests()
    {
        fakeBin = Path.Combine(workDir, "bin");
        stateDir = Path.Combine(workDir, "state");
        logFile = Path.Combine(workDir, "az.log");
        Directory.CreateDirectory(fakeBin);
        Directory.CreateDirectory(stateDir);
        string az = Path.Combine(fakeBin, "az");
        File.WriteAllText(az, FakeAz.Replace("\r\n", "\n", StringComparison.Ordinal));
        MakeExecutable(az);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(workDir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public async Task NotLoggedIn_ExitCode3_WithAzLoginHint()
    {
        Assert.SkipUnless(Bash is not null, "bash not available");

        ScriptRun run = await RunAsync([], ("FAKE_AZ_LOGGED_OUT", "1"));

        Assert.Equal(3, run.Code);
        Assert.Contains("az login", run.Err, StringComparison.Ordinal);
        Assert.DoesNotContain("create", ReadLog(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AzMissingOnPath_ExitCode3()
    {
        Assert.SkipUnless(Bash is not null, "bash not available");

        string toolsDir = Path.Combine(workDir, "tools");
        Directory.CreateDirectory(toolsDir);
        foreach (string tool in new[] { "sha256sum", "shasum", "cut", "grep", "cat", "sed", "tr", "head", "dirname", "basename", "uname" })
        {
            string? real = ResolveTool(tool);
            if (real is null)
            {
                continue;
            }

            string wrapper = Path.Combine(toolsDir, tool);
            File.WriteAllText(wrapper, $"#!/bin/sh\nexec '{real}' \"$@\"\n");
            MakeExecutable(wrapper);
        }

        ScriptRun run = await RunAsync([], pathOverride: toolsDir);

        Assert.Equal(3, run.Code);
        Assert.Contains("Brak Azure CLI", run.Err, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ModelUnavailable_ExitCode4_WithHint_AndNoCreate()
    {
        Assert.SkipUnless(Bash is not null, "bash not available");

        ScriptRun run = await RunAsync([], ("FAKE_AZ_MODEL_MISSING", "1"));

        Assert.Equal(4, run.Code);
        Assert.Contains("--model gpt-5.4-mini --model-version 2026-03-17", run.Err, StringComparison.Ordinal);
        Assert.DoesNotContain("create", ReadLog(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task FirstRun_CreatesResources_AndPrintsConfiguration()
    {
        Assert.SkipUnless(Bash is not null, "bash not available");

        ScriptRun run = await RunAsync([]);

        Assert.Equal(0, run.Code);
        string log = ReadLog();
        Assert.Contains("group create", log, StringComparison.Ordinal);
        Assert.Matches(
            new Regex(@"account create .*--kind OpenAI --sku S0 --custom-domain faqgen-[0-9a-f]{8}\b", RegexOptions.None, TimeSpan.FromSeconds(5)),
            log);
        Assert.Contains("deployment create", log, StringComparison.Ordinal);
        Assert.Contains("--model-name gpt-4o-mini", log, StringComparison.Ordinal);
        Assert.Contains("--model-version 2024-07-18", log, StringComparison.Ordinal);
        Assert.Contains("--sku-name GlobalStandard", log, StringComparison.Ordinal);
        Assert.Contains("--sku-capacity 200", log, StringComparison.Ordinal);
        Assert.Contains("appsettings.Local.json", run.Out, StringComparison.Ordinal);
        Assert.Contains("\"AzureOpenAI\"", run.Out, StringComparison.Ordinal);
        Assert.Contains("FAQGEN__AzureOpenAI__Endpoint=https://faqgen-test.openai.azure.com/", run.Out, StringComparison.Ordinal);
        Assert.Contains("FAQGEN__AzureOpenAI__Deployment=gpt-4o-mini", run.Out, StringComparison.Ordinal);
        Assert.Contains("az group delete --name rg-faqgen --yes", run.Out, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SecondRun_ResourcesExist_ExitCode0_WithoutAccountOrDeploymentCreate()
    {
        Assert.SkipUnless(Bash is not null, "bash not available");

        ScriptRun first = await RunAsync([]);
        Assert.Equal(0, first.Code);
        File.Delete(logFile);

        ScriptRun second = await RunAsync([]);

        Assert.Equal(0, second.Code);
        string log = ReadLog();
        Assert.DoesNotContain("account create", log, StringComparison.Ordinal);
        Assert.DoesNotContain("deployment create", log, StringComparison.Ordinal);
        Assert.Contains("az group delete --name rg-faqgen --yes", second.Out, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Log_NeverContainsKeys()
    {
        Assert.SkipUnless(Bash is not null, "bash not available");

        ScriptRun first = await RunAsync([]);
        ScriptRun second = await RunAsync([]);

        Assert.Equal(0, first.Code);
        Assert.Equal(0, second.Code);
        Assert.DoesNotContain("keys", ReadLog(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownParameter_ExitCode2()
    {
        Assert.SkipUnless(Bash is not null, "bash not available");

        ScriptRun run = await RunAsync(["--bogus"]);

        Assert.Equal(2, run.Code);
        Assert.Contains("--bogus", run.Err, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AccountCreateFails_ExitCode5()
    {
        Assert.SkipUnless(Bash is not null, "bash not available");

        ScriptRun run = await RunAsync([], ("FAKE_AZ_ACCOUNT_CREATE_FAILS", "1"));

        Assert.Equal(5, run.Code);
        Assert.Contains("Polecenie az nie powiodło się", run.Err, StringComparison.Ordinal);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string ReadLog() => File.Exists(logFile) ? File.ReadAllText(logFile) : "";

    private async Task<ScriptRun> RunAsync(string[] args, (string Name, string Value) env = default, string? pathOverride = null)
    {
        var info = new ProcessStartInfo(Bash!)
        {
            WorkingDirectory = FindRepoRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        info.ArgumentList.Add(ScriptPath);
        foreach (string arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        string original = Environment.GetEnvironmentVariable("PATH") ?? "";
        info.Environment["PATH"] = pathOverride ?? fakeBin + Path.PathSeparator + original;
        info.Environment["FAKE_AZ_LOG"] = logFile.Replace('\\', '/');
        info.Environment["FAKE_AZ_STATE"] = stateDir.Replace('\\', '/');
        if (env.Name is not null)
        {
            info.Environment[env.Name] = env.Value;
        }

        using var process = Process.Start(info)!;
        process.StandardInput.Close();
        Task<string> outTask = process.StandardOutput.ReadToEndAsync(Ct);
        Task<string> errTask = process.StandardError.ReadToEndAsync(Ct);
        await process.WaitForExitAsync(Ct);
        return new ScriptRun(process.ExitCode, await outTask, await errTask);
    }

    private static string? FindBash()
    {
        if (OperatingSystem.IsWindows())
        {
            foreach (string? root in new[] { Environment.GetEnvironmentVariable("ProgramFiles"), Environment.GetEnvironmentVariable("ProgramFiles(x86)") })
            {
                if (root is null)
                {
                    continue;
                }

                string candidate = Path.Combine(root, "Git", "bin", "bash.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        string exe = OperatingSystem.IsWindows() ? "bash.exe" : "bash";
        foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (OperatingSystem.IsWindows() && dir.Contains("System32", StringComparison.OrdinalIgnoreCase))
            {
                continue; // WSL launcher, not a native bash
            }

            string candidate = Path.Combine(dir, exe);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? ResolveTool(string tool)
    {
        var info = new ProcessStartInfo(Bash!)
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add($"command -v {tool}");
        using var process = Process.Start(info)!;
        string path = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        return process.ExitCode == 0 && path.StartsWith('/') ? path : null;
    }

    private static string FindRepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LegalAgent.slnx")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return dir ?? throw new InvalidOperationException("Repository root (LegalAgent.slnx) not found.");
    }

    private static void MakeExecutable(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private sealed record ScriptRun(int Code, string Out, string Err);

    private const string FakeAz = """
        #!/usr/bin/env bash
        echo "$*" >> "$FAKE_AZ_LOG"
        case "$*" in
          "account show"*)
            if [ "${FAKE_AZ_LOGGED_OUT:-0}" = 1 ]; then echo "Please run 'az login' to setup account." >&2; exit 1; fi
            echo "00000000-1111-2222-3333-444444444444"
            ;;
          "group create"*)
            echo "{}"
            ;;
          "cognitiveservices model list"*)
            printf 'gpt-4o\t2024-08-06\tStandard\n'
            if [ "${FAKE_AZ_MODEL_MISSING:-0}" != 1 ]; then printf 'gpt-4o-mini\t2024-07-18\tGlobalStandard,Standard\n'; fi
            ;;
          "cognitiveservices account deployment show"*)
            [ -e "$FAKE_AZ_STATE/deployment" ] || exit 1
            ;;
          "cognitiveservices account deployment create"*)
            touch "$FAKE_AZ_STATE/deployment"
            echo "{}"
            ;;
          "cognitiveservices account show"*)
            [ -e "$FAKE_AZ_STATE/account" ] || exit 1
            case "$*" in *properties.endpoint*) echo "https://faqgen-test.openai.azure.com/";; esac
            ;;
          "cognitiveservices account create"*)
            if [ "${FAKE_AZ_ACCOUNT_CREATE_FAILS:-0}" = 1 ]; then echo "ERROR: quota exceeded" >&2; exit 1; fi
            touch "$FAKE_AZ_STATE/account"
            echo "{}"
            ;;
          *)
            echo "unexpected: $*" >&2
            exit 9
            ;;
        esac
        """;
}
