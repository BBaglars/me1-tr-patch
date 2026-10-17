# English leftovers in data/tr (independent scan)

Scan of all 491 files / 58,682 entries in `data/tr` (tr empty: 0). Heuristics: tr identical to en (2,892 entries, 1,675 distinct), English function-word density, ASCII-only long lines, U+FFFD.

## Fixed (missed translations)
- Startup 154014, 154016, 154018 (planet descriptions Yunthorl, Antitarra, Trelyn) were copied verbatim; now translated (chunk `ui/012`, ids u20069/u20071/u20073).
- Typo "O tare" to "O taret" (turret) in 7 JUG80 files, chunk `b/028`.

## Intentional identical-to-English (kept, matches glossary: names stay unchanged)
- Person, studio and company names, credits blocks (Startup 163xxx to 183xxx, 781xxx), NPC/ship/place names, UI path macros (`${...}`), URLs, `%1 GB`, `<CUSTOM#>` placeholders, resolutions, "Mass Effect".
- Interjections: "Hmm." x120, "Oh.", "Hmph.", "Rrr...", "Argh!" etc.
- Item/model brand names: Colossus, Titan, Hydra, Predator, Solaris Amp, Gladiator/Duelist/Assassin/Mercenary (amp and armor tiers, 42 + 10 entries), Savant, Prodigy, Cipher Tool, Geth Prime, Krogan Hound.
- Keyboard labels: Caps Lock, Num Lock, Page Up/Down, Numpad N (printed on Turkish keyboards the same way); "Normal", "Normal (4:3)" are valid Turkish.

## Judgment calls left for the patron (not changed)
- Item tier names Gladiator/Duelist/Assassin/Mercenary/Savant/Prodigy and "Terminal" (5): kept English as product names; could become Gladyatör/Düellocu/Suikastçı/Paralı Asker/Âlim/Dahi.
- Debug/test strings never shown to players: "Test", "EASTEURO STRING", "zTest ...", "Sweetness ...", "Major Quest"/"MainPlot*" skill keys (Startup 163286-163295), `You have completed NNNquest`.
- Legal text (EA copyright, Epic/NVIDIA notices, 177382, 183996, 800245) is English by design.

## Clean
No tr entry contains U+FFFD. No remaining sentence-length line has English function words other than inside quoted proper names ("Bring Down the Sky", "League of One", "Synthetic Insights").
