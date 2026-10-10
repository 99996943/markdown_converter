# Kontrakt: Markdown — uzupełnienie 008

Uzupełnia `specs/001-legal-pdf-parser/contracts/markdown-output.md` oraz uzupełnienia spec 002 i spec 007. Wersja
kontraktu publicznego: **1.3.0** (MINOR — dwie nowe wartości `ListLabelKind` i nowa opcja
`ArtifactOptions.PageNumberBandRatio`; pozostałe zmiany wyniku dotyczą tylko dotąd błędnie konwertowanych układów).

## Etykiety „a.” i „i.”

- Mała litera z kropką („a.”) i mała liczba rzymska z kropką („i.”, „ii.”, „xii.”) są etykietami list, jeśli tworzą
  spójny ciąg co najmniej dwóch etykiet na tym samym wcięciu (`ListLabelKind.LetterDot`, `RomanDot`). Element:
  `- a\. tekst`, `- ii\. tekst` (kropka z ucieczką jak w `- 2\.`).
- Zagnieżdżenie według wcięcia pod elementami „1.”, „1/”, „a)”, „a/” i pod definicją słowniczka; dwie spacje na poziom.
- Samotna „a.”, ciąg niespójny, skróty („np.”, „r.”) i wielkie litery — tekst jak dotąd.

```markdown
- 15\. Pełnomocnictwo musi być udzielone:
  - a\. w formie pisemnej w karcie informacyjnej klienta,
  - b\. w formie elektronicznej za pośrednictwem systemu bankowości internetowej,
  - c\. przez złożenie oświadczenia w osobnym dokumencie.
```

```markdown
- a/ złożenia prawidłowego zlecenia, które zawiera niżej określone dane:
  - i\. numer rachunku klienta,
  - ii\. imię i nazwisko lub nazwę klienta,
  - iii\. numer rachunku beneficjenta,
```

## Numery stron „N/M”

Samodzielny numer strony „N/M” w stopce lub nagłówku strony (ciąg na ≥ 2 stronach, M = liczba stron, N zgodne z
numerem strony) jest artefaktem: nie występuje w wyniku, jest zapisany w raporcie jako numer strony. Znaczniki
`<!-- page: N -->` bez zmian. „N/M” w treści — bez zmian.

## Wiersz kontynuacji z oznaczeniem jednostki

Wiersz kontynuujący akapit lub element listy, który przypadkiem zaczyna się oznaczeniem jednostki („Rozdział I”,
„Art. 5”), jest częścią akapitu lub elementu — nie nagłówkiem; oznaczenie zostaje dosłownie, bez dopisanej kropki.

```markdown
- 1\. Prowizje i opłaty pobieramy zgodnie z taryfą, która stanowi odrębny dokument Korporacji – Rozdział I (Prowadzenie rachunków). Jest on załącznikiem do umowy.
```

## Ramki schematu kroków

Rozszerza FR-067 (schematy kroków): każda ramka — także pojedyncza, połączona strzałkami-znakami i z treścią
wychodzącą poniżej pola — to pogrubiony akapit z frazą lewej kolumny, a po nim treść prawej kolumny (akapit lub lista),
w kolejności ramek. Strzałki-znaki nie trafiają do wyniku. Bez tabeli GFM, bez tabeli zastępczej, bez TBL001.

```markdown
**Sprawdzamy Twoją tożsamość**

- 1\. Dyspozycję otwarcia kolejnego rachunku może złożyć posiadacz rachunku.
- 2\. Zanim złożysz taką dyspozycję, potwierdzimy Twoją tożsamość.
- 3\. Możemy poprosić o dokument tożsamości.

**Potwierdzamy, że otworzyliśmy rachunek**

Potwierdzenie otwarcia rachunku przekażemy Ci w formie elektronicznej.
```

## Ostrzeżenia

Brak nowych kodów. Ramki schematu i wyliczenia „a.”/„i.” nie dają `TBL001_AmbiguousGrid`.
