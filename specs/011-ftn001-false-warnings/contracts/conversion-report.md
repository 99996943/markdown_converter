# Kontrakt: ostrzeżenie FTN001_OrphanFootnote w raporcie konwersji (od 1.3.1)

Uzupełnienie `specs/001-legal-pdf-parser/contracts/public-api.md` (raport konwersji). Kontrakt Markdown (1.4.0) bez
zmian.

## Znaczenie

`FTN001_OrphanFootnote` (`pageNumber` = strona pierwszej linii definicji, komunikat
„Przypis „<etykieta>” na stronie <n> nie ma odnośnika w tekście.”) oznacza: **do definicji przypisu nie prowadzi żaden
rozpoznany odnośnik w tekście dokumentu.**

- Etykieta otwierająca linię definicji (podniesiona albo nie, z nawiasem albo bez) **nigdy** nie jest odnośnikiem,
  niezależnie od tego, czy nad obszarem przypisów jest kreska. (Do 1.3.0 podniesiona etykieta na stronie bez kreski,
  a do 1.2.3 także pod kreską, wiązała definicję z samą sobą i ukrywała ostrzeżenie.)
- Rozpoznawane odnośniki są bez zmian względem 1.3.0: podniesiony ciąg kończący słowo („Tak⁴”, „185⁸⁾”),
  podniesione cyfry przed słowem („³kurs”), także w komórkach tabel.
- Nierozpoznane odnośniki (znane ograniczenie, README parsera): podniesiony ciąg z pełnowymiarową interpunkcją za nim
  („zm.¹⁾)”, „BLIK²,”), lista odnośników („BLIK⁹,¹⁰”), odnośnik w środku słowa („zł/²230.000”), odnośnik złożony do
  sąsiedniej linii. Ich definicje dają FTN001.
- Ostrzeżenie nie zmienia Markdownu: definicja-sierota jest renderowana jak dotąd (`DocumentBuildStage`, kontrakt
  001/010).

## Zgodność

Zmiana liczby ostrzeżeń w raporcie przy identycznym Markdownie → wersja PATCH (1.3.1). Pole `warnings` raportu JSON
bez zmian w strukturze.
