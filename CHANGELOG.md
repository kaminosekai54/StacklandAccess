# Changelog

## 0.7.0 – Options, key remapping and visual menus

- F7 opens the mod's options, anywhere in the game: turn each feature on or off, and change every key (except the navigation keys). A key already used goes to the other action. Keys are saved in `StacklandAccess_keys.txt`, next to the game's saves.
- The mod's menus (lists, options, text fields of the multiplayer panel) are also drawn on screen with the game's own panel, fonts and buttons, for sighted people watching; the buttons can be clicked. The game is not paused. Can be turned off in the options.
- Production buildings page (L, second page) rebuilt: one line per building that can make something right now with your cards, and nothing else (no soil on a stove, no empty chest on the university). With several choices (a stove: omelette, quiche…), Enter opens the list of choices; Escape goes back.
- Recipes made without the idea (by chance or by trying) are now known: they appear in the recipes (L) and on the card page (F6). The mod remembers every recipe you finish (`StacklandAccess_crafted.txt`); for older crafts, a recipe counts as known when you have found its result and only that recipe gives it.
- Controller support: the whole mod can be played with a controller (directions, A, B, X, Y, shoulders, Start, Select, sticks), with two more sets of actions while holding LT or RT. The game's own controller controls are turned off while it is on (option).
- Card page: "View" menu with a table of every villager type and a table of every creature and enemy: health, attack, defence, combat type, attack speed, hit chance, special effects, food per moon, equipment. The details of a card show the same.
- First launch: a window of the game asks whether to turn on the narration. "No" turns off everything the mod says and plays; Ctrl+F2 (or the options) turns it back on.

## 0.6.0 – Multiplayer with Stacklands Together

Compatibility with the [Stacklands Together](https://steamcommunity.com/sharedfiles/filedetails/?id=3778278988) multiplayer mod (tested against version 0.7.32, without a real game session yet).

- F8, or the "Multiplayer" button of the main menu, opens a spoken multiplayer panel instead of Together's window, which screen readers cannot read:
  - Session: status, your player name, connected players, leave.
  - Steam: lobby name, password, public or friends only, create a lobby, search for lobbies and join them, invite friends.
  - Direct IP: address, port, host, join.
  - Tools: world sync, shared progress, reset ideas or quests (host only, with confirmation).
- Text fields of the panel are spoken as you type (Backspace erases, up arrow reads, Enter confirms, Escape cancels). Lobby passwords asked by a Steam invitation use the same field.
- Announced automatically: connection status, players joining and leaving, world loading.
- As the host, the other players' actions are announced with their name: packs bought and opened, cards sold, cards put on a pile, and the crafts they start and finish (setting "announce other players' actions").
- As a client, every action of the mod goes to the host the same way a mouse drag would: picking up and dropping, selling, buying packs, paying merchants, opening packs, clicking chests, recipes (L), production buildings, F5 regrouping, moving stacks with Ctrl+Enter, taking cards out of a pile.
- As a client, combat hits replayed by the host are announced, and buying a pack sends just enough money.
- Only the host can change the game speed; the speed keys say so on a client. Automatic stacking is paused during a session, so it never moves other players' cards.
- No flood of "new card" announcements while the host's world is copied to your game.
- Recipes: each card is checked by the game's own rules before it is put on the pile, also in single player.
- Production buildings page (L): gardens, farms and greenhouses (one line per plant you can grow, with the harvest and the time), fish traps, animal pens, breeding pens and hotpots. Enter puts the cards on the building.
- F5 now regroups every identical idle card (resources, food, fish, equipment), also out of mixed piles; cards of a craft in progress and cards that make something together are left alone. Ctrl+F5 does the same in the current category only.
- Production buildings page (L) also lists the buildings with a single use (university, academy, laboratory…) and the recipes that use a building on your board (temple, smithy, lighthouse…). It only offers what really produces something with the cards you have: no villager on a campfire, no recipe with missing cards, no building with nothing to put on it.
- L has an "All ideas and rumors" page again, and rumors you found are listed in their group (I reads them).
- The Industrial Revolution card (Cities DLC) is announced, listed in Locations (U), and its line explains to put a villager on it to go to the city.
- Card page: every card and every idea of the game (also the ones not discovered yet, and the installed DLCs); ideas are cards of the grid with their recipe.
- Card page: packs are in the grid too (category Packs): price and currency, where they are sold, how to unlock them, average value, and every card they can give with its chance per draw, limits and conditions (curses, cards to find first); each card lists its packs with its chance.
- Card page: cards are drawn like in the game, with the game's colour logic shown in a soft palette (each game colour gets its own palette colour) and their icon (extracted from the game; ideas use the game's idea icon); hovering or focusing a card gives its information, a click opens the full details with a larger card.
- Card page filters: card type, content (base game, island, dark forest, spirits, Cities…), how to get it; sort by sell value, food or attack. Shift+F6 makes a public website in every language (no save data), ready for GitHub Pages.
- F6: creates and opens a web page with every card of the game (searchable grid, how to get each card, recipes, packs, drops, recipes that use it), made from your own game and language; undiscovered cards hidden by default.
- E: connections of a stack (energy cables, sewer pipes, transport arrows), with what they give it and where the linked cards are; connect and disconnect from the list; pages with every connection and with the cards lacking energy or sewer. In move mode, E says where the linked cards are.
- Gardens, farms, greenhouses, mills, ovens, smelters and other production buildings are always in the Buildings category (H).

