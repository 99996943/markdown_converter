# Contract: format wyjściowy Markdown — zmiany spec 002

Uzupełnia `specs/001-legal-pdf-parser/contracts/markdown-output.md`; wszystkie zasady i niezmienniki
1–6 obowiązują bez zmian. Renderer nie zmienia się — nowe zachowanie wynika z modelu dokumentu
([data-model.md](../data-model.md)).

## Nowe i zmienione elementy

| Element źródła | Rendering | Przykład |
|----------------|-----------|----------|
| Tabela-dokument — nazwa sekcji (lewa komórka) | nagłówek `##` (poziom tytułu + 1) z oryginalną nazwą, wszystkie linie komórki złączone spacją, bez `**` | `## Jak możesz złożyć reklamację dotyczącą promocji?` |
| Tabela-dokument — treść (prawa komórka) | zwykłe akapity i listy pod nagłówkiem sekcji, scalone przez wiersze i strony; znaczniki `<!-- page: N -->` wg FR-002a (także wewnątrz akapitu) | `… obowiązywały <!-- page: 9 --> Cię opłaty …` |
| Tabela-dokument — pogrubiony śródtytuł | osobny akapit `**…**` (wieloliniowy → jeden akapit) | `**Nie możesz uczestniczyć w promocji, jeśli:**` |
| Tabela-dokument — definicje „termin – objaśnienie” | każda definicja osobnym akapitem, tekst bez zmian, bez punktorów | `Bank – mBank S.A.` |
| Tabela-dokument — wiersz nazw kolumn | pominięty (żadnego śladu w wyniku) | — |
| Podpunkt „o” (inna czcionka niż tekst) | pozycja listy z punktorem (`Bullet`): `- treść`, zagnieżdżona pod „•” | `  - pakiet Komfort – księgowość uproszczona,` |
| Podpis grafiki (FR-088) | zwykły akapit | `mBank.pl` |
| Linia „Obowiązuje od …” w bloku tytułowym (FR-093) | zwykły akapit bezpośrednio pod tytułem (w preambule) | `Obowiązuje od 01.09.2026 r. do 30.11.2026 r.` |
| Adres z łącznikiem na końcu linii (FR-094) | łącznik zostaje, bez spacji | `https://wearpay.pl/products/pierscien-platniczy-mastercard` |

## Przykład (fragment, syntetyczny układ jak `mbank-reg3`)

```markdown
# Regulamin promocji „Rozwijaj firmę z płatnościami od mBanku – edycja 1”

Obowiązuje od 01.09.2026 r. do 30.11.2026 r.

mBank.pl

<!-- page: 2 -->
## Organizator promocji

Promocję organizuje mBank S.A. z siedzibą w Warszawie …

## Uczestnik promocji

W promocji mogą uczestniczyć:

- osoby fizyczne, które …
- spółki jawne,

które w dniu składania wniosku …

**Nie możesz uczestniczyć w promocji, jeśli:**

- już raz skorzystałeś z tej promocji i/lub,
```

## Niezmienniki dodatkowe (testowane na korpusie syntetycznym)

7. W dokumencie z tabelą-dokumentem między pierwszym nagłówkiem sekcji tabeli-dokumentu a końcem
   wyniku nie ma tabeli GFM ani linii z separatorem ` \| ` pochodzących z tej tabeli.
8. Tekst wiersza nazw kolumn nie występuje w wyniku (chyba że występuje także w treści dokumentu).
9. Każdy nagłówek po pierwszej sekcji tabeli-dokumentu jest nazwą sekcji albo jednostką redakcyjną
   (FR-087).
