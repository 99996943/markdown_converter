namespace MBank.FaqGenerator;

/// <summary>Input used to read the API key (research R8): keys without echo, or a line of redirected input.</summary>
public interface IKeyInput
{
    /// <summary>Whether the standard input is redirected (a pipe or a file).</summary>
    bool IsInputRedirected { get; }

    /// <summary>Reads one key without echo.</summary>
    ConsoleKeyInfo ReadKey();

    /// <summary>Reads the next line of the standard input (the same reader as the address prompts).</summary>
    /// <returns>The line, or <c>null</c> at the end of the input.</returns>
    string? ReadLine();

    /// <summary>Makes Ctrl+C arrive as a key until the returned object is disposed.</summary>
    IDisposable CaptureControlC();
}
