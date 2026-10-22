# Stackland Access

**Stackland Access** makes [Stacklands](https://store.steampowered.com/app/1948280/Stacklands/) playable for blind and visually impaired players.
You play with the keyboard only, and everything is spoken by your screen reader (NVDA, JAWS and others). If no screen reader is running, the Windows voice is used.

*[Version française](README.fr.md)*

- **Steam Workshop:** https://steamcommunity.com/sharedfiles/filedetails/?id=3813252816
- **Card list website:** https://kaminosekai54.github.io/StacklandAccess/ (every card, recipe, pack and drop, searchable, screen reader friendly)
- **Current version:** 0.6.0 (see [CHANGELOG.md](CHANGELOG.md))

## Project status

- The base game is fully playable: menus, board, buying and selling, recipes, combat, end of moon.
- **Multiplayer:** works with the [Stacklands Together](https://steamcommunity.com/sharedfiles/filedetails/?id=3778278988) mod (see [Multiplayer](#multiplayer)). Not yet tested in a real session.
- **DLC content (for example Cities) has not been tested yet.** It may partly work, but expect missing announcements or problems. Feedback is welcome.
- English and French texts are written by hand. The other languages were translated with AI and may contain mistakes.

## Installation

**With Steam (recommended):** open the [Workshop page](https://steamcommunity.com/sharedfiles/filedetails/?id=3813252816), click Subscribe, then start the game.

**By hand:**
1. Download `StacklandAccess-x.y.z.zip` from the GitHub Releases page.
2. Extract it into `%USERPROFILE%\AppData\LocalLow\sokpop\Stacklands\Mods\`.
3. Check that you now have the file `Mods\StacklandAccess\StacklandAccess.dll`.

When the game starts, you hear "Stackland Access ready. F1 for help."

The first time, a window of the game asks whether you want the narration. "No" turns off everything the mod says and plays (for a sighted player); Ctrl+F2 turns it back on.

## First steps

1. In the main menu, use the Up and Down arrows and Enter to start a game.
2. On the board, press Left and Right to go through the cards, and Page Up / Page Down to change category.
3. Press Enter on a card to pick it up, go to another card, and press Enter again to drop it there. This is how you make everything in Stacklands.
4. Press L to see the recipes you can make right now, and Enter to let the mod set one up for you.
5. Press F1 at any time to hear the list of keys.

## Controls

Letters follow the characters printed on your keyboard, so they also work on AZERTY keyboards.

Every key below can be changed in the options (F7), except the arrows, Enter, Escape, Home, End, Page Up, Page Down and Tab. This page lists the default keys.

### Everywhere

- **F1**: help (list of keys).
- **F2**: repeat the last message.
- **F3**: turn announcements of new cards on or off.
- **F4**: turn automatic stacking on or off.
- **F6**: create a web page with every card of the game and open it in your browser (see [Card list](#card-list)).
- **F7**: options of the mod (features on or off, keys).
- **Ctrl+F2**: narration on or off.
- **F8**: multiplayer panel (only with the Stacklands Together mod).

### Menus

- **Up / Down arrows** or **Tab**: previous / next item.
- **Home / End**: first / last item.
- **Enter**: activate the button, checkbox or text field.
- **Left / Right arrows**: change a slider (volume…) or a choice.
- **I**: description of the item.
- **R**: read all the text on the screen.
- **Escape**: go back.
- **Enter** or **Space** in a cutscene: continue.

### Board: moving around

- **Left / Right arrows**: previous / next stack in the current category.
- **Page Up / Page Down**: previous / next category. Each category remembers the last stack you visited.
- **Home / End**: first / last stack of the category.
- **Up / Down arrows**: cards inside the stack, from bottom to top.

### Board: jump to a category

Press the letter again to go to the next stack of that category.

- **P**: people and villagers.
- **B**: shop (packs, buy boxes, sell box, travelling merchants).
- **H**: buildings.
- **F**: food.
- **G**: resources.
- **X**: enemies.
- **A**: animals.
- **O**: equipment.
- **K**: ideas.
- **U**: locations (including the strange portal).
- **J**: other cards.
- **Y**: everything.
- **M**: the card you are holding.

### Board: cards

- **Enter**: pick up the selected card and the cards above it.
- **Enter** while holding cards: drop them on the selected stack, the sell box, a pack to buy or a villager (to equip an item).
- **Escape** while holding cards: cancel.
- **Backspace**: take the selected card and the cards above it out of the stack. On the bottom card, it takes out only that card.
- **Backspace** while holding cards: put them down alone on the board.
- **Delete**, twice: sell the selected card and the cards above it.
- **C**: click the card (for example, take coins out of a chest).
- **Ctrl+Enter**: move the stack on the board with the arrows.
- **I** while moving a stack: what is around it.
- **E** while moving a stack: where the cards linked to it are.
- **Enter** or **Escape** while moving a stack: stop moving.
- **F5**: regroup all identical idle cards into full stacks. Cards used by a craft in progress, and cards that make something when stacked together, are left alone.
- **Ctrl+F5**: same as F5, only for the category you are browsing.

### Board: shop

- **Enter** on a pack: open it, one card at a time.
- **Enter** on a pack to buy: pay automatically with your coins.
- **Enter** on a travelling merchant: pay automatically.

### Board: information

- **I**: details of the card as a list (sell price, health, combat, effects, current action, description).
- **Right arrow** in the details of a villager: their inventory and equipment.
- **R**: resources (coins, food, number of cards…).
- **T**: moon, time left and game speed.
- **Q**: list of quests.
- **L**: list of recipes.
- **N**: history of messages.
- **Ctrl+P**: list of villagers and what they are doing, idle villagers first.
- **E**: connections of the stack (energy cables, sewer pipes, transport arrows): what they give it and where the linked cards are. Enter on a free connector lists the cards it can be linked to, nearest first; Enter on a linked one disconnects it. Right arrow: all connections on the board, then the cards with an energy or sewer problem.

### Lists (details, recipes, quests, history, villagers, help)

- **Up / Down arrows**: previous / next line.
- **Left / Right arrows**: previous / next page.
- **Enter**: use the line (set up a recipe, go to a villager, take an item…).
- **I**: more information about the line.
- **Escape**: close the list.

The recipe list (L) has several pages:
1. Recipes possible now.
2. Production buildings (sawmill, brickyard, composter, farmland). Enter puts the available cards on the building.
3. One page per group of ideas, with the possible ones first.

The mod never takes cards that are already used by another recipe.

### Game speed

- **Minus / Plus** (also on the numeric keypad): slower / faster (pause, 0.5, 1, 2, 5).
- **Numeric keypad 0**: pause.
- **Numeric keypad 1 to 4**: speed 0.5, 1, 2 or 5.
- **Space**, **Tab**, **1**, **2**, **3**: the game's own pause and speed keys.

With NVDA, keep Num Lock on to use the numeric keypad.

### Controller

The mod can be played with a controller (option "controller support", on by default; the game's own controller controls are then turned off so the two do not fight).

- Cross or left stick: arrows. **A**: Enter. **B**: Escape. **X**: information (I). **Y**: recipes (L).
- **LB / RB**: previous or next category, or page in a list. **Start**: options. **Select**: help. **L3**: repeat. **R3**: resources (in menus: read the screen).
- Hold **LT**: A moves the stack (Ctrl+Enter), X clicks, Y quests, B puts the card down alone, Up time, Down history, Left villagers, Right connections, LB / RB first or last.
- Hold **RT**: A goes to the held card, B sells, X regroups identical cards, Y regroups the category, Left / Right slower or faster, Down pause, Up normal speed, Start narration on or off.

## Card list

Press **F6** (in a menu or on the board) to create `Stacklands - cards.html` in your Documents folder and open it in your browser. The page is made from your own game, in its language:

- a search field (card name, description, recipe or ingredient) and a category filter;
- a grid of cards drawn like in the game (colours and icon); each card gives its information and how to get it (recipe, pack or drop), read by the screen reader with the card name or shown when the mouse is over it;
- Enter or a click opens all its details: category, sell value, food, combat, description, how to make it, packs, where it drops, the recipes that use it, what it gives;
- "Only cards I have found" (checked by default) hides the cards and ideas you have not discovered yet.

- packs (category Packs): price, where they are sold, how to unlock them, average value, and every card they can give with its chance per draw;
- filters: category, card type, content (base game, island, dark forest, spirits, Cities…), how to get it (recipe, pack, drop); sort by name, sell value, food or attack.

Press F6 again after discovering new cards to update it.

Clicking a card opens its details on the left and its tech tree on the right: the card in the middle, what it is made with above, what is made with it below. A solid line (straight down) means the card is used up as a material; a dashed line (smaller cards on a diagonal) means it is used as a tool and kept, like a stove. Hovering or focusing a card of the tree gives its information, a click opens its own tree.

The "View" menu of the page also shows two tables: every type of villager, and every creature and enemy, with health, attack, defence, combat type, attack speed, hit chance, special effects (poison, stun…), food per moon and equipment.

**Shift+F6** makes the same page as a public website, in every language of the game and without your save data, in `Documents\Stacklands cards site`. `tools\update-site.ps1` copies it into `docs/` for GitHub Pages.

## Multiplayer

With the [Stacklands Together](https://steamcommunity.com/sharedfiles/filedetails/?id=3778278988) mod installed, press **F8** (or choose Multiplayer in the main menu) to open the spoken multiplayer panel. Left and Right arrows change page:

- **Session**: connection status, your player name, connected players, leave the session.
- **Steam**: lobby name, password, public or friends only, create a lobby, search for lobbies and join one, invite friends.
- **Direct IP**: host address, port, host a game, join.
- **Tools**: sync the world, shared progress, reset ideas or quests (host only).

On a line you can edit, press **Enter**, type the text (each character is spoken), then **Enter** to confirm or **Escape** to cancel. **Backspace** erases, **Up arrow** reads the text again.

Connect from the main menu; the host then starts or loads the world. In a session:
- the host runs the game: as a client, everything you do is sent to the host, like a mouse drag;
- only the host can change the speed or pause;
- automatic stacking (F4) is paused so it never moves other players' cards (F5 still works);
- players joining or leaving and the connection status are announced;
- as the host, you also hear what the other players do, with their name: packs bought and opened, cards sold, cards put on a pile, crafts started and finished. This can be turned off in the settings.

## What the mod announces automatically

- New cards, grouped together.
- Enemies, combat start, each hit with remaining health, combat end.
- Deaths and lost cards.
- New ideas and completed quests.
- Recipes starting (with their duration) and finishing.
- A short rising chime when a villager becomes free.
- The strange portal when it appears.
- Status effects (poisoned, sick, on fire…).
- Warning before the end of the moon, too many cards.
- Speed changes, cutscene texts, dialogs and popups.

## Settings

Press **F7** for the mod's options, anywhere in the game:
- **Features**: Enter turns each one on or off (list below), or changes the end-of-moon warning delay.
- **Keys** (three pages: everywhere, board, lists and menus): Enter on an action, then press its new key, with Ctrl or Shift if you want; Escape cancels. If the key was already used, the other action takes the old key. I gives the default key. The last line puts back all the default keys (press Enter twice).

The keys are saved in `StacklandAccess_keys.txt`, next to the game's saves.

The on/off settings are also in **Options > Mods > Stackland Access**:
- narration (speech and sounds of the mod);
- the mod's menus shown on screen;
- announcements of new cards;
- combat announcements;
- each combat hit;
- recipe announcements;
- the end-of-moon warning delay;
- the camera following your selection;
- automatic stacking;
- stacks sorted by name;
- the "villager free" sound;
- other players' actions (multiplayer host).

The mod's menus (lists, options, text fields) are also drawn on screen in the game's style, with buttons you can click, for the people watching. The game keeps running while they are open.

You can replace the sound with your own WAV file: `Mods\StacklandAccess\Sounds\villager_free.wav`.

If the game option "auto pause when using keyboard and mouse" (Options > Accessibility) is on, the game pauses while you hold a card.

## Translations

All the mod's texts are in `StacklandAccess_strings.tsv`, one column per language. To fix a translation, edit the cell and open a pull request.

## Building from source

1. Install the .NET SDK.
2. Create `StacklandAccess.csproj.user` with the game folder:
   ```xml
   <Project><PropertyGroup><stacklands>C:\Program Files (x86)\Steam\steamapps\common\Stacklands</stacklands></PropertyGroup></Project>
   ```
3. Run `dotnet build`. The mod is installed into the game's Mods folder.
4. Run `powershell -ExecutionPolicy Bypass -File tools\package.ps1` to make the release zip.

The decompiled game code is never included in this repository (modding wiki rule).

## License

The mod code is under the MIT license (see [LICENSE](LICENSE)). Bundled third-party files are listed in [THIRD_PARTY.md](THIRD_PARTY.md).
