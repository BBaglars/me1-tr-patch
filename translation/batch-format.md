# Batch format

Translation chunks are JSON files: `{"meta": {...}, "entries": [ {"key": "...", "en": "...", "tr": ""}, ... ]}`.
- `key` is the exporter's stable key; never edit it. Any extra fields (speaker, context, flags) are carried through untouched.
- Translators fill only `tr`. Unfinished entries keep `tr` as "" (QA reports them).
- Chunk size: about 150 entries or 4000 words. One translator owns one chunk.
- Files: `chunks/<name>.json` (source, from exporter) -> `translated/<name>.json` (same shape, tr filled).
- QA: `python translation/qa.py chunks/<name>.json translated/<name>.json` (exit 1 on errors, 0 otherwise; warnings do not fail).
- A top-level JSON list of entries is also accepted by qa.py.
