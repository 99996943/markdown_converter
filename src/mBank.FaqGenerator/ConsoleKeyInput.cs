namespace MBank.FaqGenerator;

/// <summary>The real console: keys without echo, lines from the same reader as the address prompts (research R8).</summary>
/// <param name="stdin">The standard input reader.</param>
internal sealed class ConsoleKeyInput(TextReader stdin) : IKeyInput
{
    /// <inheritdoc />
    public bool IsInputRedirected => Console.IsInputRedirected;

    /// <inheritdoc />
    public ConsoleKeyInfo ReadKey() => Console.ReadKey(intercept: true);

    /// <inheritdoc />
    public string? ReadLine() => stdin.ReadLine();

    /// <inheritdoc />
    public IDisposable CaptureControlC()
    {
        bool previous = Console.TreatControlCAsInput;
        Console.TreatControlCAsInput = true;
        return new Restore(previous);
    }

    private sealed class Restore(bool previous) : IDisposable
    {
        public void Dispose() => Console.TreatControlCAsInput = previous;
    }
}
