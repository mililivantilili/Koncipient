# Test OCR (etapa 1)

Skripty, které porovnají místní OCR motory na fotkách stran. Výsledky prvního kola jsou v [docs/navrh.md](../docs/navrh.md#výsledky-prvního-testu-ocr).

**Fotky ani přepisy nikdy nepatří do repozitáře.** Skripty berou složku s daty parametrem `--data` a odmítnou složku uvnitř repozitáře.

## Složka s daty

```text
ocr-data/                    kdekoli mimo repozitář
  fotky/                     fotky stran (.jpg, .png, .tif)
  reference/                 volitelné, jen pro vyhodnocení
    str09.txt                správný text strany str09
    str07.cisla.txt          čísla z tabulky oddělená mezerami
    udaje.txt                řádky ve tvaru  str10|Jan Novák
  vystup/                    vytvoří skripty
```

## Spuštění

Potřebuješ Python 3.12+, Tesseract s češtinou a balíčky z `requirements.txt`. Na Linuxu se slovník pro sloučení instaluje balíčkem `hunspell-cs`.

```bash
pip install -r requirements.txt
```

```bash
python ocr.py --data ../../ocr-data --motor tesseract
```

```bash
python ocr.py --data ../../ocr-data --motor paddle
```

```bash
python slouceni.py --data ../../ocr-data
```

```bash
python vyhodnoceni.py --data ../../ocr-data
```

PaddleOCR si při prvním spuštění stáhne modely z Hugging Face. Za běhu programu u advokátky se nic stahovat nebude, modely budou součástí instalace.

## Soubory

| Soubor | Co dělá |
| --- | --- |
| `spolecne.py` | Cesty k datům, pojistka proti datům v repozitáři, vyrovnání osvětlení |
| `ocr.py` | Přepis motorem `tesseract-surovy`, `tesseract`, `paddle` nebo `doctr` |
| `slouceni.py` | Sloučení dvou přepisů, sporná slova rozhodne český slovník |
| `vyhodnoceni.py` | Chyby znaků, shoda slov, čísla v tabulce a přesně přepsané citlivé údaje |
| `oprava_llm.py` | Pokus s opravou malým jazykovým modelem, nedoporučeno |
