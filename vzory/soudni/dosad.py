"""Do anonymizovaných soudních rozhodnutí dosadí vymyšlené údaje a vyznačí je [[typ|hodnota]].

Vstup: JSON z stahni.py (rozhodnuti.justice.cz). Každé anonymizované místo má popisek
typu (Jméno žalovaného, Adresa žalobkyně, IČO, SPZ…). Podle něj se dosadí vymyšlená
hodnota a vyznačí se, takže výsledek má přesně známé polohy údajů jako vzory/dokumenty.
Data a částky, které soudy také anonymizují, Koncipient záměrně ponechává; dosadí se
bez vyznačení. Místa s neurčitým popiskem (Anonymizováno, hodnota) se vyplní neutrálně.

Výstup: <vystup>/<uuid>.txt ve stejném formátu jako vzory/dokumenty.

Použití:
    python dosad.py --vstup D:/korpus/soudni/raw --vystup D:/korpus/soudni/dokumenty
"""

import argparse
import json
import random
import re
from pathlib import Path

MUZI = "Jan Petr Josef Pavel Martin Tomáš Jaroslav Jiří Miroslav Zdeněk Václav Michal František Karel Milan Lukáš David Jakub Ondřej Radek Vladimír Stanislav Roman Marek Aleš Libor Vojtěch Filip Ladislav Antonín".split()
ZENY = "Jana Marie Eva Hana Anna Lenka Kateřina Věra Lucie Alena Petra Jaroslava Veronika Martina Tereza Michaela Ludmila Helena Zdeňka Ivana Monika Markéta Jitka Barbora Kristýna Pavla Dana Simona Zuzana Eliška".split()
# (mužský tvar, ženský tvar)
PRIJMENI = [("Novák", "Nováková"), ("Svoboda", "Svobodová"), ("Novotný", "Novotná"), ("Dvořák", "Dvořáková"), ("Černý", "Černá"),
            ("Procházka", "Procházková"), ("Kučera", "Kučerová"), ("Veselý", "Veselá"), ("Horák", "Horáková"), ("Němec", "Němcová"),
            ("Marek", "Marková"), ("Pokorný", "Pokorná"), ("Pospíšil", "Pospíšilová"), ("Hájek", "Hájková"), ("Jelínek", "Jelínková"),
            ("Král", "Králová"), ("Růžička", "Růžičková"), ("Beneš", "Benešová"), ("Fiala", "Fialová"), ("Sedláček", "Sedláčková"),
            ("Doležal", "Doležalová"), ("Zeman", "Zemanová"), ("Kolář", "Kolářová"), ("Navrátil", "Navrátilová"), ("Čermák", "Čermáková"),
            ("Urban", "Urbanová"), ("Vaněk", "Vaňková"), ("Blažek", "Blažková"), ("Kříž", "Křížová"), ("Kovář", "Kovářová"),
            ("Bartoš", "Bartošová"), ("Vlček", "Vlčková"), ("Polák", "Poláková"), ("Musil", "Musilová"), ("Kopecký", "Kopecká"),
            ("Šimek", "Šimková"), ("Konečný", "Konečná"), ("Malý", "Malá"), ("Holub", "Holubová"), ("Štěpánek", "Štěpánková"),
            ("Krejčí", "Krejčí"), ("Kadlec", "Kadlecová"), ("Matějka", "Matějková"), ("Staněk", "Staňková"), ("Šťastný", "Šťastná")]
ULICE = ["Masarykova", "Husova", "Nádražní", "Palackého", "Komenského", "Školní", "Zahradní", "Polní", "Lidická", "Jiráskova",
         "Smetanova", "Tyršova", "Na Výsluní", "Revoluční", "Dlouhá", "Krátká", "Lipová", "Luční", "U Potoka", "Sokolská"]
OBCE = [("602 00", "Brno"), ("110 00", "Praha 1"), ("130 00", "Praha 3"), ("700 30", "Ostrava"), ("301 00", "Plzeň"), ("370 01", "České Budějovice"),
        ("460 01", "Liberec"), ("779 00", "Olomouc"), ("500 02", "Hradec Králové"), ("530 02", "Pardubice"), ("586 01", "Jihlava"),
        ("760 01", "Zlín"), ("695 01", "Hodonín"), ("537 01", "Chrudim"), ("738 01", "Frýdek-Místek"), ("405 02", "Děčín"),
        ("397 01", "Písek"), ("674 01", "Třebíč"), ("750 02", "Přerov"), ("664 34", "Kuřim")]
