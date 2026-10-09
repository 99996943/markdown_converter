using System.Globalization;
using LegalAgent.Downloads;

namespace MBank.FaqGenerator;

/// <summary>Asks for the addresses one by one and checks each right after it is entered (FR-301, FR-302, research R11).</summary>
internal static class AddressPrompt
{
    /// <summary>Asks for <paramref name="count"/> addresses.</summary>
    /// <returns>The addresses, or <c>null</c> when the input ended first.</returns>
    public static async Task<IReadOnlyList<Uri>?> AskAsync(
        TextReader stdin,
        TextWriter stdout,
        DownloadOptions options,
        int count,
        CancellationToken cancellationToken)
    {
        var accepted = new List<Uri>(count);
        while (accepted.Count < count)
        {
            await stdout.WriteAsync(string.Create(
                CultureInfo.InvariantCulture,
                $"Podaj adres regulaminu {accepted.Count + 1} z {count}: ")).ConfigureAwait(false);
            await stdout.FlushAsync(cancellationToken).ConfigureAwait(false);

            string? line = await stdin.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                await stdout.WriteLineAsync().ConfigureAwait(false);
                return null;
            }

            AddressCheck check = AddressValidator.Check(line, accepted, options);
            if (check.Address is null)
            {
                await stdout.WriteLineAsync($"  Niepoprawny adres: {check.Message}").ConfigureAwait(false);
                continue;
            }

            accepted.Add(check.Address);
        }

        return accepted;
    }
}
