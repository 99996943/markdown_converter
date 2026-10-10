# Kontrakt: Markdown — uzupełnienie 007

Uzupełnia `specs/001-legal-pdf-parser/contracts/markdown-output.md` i uzupełnienie spec 002. Wersja kontraktu
publicznego: **1.2.0** (MINOR — dwie nowe wartości `ListLabelKind`, zmiany wyniku tylko dla dotąd błędnie
konwertowanego układu).

## Etykiety z ukośnikiem

- Etykiety „1/”, „a/” są etykietami list jak „1)”, „a)”: element `- 1/ tekst`, `- a/ tekst`. Ukośnik nie wymaga
  ucieczki; etykiety z kropką i nawiasem — jak dotąd (`- 2\.`, `- 1\)`).
- Zagnieżdżenie: wcięcie kolumny etykiety i ranga stylu (ustęp „1.” → punkt „1/” → litera „a/”), dwie spacje na
  poziom jak w 001.
- Wiersze kontynuacji (początek w kolumnie tekstu, bez etykiety) i kontynuacja na następnej stronie należą do
  elementu; część wspólna po wyliczeniu — jak FR-054.

Przykład (z repliki strony):

```markdown
- 2\. Rachunki bieżące służą do:
  - 1/ gromadzenia środków pieniężnych
  - 2/ rozliczeń pieniężnych (krajowych i zagranicznych) związanych z działalnością gospodarczą Klienta.
- 3\. Rachunki pomocnicze służą do wyodrębnionych rozliczeń pieniężnych.
```

## Paragrafy „§ N”

- Samodzielny, wyróżniony wiersz „§ 5” → nagłówek `#### § 5` (poziom: numerowany rozdział + 1).
- Wiersz „§ 3. Porady ogólne” → jeden nagłówek z całym wierszem.
- Odwołania w tekście („§ 5 ust. 2”) — bez zmian, nie są nagłówkami.

```markdown
### 2. Rachunki bankowe oraz rachunek VAT

#### § 5

- 1\. Na podstawie umowy Klienci mogą otwierać rachunki bieżące i pomocnicze, w złotych i walutach obcych.
```

## Słowniczek

Jeden element listy na definicję, w jednym wierszu: etykieta, pogrubiony termin, spacja, definicja; wyliczenia
definicji zagnieżdżone. Bez dopisanych znaków.

```markdown
- 1/ **administrator (kontroler)** osoba fizyczna, którą Klient wskazał w umowie rachunku bankowego. Może ona w imieniu Klienta:
  - a/ zarządzać uprawnieniami użytkowników systemu mBank CompanyNet dotyczącymi składania zleceń i dokumentów elektronicznych,
  - b/ uzyskiwać informacje o realizacji umowy,
- 2/ **Bank** mBank S.A; w tym regulaminie używamy także zwrotów typu „my” (np. „prowadzimy”, „przyjmujemy”, „zmieniamy”),
```

## Ostrzeżenia

- Obszary etykieta–tekst i słowniczki nie dają `TBL001_AmbiguousGrid`.
- Brak nowych kodów ostrzeżeń.
