# Vymyšlené vzory

Všechny dokumenty, jména, adresy a čísla jsou vymyšlené. PSČ začínají devítkou (v Česku neexistují), e-maily jsou na doméně `example.cz` a čísla účtů mají kód banky `0000`.

| Složka nebo soubor | Co je |
| --- | --- |
| `dokumenty/*.txt` | Šest typických dokumentů advokátní kanceláře (žaloba, rozsudek, kupní smlouva, plná moc, předvolání, výpis z obchodního rejstříku). Citlivé údaje jsou vyznačené `[[typ|hodnota]]` |
| `generuj.py` | Z dokumentů udělá stránky jako fotky z telefonu (natočení, perspektiva, stín, rozmazání) a referenční JSON s vyznačenými údaji |
| `sada/` | Vygenerovaná sada: `fotky/*.jpg` a `reference/*.json` |

Sada slouží k měření anonymizace nástrojem `tools/Koncipient.MereniAnonymizace` a automatickým testem `SadaVzoruTesty` (z bezchybného textu nesmí uniknout žádný údaj).

Sadu po úpravě dokumentů vygeneruješ znovu (potřebuješ Python s Pillow a numpy):

```bash
python vzory/generuj.py --vystup vzory/sada
```

Typy údajů: `osoba`, `firma`, `adresa`, `nemovitost`, `znacka` (spisová značka, číslo jednací), `ico`, `rc` (rodné číslo), `narozeni`, `ucet`, `telefon`, `email`, `jiny` (evidenční číslo, SPZ, číslo dokladu).

Pozor: dokumenty i pravidla detektoru psal stejný autor, takže výsledek na sadě je spíš horní odhad. Skutečné spisy budou rozmanitější.
