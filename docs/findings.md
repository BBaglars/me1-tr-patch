# ME1 Turkish patch: language slot, audio, TOC and glyph findings (t10)

Source: s3-investigator report. VERIFIED = read from files on disk. INFERRED = reasoned, needs an in-game test.

## Language slot
- VERIFIED: `MassEffectLauncher.exe` maps en_US/fr_FR/de_DE/it_IT/ja_JP/es_ES/pl_PL/ru_RU to INT/FR/DE/IT/JA/ES/PL/RA. It knows `-language=`, `-OVERRIDELANGUAGE=`, `-Subtitles`, and the settings keys `Language`, `SubtitleSize`, `EnglishVOEnabled`.
- VERIFIED: `MassEffect1.exe` contains `\Localised\DE\` paths, `_DE`/`_LOC` suffixes and a `LANGUAGE=` key.
- INFERRED: the slot comes from the Steam/store language or the launcher Language option and reaches the exe as `-language=DE` / `-Subtitles DE`. No ini or registry language setting exists (game never launched here). Quick test: run `MassEffect1.exe -language=DE`, or set Steam language to German.
- File sets exist only for INT, DE, FR, IT, PLPC, RA. `Startup_GE.pcc` is almost the same size as `Startup_DE.pcc` (45303818 vs 45303814). Which one the DE slot loads is UNKNOWN: patch both or test.

## Voice-over
- Plain DE switches to German VO (718 `*_de*` of 3942 files in Content/Packages/ISACT, `*_ss_DE.pcc`, `snd_gui_computer_DE.pcc`).
- The launcher has `EnglishVOEnabled`. Patch 1.03 notes say English VO with another subtitle language works for ME1 (not Russian).
- So Deutsch text plus English VO enabled should give Turkish text over English audio. Needs in-game confirmation, including whether the 59 INT-only LOC files stay English.
- Do not touch `*_ss_DE`, ISACT or `snd_*`.

## PCConsoleTOC.bin
- Location: `ME1/BioGame/PCConsoleTOC.bin` (795116 bytes). `PCConsoleTOC.txt` only points to it. Magic `0x3AB70C13`.
- Entry layout: u16 entrySize, u16 flags, u32 fileSize, byte[20] hash, then the `BioGame\...\name` string. Example: `BIOA_LAV00_LOC_DE.pcc` stores 1088480, equal to its disk size.
- Only 1 of 8966 hashes is non-zero. Sizes match disk for every .pcc (the only mismatches are 51 .bik movies). So the TOC carries the size, not a content hash.
- Recommendation: after repacking, rewrite fileSize in place for each patched file in a staged TOC copy (no insert or delete; entries sit in hash buckets). Back up the TOC too (t13). Whether the game rejects a mismatch is UNKNOWN; update it anyway.

## Turkish glyphs (gate for t12)
- Needed beyond Latin-1: Ğ ğ (U+011E/F), İ ı (U+0130/1), Ş ş (U+015E/F). Ç Ö Ü are Latin-1 and already used by DE/FR.
- The game uses Scaleform GFx fonts (`gfxfontlib.swf`, GFxFontMap, GFxFontProvider strings in the exe). The launcher fonts folder has `gfxfontlib.swf`, `gfxfonts_INT.swf`, `gfxfonts_JPN.swf`. In-game GFx movies live inside compressed `GUI_SF_*.pcc` / EntryMenu / Startup packages, so coverage cannot be checked statically.
- INFERRED only: PLPC ships and Polish needs Latin Extended-A, so ğ ş ı İ may exist or fall back to a device font. Not proof.
- Do an in-game glyph test with `Ğğ İı Şş Çç Öö Üü` (subtitle, journal/codex, menu) in one staged DE package once t9 works, or inspect the font DefineFont tags in the GFx SWFs (LegendaryExplorer or JPEXS FFDec). Do this before t12.
- Fallbacks in order: (1) add glyphs to the font in the affected GFx movies; (2) device-font fallback via the GFx font map; (3) ASCII-fold (ğ->g, ş->s, ı->i, İ->I) as a separate export step, keeping the true-Unicode translation JSON.

## Recommendation (k3)
1. Target the DE slot with text language Deutsch and English VO enabled.
2. Patch `*_LOC_DE.pcc` (431) and `Startup_DE.pcc` (decide on `Startup_GE` by test), update PCConsoleTOC.bin sizes, leave audio untouched.
3. Run the glyph test before t12 and bulk translation.
4. The 59 INT-only LOC files are not covered by a DE patch; the INT-to-DE mapping report (t20) decides.

Not done: string and word counts (waiting on the export tool).

## Addendum: INT packages also hold a German TLK
Toolsmith finding, relayed by the investigator: every `*_LOC_INT.pcc` also contains a `tlk_DE` export. With text language Deutsch and English VO enabled, the game may read German text from the INT packages instead of the `_LOC_DE.pcc` ones. If so, patching only the DE packages shows no Turkish.

t33 must check which package set the game reads. If it reads INT, apply the same `tlk_DE` / `tlk_M_DE` patch to staged copies of the `*_LOC_INT.pcc` files too. That would also cover the 59 INT-only LOC files.

## Offline glyph check (t33, toolsmith)
- Method: `Me1Tlk swf <pkg> <dir>` dumps the GFxMovieInfo RawData of a package; `tools/swf_fonts.py` parses the DefineFont2/3 code tables.
- Startup_INT/DE/GE/PLPC all carry the same `GUI_SF_Fonts_Fonts.swf` (identical MD5, 7,042,785 bytes). It holds three fonts: `Biomass_jpn` (7255 glyphs), `Tigensu_jpn` (7345), `Myriad Pro+DFGGothicP-W5` (7840).
- All three contain every needed code point: Ğ ğ İ ı Ş ş Ç ç Ö ö Ü ü (parser sanity: Hiragana present, Thai absent). `gfxfontlib.swf` in Launcher\Content\fonts also has them.
- Verdict: glyphs very likely render without any font work (fallback 0). Fallbacks 1-3 above stay as plan B; the in-game test below still confirms the language slot, which package set is read, and TOC acceptance.

## In-game test set (glyphtest/)
- Staging (not committed, 91 MB, rebuild with the commands in BUILD.md): `glyphtest/staging/CookedPCConsole/{Startup_DE,Startup_GE,BIOA_PRO10_00_DSG_LOC_DE,BIOA_PRO10_00_DSG_LOC_INT}.pcc` + `glyphtest/staging/PCConsoleTOC.bin` (4 sizes updated).
- Startup_DE and Startup_GE: menu words (Options, Continue, Cancel, Back, Yes, No, Exit, Load/Save Game, Resume, Accept, Credits, Quit; 140 ids) become `TR <Turkish word> ĞğİıŞşÇçÖöÜü`. BIOA_PRO10_00_DSG: all 52 strings become `TR ĞğİıŞşÇçÖöÜü | Merhaba komutan, güvenli bölgeye çıkın. <English start>`; mirrored into tlk_DE of the LOC_INT package.
