using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StacklandAccessNS
{
    /// <summary>
    /// Keyboard exploration and manipulation of the board: categories of stacks, cards inside a stack,
    /// picking a card up and dropping it on another stack, the sell box or a pack to buy.
    /// </summary>
    public partial class BoardNavigator
    {
        public enum Category { All, People, Food, Resources, Buildings, Enemies, Animals, Ideas, Locations, Equipment, Shop, Other }

        private static readonly Category[] Order = (Category[])Enum.GetValues(typeof(Category));

        private Category category = Category.All;
        public Category CurrentCategory => category;
        private Draggable current;
        private int stackIndex;
        private int lastListIndex;

        /// <summary>Card picked up by the player (nothing moves in the game until it is dropped).</summary>
        public GameCard Held { get; private set; }

        private GameCard sellConfirmCard;
        private float sellConfirmTime;

        /// <summary>Stack the player just dropped something on, used to announce the recipe that starts.</summary>
        public static GameCard LastDropRoot;
        public static float LastDropTime = -100f;

        public bool IsHolding => Held != null && !Held.Destroyed;

        public void Reset()
        {
            Held = null;
            current = null;
            stackIndex = 0;
            category = Category.All;
        }

        // ------------------------------------------------------------------ listing

        private static bool OnCurrentBoard(Draggable d)
        {
            if (d == null || !d.gameObject.activeInHierarchy) return false;
            var board = d.MyBoard;
            return board != null && board.IsCurrent;
        }

        private static int cacheFrame = -1;
        private static List<Draggable> cache;

        /// <summary>Forget the cached list (after the mod moved cards in this frame).</summary>
        public static void InvalidateCache() => cacheFrame = -1;

        /// <summary>Every navigable object of the current board, computed once per frame.</summary>
        public static List<Draggable> AllObjects()
        {
            if (cache != null && cacheFrame == Time.frameCount) return new List<Draggable>(cache);
            cache = ComputeAllObjects();
            cacheFrame = Time.frameCount;
            return new List<Draggable>(cache);
        }

        private static List<Draggable> ComputeAllObjects()
        {
            var wm = WorldManager.instance;
            var list = new List<Draggable>();
            if (wm == null || wm.CurrentBoard == null) return list;
            foreach (var c in wm.AllCards)
            {
                if (c == null || c.Destroyed || c.CardData == null) continue;
                if (c.Parent != null || c.IsEquipped || c.IsWorking || c.EquipmentHolder != null) continue;
                if (!OnCurrentBoard(c)) continue;
                list.Add(c);
            }
            foreach (var b in wm.AllBoosters)
            {
                if (OnCurrentBoard(b)) list.Add(b);
            }
            foreach (var t in wm.CardTargets)
            {
                if (OnCurrentBoard(t)) list.Add(t);
            }
            float gh = Mathf.Max(0.3f, wm.GridHeight);
            if (AccessController.SortByName)
            {
                // By name, so identical cards follow each other; then by place on the board.
                return list
                    .Select(d => new { d, name = CardInfo.Name(d) })
                    .OrderBy(x => x.name, System.StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(x => Mathf.RoundToInt(-x.d.transform.position.z / gh))
                    .ThenBy(x => x.d.transform.position.x)
                    .Select(x => x.d)
                    .ToList();
            }
            return list
                .OrderBy(d => Mathf.RoundToInt(-d.transform.position.z / gh))
                .ThenBy(d => d.transform.position.x)
                .ToList();
        }

        public static Category CategoryOf(Draggable d)
        {
            if (!(d is GameCard gc)) return Category.Shop;
            return CategoryOf(gc.CardData);
        }

        /// <summary>Category of a kind of card (also used for cards that are not on the board).</summary>
        public static Category CategoryOf(CardData cd)
        {
            if (cd == null) return Category.Other;
            if (IsMerchant(cd)) return Category.Shop;
            if (cd is Portal || cd is IndustrialRevolution || cd.Id == "event_industrial_revolution") return Category.Locations;
            if (cd is Enemy) return Category.Enemies;
            if (cd is Mob) return Category.Animals;
            // Production buildings, whatever card colour the game gives them.
            if (cd is Garden || cd is Greenhouse || cd is Farmland || cd is Sawmill || cd is Brickyard || cd is Composter
                || cd is FishTrap || cd is AnimalPen || cd is BreedingPen || cd is Smelter || cd is FlourMill
                || cd is Distillery || cd is Oven || cd is Barrel || cd.Id == "farm" || cd.Id == "garden" || cd.Id == "greenhouse")
                return Category.Buildings;
            switch (cd.MyCardType)
            {
                case CardType.Humans: return Category.People;
                case CardType.Food:
                case CardType.Fish: return Category.Food;
                case CardType.Resources: return Category.Resources;
                case CardType.Structures:
                case CardType.Landmarks: return Category.Buildings;
                case CardType.Ideas:
                case CardType.Rumors: return Category.Ideas;
                case CardType.Locations: return Category.Locations;
                case CardType.Equipable: return Category.Equipment;
                case CardType.Mobs: return Category.Animals;
                default: return Category.Other;
            }
        }

        private List<Draggable> ListFor(Category cat)
        {
            var all = AllObjects();
            if (cat == Category.All) return all;
            var list = all.Where(d => CategoryOf(d) == cat).ToList();
            if (cat == Category.Shop && AccessController.SortByName)
            {
                // The shop keeps the game's order (left to right, as on screen), more intuitive than by name.
                float gh = Mathf.Max(0.3f, WorldManager.instance.GridHeight);
                list = list.OrderBy(d => Mathf.RoundToInt(-d.transform.position.z / gh)).ThenBy(d => d.transform.position.x).ToList();
            }
            return list;
        }

        /// <summary>Cards you pay with coins: travelling cart, merchant of the greed world.</summary>
        public static bool IsMerchant(CardData cd) => cd is TravellingCart || cd is Merchant;

        public static string CategoryName(Category c) => Loc.T("cat_" + c.ToString().ToLowerInvariant());

        // ------------------------------------------------------------------ input

        public void HandleInput()
        {
            var wm = WorldManager.instance;
            if (wm == null || wm.CurrentBoard == null) return;

            if (Held != null && Held.Destroyed) Held = null;

            if (IsMoving) { HandleMoveMode(); return; }
            if (Keys.EnterDown && Keys.Ctrl) { StartMoveMode(); return; }

            if (Keys.Repeat(Key.RightArrow)) Move(+1);
            else if (Keys.Repeat(Key.LeftArrow)) Move(-1);
            else if (Keys.Down(Key.Home)) MoveTo(0);
            else if (Keys.Down(Key.End)) MoveTo(int.MaxValue);
            else if (Keys.Down(Key.PageDown)) ChangeCategory(+1);
            else if (Keys.Down(Key.PageUp)) ChangeCategory(-1);
            else if (Keys.Repeat(Key.UpArrow)) MoveInStack(+1);
            else if (Keys.Repeat(Key.DownArrow)) MoveInStack(-1);
            else if (Keys.EnterDown) Activate();
            else if (Bindings.Pressed("drop_alone")) DropAlone();
            else if (Keys.Down(Key.Escape) && IsHolding) { Held = null; Speech.Say(Loc.T("pick_cancel")); }
            else if (Bindings.Pressed("sell")) Sell();
            else if (Bindings.Pressed("click")) ClickCard();
            else if (Bindings.Pressed("details")) OpenDetails();
            else if (Bindings.Pressed("go_held")) GoToHeld();
            else if (Bindings.Pressed("connections"))
            {
                Sync();
                Connections.Open(List, this, current is GameCard cg ? cg.GetRootCard() : null);
            }
            else if (Bindings.Pressed("cat_people")) Jump(Category.People);
            else if (Bindings.Pressed("cat_shop")) Jump(Category.Shop);
            else if (Bindings.Pressed("cat_buildings")) Jump(Category.Buildings);
            else if (Bindings.Pressed("cat_food")) Jump(Category.Food);
            else if (Bindings.Pressed("cat_resources")) Jump(Category.Resources);
            else if (Bindings.Pressed("cat_enemies")) Jump(Category.Enemies);
            else if (Bindings.Pressed("cat_animals")) Jump(Category.Animals);
            else if (Bindings.Pressed("cat_equipment")) Jump(Category.Equipment);
            else if (Bindings.Pressed("cat_ideas")) Jump(Category.Ideas);
            else if (Bindings.Pressed("cat_locations")) Jump(Category.Locations);
            else if (Bindings.Pressed("cat_other")) Jump(Category.Other);
            else if (Bindings.Pressed("cat_all")) Jump(Category.All);
        }

        // ------------------------------------------------------------------ moving a stack on the board

        private Draggable moving;

        public bool IsMoving => moving != null;

        /// <summary>Ctrl+Enter: move the focused stack (or pack) on the board with the arrows.</summary>
        private void StartMoveMode()
        {
            Sync();
            if (current == null) { Speech.Say(Loc.T("not_now")); return; }
            if (!CanAct()) return;
            var target = current is GameCard gc ? (Draggable)gc.GetRootCard() : current;
            if (!(target is GameCard) && !(target is Boosterpack)) { Speech.Say(Loc.T("cant_pick", CardInfo.Name(target))); return; }
            if (!target.CanBeDragged()) { Speech.Say(Loc.T("cant_pick", CardInfo.Name(target))); return; }
            moving = target;
            Speech.Say(TextCleaner.Join(Loc.T("move_start", CardInfo.Name(target)), Loc.Hint("move_hint"), Surroundings(target)));
        }

        private void HandleMoveMode()
        {
            if (moving == null || (moving is GameCard g && g.Destroyed)) { moving = null; return; }
            if (Keys.EnterDown || Keys.Down(Key.Escape))
            {
                var done = moving;
                moving = null;
                current = done;
                InvalidateCache();
                Speech.Say(Loc.T("move_end", CardInfo.Name(done)));
                return;
            }
            var wm = WorldManager.instance;
            Vector3 delta = Vector3.zero;
            if (Keys.Repeat(Key.RightArrow)) delta.x = wm.GridWidth * 1.4f;
            else if (Keys.Repeat(Key.LeftArrow)) delta.x = -wm.GridWidth * 1.4f;
            else if (Keys.Repeat(Key.UpArrow)) delta.z = wm.GridHeight * 1.4f;
            else if (Keys.Repeat(Key.DownArrow)) delta.z = -wm.GridHeight * 1.4f;
            else if (Bindings.Pressed("details")) { Speech.Say(Surroundings(moving)); return; }
            else if (Bindings.Pressed("connections")) { Speech.Say(MovingConnections(moving)); return; }
            if (delta == Vector3.zero) return;
            Vector3 wanted = moving.transform.position + delta;
            moving.transform.position = wanted;
            moving.TargetPosition = wanted;
            if (moving is GameCard mc) mc.Clampieee();
            if (Together.IsClient)
            {
                // Tell the host where the stack now is, as if it had been dragged there.
                if (moving is GameCard mg) { Together.Grab(mg); Together.Release(mg, ""); }
                else if (moving is Boosterpack bp) { Together.GrabBooster(bp); Together.ReleaseBooster(bp); }
            }
            bool edge = Vector3.Distance(new Vector3(wanted.x, 0, wanted.z), new Vector3(moving.transform.position.x, 0, moving.transform.position.z)) > 0.05f;
            CameraFollow(moving);
            InvalidateCache();
            Speech.Say(TextCleaner.Join(edge ? Loc.T("move_edge") : "", Surroundings(moving)));
        }

        /// <summary>What is around an object: the closest stacks within about two cards.</summary>
        private static string Surroundings(Draggable d)
        {
            var wm = WorldManager.instance;
            float radius = wm.GridWidth * 2.6f;
            Vector2 me = new Vector2(d.transform.position.x, d.transform.position.z);
            var near = AllObjects().Where(o => o != d)
                .Select(o => new { o, dist = Vector2.Distance(me, new Vector2(o.transform.position.x, o.transform.position.z)) })
                .Where(x => x.dist <= radius)
                .OrderBy(x => x.dist)
                .Take(3)
                .Select(x => CardInfo.Name(x.o))
                .ToList();
            return near.Count == 0 ? Loc.T("move_free") : Loc.T("move_near", string.Join(", ", near));
        }

        /// <summary>Make sure the focus is valid; returns the list of the current category.</summary>
        /// <summary>Last object visited in each category, to come back to it.</summary>
        private readonly Dictionary<Category, Draggable> memory = new Dictionary<Category, Draggable>();

        /// <summary>Enters a category on the last object visited there, or on the first one if it is gone.</summary>
        private void EnterCategory(Category cat, List<Draggable> list)
        {
            category = cat;
            int idx = 0;
            if (memory.TryGetValue(cat, out var m) && m != null)
            {
                if (m is GameCard mc && !mc.Destroyed && mc.Parent != null) m = mc.GetRootCard();
                int found = list.IndexOf(m);
                if (found >= 0) idx = found;
            }
            current = list.Count > 0 ? list[idx] : null;
            lastListIndex = idx;
            stackIndex = 0;
            if (current != null) memory[cat] = current;
        }

        private List<Draggable> Sync()
        {
            // The focused card may have been put on another pile (auto stacking, recipe...): follow it.
            if (current is GameCard cg && !cg.Destroyed && cg.Parent != null) current = cg.GetRootCard();
            var list = ListFor(category);
            if (current != null && !list.Contains(current))
            {
                current = null;
            }
            if (current == null && list.Count > 0)
            {
                lastListIndex = Mathf.Clamp(lastListIndex, 0, list.Count - 1);
                current = list[lastListIndex];
                stackIndex = 0;
            }
            if (current != null) { lastListIndex = list.IndexOf(current); memory[category] = current; }
            return list;
        }

        private void Move(int delta)
        {
            var list = Sync();
            if (list.Count == 0) { Speech.Say(Loc.T("cat_empty", CategoryName(category))); return; }
            int i = list.IndexOf(current);
            int ni = Mathf.Clamp(i + delta, 0, list.Count - 1);
            Focus(list, ni);
        }

        private void MoveTo(int index)
        {
            var list = Sync();
            if (list.Count == 0) { Speech.Say(Loc.T("cat_empty", CategoryName(category))); return; }
            Focus(list, Mathf.Clamp(index, 0, list.Count - 1));
        }

        private void Focus(List<Draggable> list, int index)
        {
            current = list[index];
            lastListIndex = index;
            memory[category] = current;
            stackIndex = 0;
            CameraFollow(current);
            Speech.Say(TextCleaner.Join(CardInfo.ObjectLine(current), Loc.T("of", index + 1, list.Count)));
        }

        public void AnnounceCurrent()
        {
            var intro = WorldManager.instance != null ? WorldManager.instance.IntroPack : null;
            if (intro != null && !IsHolding)
            {
                // New game: the starting pack must be opened first.
                category = Category.Shop;
                current = intro;
                stackIndex = 0;
            }
            var list = Sync();
            if (list.Count == 0) { Speech.Say(Loc.T("cat_empty", CategoryName(category)), false); return; }
            Speech.Say(TextCleaner.Join(CardInfo.ObjectLine(current), Loc.T("of", list.IndexOf(current) + 1, list.Count)), false);
        }

        /// <summary>Lists used for the details (set by the AccessController).</summary>
        public ListBrowser List;

        /// <summary>Move mode, E: where the cards linked to the moving stack are, and what the links give it.</summary>
        private static string MovingConnections(Draggable d)
        {
            if (!(d is GameCard root)) return Loc.T("conn_none");
            var parts = new List<string>();
            foreach (var card in root.GetAllCardsInStack())
            {
                foreach (var c in card.CardConnectorChildren ?? new List<CardConnector>())
                {
                    var other = c.GetConnectedGameCard();
                    if (other == null) continue;
                    parts.Add(Loc.T("conn_link", CardInfo.Name(card), CardInfo.Name(other), Connections.TypeName(c.ConnectionType), Connections.RelativePosition(card, other)));
                }
                string eff = Connections.Effects(card);
                if (eff != "") parts.Add(eff);
            }
            return parts.Count == 0 ? Loc.T("conn_none") : TextCleaner.Join(parts.ToArray());
        }

        private void OpenDetails()
        {
            Sync();
            if (current == null) { Speech.Say(Loc.T("no_info")); return; }
            Draggable target = (Draggable)CurrentCard() ?? current;
            if (List == null) { Speech.Say(CardInfo.Details(target)); return; }
            var pages = new List<ListBrowser.Page> { new ListBrowser.Page(Loc.T("details") + " " + CardInfo.Name(target), CardInfo.DetailLines(target)) };
            if (target is GameCard gc && (gc.CardData.HasInventory || (gc.EquipmentChildren != null && gc.EquipmentChildren.Count > 0) || gc.WorkerChildren.Count > 0))
                pages.Add(new ListBrowser.Page(Loc.T("inventory"), InventoryEntries(gc), Loc.T("inventory_empty")));
            List.OpenPages(pages, "details", 0, briefIntro: true);
        }

        /// <summary>Equipped items (and workers in a building): Enter takes the item in hand, I gives its details.</summary>
        private List<ListBrowser.Entry> InventoryEntries(GameCard holder)
        {
            var list = new List<ListBrowser.Entry>();
            var items = new List<GameCard>();
            if (holder.EquipmentChildren != null) items.AddRange(holder.EquipmentChildren.Where(x => x != null && !x.Destroyed));
            items.AddRange(holder.WorkerChildren.Where(x => x != null && !x.Destroyed));
            foreach (var item in items)
            {
                var it = item;
                list.Add(new ListBrowser.Entry(CardInfo.Name(it))
                {
                    OnActivate = () => PickUp(it),
                    Info = () => CardInfo.Details(it),
                });
            }
            return list;
        }

        /// <summary>Takes a card in hand (used by the inventory list).</summary>
        public void PickUp(GameCard card)
        {
            if (card == null || card.Destroyed) return;
            if (!CanAct()) return;
            if (!card.CanBeDragged()) { Speech.Say(Loc.T("cant_pick", CardInfo.Name(card))); return; }
            Held = card;
            Speech.Say(TextCleaner.Join(Loc.T("picked", CardInfo.Name(card)), Loc.Hint(card.IsEquipped ? "picked_equip_hint" : "picked_hint")));
        }

        /// <summary>Puts the focus on a stack (after a recipe was built...).</summary>
        public void FocusStack(GameCard root)
        {
            if (root == null) return;
            category = Category.All;
            current = root.GetRootCard();
            stackIndex = 0;
            CameraFollow(current);
        }

        /// <summary>Direct shortcut to a category; pressing it again goes to the next stack of that category.</summary>
        private void Jump(Category cat)
        {
            var list = ListFor(cat);
            if (list.Count == 0) { Speech.Say(Loc.T("cat_empty", CategoryName(cat))); return; }
            if (category == cat && current != null && list.Contains(current))
            {
                int i = (list.IndexOf(current) + 1) % list.Count;
                Focus(list, i);
                return;
            }
            EnterCategory(cat, list);
            CameraFollow(current);
            Speech.Say(TextCleaner.Join(Loc.T("cat_count", CategoryName(cat), list.Count), CardInfo.ObjectLine(current), Loc.T("of", lastListIndex + 1, list.Count)));
        }

        /// <summary>Tells what is held and puts the focus on the stack it comes from.</summary>
        private void GoToHeld()
        {
            if (!IsHolding) { Speech.Say(Loc.T("held_none")); return; }
            var root = Held.GetRootCard();
            category = Category.All;
            current = root;
            var stack = root.GetAllCardsInStack();
            stackIndex = Mathf.Max(0, stack.IndexOf(Held));
            CameraFollow(root);
            Speech.Say(Loc.T("held_is", CardInfo.Name(Held), CardInfo.StackLine(root)));
        }

        private void ChangeCategory(int dir)
        {
            int idx = Array.IndexOf(Order, category);
            for (int step = 0; step < Order.Length; step++)
            {
                idx = (idx + dir + Order.Length) % Order.Length;
                var cat = Order[idx];
                var list = ListFor(cat);
                if (list.Count == 0 && cat != Category.All) continue;
                if (list.Count == 0) { category = cat; current = null; lastListIndex = 0; Speech.Say(Loc.T("cat_empty", CategoryName(cat))); return; }
                EnterCategory(cat, list);
                CameraFollow(current);
                Speech.Say(TextCleaner.Join(Loc.T("cat_count", CategoryName(cat), list.Count), CardInfo.ObjectLine(current), Loc.T("of", lastListIndex + 1, list.Count)));
                return;
            }
        }

        private List<GameCard> CurrentStack()
        {
            if (current is GameCard root) return root.GetAllCardsInStack();
            return null;
        }

        private GameCard CurrentCard()
        {
            var stack = CurrentStack();
            if (stack == null || stack.Count == 0) return null;
            stackIndex = Mathf.Clamp(stackIndex, 0, stack.Count - 1);
            return stack[stackIndex];
        }

        private void MoveInStack(int delta)
        {
            Sync();
            var stack = CurrentStack();
            if (stack == null) { if (current != null) Speech.Say(CardInfo.ObjectLine(current)); return; }
            stackIndex = Mathf.Clamp(stackIndex + delta, 0, stack.Count - 1);
            Speech.Say(CardInfo.CardInStack(stack[stackIndex], stackIndex, stack.Count));
        }

        // ------------------------------------------------------------------ camera

        private static System.Reflection.MethodInfo groundOffset;

        private static void CameraFollow(Draggable d)
        {
            if (d == null || !AccessController.CameraFollowEnabled) return;
            var cam = GameCamera.instance;
            if (cam == null || cam.TargetPositionOverride.HasValue) return;
            try
            {
                if (groundOffset == null) groundOffset = AccessTools.Method(typeof(GameCamera), "GetCurrentGroundOffset");
                var off = (Vector3)groundOffset.Invoke(cam, null);
                cam.cameraTargetPosition = d.AutoMoveSnapPosition - off;
            }
            catch (System.Exception ex) { Log.Once(ex); }
        }
    }
}
