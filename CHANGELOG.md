# Changelog

## 0.5.0
- The "villager free" beep is replaced by a short rising "ready" chime, which is easier to understand. It is read from `Sounds/villager_free.wav` (Kenney, CC0) and can be replaced with any WAV file. A built-in fallback sound plays if the file is missing.
- The setting is now called "sound when a villager becomes free".
- Mod upload screen: Workshop tags are announced as "selected" or "not selected".
- Release preparation: English README (French version in `README.fr.md`), MIT license, third-party licenses, release zip script.

## 0.4.1
- Recipes: "missing: Villager" instead of another villager type (dog…), and "(busy)" when the card exists but is already working.
- Distinctive beep (two rising notes) when a villager finishes their work and becomes free (can be turned off in the mod options).
- Strange portal: announced when it appears, listed under Locations (U), with the number of enemies it will still spawn; place villagers on it to travel to the Dark Forest.

## 0.4.0
- Game speed on the numeric keypad: 0 pause, 1 to 4 for 0.5, 1, 2, 5 (in addition to minus and plus).
- Recipes (L): cards already used by another recipe are never taken; one page per idea group (possible ones first, then alphabetical); new "Production buildings" page (sawmill, brickyard, composter, farmland): Enter places the available cards on them.
- The shop keeps the game's order.
- F5: merges identical resources and food into full stacks.
- Ctrl+P: list of villagers and what they are doing (idle first); Enter jumps to the villager.
- Ctrl+Enter: move a stack on the board with the arrow keys, announcing what is around.

## 0.3.0
- Backspace without holding anything: takes the selected card and those above it out of the stack (on the bottom card: takes it out alone).
- All game languages: Dutch, German, Italian, Spanish, Polish, Brazilian Portuguese, Japanese, Korean, Chinese (Simplified and Traditional), in addition to English and French. Translations made with AI, to be reviewed by native speakers.

## 0.2.0
- Reliability: hand-made stacks are saved in the save file; mod errors are written to the log (Player.log) and announced once; at startup the mod checks that the game internals it uses still exist; recipes: several stacking orders are tried, unavailable ideas or ideas at their maximum on the board are reported, warning if the game does not accept the stack.
- Listening: instructions given only the first 3 times, important announcements repeated if a key press cut them off, fixed plurals, shorter stack summaries ("Villager, 3 Wood"), stacks sorted by name in each category (configurable).
- Performance: board list computed once per frame, lighter effect scan.
- Project: texts moved to `StacklandAccess_strings.tsv` (translatable), board code split into two files, Workshop icon, version 0.2.0.

## 0.1.0
- First version: menus, board browsed by category, picking up and dropping cards, buying and selling, recipes, inventory, event announcements, combat, shortcuts.
