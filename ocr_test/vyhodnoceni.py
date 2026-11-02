"""Porovná přepisy všech motorů se správným textem.

Reference ve složce <data>/reference/ (vše volitelné):
    <strana>.txt        ručně přepsaný správný text strany
    <strana>.cisla.txt  čísla z tabulky oddělená mezerami
    udaje.txt           citlivé údaje, které musí OCR přepsat přesně,
                        po řádcích ve tvaru  <strana>|<údaj>

Měří:
    chyby znaků   Levenshteinova vzdálenost / délka reference (CER)
    shoda slov    podíl slov reference nalezených ve správném pořadí
    čísla         kolik desetinných čísel z tabulky motor přečetl
    údaje         kolik citlivých údajů je v přepisu přesně, to je
                  nejdůležitější pro anonymizaci

Příklad:
    python vyhodnoceni.py --data D:/ocr-data
"""

import re
from collections import Counter
from difflib import SequenceMatcher

from spolecne import argumenty, zkontroluj_data


def norm(text):
    text = text.replace("–", "-").replace("—", "-")
    return re.sub(r"\s+", " ", text).strip()


def chyby_znaku(ref, hyp):
    predchozi = list(range(len(hyp) + 1))
    for i, zr in enumerate(ref, 1):
        radek = [i]
        for j, zh in enumerate(hyp, 1):
            radek.append(min(predchozi[j] + 1, radek[j - 1] + 1, predchozi[j - 1] + (zr != zh)))
        predchozi = radek
    return predchozi[-1] / max(len(ref), 1)


def shoda_slov(ref, hyp):
    r, h = ref.split(), hyp.split()
    sm = SequenceMatcher(None, r, h, autojunk=False)
    return sum(m.size for m in sm.get_matching_blocks()) / max(len(r), 1)


def cisla_spravne(ref_cisla, hyp):
    nalezene = Counter(x.replace(".", ",") for x in re.findall(r"\d+[,.]\d+", hyp))
    ok = 0
    for x in ref_cisla:
        if nalezene[x] > 0:
            ok += 1
            nalezene[x] -= 1
    return ok


def main():
    a = argumenty(__doc__.splitlines()[0]).parse_args()
    data = zkontroluj_data(a.data)
    ref_dir = data / "reference"
    motory = sorted(d for d in (data / "vystup").iterdir()
                    if d.is_dir() and d.name != "upravene")

    def prepis(motor, strana):
        f = motor / f"{strana}.txt"
        return norm(f.read_text(encoding="utf-8")) if f.exists() else ""

    for f in sorted(ref_dir.glob("*.txt")):
        if "." in f.stem or f.name == "udaje.txt":
            continue
        ref = norm(f.read_text(encoding="utf-8"))
        print(f"\nStrana {f.stem}:")
        for m in motory:
            h = prepis(m, f.stem)
            print(f"  {m.name:18s} chyby znaků {chyby_znaku(ref, h) * 100:5.1f} %"
                  f"   shoda slov {shoda_slov(ref, h) * 100:5.1f} %")

    for f in sorted(ref_dir.glob("*.cisla.txt")):
        strana = f.name.removesuffix(".cisla.txt")
        ref_cisla = [x for x in f.read_text(encoding="utf-8").split() if "," in x]
        print(f"\nČísla v tabulce, strana {strana}:")
        for m in motory:
            print(f"  {m.name:18s} {cisla_spravne(ref_cisla, prepis(m, strana))}/{len(ref_cisla)}")

    udaje_f = ref_dir / "udaje.txt"
    if udaje_f.exists():
        udaje = [r.split("|", 1) for r in udaje_f.read_text(encoding="utf-8").splitlines() if "|" in r]
        print("\nCitlivé údaje přepsané přesně:")
        for m in motory:
            chybi = [u for s, u in udaje if norm(u) not in prepis(m, s)]
            print(f"  {m.name:18s} {len(udaje) - len(chybi)}/{len(udaje)}"
                  + (f"   chybí: {', '.join(chybi)}" if chybi else ""))


if __name__ == "__main__":
    main()
