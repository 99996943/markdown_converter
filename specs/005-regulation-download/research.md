# Research: Pobieranie regulaminów (spec 005)

Wszystkie decyzje rozstrzygnięte bez nowych pakietów poza jednym (R10). Numeracja R1–R13.

## R1. Podział na projekty

- **Decision**: nowa biblioteka `src/LegalAgent.Downloads` (cała logika: sprawdzanie adresów, nazwy plików,
  pobieranie, zapis atomowy, manifest, sprzątanie) i nowa aplikacja konsolowa `src/mBank.FaqGenerator`
  (argumenty, konfiguracja, pytania w konsoli, komunikaty, kody wyjścia). Testy: `tests/LegalAgent.Downloads.Tests`
  (biblioteka) i `tests/mBank.FaqGenerator.Tests` (warstwa aplikacji przez `Program.RunAsync`).
- **Rationale**: konstytucja — biblioteka ogólna bez zaszytych źródeł, aplikacja to cienka warstwa; osobny
  projekt testowy biblioteki. Nazwa biblioteki w konwencji `LegalAgent.*`; nazwa aplikacji z opisu właściciela.
  Liczba 5 i lista hostów mBanku należą do aplikacji (stała i `appsettings.json`), biblioteka przyjmuje dowolną
  niepustą listę adresów i dowolną listę hostów.
- **Alternatives**: polecenie w `legalagent-pdf` (odrzucone — opis wymaga osobnej aplikacji `mBank.FaqGenerator`,
  w której pojawią się kolejne etapy); logika w aplikacji (odrzucone — konstytucja).

## R2. Klient HTTP i testy offline

- **Decision**: biblioteka przyjmuje `HttpClient` w konstruktorze `DocumentDownloader`. Aplikacja tworzy jeden
  `HttpClient` na `SocketsHttpHandler { AllowAutoRedirect = false, AutomaticDecompression = All,
  ConnectTimeout = 15 s }` z `HttpClient.Timeout = Infinite` (limit liczy biblioteka, R4). `Program.RunAsync` ma
  przeciążenie z `HttpMessageHandler`, więc testy aplikacji i biblioteki używają atrapy `FakeHttpHandler`
  (odpowiedzi skryptowane: kod, nagłówki, treść, opóźnienie respektujące anulowanie, wyjątek). Żaden test nie
  łączy się z siecią.
- **Rationale**: opis wymaga `HttpClient` i asynchroniczności; zasada I wymaga testów offline; brak
  `IHttpClientFactory` (pakiet `Microsoft.Extensions.Http`) — jedna instancja na uruchomienie wystarcza (VI).
- **Alternatives**: `IHttpClientFactory` + DI (nowa zależność bez korzyści w aplikacji jednorazowej); lokalny
  serwer `HttpListener` w testach (wolniejszy, porty, różnice między systemami).

## R3. Przekierowania i lista dozwolonych hostów

- **Decision**: przekierowania wykonuje biblioteka ręcznie (301, 302, 303, 307, 308; nagłówek `Location`
  względny rozwiązywany względem bieżącego adresu), maksymalnie `MaxRedirects` = 5. Każdy adres w łańcuchu
  przechodzi tę samą kontrolę co adres wpisany: schemat `https` (albo `http`, gdy `AllowHttp`), host dozwolony.
  Host dozwolony = `Uri.IdnHost` równy wpisowi listy albo kończący się na `"." + wpis` (bez rozróżniania wielkości
  liter); adres z danymi logowania (`user@`) jest odrzucany. Błąd: `RedirectNotAllowed` z hostem docelowym albo
  `TooManyRedirects`.
- **Rationale**: FR-302, FR-312 i wyjaśnienie 2 — automatyczne przekierowania `HttpClient` nie pozwalają sprawdzić
  hosta przed wysłaniem żądania. `IdnHost` daje porównanie odporne na Unicode w nazwach.
- **Alternatives**: `AllowAutoRedirect = true` i kontrola `RequestMessage.RequestUri` po fakcie (żądanie do obcego
  hosta już wysłane).

## R4. Limit czasu, rozmiar, anulowanie

- **Decision**: każdy adres ma własny `CancellationTokenSource.CreateLinkedTokenSource(userToken)` z
  `CancelAfter(Timeout)` obejmujący całe pobranie (nagłówki, przekierowania, treść, zapis). `OperationCanceledException`
  przy anulowaniu przez użytkownika → `Cancelled`; przy upływie limitu → `Timeout`. `HttpCompletionOption.ResponseHeadersRead`
  i strumieniowe kopiowanie buforem 81920 B; `Content-Length` > limit → `TooLarge` przed czytaniem treści;
  licznik bajtów przerywa strumień po przekroczeniu limitu (serwer bez `Content-Length`).