FIRMY = ["ALFA finance s.r.o.", "Rychlá půjčka a.s.", "STAVMONT CZ s.r.o.", "Moravia Credit, a.s.", "Dopravní služby Kraus s.r.o.",
         "BYTY Nova spol. s r.o.", "Energie Plus a.s.", "Autoservis Malina s.r.o.", "Inkaso Partner s.r.o.", "TERMO-KLIMA v.o.s.",
         "Zemědělské družstvo Lhotka", "Realitní kancelář Domov s.r.o.", "PROFI Leasing a.s.", "Kovovýroba Hrubý s.r.o.", "Pojišťovna Jistota a.s."]
ZENSKE_ROLE = re.compile(r"\b(žalobkyně|žalovaná|žalované|advokátky|advokátka|zástupkyně|obviněné|povinné|oprávněné|navrhovatelky|odpůrkyně|dlužnice|matky|manželky)\b")
MUZSKE_ROLE = re.compile(r"\b(žalobce|žalobci|žalovaný|žalovaného|žalovanému|advokát|advokáta|zástupce|obviněného|povinného|oprávněného|navrhovatele|odpůrce|dlužníka|otce|manžela)\b")
NEURCITE = ("anonymizováno", "hodnota", "číslo", "anonymizována dvě slova", "dvě slova", "slovo")


class Generator:
    def __init__(self, semeno):
        self.n = random.Random(semeno)
        self.osoby = {}  # popisek → (jméno, příjmení, rod) aby stejná osoba měla stejné jméno

    def osoba(self, popisek, jen=None):
        klic = re.sub(r"^(jméno|příjmení|rodné přijmení)\s*", "", popisek.lower()).strip() or popisek
        if klic not in self.osoby:
            p = popisek.lower()
            zena = bool(ZENSKE_ROLE.search(p)) or (not MUZSKE_ROLE.search(p) and self.n.random() < 0.5)
            m, z = self.n.choice(PRIJMENI)
            self.osoby[klic] = (self.n.choice(ZENY if zena else MUZI), z if zena else m)
        jmeno, prijmeni = self.osoby[klic]
        return prijmeni if jen == "prijmeni" else jmeno if jen == "jmeno" else f"{jmeno} {prijmeni}"

    def adresa(self):
        psc, obec = self.n.choice(OBCE)
        cislo = f"{self.n.randint(1, 2999)}" + (f"/{self.n.randint(1, 60)}" if self.n.random() < 0.5 else "")
        return f"{self.n.choice(ULICE)} {cislo}, {psc} {obec}"

    def datum(self):
        return f"{self.n.randint(1, 28)}. {self.n.randint(1, 12)}. {self.n.randint(1950, 2004)}"

    def hodnota(self, popisek, nasleduje):
        p = popisek.lower().strip()
        n = self.n
        if p in ("datum",):
            return f"{n.randint(1, 28)}. {n.randint(1, 12)}. {n.randint(2018, 2025)}", None
        if p in ("částka",):
            return f"{n.randint(1, 900)} {n.randint(0, 999):03d} Kč", None
        if p.startswith("tituly"):
            return n.choice(["Ing.", "Mgr.", "JUDr.", "Bc.", "MUDr."]), None
        if any(p.startswith(x) for x in NEURCITE):
            return "XY", None
        if "datum narození" in p:
            return self.datum(), "narozeni"
        if "rodné" in p and "číslo" in p or p == "rč":
            return f"{n.randint(50, 99)}{n.choice(['0', '5'])}{n.randint(1, 9)}{n.randint(10, 28)}/{n.randint(1000, 9999)}", "rc"
        if "iban" in p:
            return f"CZ{n.randint(10, 99)} 0800 0000 {n.randint(1000, 9999)} {n.randint(1000, 9999)} {n.randint(1000, 9999)}", "ucet"
        if "účt" in p or "účet" in p:
            return f"{n.randint(100000, 9999999999)}/{n.choice(['0100', '0300', '0800', '2010', '5500'])}", "ucet"
        if "ičo" in p:
            while True:  # platný kontrolní součet jako u skutečných IČO
                zaklad = [n.randint(0, 9) for _ in range(7)]
                zb = sum(c * (8 - i) for i, c in enumerate(zaklad)) % 11
                return "".join(map(str, zaklad)) + str(1 if zb == 0 else 0 if zb == 1 else 11 - zb), "ico"
        if "spz" in p or "registrační" in p:
            return f"{n.randint(1, 9)}{n.choice('ABCEHJKLMPSTUZ')}{n.randint(1, 9)} {n.randint(1000, 9999)}", "jiny"
        if "vin" in p:
            return "TMB" + "".join(n.choice("ABCDEFGHJKLMNPRSTUVWXYZ0123456789") for _ in range(14)), "jiny"
        if "symbol" in p:
            return f"{n.randint(100000, 9999999999)}", "jiny"
        if "tel" in p:
            return f"+420 {n.randint(600, 799)} {n.randint(100, 999)} {n.randint(100, 999)}", "telefon"
        if "mail" in p:
            j, pr = self.osoba(p).split(" ", 1)
            return f"{j.lower()}.{pr.lower()}@seznam.cz".replace("á", "a").replace("é", "e").replace("í", "i").replace("ý", "y").replace("ó", "o").replace("ú", "u").replace("ů", "u").replace("č", "c").replace("ř", "r").replace("š", "s").replace("ž", "z").replace("ě", "e").replace("ň", "n").replace("ť", "t").replace("ď", "d"), "email"
        if "spisová" in p or "značka" in p:
            return f"{n.randint(1, 99)} C {n.randint(1, 999)}/{n.randint(2015, 2025)}", "znacka"
        if "osobní údaje" in p:
            return f"nar. {self.datum()}, bytem {self.adresa()}", "narozeni+adresa"
        if "údaje o zástupci" in p:
            return f"zastoupen advokátem JUDr. {self.osoba('advokát ' + p)}, sídlem {self.adresa()}", "osoba+adresa"
        if "ulice" in p:
            return f"{n.choice(ULICE)} {n.randint(1, 999)}", "adresa"
        if "obec" in p:
            return n.choice(OBCE)[1], "adresa"
        if "adresa" in p:
            return self.adresa(), "adresa"
        if "právnická" in p or "společnost" in p or ("jméno" in p and re.match(r"^[.,\s]*IČ", nasleduje or "")):
            return n.choice(FIRMY), "firma"
        if "rodné přijmení" in p or p == "příjmení":
            return self.osoba(p, jen="prijmeni"), "osoba"
        if p == "jméno":
            return self.osoba(p, jen="jmeno"), "osoba"
        if "jméno" in p or p in ("fo", "jméno fo"):
            return self.osoba(p), "osoba"
        return "XY", None


