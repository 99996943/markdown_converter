using System.Text;

namespace MBank.FaqGenerator;

/// <summary>Reads the API key (FR-410–413, research R8); the key is kept only in the returned string.</summary>
internal static class KeyPrompt
{
    /// <summary>Prompt shown in the console.</summary>
    public const string Prompt = "Klucz API Azure OpenAI: ";

    /// <summary>
    /// Reads the key: a line of the redirected input (no prompt, no echo), or keys typed in the console — one <c>*</c> per
    /// character, Backspace removes, Enter ends, other control keys are ignored, an empty key is asked for again.
    /// </summary>
    /// <returns>The trimmed key, or <c>null</c> when the redirected input has none.</returns>
    /// <exception cref="OperationCanceledException">Ctrl+C was pressed.</exception>
    public static string? Read(IKeyInput input, TextWriter stdout)
    {
        if (input.IsInputRedirected)
        {
            string? line = input.ReadLine()?.Trim();
            return string.IsNullOrEmpty(line) ? null : line;
        }

        // ReadKey blocks and ignores the cancellation token, so Ctrl+C has to arrive as a key (research R8).
        using IDisposable capture = input.CaptureControlC();
        while (true)
        {
            string key = ReadOnce(input, stdout);
            if (key.Length > 0)
            {
                return key;
            }

            stdout.WriteLine("Klucz nie może być pusty.");
        }
    }

    private static string ReadOnce(IKeyInput input, TextWriter stdout)
    {
        stdout.Write(Prompt);
        stdout.Flush();
        var key = new StringBuilder();
        while (true)
        {
            ConsoleKeyInfo info = input.ReadKey();
            if (info.Key == ConsoleKey.C && info.Modifiers.HasFlag(ConsoleModifiers.Control) || info.KeyChar == '\u0003')
            {
                stdout.WriteLine();
                throw new OperationCanceledException("Przerwano.");
            }

            switch (info.Key)
            {
                case ConsoleKey.Enter:
                    stdout.WriteLine();
                    return key.ToString().Trim();
                case ConsoleKey.Backspace:
                    if (key.Length > 0)
                    {
                        key.Length--;
                        stdout.Write("\b \b");
                    }

                    break;
                default:
                    if (info.KeyChar != '\0' && !char.IsControl(info.KeyChar))
                    {
                        key.Append(info.KeyChar);
                        stdout.Write('*');
                    }

                    break;
            }

            stdout.Flush();
        }
    }
}
