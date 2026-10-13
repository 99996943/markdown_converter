# Quickstart: weryfikacja spec 013

Wymagania: SDK z `global.json`, runtime .NET 9. Testy bez sieci i bez Azure (fałszywy model, syntetyczne regulaminy).

## 1. Kotwice, położenie jednostek, renderer

```bash
dotnet test tests/LegalAgent.Faq.Tests -- --filter-class "*MarkdownAnchorsTests"
dotnet test tests/LegalAgent.Faq.Tests -- --filter-class "*UnitExtractorTests"
dotnet test tests/LegalAgent.Faq.Tests -- --filter-class "*RendererTests"
```

Oczekiwane: kotwice jak w algorytmie VS Code (research R3, w tym „§ 26. Tytuł” → `-26-tytuł`, powtórzenia `-1`);
każda jednostka listy ma stronę początku sekcji; wiersz źródła w postaci z
[contracts/faq-file-links.md](./contracts/faq-file-links.md), przypadki bez linku jak w tabeli kontraktu.

## 2. Aplikacja end-to-end (offline)

```bash
dotnet test tests/mBank.FaqGenerator.Tests
```

Oczekiwane: FAQ z fałszywego modelu ma w każdym źródle z jednostką `#page=N` i link `../downloads/<plik>.md#kotwica`;
każda kotwica jest kotwicą nagłówka zapisanego Markdownu (SC-141); poza adresami linków plik jak w 1.4.0 (SC-143).

## 3. Prawdziwy przebieg (właściciel)

```bash
az cognitiveservices account keys list -g rg-faqgen -n <name> --query key1 -o tsv \
  | dotnet run --project src/mBank.FaqGenerator -c Release -- --url <a> … --url <e>
```

Otworzyć `faq/FAQ_mBank.md` w VS Code: klik w jednostkę przewija `downloads/<plik>.md` do nagłówka; klik w nazwę
dokumentu otwiera PDF na stronie jednostki (SC-142, 10 pytań).
