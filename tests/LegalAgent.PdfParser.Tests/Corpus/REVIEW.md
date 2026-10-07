# Przegląd plików wzorcowych korpusu (T093)

Pliki `*.expected.md` wygenerowano poleceniem `UPDATE_GOLDEN=1 dotnet test --filter "FullyQualifiedName~GoldenTests"`
(2026-10-07), a następnie przejrzano względem PDF-ów. Złote pliki są **zatwierdzonym stanem bazowym** (regresja
wykrywa każdą zmianę wyniku), a nie idealną transkrypcją: znane odchylenia od PDF-u wymieniono niżej. Jakość
względem niezależnych danych referencyjnych mierzy `QualityMetricsTests` (SC-001 – SC-005).

Zakres przeglądu: dokumenty bankowe (syntetyczne) w całości; akty — blok tytułowy, wszystkie nagłówki (lista
`grep '^#'`), pierwsze 3–5 stron, każda strona z tabelą, przypisami lub adnotacjami bocznymi oraz losowe strony
środkowe. Przy 147-stronicowym Prawie bankowym i 114-stronicowej ustawie o usługach płatniczych pełne porównanie
słowo w słowo nie było wykonalne ręcznie — kompletność tekstu sprawdza metryka SC-002.

| Dokument | Sprawdzono | Znane odchylenia (do ewentualnej poprawy) |
|----------|-----------|--------------------------------------------|
| `acts/ustawa-o-sluzbie-cywilnej` (ISAP) | tytuł, 12 rozdziałów, art. 1–8, s. 1–5, 10–13, 15–16, 21–22 | Artykuły w notacji zmian są osobnymi jednostkami z nawiasem w nagłówku (`### \[Art. 31.`, `### \<Art. 31.` — oba brzmienia; FR-043). Winieta „Dz. U. 2008 Nr 227 poz. 1505” zostaje pogrubionym akapitem. |
| `acts/dz-u-2026-1298-obwieszczenie-msz` | całość | „I. Obowiązująca do …” jest pozycją listy, a „II. Obowiązująca od …” nagłówkiem; wiersze sekcji taryfy („I. Czynności …”) stoją w kolumnie 2 (tam zaczyna się wyśrodkowany tekst). |
| `acts/dz-u-2024-1646-prawo-bankowe` | blok obwieszczenia i tytułu ustawy, wszystkie nagłówki działów/rozdziałów/oddziałów, s. 1–6, 70–72, 140–147 | Podziały literowe w rozdziałach („A. Banki państwowe”) są nagłówkami typograficznymi (`###`) — zgodne z układem. |
| `acts/dz-u-2024-30-uslugi-platnicze` | j.w., s. 1–5, 11 (fragment pogrubiony), 60–62, 110–114 | — |
| `acts/dz-u-2024-1497-kredyt-konsumencki` | j.w., załączniki (formularze), tabele | W załącznikach: legenda wzoru („tk – okres …”) i ustęp „2. Poszczególne litery …” wychodzą jako nagłówki `##` (pogrubione linie w formularzu). |
| `acts/dz-u-2020-287-prawa-konsumenta` | całość | — |
| `banking/regulamin-rachunku` | całość | — |
| `banking/taryfa-z-siatka` | całość | — |
| `banking/taryfa-bez-siatki` | całość | Wiersze sekcji („I. Rachunki”) to pogrubiona pierwsza komórka, nie komórka scalona (tekst nie przekracza kolumny — zgodne z FR-066). |
| `banking/regulamin-dwie-kolumny` | całość | Dokument 2-stronicowy: nagłówek bieżący zostaje w tekście (FR-025 — w dokumentach < 3 stron usuwa się tylko numery stron). „Postanowienia końcowe” jest `###` (poniżej poprzedniego §). |

Prywatny korpus (`LEGALAGENT_PRIVATE_CORPUS`, np. `Corpus/private` z regulaminem mBanku) nie ma złotego pliku
w repozytorium; test sprawdza tam kompletność konwersji. Znane odchylenia regulaminu mBanku: tabela kroków
„Prośba o przelew BLIK” (układ wielopoziomowy) i trzy macierze „Co i gdzie możesz zrobić” wychodzą awaryjnie;
podtytuł „obowiązuje od …” jest nagłówkiem `##` (decyzja właściciela otwarta).