- **Rationale**: FR-311, FR-315; jeden limit na plik jest zrozumiały dla użytkownika („60 s na plik”).
- **Alternatives**: `HttpClient.Timeout` (obejmuje tylko do nagłówków przy `ResponseHeadersRead` i daje
  `TaskCanceledException` nieodróżnialny od anulowania).

## R5. Rozpoznanie PDF

- **Decision**: pierwsze 5 bajtów treści musi być `%PDF-` (ASCII). Sprawdzane po odebraniu pierwszych bajtów,
  zanim reszta trafi na dysk; inaczej `NotPdf`. `Content-Type` ignorowany (serwery podają `application/octet-stream`).
- **Rationale**: FR-313; strona błędu HTML z kodem 200 odpada od razu.
- **Alternatives**: szukanie nagłówka w pierwszych 1024 B (dopuszczane przez czytniki, ale specyfikacja wymaga
  początku pliku; regulaminy banku nie mają śmieci przed nagłówkiem).

## R6. Zapis atomowy

- **Decision**: treść trafia do `<nazwa>.part` w katalogu pobrań (`FileMode.Create`, ten sam wolumin), z
  równoczesnym `IncrementalHash` SHA-256. Po pełnym odebraniu i zamknięciu: `File.Move(part, final, overwrite: true)`.
  Każda ścieżka błędu i anulowania usuwa `.part` w `finally`. Pozostałość po twardym zabiciu procesu jest
  nadpisywana przy następnym uruchomieniu i usuwana przez sprzątanie (R9).
- **Rationale**: FR-322, SC-062 — wcześniejsza wersja pliku nie jest naruszana do chwili podmiany; `Move` w obrębie
  katalogu jest atomowe na Linuksie i NTFS.
- **Alternatives**: katalog tymczasowy systemu (inny wolumin → kopiowanie zamiast atomowej podmiany).

## R7. Nazwy plików

- **Decision**: `FileNamePlanner.Plan(adresy)` — deterministycznie, tylko z listy i kolejności:
  1. ostatni niepusty segment `Uri.AbsolutePath`, `Uri.UnescapeDataString`, normalizacja NFC;
  2. znaki sterujące i `< > : " / \ | ? *` → `-`, ciągi `-` scalane, obcięte spacje i kropki z brzegów;
  3. rozszerzenie: końcówka `.pdf` (bez rozróżniania wielkości liter) zamieniana na `.pdf`, w przeciwnym razie
     dopisywane `.pdf`;
  4. nazwa zarezerwowana w Windows (`CON`, `PRN`, `AUX`, `NUL`, `COM1–9`, `LPT1–9`) → przedrostek `_`;
  5. rdzeń dłuższy niż 120 znaków jest obcinany;
  6. pusty rdzeń → `regulamin-N.pdf` (N = numer adresu 1–5);
  7. kolizja z wcześniejszą nazwą (porównanie bez rozróżniania wielkości liter, bo Windows) → `<rdzeń>-N.pdf`.
  Polskie litery zostają.
- **Rationale**: FR-321, przypadki brzegowe specyfikacji; bezpieczne na Linuksie i Windows, czytelne.
- **Alternatives**: nazwa ze skrótu adresu (nieczytelna), z `Content-Disposition` (zależy od serwera — nazwa
  znana dopiero po pobraniu, kolizje nieprzewidywalne).

## R8. Manifest

- **Decision**: `manifest.json` w katalogu pobrań, kontrakt `contracts/download-manifest.md` (`schemaVersion` 1).
  Pozycje w kolejności numerów; komunikaty błędów budowane z własnych szablonów (rodzaj błędu + kod HTTP / host /
  limit), bez treści wyjątków systemowych (zależnych od systemu i języka) — pełny opis wyjątku idzie tylko na
  stderr. `lastModified` z nagłówka `Last-Modified` w ISO 8601 UTC. Zapis przez `System.Text.Json` (wcięcia, LF,
  UTF-8 bez BOM, `InvariantCulture`), atomowo jak R6. Manifest zapisywany zawsze, gdy rozpoczęto pobieranie (także
  przy błędach i przerwaniu); nie jest zapisywany, gdy lista adresów jest błędna.
- **Rationale**: FR-324, SC-063 (identyczny manifest przy niezmienionych plikach), pole `resource` w OKF dla etapu
  FAQ.
- **Alternatives**: znacznik czasu pobrania (łamie SC-063).

