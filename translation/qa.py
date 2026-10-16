#!/usr/bin/env python3
"""QA for translated chunks. Usage: qa.py source.json translated.json [--strict-len]
Exit 1 if any error, else 0. Warnings never fail."""
import json, re, sys
from collections import Counter

TAG = re.compile(r"</?[A-Za-z][^<>]*>")
PLACEHOLDER = re.compile(r"\{\d+\}|%[-+0# ]*\d*(?:\.\d+)?[sdifxXc]|%%|\[[^\[\]\n]{1,40}\]")
TR_LETTERS = "ğşıİçöüĞŞÇÖÜâîûÂÎÛ"
PUNCT = "…—–‘’“”«»·°×"

def load(path):
    with open(path, encoding="utf-8") as f:
        d = json.load(f)
    items = d["entries"] if isinstance(d, dict) else d
    return {e["key"]: e for e in items}

def allowed(ch):
    return ch in "\n\r\t" or 32 <= ord(ch) < 127 or ch in TR_LETTERS or ch in PUNCT

def check(src, tr):
    errs, warns = [], []
    en, t = src.get("en", ""), tr.get("tr", "")
    if t == "":
        if en.strip():
            errs.append("untranslated")
        return errs, warns
    for name, rx in (("tag", TAG), ("placeholder", PLACEHOLDER)):
        a, b = Counter(rx.findall(en)), Counter(rx.findall(t))
        if a != b:
            errs.append(f"{name} mismatch: src={dict(a)} tr={dict(b)}")
    if en.count("\n") != t.count("\n"):
        errs.append(f"line breaks: src={en.count(chr(10))} tr={t.count(chr(10))}")
    bad = sorted({c for c in t if not allowed(c)})
    if bad:
        errs.append("disallowed chars: " + " ".join(f"U+{ord(c):04X}" for c in bad))
    if t != t.strip() and en == en.strip():
        warns.append("leading/trailing whitespace added")
    plain_en = TAG.sub("", en).strip()
    plain_tr = TAG.sub("", t).strip()
    if len(plain_en) >= 12:
        r = len(plain_tr) / max(1, len(plain_en))
        if r > 1.6 or r < 0.5:
            warns.append(f"length ratio {r:.2f}")
    if len(plain_en) >= 20 and plain_en == plain_tr:
        warns.append("identical to source")
    if re.search(r"\bI\b", t) is None and re.search(r"[a-z]I[a-z]", t):
        warns.append("capital I inside word (check ı/İ)")
    return errs, warns

def main(argv):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if len(argv) < 3:
        print(__doc__); return 2
    S, T = load(argv[1]), load(argv[2])
    ne = nw = 0
    for k in S.keys() - T.keys():
        print(f"ERROR {k}: missing in translation"); ne += 1
    for k in T.keys() - S.keys():
        print(f"ERROR {k}: unknown key"); ne += 1
    for k in S.keys() & T.keys():
        e, w = check(S[k], T[k])
        for m in e: print(f"ERROR {k}: {m}"); ne += 1
        for m in w: print(f"WARN  {k}: {m}"); nw += 1
    print(f"{len(S)} entries, {ne} errors, {nw} warnings")
    return 1 if ne else 0

if __name__ == "__main__":
    sys.exit(main(sys.argv))
