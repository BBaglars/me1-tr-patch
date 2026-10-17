#!/usr/bin/env python3
r"""Verify a re-export of the staging packages against data/tr.
Usage: python -I tools/verify.py <reexport-dir>
The re-export dir is produced by Me1Tlk 'export --game staging\CookedPCConsole --out <dir>'. For every
data/tr entry the re-exported text (field 'en') must equal 'tr'; blank entries must stay unchanged."""
import json, glob, os, sys
R = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
def load(p):
    with open(p, encoding="utf-8") as f: return {e["key"]: e for e in json.load(f)["entries"]}
def main(rx):
    ok = bad = miss = files = 0
    for f in sorted(glob.glob(os.path.join(R, "data", "tr", "*.json"))):
        n = os.path.basename(f); rp = os.path.join(rx, n)
        if not os.path.exists(rp): print("MISSING re-export", n); miss += 1; continue
        T, X = load(f), load(rp); files += 1
        for k, e in T.items():
            if k not in X: print("MISSING key", k); bad += 1; continue
            want = e["tr"] if e["tr"] != "" else e["en"]
            if X[k]["en"] == want: ok += 1
            else:
                bad += 1
                if bad <= 20: print("DIFF", k, repr(want[:60]), repr(X[k]["en"][:60]))
        for k in X.keys() - T.keys(): print("EXTRA key", k); bad += 1
    print("files %d, strings ok %d, mismatches %d, missing files %d" % (files, ok, bad, miss))
    return 1 if bad or miss else 0
if __name__ == "__main__": sys.exit(main(sys.argv[1]))
