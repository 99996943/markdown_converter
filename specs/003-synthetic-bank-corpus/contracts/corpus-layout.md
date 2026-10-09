# Kontrakt: układ katalogu `corpus/`

```text
corpus/
├── README.md                 # instrukcja (pisana ręcznie; nie zarządzana przez generator)
├── przebieg.json             # zapisane parametry przebiegu korpusu (RunParameters)
├── manifest.json             # manifest (zarządzany)
├── zrodla/                   # pliki źródłowe treści (ręczne; contracts/content-format.md)
│   ├── typy.yaml
│   ├── fakty.yaml
│   ├── zabronione.yaml
│   ├── akty.yaml
│   ├── szablony/<szablon>.yaml
│   ├── bloki/{wspolne,regulaminy,taryfy,procedury}/<temat>.yaml
│   └── zatrucia/<rodzaj>.yaml
├── akty/                     # PDF commitowane ręcznie; .md i ZRODLA.md przez `refresh`
│   ├── ZRODLA.md
│   ├── <id-aktu>.pdf
│   └── <id-aktu>.md
├── regulaminy/  REG-01.pdf, REG-01.md, …, REG-03-w1.pdf, REG-03-w2.pdf, REG-03.pdf (najnowsza) …
├── taryfy/      TAR-01.pdf, TAR-01.md, …
├── procedury/   PRO-01.pdf, PRO-01.md, …
└── zatrute/
    ├── regulaminy/<rodzaj-problemu>/ZAT-REG-<RODZ>-01.pdf, .md, …
    ├── taryfy/<rodzaj-problemu>/…
    └── procedury/<rodzaj-problemu>/…
```

## Nazwy plików

- Dokument bazowy: `<PREFIKS>-<NN>.pdf` / `.md`; prefiks z `zrodla/typy.yaml` (obecnie `REG`, `TAR`, `PRO`), katalog `<typ>/` = `Id` typu; `NN` od `01`, szerokość
  dopasowana do liczby dokumentów (min. 2 cyfry).
- Wersje: najnowsza wersja nosi nazwę bazową (`REG-03.pdf`); wcześniejsze `REG-03-w1.pdf`,
  `REG-03-w2.pdf` (numer wersji). Wszystkie wersje są dokumentami tego samego katalogu typu.
- Zatrute: `ZAT-<PREFIKS>-<SKRÓT RODZAJU>-<NN>`; skróty: `STA` (falszywe-stawki), `POL`
  (polecenia-dla-ai), `PODS` (podszywanie), `NIEAKT` (nieaktualny-jako-obowiazujacy), `SPR`
  (sprzecznosc-z-oryginalem); rodzaj dodany z plików źródłowych — skrót z pliku wzorca.
- Akty: identyfikator z `akty.yaml`, np. `dz-u-2025-644-aml.pdf` (zgodnie z nazewnictwem korpusu testów).
- Nazwy plików: tylko ASCII, małe/wielkie litery jak wyżej, bez spacji.

## Zarządzanie plikami (FR-108)

Zarządzane przez `generate`: `manifest.json`, `regulaminy/`, `taryfy/`, `procedury/`, `zatrute/`
(rekurencyjnie, tylko `*.pdf` i `*.md`). Przez `refresh`: także `akty/*.md`, `akty/ZRODLA.md`.
Nigdy nie usuwane: `README.md`, `przebieg.json` (zapisywany tylko z `--save-params`), `zrodla/`,
`akty/*.pdf`, pliki innych rozszerzeń.

Kodowanie: Markdown, JSON — UTF-8 bez BOM, końce linii `\n`, plik kończy się `\n`.
`.gitattributes`: `corpus/**/*.md text eol=lf`, `corpus/**/*.pdf binary`.
