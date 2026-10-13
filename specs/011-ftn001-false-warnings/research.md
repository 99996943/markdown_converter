# Research: ostrzeżenia FTN001_OrphanFootnote po spec 010

**Data**: 2026-10-13 · **Spec**: [spec.md](spec.md)

## Metoda

Tymczasowe narzędzie diagnostyczne (poza repozytorium, w scratchpadzie sesji) uruchamia potok parsera z dwoma
dodatkowymi etapami: przed `FootnoteDetection` (linie dolnej części strony, odcinki linii kwalifikujące się jako
kreska przypisów, rozmiar i linia bazowa każdego znaku w słowach z mniejszymi znakami) i po nim (szkice przypisów
z etykietą, stroną i `IsOrphan`, słowa z `FootnoteId` i rola ich linii). Zbudowane dwa razy: na bieżącym kodzie
(1.3.0) i na worktree z commita `ffd5f7c` (wersja 1.2.3, przed zmianami spec 010 w `FootnoteDetectionStage`).
Uruchomione na akcie D20081505Lj, na `Corpus/acts`, prywatnym korpusie i regulaminach z
`publish/mBank.FaqGenerator-win-x64/downloads`.

## R1. Przyczyna 5 ostrzeżeń aktu D20081505Lj

**Wynik**: wszystkie 5 ostrzeżeń jest **zasadnych**: to prawdziwe przypisy, których odnośnika parser nie rozpoznaje
ani w 1.2.3, ani w 1.3.0. Parser 1.2.3 ich nie zgłaszał, bo **etykieta definicji przypisu była liczona jako
odnośnik do samej siebie**. Spec 010 to usunął pod kreską.

| Strona | Definicja (pod kreską, 9,9 pt, etykieta 6,5 pt podniesiona) | Odnośnik w tekście (12 pt) | Dlaczego nierozpoznany |
|---|---|---|---|
| 13 | „1) Zmiany tekstu jednolitego…” | „z późn. zm.¹⁾)” | po podniesionym „1)” stoi pełnowymiarowy „)” |
| 68 | „9) Art. 187 zawiera zmiany…” | „187–189⁹⁾,” (s. 67) | po „9)” stoi pełnowymiarowy „,” |
| 70 | „10) Zmiany wymienionej ustawy…” | „z późn. zm.¹⁰⁾).” | po „10)” stoją „).” |
| 71 | „11) Zmiany wymienionej ustawy…” | „z późn. zm.¹¹⁾),” | po „11)” stoją „),” |
| 71 | „12) Ustawa została ogłoszona…” | „od dnia ogłoszenia¹²⁾,” | po „12)” stoi „,” |

Z pomiarów (s. 70): `zm.10)).` ma znaki `z m .` 12 pt na linii 710,7, potem `1 0 )` 8 pt na linii 706,2
(podniesione), potem `) .` 12 pt na linii 710,7. Reguła odnośnika na końcu słowa szuka podniesionego ciągu
**kończącego** słowo, więc ostatni pełnowymiarowy znak ją wyłącza. Reguła odnośnika przed słowem (spec 010) wymaga
podniesionego ciągu **otwierającego** słowo. Odnośnik w środku słowa nie pasuje do żadnej z nich.

**Mechanizm maskowania w 1.2.3**: `FindMarkers` przegląda wszystkie linie o roli Unknown, także linie definicji
przypisów. W definicji „¹⁾ Zmiany tekstu…” pierwsze słowo „1)” składa się wyłącznie z podniesionych znaków, więc
1.2.3 uznawał je za odnośnik i wiązał z definicją o tej samej etykiecie (strona definicji ≥ strona odnośnika).
Każda definicja z podniesioną etykietą miała więc „odnośnik” w sobie. Narzędzie potwierdza to dla 1.2.3:
`LINK p13 fid=0 word='1)' role=Footnote`, tak samo dla s. 68, 70, 71. Markdown się nie zmieniał, bo tekst definicji
powstaje z linii przed rozdzieleniem słowa, a renderer nie pokazuje odnośnika stojącego w linii przypisu.

**Co zmieniło spec 010** (commit f4d0854, T010): `FindMarkers` pomija podniesioną etykietę otwierającą linię
**pod pierwszą kreską przypisów** (`BelowFirstRule`), bo w regulaminach bankowych definicje „³ Zapis obowiązuje…”
nie mogą być własnymi odnośnikami. Tym samym zniknęło maskowanie na stronach z kreską.

