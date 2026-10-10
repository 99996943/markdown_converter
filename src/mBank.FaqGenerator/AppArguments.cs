using System.Globalization;

namespace MBank.FaqGenerator;

/// <summary>Parsed command line (contracts/cli.md → „Wywołanie”).</summary>
internal sealed record AppArguments
{
    /// <summary>Usage text.</summary>
    public const string Usage =
        """
        Użycie:
          mBank.FaqGenerator [--url <adres>]... [--output <katalog>] [--faq-output <katalog>]
          mBank.FaqGenerator --help | --version

        Pobiera 5 regulaminów PDF do katalogu pobrań (domyślnie ./downloads) i zapisuje manifest.json, konwertuje je
        do Markdown (<nazwa>.md obok PDF-ów), a następnie generuje FAQ modelem Azure OpenAI i zapisuje FAQ_mBank.md
        w katalogu FAQ (domyślnie ./faq).

        Opcje:
          --url <adres>            adres regulaminu; podaj dokładnie 5 razy albo wcale
          --output <katalog>       katalog pobrań (nadpisuje Download:OutputDirectory)
          --faq-output <katalog>   katalog pliku FAQ_mBank.md (nadpisuje Faq:OutputDirectory)
          -h, --help               pokaż tę pomoc
          --version                pokaż wersję

        Bez --url adresy pochodzą z konfiguracji (Download:Urls), a gdy jej lista jest pusta — z pytań w konsoli.
        Konfiguracja: appsettings.json, appsettings.Local.json, zmienne FAQGEN__<Sekcja>__<Pole>,
        np. FAQGEN__Download__TimeoutSeconds=120. Sekcje:
          Download      adresy, dozwolone hosty, katalog pobrań, limity
          AzureOpenAI   AzureOpenAI:Endpoint (wymagany adres https zasobu, np. FAQGEN__AzureOpenAI__Endpoint),
                        AzureOpenAI:Deployment (nazwa wdrożenia), Model, TimeoutSeconds, Temperature, Seed,
                        MaxOutputTokens
          Faq           OutputDirectory, CandidatesPerDocument, MaxDocumentTokens
        Zasób Azure OpenAI tworzy skrypt scripts/azure/create-openai.sh.

        Klucz API: aplikacja pyta o niego po pobraniu i konwersji; wpisane znaki są zastępowane gwiazdkami.
        Przy przekierowanym wejściu klucz jest kolejnym wierszem, np. przekazany potokiem:
          az cognitiveservices account keys list -g rg-faqgen -n <zasób> --query key1 -o tsv | mBank.FaqGenerator --url …
        Klucza nie podaje się nigdy opcją ani zmienną (AzureOpenAI:ApiKey jest odrzucany).

        Kody wyjścia:
          0   pobrano 5 z 5, przekonwertowano 5 z 5 i zapisano FAQ_mBank.md
          1   błąd nieoczekiwany
          2   błędne argumenty/konfiguracja/adresy lub brak wejścia (adresów albo klucza)
          3   nie wszystkie pliki pobrane
          4   błąd zapisu (katalog pobrań, Markdown, FAQ)
          5   błąd konwersji
          6   błąd usługi modelu albo dokument za długi dla modelu
          7   odpowiedź modelu odrzucona
          130 przerwano
        """;

    /// <summary>Addresses from <c>--url</c>, in order.</summary>
    public IReadOnlyList<string> Urls { get; init; } = [];

    /// <summary>Value of <c>--output</c>.</summary>
    public string? Output { get; init; }

    /// <summary>Value of <c>--faq-output</c>.</summary>
    public string? FaqOutput { get; init; }

    /// <summary>Whether help was requested.</summary>
    public bool Help { get; init; }

    /// <summary>Whether the version was requested.</summary>
    public bool Version { get; init; }

    /// <summary>Error message when the command line is invalid.</summary>
    public string? Error { get; init; }

    /// <summary>Parses the command line.</summary>
    public static AppArguments Parse(IReadOnlyList<string> args, int requiredCount)
    {
        var urls = new List<string>();
        string? output = null;
        string? faqOutput = null;
        bool help = false;
        bool version = false;
        for (int i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "-h" or "--help":
                    help = true;
                    break;
                case "--version":
                    version = true;
                    break;
                case "--url" or "--output" or "--faq-output" when i + 1 >= args.Count:
                    return new AppArguments { Error = $"opcja {args[i]} wymaga wartości." };
                case "--url":
                    urls.Add(args[++i]);
                    break;
                case "--output":
                    output = args[++i];
                    break;
                case "--faq-output":
                    faqOutput = args[++i];
                    break;
                default:
                    return new AppArguments { Error = $"nieznany argument „{args[i]}”." };
            }
        }

        if (urls.Count != 0 && urls.Count != requiredCount)
        {
            return new AppArguments
            {
                Error = string.Create(
                    CultureInfo.InvariantCulture,
                    $"podaj dokładnie {requiredCount} adresów opcją --url albo żadnego (podano {urls.Count})."),
            };
        }

        return new AppArguments { Urls = urls, Output = output, FaqOutput = faqOutput, Help = help, Version = version };
    }
}
