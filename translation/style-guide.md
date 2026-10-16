# Style guide: Mass Effect 1 Turkish subtitles/text

## Tone
- Natural spoken Turkish, military/sci-fi register; no archaisms, no literal calques. Keep each character's attitude (Wrex gruff, Tali shy, Garrus dry, Joker sarcastic).
- Preserve meaning first, then brevity: subtitles must fit; target length ratio tr/en between 0.7 and 1.4 (QA warns outside 0.5-1.6). Shorten, never drop information.

## Address forms
- sen: squadmates and friends talking to each other and to Shepard in private (Garrus, Wrex, Tali, Liara, Kaidan, Ashley once close), Shepard to squad.
- siz: strangers, officials, Council, Anderson/Udina to Shepard in formal scenes, crew to "Komutan", enemies talking formally, all UI and system text.
- Hostile/contemptuous speech (Wrex, mercenaries) may use sen.
- Keep a speaker's form constant within a scene.

## Names, lore
- See glossary.md. Names stay unchanged; Turkish suffixes attach with an apostrophe: Normandy'de, Shepard'ın, Citadel'e, Spectre'lar.
- Use vowel harmony of the pronounced name (Shepard -> "ın", Wrex -> "in", Tali -> "nin").

## Tags and placeholders (must be preserved exactly)
- Never alter, translate, reorder-away or delete: `<...>` tags (`<font ...>`, `</font>`, `<br>`), `{0}`-style and `%s`/`%d` placeholders, `[...]` markers, `\n` / real line breaks, `_` special tokens, and any run of ASCII-only symbol tokens.
- Tag attributes (colors, ids) stay byte-identical. Text inside tags is translated.
- Word order may change but each placeholder appears exactly the same number of times.
- Line breaks: keep the same number of line breaks as source.

## Characters
- Use proper Turkish letters: ğ ş ı İ ç ö ü Ğ Ş Ç Ö Ü. Capital dotted İ for "I" in "İ..." words; lowercase ı for undotted.
- Allowed characters: ASCII printable, those twelve letters, and common punctuation (… — – ' ' " " « »). Nothing else (no emojis, no combining marks).
- Do not leave English untranslated except names and glossary-kept terms.

## Numbers, UI
- Keep digits and units; use Turkish decimal comma only in prose, not in format strings.
- Button/menu labels: short nouns (Kaydet, Yükle, Seçenekler). Title case as in the source.

## Empty / non-text
- If en is empty, a pure number, or a pure tag/placeholder, copy it unchanged into tr.

## Batch format
See batch-format.md.