## 0.5.0 – First public release

First release of Stackland Access, an accessibility mod that makes Stacklands playable for blind and visually impaired players, with the keyboard and a screen reader.

### Speech
- Output through your screen reader (NVDA, JAWS and others, via Tolk). The Windows voice (SAPI) is used if no screen reader is running.
- Important announcements are repeated once if a key press cut them off.
- Instructions are given only the first 3 times, then the mod stays short.
- F2 repeats the last message, N opens the message history.

### Menus
- Every menu, option, dialog, popup and cutscene is read.
- Sliders and choices are announced with their name and value (volume, new game options…).
- The "update info" popup of the main menu and the key rebinding screen are read.
- Tooltips and encyclopedia cards are available with I, and R reads the whole screen.
- Workshop upload screen: tags are announced as "selected" or "not selected".

### Board
- Everything on the board is sorted into categories: people, food, resources, buildings, enemies, animals, ideas, locations, equipment, shop, other, all.
- Left and Right go through the stacks, Page Up and Page Down change category, Up and Down go through the cards of a stack.
- Each category remembers the last stack visited.
- One-key shortcuts to each category: P, B, H, F, G, X, A, O, K, U, J, Y. M goes to the card you are holding.
- Stacks are sorted by name (configurable); the shop keeps the game's order.
- Ctrl+Enter moves a stack on the board with the arrows and describes what is around it.
- The camera follows the selection (configurable).

### Cards
- Enter picks up a card and the cards above it; Enter again drops them on a stack, the sell box, a pack to buy or a villager (to equip an item).
- Backspace takes the selected card and the cards above it out of the stack; while holding cards, it puts them down alone.
- Delete, twice, sells cards. C clicks a card (for example, to take coins out of a chest).
- Enter on a pack opens it one card at a time.
- Enter on a pack to buy or a travelling merchant pays automatically with your coins, and explains why when it cannot.
- I gives the details of a card as a list: sell price, stack value, health, combat stats, effects, equipment, current action, description, contents of chests.
- Right arrow in a villager's details opens their inventory, to take off or move equipment.

### Recipes (L)
- Page "possible now": Enter sets the recipe up for you, using only cards that are free.
- Page "production buildings" (sawmill, brickyard, composter, farmland): Enter puts the available cards on the building.
- One page per group of ideas, possible ideas first, then alphabetical order.
- Missing cards are named clearly, and cards that exist but are busy are marked "(busy)".
- Ideas that cannot be made yet, or are already at their maximum on the board, are reported.

### Stacking
- Identical idle resources and food are stacked automatically (F4 to turn it on or off).
- Stacks you build yourself are never split; new cards join them. They are kept in the save file.
- F5 regroups all identical resources and food into full stacks.

### Announcements
- New cards (grouped), enemies, new ideas, completed quests.
- Combat: start, each hit with remaining health, end, deaths and lost cards.
- Recipes starting (with their duration) and finishing.
- A short rising chime when a villager becomes free (Kenney, CC0). The sound file `Sounds/villager_free.wav` can be replaced.
- Ctrl+P lists villagers and what they are doing, idle villagers first.
- The strange portal when it appears, with the number of enemies it will still bring.
- Status effects, view changes, speed changes, warning before the end of the moon, too many cards.

### Time and status
- R: resources (coins, food, number of cards…). T: moon, time left, speed. Q: quests.
- Minus and Plus (also on the numeric keypad) change speed: pause, 0.5, 1, 2, 5. Numeric keypad 0 to 4 sets it directly.
- Follows the game's "auto pause when using keyboard and mouse" option while you hold a card.

### Keyboard
- Letters follow the characters printed on your keyboard, so AZERTY and other layouts work.
- Game keys that clash with the mod's keys are disabled automatically.

### Languages
- English, French, Dutch, German, Italian, Spanish, Polish, Brazilian Portuguese, Japanese, Korean, Chinese (Simplified and Traditional), following the game's language.
- Languages other than English and French were translated with AI; corrections are welcome in `StacklandAccess_strings.tsv`.

### Settings
- In Options > Mods > Stackland Access: new card announcements, combat, each hit, recipes, end-of-moon warning delay, camera follow, automatic stacking, sort by name, "villager free" sound.

### Known limitations
- DLC content (for example Cities) has not been tested yet.
- Mod errors are written to `Player.log` and announced once. At startup, the mod checks that the game parts it relies on still exist.