def oznac(hodnota, typ):
    if typ is None:
        return hodnota
    if typ == "narozeni+adresa":
        m = re.match(r"nar\. (.+), bytem (.+)", hodnota)
        return f"nar. [[narozeni|{m.group(1)}]], bytem [[adresa|{m.group(2)}]]"
    if typ == "osoba+adresa":
        m = re.match(r"zastoupen advokátem (.+), sídlem (.+)", hodnota)
        return f"zastoupen advokátem [[osoba|{m.group(1)}]], sídlem [[adresa|{m.group(2)}]]"
    return f"[[{typ}|{hodnota}]]"


def preved(dok, semeno, max_znaku):
    g = Generator(semeno)
    odstavce = []
    for cast in ("header", "verdict", "justification", "information"):
        for odstavec in dok.get(cast) or []:
            texty = odstavec.get("texts", [])
            vysledek = ""
            for i, t in enumerate(texty):
                if t.get("anonStyle") == "NONE":
                    vysledek += t["text"]
                else:
                    nasleduje = texty[i + 1]["text"] if i + 1 < len(texty) else ""
                    hodnota, typ = g.hodnota(re.sub(r"\s+", " ", t["text"]).strip(), nasleduje)
                    # dva anonymizované údaje těsně za sebou (jméno a datum) oddělit čárkou jako v podání
                    if i > 0 and texty[i - 1].get("anonStyle") != "NONE":
                        vysledek += ", "
                    vysledek += oznac(hodnota, typ)
            vysledek = re.sub(r"[ \t]+", " ", vysledek).strip()
            if vysledek:
                odstavce.append(vysledek)
        if sum(len(o) for o in odstavce) > max_znaku:
            break
    # nejvýš max_znaku, celé odstavce
    text, delka = [], 0
    for o in odstavce:
        if delka + len(o) > max_znaku and text:
            break
        text.append(o)
        delka += len(o)
    jc = (dok.get("_meta") or {}).get("jednaciCislo") or ""
    return "\n\n".join(text), jc


def main():
    p = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    p.add_argument("--vstup", required=True, type=Path)
    p.add_argument("--vystup", required=True, type=Path)
    p.add_argument("--max-znaku", type=int, default=4500, help="délka dokumentu (asi 2 strany)")
    a = p.parse_args()
    a.vystup.mkdir(parents=True, exist_ok=True)
    pocet = 0
    for i, f in enumerate(sorted(a.vstup.glob("*.json"))):
        dok = json.loads(f.read_text(encoding="utf-8"))
        text, _ = preved(dok, i, a.max_znaku)
        if text.count("[[") < 2:
            continue  # rozhodnutí bez osobních údajů nemá pro měření cenu
        (a.vystup / (f.stem + ".txt")).write_text(text, encoding="utf-8")
        pocet += 1
    print(f"převedeno {pocet} rozhodnutí")


if __name__ == "__main__":
    main()
