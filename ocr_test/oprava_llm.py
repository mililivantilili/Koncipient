"""Pokus: oprava přepisu malým místním jazykovým modelem.

Výsledek z 2026-11-02 (Qwen3-1.7B, strana 8): chyby se nesnížily
(3,3 % -> 3,4 % znaků), model zanesl gramatickou chybu a na stranu
potřeboval asi 7 minut na 4jádrovém procesoru. Pro Koncipient se
nedoporučuje, skript zůstává kvůli zopakování testu s jiným modelem.

Příklad:
    python oprava_llm.py --data D:/ocr-data --motor tesseract --model Qwen/Qwen3-1.7B
"""

import re
import time

from spolecne import argumenty, vystup, zkontroluj_data

POKYN = ("Jsi korektor textu z OCR. Oprav pouze zjevné chyby rozpoznání písmen "
         "a diakritiky. Nic nepřidávej, nic nevynechávej, neměň čísla, jména ani "
         "pořadí slov. Vrať jen opravený text.")


def main():
    p = argumenty(__doc__.splitlines()[0])
    p.add_argument("--motor", default="tesseract", help="který přepis opravit")
    p.add_argument("--model", default="Qwen/Qwen3-1.7B")
    a = p.parse_args()
    data = zkontroluj_data(a.data)

    import torch
    from transformers import AutoModelForCausalLM, AutoTokenizer
    tok = AutoTokenizer.from_pretrained(a.model)
    model = AutoModelForCausalLM.from_pretrained(a.model, dtype=torch.bfloat16)

    cil = vystup(data, f"{a.motor}+llm")
    for f in sorted((data / "vystup" / a.motor).glob("*.txt")):
        start = time.time()
        odstavce = [o.strip() for o in re.split(r"\n\s*\n", f.read_text(encoding="utf-8")) if o.strip()]
        opravene = []
        for o in odstavce:
            zpravy = [{"role": "system", "content": POKYN}, {"role": "user", "content": o}]
            ids = tok.apply_chat_template(zpravy, add_generation_prompt=True,
                                          return_tensors="pt", enable_thinking=False)
            if not torch.is_tensor(ids):
                ids = ids["input_ids"]
            g = model.generate(ids, max_new_tokens=int(ids.shape[1] * 1.5) + 20, do_sample=False)
            opravene.append(tok.decode(g[0, ids.shape[1]:], skip_special_tokens=True).strip())
        (cil / f.name).write_text("\n\n".join(opravene), encoding="utf-8")
        print(f"{f.stem}: {time.time() - start:.0f} s", flush=True)


if __name__ == "__main__":
    main()