## R9. Sprzątanie katalogu (FR-325)

- **Decision**: tylko gdy wszystkie pozycje mają wynik `Downloaded`: usuń pliki najwyższego poziomu katalogu
  pobrań z rozszerzeniem `.pdf` (bez rozróżniania wielkości liter), których nazwa nie należy do bieżącego zestawu
  (porównanie bez rozróżniania wielkości liter), oraz pozostałe `*.part`. Podkatalogi i inne pliki nietknięte.
  Wynik zawiera listę usuniętych nazw (posortowaną `Ordinal`). Błąd usuwania → `CleanupFailed` (kod 4).
- **Rationale**: wyjaśnienie 4; nic nie jest usuwane przy niepełnym wyniku.

## R10. Konfiguracja aplikacji

- **Decision**: `Microsoft.Extensions.Configuration` z warstwami (rosnący priorytet): `appsettings.json` obok
  pliku wykonywalnego (w repozytorium: `AllowedHosts: ["mbank.pl"]`, `Urls: []`, limity, katalog), opcjonalny
  `appsettings.Local.json` (już w `.gitignore`), zmienne `FAQGEN__<Sekcja>__<Pole>`, na końcu argumenty `--url`
  / `--output`. Wiązanie przez `Microsoft.Extensions.Configuration.Binder` (już przypięty). **Nowa zależność**:
  `Microsoft.Extensions.Configuration.Json` 9.0.20 (ta sama rodzina i wersja co przypięte pakiety) — uzasadnienie:
  zasada V wymaga adresów i hostów w konfiguracji, a lista 5 adresów w zmiennych środowiskowych jest niewygodna.
- **Rationale**: FR-303, zasady III i V, wyjaśnienie 3 (argumenty > konfiguracja).
- **Alternatives**: tylko zmienne środowiskowe (jak `legalagent-pdf`; lista adresów jako `FAQGEN__Download__Urls__0..4`
  pozostaje możliwa, ale nie jako jedyna droga); własny parser pliku (więcej kodu niż pakiet).

## R11. Pytania w konsoli

- **Decision**: `Program.RunAsync(args, TextReader stdin, TextWriter stdout, TextWriter stderr, env, configDirectory, handler?, ct)`.
  Pętla: „Podaj adres regulaminu {n} z 5: ”, odczyt linii, `Trim`, sprawdzenie (`AddressValidator`), przy błędzie
  komunikat i to samo `n`. `ReadLineAsync` zwraca `null` (koniec wejścia, potok zamknięty) → komunikat jak podać
  adresy i kod 2 (FR-304). Ctrl+C w trakcie pytań → 130.
- **Rationale**: FR-301, FR-302, FR-304; `TextReader` pozwala testować pytania bez konsoli.

## R12. Równoległość i postęp

- **Decision**: `Task.WhenAll` po wszystkich adresach (5 → bez ograniczania współbieżności). Postęp przez
  `IProgress<DownloadEvent>` (`Started`, `Completed`, `Failed`); aplikacja wypisuje zdarzenia pod blokadą (kolejność
  linii postępu może się różnić między uruchomieniami), podsumowanie zawsze w kolejności numerów. Test
  współbieżności: atrapa wstrzymuje każdą odpowiedź, dopóki nie nadejdzie 5 żądań (z limitem 5 s) —
  deterministyczny dowód SC-060 bez pomiaru czasu.
- **Rationale**: FR-310, SC-060; brak zależności testu od obciążenia maszyny.

## R13. Kody wyjścia i nagłówki żądań

- **Decision**: 0 — wszystkie pliki pobrane i zapisane; 1 — błąd nieoczekiwany; 2 — błędne argumenty,
  konfiguracja, lista adresów lub brak wejścia; 3 — co najmniej jedno pobranie nieudane; 4 — katalog pobrań nie
  do utworzenia/zapisu manifestu albo błąd sprzątania; 130 — przerwanie. `User-Agent`:
  `mBank.FaqGenerator/<wersja>` z konfiguracji (`Download:UserAgent`), bo część serwerów odrzuca żądania bez
  niego. Brak ponowień (specyfikacja).
- **Rationale**: FR-331, zasada IV; numeracja zbieżna z `legalagent-pdf` (2 argumenty, 130 przerwanie, 1
  nieoczekiwany).
- **Ryzyko**: serwer mBanku może odpowiadać 403 dla klientów niebędących przeglądarką (ochrona przed botami).
  Wtedy ręczna weryfikacja z prawdziwymi adresami pokaże błąd `HttpStatus 403`; `UserAgent` jest konfigurowalny.
