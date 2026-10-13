# Data Model: ostrzeżenia FTN001 (spec 011)

Model publiczny bez zmian. Zmienia się tylko sposób ustalania `FootnoteDraft.IsOrphan`.

## Encje

- **`FootnoteDraft`** (`Layout/FootnoteDraft.cs`, publiczny): `Id`, `Label`, `Page`, `Inlines`, `IsOrphan`.
  - `IsOrphan = true`, gdy żaden **odnośnik w tekście** nie został z nim powiązany. Od 1.3.1 etykieta własnej
    definicji (ani innej definicji) nie jest odnośnikiem.
  - Każdy szkic z `IsOrphan = true` daje jedno ostrzeżenie `FTN001_OrphanFootnote` (strona = `Page`).
- **`Marker`** (prywatny rekord etapu): `Line`, `WordIndex`, `GlyphStart`, `GlyphEnd`, `Label`. Powstaje w
  `FindMarkers` przed ustaleniem obszaru przypisów i zasila:
  1. zbiór `referenced` (etykiety z odnośnikami na stronie), z którego korzysta wykrywanie obszaru bez kreski,
     `BareLabel` i `LoneLabel`. **Bez zmian.**
  2. wiązanie z definicją w `LinkMarkers`. **Zmiana**: pominięty znacznik etykiety definicji.
- **Linia przypisu**: `LayoutLine` z `Role == LineRole.Footnote`, nadaną w pętli obszaru przypisów przed wiązaniem.

## Reguła (FR-801)

Znacznik **jest etykietą definicji** (i nie wiąże), gdy jednocześnie:
- jego linia ma rolę `Footnote` (należy do obszaru przypisów strony), oraz
- leży w pierwszym słowie linii (`WordIndex == 0`) i zaczyna to słowo (`GlyphStart == 0`).

Pozostałe znaczniki, także podniesione w środku linii przypisu (odnośnik z jednego przypisu do innego), wiążą
jak dotąd.

## Przejścia stanu szkicu

```text
utworzony (AddLine) ──► powiązany (LinkMarkers: znacznik w tekście)   → IsOrphan = false
                    └─► niepowiązany                                   → IsOrphan = true, FTN001
```

## Niezmienniki

- Markdown, `LegalDocument` (poza liczbą sierot w raporcie) i chunki identyczne z 1.3.0 (FR-807).
- Liczba szkiców i ich tekst bez zmian (obszar i etykiety liczone na pełnym `referenced`).
