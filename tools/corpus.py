#!/usr/bin/env python3
"""Dedup corpus pipeline. Run with: python -I tools/corpus.py <cmd>
  build    data/en -> translation/corpus.json (unique texts, key->uid) + chunks/<lane>/NNN.json + manifest.json
           (never overwrites chunks that already exist; keeps translators' work)
  merge    chunks -> data/tr/<pkg>.json (fan-out; untranslated stay "")
  report   progress per lane / total
  qa       qa.py on every data/tr file plus per-chunk (chunk used as its own source)
Chunks are worked IN PLACE: fill "tr", QA with: python -I translation/qa.py <chunk> <chunk>
"""
import json, glob, os, re, subprocess, sys
R = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
EN, TR, CH = (os.path.join(R, *p) for p in (("data", "en"), ("data", "tr"), ("translation", "chunks")))
CORPUS, MANIFEST = (os.path.join(R, "translation", n) for n in ("corpus.json", "manifest.json"))
LANES = {"ui": "t118", "a": "t119", "b": "t120"}
A_FAM = ("NOR", "STA", "PRO", "END", "CRD", "FRE")
MAXN, MAXW = 150, 4000

def jl(p):
    with open(p, encoding="utf-8") as f: return json.load(f)
def jd(p, o):
    os.makedirs(os.path.dirname(p), exist_ok=True)
    with open(p, "w", encoding="utf-8", newline="\n") as f: json.dump(o, f, ensure_ascii=False, indent=1); f.write("\n")

def family(pkg):
    if not pkg.startswith("BIOA_"): return "UI:" + pkg
    return pkg.split("_")[1]
def lane_of(fam):
    if fam.startswith("UI:"): return "ui"
    return "a" if fam.startswith(A_FAM) else "b"

def files():
    return sorted(f for f in glob.glob(os.path.join(EN, "*.json")) if not os.path.basename(f).startswith("_"))

def build():
    uniq, order, keymap, pkgs = {}, [], {}, {}
    seed = {}
    for f in glob.glob(os.path.join(TR, "*.json")):
        for e in jl(f)["entries"]:
            if e["tr"].strip(): seed.setdefault(e["en"], e["tr"])
    for f in files():
        pkg = os.path.basename(f)[:-5]
        for e in jl(f)["entries"]:
            if not e["en"].strip(): continue
            t = e["en"]
            if t not in uniq:
                uniq[t] = {"en": t, "pkgs": [], "fam": family(pkg), "first": (pkg, len(order))}
                order.append(t)
            u = uniq[t]
            if pkg not in u["pkgs"]: u["pkgs"].append(pkg)
            # lane by priority ui > a > b over all families the text appears in
            lanes = {lane_of(family(p)) for p in u["pkgs"]}
            u["lane"] = next(l for l in ("ui", "a", "b") if l in lanes)
    # stable uids: texts already in a previous corpus keep their uid; new texts get fresh uids after the highest one
    prev = {x["en"]: x["uid"] for x in jl(CORPUS)["texts"]} if os.path.exists(CORPUS) else {}
    nxt = max((int(u[1:]) for u in prev.values()), default=-1) + 1
    for t in order:
        if t in prev: uniq[t]["uid"] = prev[t]
        else: uniq[t]["uid"] = "u%05d" % nxt; nxt += 1
    order = sorted(order, key=lambda t: uniq[t]["uid"])
    corpus = {"count": len(order), "texts": [{"uid": uniq[t]["uid"], "lane": uniq[t]["lane"], "pkgs": uniq[t]["pkgs"], "en": t} for t in order]}
    jd(CORPUS, corpus)
    manifest = {"total": len(order), "lanes": {}, "chunks": {}}
    for lane in LANES:
        items = [uniq[t] for t in order if uniq[t]["lane"] == lane]
        # group by (family of first package, first package, order)
        items.sort(key=lambda u: (family(u["pkgs"][0]), u["pkgs"][0], u["uid"]))
        n, cur, w, ids = 0, [], 0, []
        old = jl(MANIFEST)["chunks"] if os.path.exists(MANIFEST) else {}
        fixed = {u for us in old.values() for u in us}  # a text already chunked stays in its chunk even if a new package moves its lane
        for c in sorted(old):
            if c.startswith(lane + "/"):
                n += 1; ids.extend(old[c]); manifest["chunks"][c] = old[c]
        items = [u for u in items if u["uid"] not in fixed]
        def flush():
            nonlocal n, cur, w
            if not cur: return
            n += 1; name = "%s/%03d" % (lane, n); p = os.path.join(CH, lane, "%03d.json" % n)
            ids.extend(u["uid"] for u in cur)
            manifest["chunks"][name] = [u["uid"] for u in cur]
            if not os.path.exists(p):
                ent = [{"key": u["uid"], "en": u["en"], "tr": seed.get(u["en"], ""), "ctx": u["pkgs"][:3]} for u in cur]
                jd(p, {"meta": {"lane": lane, "task": LANES[lane], "chunk": name, "family": family(cur[0]["pkgs"][0])}, "entries": ent})
            cur, w = [], 0
        for u in items:
            uw = len(u["en"].split())
            if cur and (len(cur) >= MAXN or w + uw > MAXW or family(u["pkgs"][0]) != family(cur[0]["pkgs"][0]) and len(cur) >= 60): flush()
            cur.append(u); w += uw
        flush()
        manifest["lanes"][lane] = {"task": LANES[lane], "texts": len(ids), "words": sum(len(u["en"].split()) for u in uniq.values() if u["lane"] == lane), "chunks": n, "chunk_range": "%s/001-%03d" % (lane, n)}
    allids = [i for c in manifest["chunks"].values() for i in c]
    assert len(allids) == len(set(allids)) == len(order), "coverage gap/overlap"
    jd(MANIFEST, manifest)
    print("unique", len(order)); [print(l, v) for l, v in manifest["lanes"].items()]

