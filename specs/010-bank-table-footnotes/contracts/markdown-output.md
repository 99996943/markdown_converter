# Kontrakt: Markdown — uzupełnienie 010

Uzupełnia `specs/001-legal-pdf-parser/contracts/markdown-output.md` oraz uzupełnienia spec 002, 007 i 008. Wersja
kontraktu publicznego: **1.4.0** (MINOR — nowe pola modelu `TableCell.RowSpan`, `TableCell.ContinuesAbove`,
`TableBlock.HeaderGroup`; powtarzanie tekstu komórek scalonych i wiersza grupującego w tabelach GFM; odnośnik
przypisu przed słowem). Pozostałe zmiany wyniku dotyczą tylko dotąd błędnie konwertowanych układów (kolumny tabel z
liniami, przypisy w komórkach i pod kreską przy rozmiarze tekstu).

## Komórka scalona pionowo

- W tabeli z liniami komórka, w której kolumnie między wierszami nie ma linii poziomej (przy linii w innej kolumnie),
  obejmuje te wiersze. W tabeli GFM jej tekst stoi w **każdym** objętym wierszu, w tej samej kolumnie:

  ```markdown
  | rodzaj przelewu | jaki kurs zastosujemy |
  | --- | --- |
  | przelew wychodzący A | kurs z momentu złożenia zlecenia[^1] |
  | przelew wychodzący A | kurs z momentu realizacji[^2] |
  | przelew wychodzący A | kurs negocjowany |
  | przelew wychodzący B | kurs z momentu złożenia zlecenia |
  ```

- Model: komórka w pierwszym objętym wierszu ma `RowSpan` = liczba wierszy; w kolejnych objętych wierszach komórka
  `ContinuesAbove` = true z tymi samymi inline'ami. Każdy wiersz ma pełny zestaw kolumn (suma `ColumnSpan` =
  `ColumnCount`).
- Fragmenty chunkera (spec 004) z części tabeli mają tekst scalonej komórki w każdym wierszu, także gdy część zaczyna
  się w środku grupy.
- Tabele bez linii i tabele z linią pod każdym wierszem we wszystkich kolumnach — bez scaleń (jak dotąd).
- Format zapasowy (`a \| b`, TBL001) — komórki scalone nie występują.

## Wiersz grupujący nad nagłówkiem

- Wiersz nagłówka nad kilkoma kolumnami (np. „Gdzie można zrobić?” nad kolumnami 2–4) nie jest osobnym wierszem
  tabeli. Jego tekst stoi na początku każdego objętego nagłówka kolumny, oddzielony spacją; kolumna bez wiersza
  grupującego ma sam nagłówek:

  ```markdown
  | Co można zrobić? | Gdzie można zrobić? serwis transakcyjny (na naszej stronie/ w aplikacji mobilnej) | Gdzie można zrobić? BOK | Gdzie można zrobić? placówka |
  | --- | --- | --- | --- |
  | zmienić typ rachunku | TAK | TAK | TAK |
  ```

- Model: `TableBlock.HeaderGroup` (komórki z `ColumnSpan` według objętych kolumn; puste komórki dla kolumn bez grupy).
- Fragmenty chunkera powtarzają nagłówek z tekstem grupy w każdej części (jak nagłówek, FR-223 spec 004).

## Nagłówek kolumny w kilku liniach

- Linie nagłówka leżące nad jedną kolumną danych tworzą jedną komórkę nagłówka; ich tekst jest złożony w kolejności
  czytania, spacjami (z dzieleniem wyrazów jak w akapicie): `**serwis transakcyjny** (nasza strona internetowa)`.
  Formatowanie pogrubienia według liter, jak w innych komórkach.
- Liczba kolumn tabeli z liniami wynika z linii pionowych; żadna kolumna nie powstaje z podziału nagłówka.

## Przypisy

- Format bez zmian: `[^n]` w miejscu odnośnika, `[^n]: treść` po treści sekcji, numeracja kolejna od 1 (inwariant 3
  spec 001).
- Odnośnik **przed słowem** (mała podniesiona cyfra na początku słowa lub komórki): `[^n]` bezpośrednio przed słowem,
  bez spacji, jak w PDF: `[^3]kurs z momentu złożenia zlecenia`.
- Odnośniki w komórkach tabel GFM: `Tak[^4]`, `TAK/TAK[^5]`, `[^3]kurs …` — dotąd w komórkach tabel odnośnik
  rozpoznany przez wykrywanie przypisów wychodził jako zwykły tekst.
- Definicje pod krótką kreską z tekstem tego samego rozmiaru co tekst główny i etykietą gołą cyfrą (podniesioną lub
  nie) są definicjami `[^n]: …`, jeśli na stronie są odnośniki o tych numerach. Każda etykieta zaczyna osobną
  definicję.

## Bez zmian

Taryfy bez siatki, słowniki definicji (`- 1/ **termin** definicja`, spec 007), tabele-dokumenty (spec 002), łączenie
tabel przez strony, znaczniki stron, ucieczki etykiet list.
