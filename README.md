# Stackland Access

Screen reader accessibility mod for **Stacklands**, made for blind players. Everything goes through your screen reader (NVDA, JAWS… via Tolk, with Windows SAPI as a fallback) and the keyboard.
The mod speaks the game's language: English, French, Dutch, German, Italian, Spanish, Polish, Brazilian Portuguese, Japanese, Korean, Chinese (Simplified and Traditional).

*[Version française](README.fr.md)*

## Installation

- **Steam Workshop**: subscribe to "Stackland Access" and start the game.
- **Manual**: download `StacklandAccess-x.y.z.zip` from the GitHub releases and extract it into
  `%USERPROFILE%\AppData\LocalLow\sokpop\Stacklands\Mods\` (you should end up with `Mods\StacklandAccess\StacklandAccess.dll`).

When the game starts you hear "Stackland Access ready. F1 for help."

## Menus

| Key | Action |
|---|---|
| Up / Down, Tab | previous / next item |
| Home / End | first / last item |
| Enter | activate (button, checkbox, text field) |
| Left / Right | adjust a slider (volume…) or a choice |
| I | description of the item (tooltip, encyclopedia card) |
| R | read all text on screen |
| Escape | back (handled by the game) |

Dialogs, screens and cutscenes (end of moon, events) are read automatically. In a cutscene, Enter or Space continues.

## Board

Everything on the board (card stacks, packs, buy boxes, sell box) is sorted into categories:
All, People, Food, Resources, Buildings, Enemies, Animals, Ideas, Locations, Equipment, Shop, Other.

| Key | Action |
|---|---|
| Left / Right | previous / next stack |
| Page Up / Page Down | previous / next category; each category remembers the last stack visited |
| Home / End | first / last stack |
| Up / Down | cards of the stack, bottom to top |
| Enter | pick up the card (and those above), then Enter on the target: a stack, the sell box, a pack to buy |
| Enter on a pack | open it (one card per press) |
| Enter on a pack to buy / a travelling merchant | pay automatically with your coins |
| Backspace | take the selected card (and those above) out of the stack; while holding: drop it alone |
| Escape | cancel the pick-up |
| Delete (twice) | sell the card and those above |
| C | click the card (e.g. take coins out of a chest) |
| I | card details as a list: sell price, health, combat, effects, equipment, current action, description. Right arrow: inventory |
| P / B / H / F / G / X / A / O / K / U / J / Y | jump to people, shop, buildings, food, resources, enemies, animals, equipment, ideas, locations, other, all; press again for the next one |
| M | the card you are holding |
| Ctrl+P | villagers and what they are doing, idle first |
| Ctrl+Enter | move the stack on the board with the arrows (I: what is around), Enter or Escape to finish |
| F5 | merge identical resources and food into full stacks |
| R | resources: coins, food, cards… |
| T | moon, time left, speed |
| Q | quests |
| L | recipes, by pages: possible now, production buildings, then one page per idea group; Enter sets the recipe up, I describes the result |
| N | message history |
| F1 | help |
| F2 | repeat last message |
| F3 | new-card announcements on / off |
| F4 | automatic stacking on / off (hand-made stacks are never touched) |
| - / + (or numpad) | slower / faster: pause, 0.5, 1, 2, 5 |
| Numpad 0 to 4 | pause, 0.5, 1, 2, 5 directly (NumLock on) |

Letters follow the characters printed on your keyboard (AZERTY works).

In lists (details, recipes, quests, history, help): Up / Down to browse, Left / Right to change page, Enter to use, I for info, Escape to close.

## Automatic announcements

New cards, enemies, new ideas, combat (start, each hit with remaining health, end), deaths, a short rising chime when a villager becomes free (`Sounds/villager_free.wav`, you can replace it with any WAV file), the strange portal, recipes starting and finishing, status effects, quests completed, speed changes, end-of-moon warning, card limit exceeded, cutscene texts.

Settings are in **Options > Mods > Stackland Access**.

## Translations

The mod's texts are in `StacklandAccess_strings.tsv`, one column per game language. Languages other than English and French were translated with AI: corrections are welcome (edit the cell and open a pull request).

## Building from source

Requires the .NET SDK. Set the game folder in `StacklandAccess.csproj.user`:

```xml
<Project><PropertyGroup><stacklands>C:\Program Files (x86)\Steam\steamapps\common\Stacklands</stacklands></PropertyGroup></Project>
```

Then `dotnet build -c Release`. The build installs the mod into the game's Mods folder. `pwsh tools/package.ps1` makes the release zip.

The decompiled game code is never included in this repository (modding wiki rule).

## License

Mod code: MIT (see `LICENSE`). Bundled third-party files: see `THIRD_PARTY.md`.
