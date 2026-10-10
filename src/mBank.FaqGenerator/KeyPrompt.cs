using System.Text;

namespace MBank.FaqGenerator;

/// <summary>Reads the API key (FR-410–413, research R8); the key is kept only in the returned string.</summary>
internal static class KeyPrompt
{
    /// <summary>Prompt shown in the console.</summary>
    public const string Prompt = "Klucz API Azure OpenAI: ";

    /// <summary>Reads the key: a line of the redirected input, or keys typed in the console (one <c>*</c> per character).</summary>
    /// <returns>The trimmed key, or <c>null</c> when the redirected input has none.</returns>
    public static string? Read(IKeyInput input, TextWriter stdout)
    {
        if (input.IsInputRedirected)
        {
            string? line = input.ReadLine()?.Trim();
            return string.IsNullOrEmpty(line) ? null : line;
        }

        stdout.Write(Prompt);
        stdout.Flush();
        var key = new StringBuilder();
        while (true)
        {
            ConsoleKeyInfo info = input.ReadKey();
            if (info.Key == ConsoleKey.Enter)
            {
                stdout.WriteLine();
                return key.ToString().Trim();
            }

            key.Append(info.KeyChar);
            stdout.Write('*');
        }
    }
}
