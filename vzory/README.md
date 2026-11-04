# Vymyšlené vzory

Všechny dokumenty, jména, adresy a čísla jsou vymyšlené. PSČ začínají devítkou (v Česku neexistují), e-maily jsou na doméně `example.cz` a čísla účtů mají kód banky `0000`.

| Složka nebo soubor | Co je |
| --- | --- |
| `dokumenty/*.txt` | Šest typických dokumentů advokátní kanceláře (žaloba, rozsudek, kupní smlouva, plná moc, předvolání, výpis z obchodního rejstříku). Citlivé údaje jsou vyznačené `[[typ|hodnota]]` |
| `generuj.py` | Z dokumentů udělá stránky jako fotky z telefonu (natočení, perspektiva, stín, rozmazání) a referenční JSON s vyznačenými údaji |
| `sada/` | Vygenerovaná sada: `fotky/*.jpg` a `reference/*.json` |
| `prazdne/` | Tři fotky prázdného papíru na stole z mobilu (Matěj, 2026-11-04). Hlídají, že se prázdná stránka nezahodí jako oddělovač. |

Sada slouží k měření anonymizace nástrojem `tools/Koncipient.MereniAnonymizace` a automatickým testem `SadaVzoruTesty` (z bezchybného textu nesmí uniknout žádný údaj).

Sadu po úpravě dokumentů vygeneruješ znovu (potřebuješ Python s Pillow a numpy):

```bash
python vzory/generuj.py --vystup vzory/sada
```

Typy údajů: `osoba`, `firma`, `adresa`, `nemovitost`, `znacka` (spisová značka, číslo jednací), `ico`, `rc` (rodné číslo), `narozeni`, `ucet`, `telefon`, `email`, `jiny` (evidenční číslo, SPZ, číslo dokladu).

Pozor: dokumenty i pravidla detektoru psal stejný autor, takže výsledek na sadě je spíš horní odhad. Skutečné spisy budou rozmanitější.

## Korpus soudních rozhodnutí (`soudni/`)

Větší sada pro měření, data se do repozitáře nedávají:

```bash
python vzory/soudni/stahni.py --vystup ../korpus/soudni/raw --pocet 160
python vzory/soudni/dosad.py --vstup ../korpus/soudni/raw --vystup ../korpus/soudni/dokumenty
python vzory/generuj.py --dokumenty ../korpus/soudni/dokumenty --vystup ../korpus/sada --ruzne-pismo
dotnet run --project tools/Koncipient.MereniAnonymizace -c Release -- ../korpus/sada --text --cast vyvoj
```

`stahni.py` stáhne oficiálně anonymizovaná rozhodnutí z rozhodnuti.justice.cz, `dosad.py` na anonymizovaná místa dosadí vymyšlené údaje podle jejich typu a vyznačí je. Měřicí nástroj má volbu `--cast vyvoj|kontrola` (pevné rozdělení 70:30): pravidla se ladí jen na vývojové části, kontrolní slouží k ověření, že se detektor nepřeučil.

