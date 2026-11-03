"""Sloučí přepisy dvou motorů a sporná slova rozhodne podle českého slovníku.

Kde se motory shodnou, slovo platí. Kde se liší, vyhraje slovo ze slovníku.
U čísel a paragrafů má přednost druhý motor (PaddleOCR čte čísla lépe),
jinak první (Tesseract má méně chyb ve znacích). Počet sporných slov
se vypíše, v programu se taková místa podbarví ke kontrole.

Příklad:
    python slouceni.py --data D:/ocr-data --a tesseract --b paddle

Slovník: hunspell cs_CZ (na Linuxu balíček hunspell-cs), cesta --slovnik.
"""

import difflib
import re

from spolecne import argumenty, vystup, zkontroluj_data

INTERPUNKCE = ".,;:()[]\"'„“”«»!?-–—|"


class Slovnik:
    def __init__(self, cesta):
        from spylls.hunspell import Dictionary
        self.d = Dictionary.from_files(cesta)

    def platne(self, slovo):
        jadro = slovo.strip(INTERPUNKCE)
        if not jadro:
            return False
        if any(z.isdigit() for z in jadro):
            # platné je jen čisté číslo (2,1 / 4136/3 / 12.3.), ne směs jako „Těs1n“
            return re.fullmatch(r"[\d.,/:\-–%+()]+", jadro) is not None
        try:
            return bool(self.d.lookup(jadro) or self.d.lookup(jadro.lower()))
        except Exception:
            return False


def klic(slovo):
    return slovo.strip(INTERPUNKCE).lower()


def ma_cislo(text):
    """Čísla, paragrafy a e-maily čte PaddleOCR spolehlivěji než Windows OCR."""
    return any(z.isdigit() for z in text) or "§" in text or "@" in text


def slouc(text_a, text_b, slovnik):
    """Vrátí sloučený text a počet sporných slov."""
    A, B = text_a.split(), text_b.split()
    sm = difflib.SequenceMatcher(None, [klic(w) for w in A], [klic(w) for w in B],
                                 autojunk=False)
    vysledek, sporna = [], 0

    def vyber(a, b):
        # jeden motor má číslo, § nebo @ a druhý ne (S místo §, l místo 1): vyhraje číslo
        if ma_cislo(a) != ma_cislo(b):
            return a if ma_cislo(a) else b
        pa, pb = slovnik.platne(a), slovnik.platne(b)
        if pa != pb:
            return a if pa else b
        return b if ma_cislo(a + b) else a

    def podil_platnych(slova):
        return sum(slovnik.platne(w) for w in slova) / len(slova)

    for op, a1, a2, b1, b2 in sm.get_opcodes():
        if op == "equal":
            vysledek += A[a1:a2]
            continue
        sporna += max(a2 - a1, b2 - b1)
        if op == "replace" and a2 - a1 == b2 - b1:
            vysledek += [vyber(a, b) for a, b in zip(A[a1:a2], B[b1:b2])]
        elif op == "replace" and b2 - b1 <= 3 and all(ma_cislo(w) for w in B[b1:b2]) and not ma_cislo(" ".join(A[a1:a2])):
            # druhý motor přečetl číslo nebo §, kde první má něco jiného (S místo §)
            vysledek += B[b1:b2]
        elif op == "replace":
            pa, pb = podil_platnych(A[a1:a2]), podil_platnych(B[b1:b2])
            lepsi_b = pb > pa  # při shodě zůstává první (hlavní) motor
            vysledek += B[b1:b2] if lepsi_b else A[a1:a2]
        elif op == "delete":
            # slova jen v prvním motoru: ponechat, pokud jsou většinou ze slovníku
            if podil_platnych(A[a1:a2]) >= 0.5:
                vysledek += A[a1:a2]
        elif op == "insert" and b2 - b1 <= 3 and all(ma_cislo(w) for w in B[b1:b2]):
            # krátký úsek jen v druhém motoru, a to jen čísla, § nebo e-mail, které první vynechal;
            # ostatní (útržky z okraje sousední stránky, patičky) se nevkládá
            vysledek += B[b1:b2]
    return " ".join(vysledek), sporna


def main():
    p = argumenty(__doc__.splitlines()[0])
    p.add_argument("--a", default="tesseract", help="první motor (základ textu)")
    p.add_argument("--b", default="paddle", help="druhý motor")
    p.add_argument("--slovnik", default="/usr/share/hunspell/cs_CZ",
                   help="cesta ke slovníku hunspell bez přípony")
    a = p.parse_args()
    data = zkontroluj_data(a.data)
    slovnik = Slovnik(a.slovnik)
    dir_a, dir_b = data / "vystup" / a.a, data / "vystup" / a.b
    cil = vystup(data, "slouceni")
    for fa in sorted(dir_a.glob("*.txt")):
        fb = dir_b / fa.name
        if not fb.exists():
            print(f"{fa.name}: chybí přepis motoru {a.b}, přeskakuji")
            continue
        text, sporna = slouc(fa.read_text(encoding="utf-8"), fb.read_text(encoding="utf-8"), slovnik)
        (cil / fa.name).write_text(text, encoding="utf-8")
        print(f"{fa.stem}: {len(text.split())} slov, sporných {sporna}")


if __name__ == "__main__":
    main()
