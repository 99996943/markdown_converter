# Kontrakt: plik `FAQ_mBank.md`

Dokument OKF (konstytucja, „Format wiedzy”; research R10): front matter YAML, potem treść Markdown (CommonMark).
Kodowanie UTF-8 bez BOM, końce wierszy `\n`, plik kończy się jednym `\n`. Zapis atomowy: `FAQ_mBank.md.tmp`, potem
przeniesienie z nadpisaniem.

## Układ

```markdown
---
type: faq
title: "FAQ — regulaminy mBanku"
description: "10 najważniejszych pytań i odpowiedzi na podstawie 5 regulaminów mBanku."
resource:
  - "https://www.mbank.pl/pdf/regulaminy/a.pdf"
  - "https://www.mbank.pl/pdf/regulaminy/b.pdf"
  - "https://www.mbank.pl/pdf/regulaminy/c.pdf"
  - "https://www.mbank.pl/pdf/regulaminy/d.pdf"
  - "https://www.mbank.pl/pdf/regulaminy/e.pdf"
timestamp: "2026-10-09T14:03:12Z"
model: "gpt-4o-mini"
deployment: "gpt-4o-mini"
---

## Ile kosztuje prowadzenie podstawowego rachunku płatniczego?

Odpowiedź modelu, jeden lub więcej akapitów, bez zmian poza przycięciem białych znaków na brzegach.

Źródło: [Regulamin podstawowego rachunku płatniczego](https://www.mbank.pl/pdf/regulaminy/a.pdf), § 12

## Następne pytanie?

…

Źródła: [Regulamin A](https://…/a.pdf), § 3; [Taryfa B](https://…/b.pdf)
```

## Reguły

- **Kolejność pól front matter** jest stała: `type`, `title`, `description`, `resource`, `timestamp`, `model`,
  `deployment`.
- **Wartości tekstowe:** w cudzysłowach podwójnych, z ucieczką `\\` i `\"`. Znaki nowego wiersza w wartościach
  są niedozwolone, zastępuje je spacja.
- **`resource`:** adresy 5 dokumentów w kolejności `D1`…`D5` (kolejność adresów podanych przez użytkownika),
  czyli adresy podane przez użytkownika, nie cele przekierowań.
- **`timestamp`:** chwila zapisu w UTC, format `yyyy-MM-ddTHH:mm:ssZ`, z `TimeProvider` (testy podają stały czas).
- **`model` i `deployment`:** z konfiguracji `AzureOpenAI`.
- **Tytuł i opis:** należą do aplikacji. Biblioteka przyjmuje je w `FaqFileHeader`.
- **Treść:** dokładnie 10 sekcji `## <pytanie>` w kolejności zwróconej przez model w kroku wyboru. Nie ma nagłówka
  `#` (tytuł jest w front matter) ani numeracji dopisanej przez aplikację.
- **Tekst pytania i odpowiedzi:** dosłownie z odpowiedzi modelu (po przycięciu). Znaki specjalne Markdown w
  pytaniu nie są uciekane: pytanie to zwykły tekst nagłówka. Wyjątek: wiodące `#` jest poprzedzane `\`.
- **Wiersz źródeł:**
  - etykieta `Źródło:` dla jednego źródła, `Źródła:` dla kilku; źródła oddzielone `; `;
  - źródło to `[<Name dokumentu>](<Resource>)`, a gdy wskazano jednostkę: `, <jednostka>`;
  - etykieta i link to jedyny tekst dodawany przez aplikację do treści (FR-432 — formatowanie źródła, nie
    treść merytoryczna).
- **Odstępy:** między sekcjami dokładnie jeden pusty wiersz, bez spacji na końcach wierszy.

## Stabilność

Dla tego samego `FaqResult`, tego samego nagłówka i tej samej chwili renderer daje identyczny tekst (test
golden w `tests/LegalAgent.Faq.Tests`). Treść `FaqResult` z prawdziwego modelu może się różnić między
uruchomieniami (research R6).
