"""Stáhne vzorek anonymizovaných soudních rozhodnutí z otevřených dat rozhodnuti.justice.cz.

Rozhodnutí jsou úřední díla zveřejněná ministerstvem spravedlnosti, osobní údaje v nich
jsou anonymizované a označené (anonStyle ANON s popiskem typu, např. „Jméno žalovaného“).
Stažená data patří mimo repozitář.

Použití:
    python stahni.py --vystup D:/korpus/soudni/raw --pocet 200
"""

import argparse
import json
import random
import time
import urllib.request
from pathlib import Path

API = "https://rozhodnuti.justice.cz/api/opendata"


def nacti(url):
    with urllib.request.urlopen(url, timeout=60) as r:
        return json.load(r)


def main():
    p = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    p.add_argument("--vystup", required=True, type=Path)
    p.add_argument("--pocet", type=int, default=200)
    p.add_argument("--semeno", type=int, default=1)
    a = p.parse_args()
    a.vystup.mkdir(parents=True, exist_ok=True)
    nahoda = random.Random(a.semeno)

    # různé dny z let 2024–2026, z každého dne jen pár rozhodnutí kvůli pestrosti soudů a agend
    dny = []
    for rok in (2024, 2025, 2026):
        for mesic in nahoda.sample(range(1, 13), 4):
            try:
                dny += [d["odkaz"] for d in nacti(f"{API}/{rok}/{mesic}")]
            except Exception:
                pass
    nahoda.shuffle(dny)
    stazeno = len(list(a.vystup.glob("*.json")))
    for den in dny:
        if stazeno >= a.pocet:
            break
        try:
            polozky = nacti(den)["items"]
        except Exception:
            continue
        for polozka in nahoda.sample(polozky, min(3, len(polozky))):
            cil = a.vystup / (polozka["odkaz"].rsplit("/", 1)[-1] + ".json")
            if cil.exists():
                continue
            try:
                dok = nacti(polozka["odkaz"])
            except Exception:
                continue
            dok["_meta"] = {k: polozka.get(k) for k in ("jednaciCislo", "soud", "predmetRizeni")}
            cil.write_text(json.dumps(dok, ensure_ascii=False), encoding="utf-8")
            stazeno += 1
            time.sleep(0.3)  # šetrně k serveru
        print(f"staženo {stazeno}", flush=True)


if __name__ == "__main__":
    main()