**Niespójność, która zostaje w 1.3.0**: na stronie bez kreski (s. 67 aktu: definicje 2)–8) jako blok małych linii
na dole strony) samoodnośnik działa dalej. 6 definicji (2)–7)) jest „powiązanych” tylko ze sobą, a prawdziwe
odnośniki („135–139²⁾,” itd.) też są nierozpoznane. Tylko 8) ma prawdziwy odnośnik („161–185⁸⁾ oraz”). Uczciwa
liczba przypisów bez rozpoznanego odnośnika w akcie to więc 11, nie 5.

**Klasyfikacja**: 5/5 zasadne (FR-800). Żadne nie jest fałszywą definicją: każda definicja to prawdziwy przypis aktu.

## R2. Porównanie FTN001 1.2.3 → 1.3.0 na wszystkich dokumentach (FR-800a)

| Dokument | 1.2.3 | 1.3.0 | Przyczyna nowych |
|---|---|---|---|
| acts/dz-u-2020-287-prawa-konsumenta | 0 | 2 | K1 |
| acts/dz-u-2024-1497-kredyt-konsumencki | 0 | 5 | K1 |
| acts/dz-u-2024-1646-prawo-bankowe | 7 | 24 | K1 (17) |
| acts/dz-u-2024-30-uslugi-platnicze | 0 | 10 | K1 |
| acts/dz-u-2026-1298-obwieszczenie-msz | 0 | 6 | K1 (etykiety „*)”, „**)”, „***)”) |
| acts/ustawa-o-sluzbie-cywilnej | 0 | 5 | K1: **ten sam akt co D20081505Lj** (inny plik, te same 5 ostrzeżeń) |
| private/mbank-firm-1 | 0 | 2 | K2 |
| private/mbank-firm-2, firm-5, ind-1 | 1 | 0 | spadek: definicja bez etykiety z 1.2.3 rozpoznana poprawnie w spec 010 |
| private/mbank-ind-4 | 1 | 7 | K2 |
| private/mbank-reg1, mbank-regulamin-pdp, „Regulamin podstawowego rachunku…” (katalog główny) | 1 | 6 | K2 + K3 |
| pozostałe (corp-1…6, firm-3, firm-4, firm-6, ind-2, ind-3, ind-5, reg2, reg3, downloads 1–5) | bez zmian | | |
| `Corpus/banking` `regulamin-rachunku` | 1 → 1 (T001) | — | nie jest nowe: odnośnik „¹” to znak pełnej wielkości na linii bazowej (generator od spec 001), więc parser go nie rozpoznaje; golden `[^1]` bez odnośnika identyczny w 1.2.3 (`ffd5f7c`) |
| `Corpus/banking` (pozostałe 5) | 0 → 0 (T001) | | |

Klasy przyczyn:

- **K1, samoodnośnik usunięty pod kreską** (akty, 45 ostrzeżeń): jak w R1. Definicje istniały w 1.2.3, były
  „powiązane” tylko ze sobą (`role=Footnote` przy każdym odnośniku). Prawdziwe odnośniki w aktach to podniesiony
  ciąg z pełnowymiarową interpunkcją za nim: „zm.¹⁾)”, „…¹³⁹²⁾,”, „ogłoszenia¹²⁾,”. Ostrzeżenia **zasadne**.
- **K2, nowe definicje ze spec 010 bez rozpoznanego odnośnika** (bank): definicje „¹ Na naszej stronie…”,
  „⁹ …”, „¹⁰ …” pod kreską są od spec 010 przypisami (zaakceptowane goldeny). Ich odnośniki:
  - „BLIK², ” i „powtarzalna³⁾,”: podniesiona cyfra, za nią pełnowymiarowa interpunkcja (ten sam wzorzec co K1);
  - „BLIK⁹,¹⁰”, „BLIK¹⁶,¹⁷”: dwa odnośniki w jednym podniesionym ciągu z przecinkiem (`MarkerLabel` przyjmuje tylko
    jedną liczbę);
  - firm-1 s. 2: „zł/²230.000”: podniesiona cyfra w środku słowa po „/”;
  - reg1/pdp/ind-4 s. 7: podniesione „1” trafia przy składaniu linii do sąsiedniej linii tabeli słownika
    („1 odstępach czasu…”), więc nie jest podniesione względem swojej linii.

  W 1.2.3 tych definicji nie było (zwykłe akapity), więc nie było też ostrzeżeń. Ostrzeżenia **zasadne**.
