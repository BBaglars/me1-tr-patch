# Missing-text inventory (t200)

Method: three read-only scans over all 4946 `.pcc` under `BioGame/CookedPCConsole` (`Me1Tlk scan | tlks | props`, decompressed exports; nothing written to the game directory).

## Where "Reverse and hold at 38.5." lives (k1)
- Package `BIOA_NOR10_04_DS1.pcc` (Normandy comm room), export `nor10_comm_room2_d.tlk`, string id 149244. It is a plain `BioTlkFile` (Huffman-coded, so a raw byte search finds nothing; `scan` over UTF-16/ASCII gave 0 hits in 4946 packages).
- Cause: our exporter only read `*_LOC_INT.pcc` and `Startup_INT.pcc`. 32 further packages (cinematic `_CIN` and dialogue `_DSG`/`_DS1` packages without `_LOC`) carry their own TLK set, each with the same 24 language exports (`tlk`, `tlk_M`, `tlk_DE`, ... `tlk_M_RU`). The INT slot reads the plain `tlk` / `tlk_M`.

## Inventory by source type
| Source | Count | Player-visible | Covered before | Plan |
|---|---|---|---|---|
| TLK in `*_LOC_INT.pcc` + `Startup_INT` | 490 + 1 packages, 58,682 entries | yes | yes | unchanged |
| TLK in non-LOC packages (32 packages: 22 CIN, 10 DSG/DS1) | 1,528 entries (89 texts not already in data/en, 709 words; the rest duplicate existing English texts) | yes (subtitles of cinematics, e.g. GLO00_A_Opening_Flyby_CIN 44, NOR10_04A_DSG 177, WAR30_02I_CIN 181, ICE60_14_CIN 115) | no | DONE in Me1Tlk: `--extra data/extra-tlk-packages.txt` (export and import; import falls back to `<stem>.pcc`). Exported to `data/en/<package>.json` |
| `BioSeqAct_ShowMessageBox.sMessageLines` (StrProperty) | 23 lines, BIOA_LAV70_00_DSG | probably cut/unused content ("The party finds a way to reactivate the mining laser...") | no | low priority; exportable as property strings, would need a `prop` key kind |
| `SFXSFHandler_PhotoMode` text options/pages/sliders (StrProperty arrays) | ~80 values in SFXGame.pcc (Camera Mode, Free, Tilt, Blur Strength ...) | yes (Legendary photo mode) | no | property-string export/import; candidate for t201 follow-up |
| Kismet comments / LinkDesc / VariableLabel / TrackTitle / ActorGameName / tags / anim and sound names | millions of occurrences | no (developer data, internal ids) | n/a | leave |
| `Coalesced_INT.bin` (config; 26k UTF-16 strings) | 26,031 | no (GPU names, editor commands, key bindings) | n/a | leave |
| GFx/SWF movies (GUI_SF_*, fonts) | not parsed | partly (text baked into SWF, fonts) | no | out of scope here; list in final remainder |
| Audio (VO, Wwise/ISACT) | - | spoken English by design (INT slot) | n/a | untranslatable |
| 2DA (`Bio2DA` exports) | not scanned for strings | names/ints; display text goes through TLK ids | n/a | no text expected |

## Result after the fix (t204)
- Staging rebuilt (`scripts/build-staging.ps1 -Json data/tr`): 523 patched packages (491 old + 32 non-LOC), TOC sizes updated for all 523 (stagedFilesWithoutEntry=0), 631 MB. Written only to `staging\`; the game directory was not touched by this cabal.
- Verification: `Me1Tlk export --game staging\CookedPCConsole --extra data/extra-tlk-packages.txt` re-read 60,210 strings; `tools/verify.py`: mismatches 0, missing 0 (includes tlk and tlk_M of the 32 non-LOC packages; maleDiff 0). The Anderson line now reads "Geri git ve 38,5'te sabit tut.".
- Install: the patron runs `install.ps1` as a dry-run first; 32 additional files are new in the set (backed up on install by the manifest).

## Remainder that stays English (k5)
- `SFXSFHandler_PhotoMode` strings in SFXGame.pcc (~80 values: Camera Mode, Free, Tilt, Blur Strength ...): player-visible in photo mode, StrProperty arrays, not exported. Needs a property-string key kind in Me1Tlk (SFXGame.pcc is shared engine code, large); not done.
- `BioSeqAct_ShowMessageBox` lines (23, BIOA_LAV70_00_DSG): probably unreachable placeholder content; not exported.
- Text baked into GFx/SWF movies (GUI_SF_*), the executable/launcher, and all voice-over audio: not reachable with the TLK tooling; INT slot keeps English VO by design.
- Developer-only strings (Kismet labels, comments, tags, Coalesced config) are not player-visible.