def trmap():
    m = {}
    for p in glob.glob(os.path.join(CH, "*", "*.json")):
        for e in jl(p)["entries"]:
            if e["tr"].strip(): m[e["en"]] = e["tr"]
    return m

def merge():
    m = trmap(); done = tot = 0
    for f in files():
        d = jl(f); 
        for e in d["entries"]:
            if e["en"].strip():
                e["tr"] = m.get(e["en"], ""); tot += 1; done += bool(e["tr"])
            else: e["tr"] = e["tr"] if e["tr"] else e["en"]
        jd(os.path.join(TR, os.path.basename(f)), d)
    print("merged: %d/%d strings translated, %d files" % (done, tot, len(files())))

def report():
    mf = jl(MANIFEST); T = D = 0
    for lane in LANES:
        t = d = 0; cd = 0; cn = 0
        for p in sorted(glob.glob(os.path.join(CH, lane, "*.json"))):
            es = jl(p)["entries"]; k = sum(1 for e in es if e["tr"].strip()); t += len(es); d += k; cn += 1; cd += k == len(es)
        print("%-3s %s texts %d/%d (%.1f%%)  chunks %d/%d" % (lane, LANES[lane], d, t, 100 * d / max(t, 1), cd, cn)); T += t; D += d
    print("TOTAL unique %d/%d (%.1f%%)" % (D, T, 100 * D / max(T, 1)))

def qa():
    bad = 0
    for f in files():
        b = os.path.basename(f); tp = os.path.join(TR, b)
        if not os.path.exists(tp): print("missing", b); bad += 1; continue
        r = subprocess.run([sys.executable, "-I", os.path.join(R, "translation", "qa.py"), f, tp], capture_output=True, text=True, encoding="utf-8")
        if r.returncode: bad += 1; print(b, [l for l in r.stdout.splitlines() if l.startswith("ERROR")][:3], r.stdout.splitlines()[-1])
    print("files with errors:", bad, "of", len(files())); return bad

def check_coverage():
    mf = jl(MANIFEST); ids = [i for c in mf["chunks"].values() for i in c]
    ok = len(ids) == len(set(ids)) == jl(CORPUS)["count"]
    seen = set()
    for p in glob.glob(os.path.join(CH, "*", "*.json")):
        seen |= {e["key"] for e in jl(p)["entries"]}
    ok = ok and seen == set(ids); print("coverage", "OK" if ok else "FAIL", len(ids)); return ok

if __name__ == "__main__":
    c = sys.argv[1] if len(sys.argv) > 1 else ""
    fn = {"build": build, "merge": merge, "report": report, "qa": qa, "coverage": check_coverage}.get(c)
    if not fn: print(__doc__); sys.exit(2)
    r = fn(); sys.exit(1 if r in (False,) or (c == "qa" and r) else 0)