- **K3, nie-przypisy jako definicje** (reg1/pdp s. 44): linie „1) rachunki oszczędnościowo – rozliczeniowe,”,
  „2) karty…” to punkty listy **wewnątrz** przypisu ⁶ pod kreską, a nie osobne przypisy. Szkice „1)”, „2)” istniały
  już w 1.2.3 (golden: `[^12]: rachunki…`, `[^13]: karty…`) i były maskowane samoodnośnikiem z innych stron
  (podniesione „1”, „2” z definicji s. 7 i s. 15). Te 2 ostrzeżenia na dokument są **trafne co do treści**: definicje
  są fałszywe, ale błąd to podział przypisu ⁶ na trzy, istniejący od 1.2.3.

Po 1.3.0 nigdzie nie występuje maskowanie poza aktem ustawa-o-sluzbie-cywilnej / D20081505Lj (s. 67, strona bez kreski).

## R3. Wniosek dla zakresu spec 011

- **Decyzja (potwierdzona w R4)**: ostrzeżeń nie przywracać do poziomu 1.2.3. Liczba 0 w
  1.2.3 była artefaktem samoodnośnika, a nie poprawnym wynikiem. Przywrócenie samoodnośnika (cofnięcie zmiany
  `FindMarkers` z T010) ukryłoby 45 prawdziwych sygnałów w aktach i zepsułoby przypisy regulaminów bankowych ze
  spec 010 (US3).
- **Uzasadnienie**: FR-801 dotyczy ostrzeżeń fałszywych, a takich w akcie nie ma (R1). Dla zasadnych FR-802
  dopuszcza (a) znane ograniczenie albo (b) bezpieczne rozpoznanie odnośnika.
- **Rozważane warianty**:
  - (a) **Tylko znane ograniczenie**: README opisuje odnośnik z interpunkcją za nim, listę „9,10”, odnośnik w środku
    słowa i K3; bez zmian kodu, bez 1.3.1. Bramka w testach utrwala liczby z 1.3.0.
  - (a′) **(a) + usunięcie samoodnośnika także na stronach bez kreski**: spójność; akt 5 → 11 ostrzeżeń, Markdown bez
    zmian (do sprawdzenia). Wersja 1.3.1.
  - (b) **Rozpoznanie odnośnika z pełnowymiarową interpunkcją za nim** (`)`, `,`, `.`, `;`, `:`): usuwa K1 i
    większość K2. Zmienia Markdown: „zm.1))” → „zm.[^n])” w 6 aktach publicznych i w goldenach prywatnych reg1,
    regulamin-pdp, ind-4. To zmiana goldenów (FR-163, wymaga zgody) i rozszerzenie wykrywania, czyli poza
    „małym zakresem” z opisu.
  - (c) **Przywrócenie samoodnośnika**: odrzucone (patrz wyżej).
- **Bramka FR-810**: akt jest już w publicznym korpusie jako `Corpus/acts/ustawa-o-sluzbie-cywilnej.pdf` (inny plik
  niż D20081505Lj, to samo zachowanie). Kopia w katalogu prywatnym jest zbędna; bramkę można oprzeć na pliku
  publicznym i uruchamiać w CI.

## R4. Decyzja i sonda wariantu a′

- **Decyzja właściciela (2026-10-13)**: wariant a′. Bramka FR-810 na publicznym akcie `ustawa-o-sluzbie-cywilnej`.
- **Sonda** (kopia kodu 1.3.0 w scratchpadzie): w `Execute` znaczniki przekazywane do `LinkMarkers` z pominięciem
  tych, których linia ma rolę `Footnote`, a znacznik otwiera pierwsze słowo (`WordIndex == 0`, `GlyphStart == 0`).
  Zbiór `referenced` liczony jak dotąd (ze wszystkich znaczników), więc obszar przypisów bez kreski, `BareLabel`
  i `LoneLabel` działają bez zmian.
  - Wynik na wszystkich dokumentach z R2: Markdown identyczny z 1.3.0 (bajt w bajt), FTN001 bez zmian poza
    `ustawa-o-sluzbie-cywilnej` i D20081505Lj: 5 → 11 (dodatkowo 2)–7) z s. 67).
- **Odrzucone**: filtr już w `FindMarkers` (jak `BelowFirstRule`) dla linii na dole strony bez kreski. Wtedy
  `referenced` traci samoodnośnik, pierwsza etykieta bloku bez kreski („2)”) nie ma odnośnika, obszar nie powstaje i
  definicje s. 67 wróciłyby do treści jako akapity, czyli zmiana Markdownu (wniosek z kodu, nie sprawdzony sondą).
