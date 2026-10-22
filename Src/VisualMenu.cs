using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StacklandAccessNS
{
    /// <summary>
    /// On-screen version of the mod's spoken menus (lists, options, text fields), drawn with the game's own
    /// look: the panel of its pop-ups, its fonts and its buttons. Sighted players can follow and click;
    /// the game keeps running (unlike the game's pop-ups, which pause it).
    /// </summary>
    public static class VisualMenu
    {
        private const int MaxRows = 9;
        private const int MaxChars = 110;

        private static GameObject canvasGo;
        private static RectTransform panel;
        private static TextMeshProUGUI title, body;
        private static readonly List<GameObject> rows = new List<GameObject>();
        private static string shown;
        private static bool broken;

        // A click waiting for the list: entry index (-1 = close), or a page change.
        private static bool clickPending;
        private static int clickIndex;
        private static int clickPage;

        /// <summary>Gives the list a click made on the visual menu, once.</summary>
        public static bool TakeClick(out int index, out int pageMove)
        {
            index = clickIndex;
            pageMove = clickPage;
            if (!clickPending) return false;
            clickPending = false;
            return true;
        }

        private static void Click(int index, int page)
        {
            clickPending = true;
            clickIndex = index;
            clickPage = page;
        }

        public static void Update(ListBrowser list)
        {
            if (broken) return;
            try
            {
                string key = null;
                if (AccessController.VisualMenusEnabled && GameCanvas.instance != null)
                {
                    if (TextPrompt.Active) key = "prompt|" + TextPrompt.Label + "|" + TextPrompt.DisplayText;
                    else if (list != null && list.IsOpen) key = "list|" + list.Version + "|" + Bindings.Capturing;
                }
                if (key == null) { Hide(); return; }
                if (key == shown && panel != null && panel.gameObject.activeSelf) return;
                shown = key;
                if (!Ensure()) return;
                if (TextPrompt.Active) BuildPrompt();
                else BuildList(list);
                panel.gameObject.SetActive(true);
                canvasGo.SetActive(true);
                LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
            }
            catch (System.Exception ex)
            {
                // Never let the drawing break the spoken menus.
                broken = true;
                Log.Once(ex);
                Hide();
            }
        }

        private static void Hide()
        {
            shown = null;
            clickPending = false;
            if (canvasGo != null && canvasGo.activeSelf) canvasGo.SetActive(false);
        }

        // ------------------------------------------------------------------ contents

        private static void BuildList(ListBrowser list)
        {
            ClearRows();
            var pages = list.Pages;
            var page = pages[Mathf.Clamp(list.PageIndex, 0, pages.Count - 1)];
            int count = page.Entries.Count;
            int index = Mathf.Clamp(list.Index, 0, Mathf.Max(0, count - 1));
            title.text = Escape(page.Name);

            var info = new List<string>();
            if (pages.Count > 1) info.Add(Loc.T("vis_page", list.PageIndex + 1, pages.Count));
            int start = Mathf.Clamp(index - MaxRows / 2, 0, Mathf.Max(0, count - MaxRows));
            int end = Mathf.Min(count, start + MaxRows);
            if (count == 0) info.Add(Escape(page.EmptyText ?? Loc.T("list_empty", page.Name)));
            else if (count > MaxRows) info.Add(Loc.T("vis_range", start + 1, end, count));
            if (Bindings.Capturing) info.Add("<b>" + Escape(Loc.T("vis_capture")) + "</b>");
            else info.Add("<size=80%>" + Escape(Loc.T("vis_hint", Bindings.Spoken("list_info"))) + "</size>");
            body.text = string.Join("\n", info);

            for (int i = start; i < end; i++)
            {
                int n = i;
                string text = Escape(Shorten(page.Entries[i].Title));
                if (i == index) text = "<b>» " + text + " «</b>";
                AddButton(text, () => Click(n, 0));
            }
            if (list.PageIndex > 0) AddButton("« " + Escape(pages[list.PageIndex - 1].Name), () => Click(0, -1));
            if (list.PageIndex < pages.Count - 1) AddButton(Escape(pages[list.PageIndex + 1].Name) + " »", () => Click(0, +1));
            AddButton(Escape(Loc.T("vis_close")), () => Click(-1, 0));
        }

        private static void BuildPrompt()
        {
            ClearRows();
            title.text = Escape(TextPrompt.Label);
            string value = TextPrompt.DisplayText;
            body.text = "<b>" + (string.IsNullOrEmpty(value) ? "_" : Escape(value) + "_") + "</b>\n<size=80%>" + Escape(Loc.T("vis_prompt_hint")) + "</size>";
        }

        private static string Shorten(string s)
        {
            s = TextCleaner.Clean(s ?? "");
            return s.Length <= MaxChars ? s : s.Substring(0, MaxChars - 1) + "…";
        }

        /// <summary>Text shown as is: no rich text tags from the entries.</summary>
        private static string Escape(string s) => (s ?? "").Replace("<", "<noparse><</noparse>");

        // ------------------------------------------------------------------ building the panel

        private static void ClearRows()
        {
            foreach (var r in rows) if (r != null) Object.Destroy(r);
            rows.Clear();
        }

        private static void AddButton(string text, System.Action onClick)
        {
            var prefab = PrefabManager.instance != null ? PrefabManager.instance.ButtonPrefab : null;
            GameObject go;
            if (prefab != null)
            {
                var b = Object.Instantiate(prefab, panel);
                b.transform.localScale = Vector3.one;
                b.transform.localPosition = Vector3.zero;
                b.transform.localRotation = Quaternion.identity;
                b.TooltipText = null;
                if (b.TextMeshPro != null)
                {
                    b.TextMeshPro.richText = true;
                    b.TextMeshPro.text = text;
                }
                b.Clicked += () => onClick();
                go = b.gameObject;
            }
            else
            {
                // No game button available: a plain text line (still clickable).
                go = new GameObject("Row", typeof(RectTransform));
                go.transform.SetParent(panel, false);
                var img = go.AddComponent<Image>();
                img.color = new Color(1f, 1f, 1f, 0.08f);
                var btn = go.AddComponent<Button>();
                btn.onClick.AddListener(() => onClick());
                var t = NewText(go.transform, body, 0.9f);
                t.text = text;
            }
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            float h = ((RectTransform)go.transform).rect.height;
            le.minHeight = h > 10f ? h : 56f;
            rows.Add(go);
        }

        private static bool Ensure()
        {
            if (canvasGo != null && panel != null) return true;
            var gc = GameCanvas.instance;
            if (gc == null) return false;

            canvasGo = new GameObject("StacklandAccessVisualMenu");
            Object.DontDestroyOnLoad(canvasGo);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            var gameScaler = gc.Canvas != null ? gc.Canvas.GetComponent<CanvasScaler>() : null;
            if (gameScaler != null)
            {
                scaler.uiScaleMode = gameScaler.uiScaleMode;
                scaler.referenceResolution = gameScaler.referenceResolution;
                scaler.screenMatchMode = gameScaler.screenMatchMode;
                scaler.matchWidthOrHeight = gameScaler.matchWidthOrHeight;
                scaler.referencePixelsPerUnit = gameScaler.referencePixelsPerUnit;
                scaler.scaleFactor = gameScaler.scaleFactor;
            }
            else
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
            }
            canvasGo.AddComponent<GraphicRaycaster>();

            // Dark veil, like the game's pop-ups (it also keeps clicks off the board under the menu).
            var veil = new GameObject("Veil", typeof(RectTransform));
            veil.transform.SetParent(canvasGo.transform, false);
            var vr = (RectTransform)veil.transform;
            vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one; vr.offsetMin = vr.offsetMax = Vector2.zero;
            veil.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);

            // The panel: the game's pop-up background when it can be found.
            var pgo = new GameObject("Panel", typeof(RectTransform));
            pgo.transform.SetParent(canvasGo.transform, false);
            panel = (RectTransform)pgo.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(860f, 0f);
            var bg = pgo.AddComponent<Image>();
            CopyPanelLook(bg);
            var layout = pgo.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(36, 36, 28, 28);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            var fit = pgo.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var modal = ModalScreen.instance;
            var anyText = gc.GetComponentInChildren<TextMeshProUGUI>(true);
            title = NewText(panel, modal != null && modal.TitleText != null ? modal.TitleText : anyText, 1f);
            title.fontStyle |= FontStyles.Bold;
            body = NewText(panel, modal != null && modal.TextText != null ? modal.TextText : anyText, 1f);
            canvasGo.SetActive(false);
            return true;
        }

        private static TextMeshProUGUI NewText(Transform parent, TextMeshProUGUI model, float scale)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (model != null)
            {
                t.font = model.font;
                t.fontSharedMaterial = model.fontSharedMaterial;
                t.fontSize = Mathf.Clamp(model.fontSize * scale, 20f, 48f);
                t.color = model.color;
            }
            else
            {
                t.fontSize = 28f * scale;
                t.color = Color.black;
            }
            t.enableAutoSizing = false;
            t.alignment = TextAlignmentOptions.Center;
            t.richText = true;
            t.enableWordWrapping = true;
            if (parent is RectTransform pr && pr.GetComponent<Button>() != null)
            {
                var r = (RectTransform)go.transform;
                r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(10, 4); r.offsetMax = new Vector2(-10, -4);
            }
            return t;
        }

        /// <summary>Uses the background picture and colour of the game's pop-up window.</summary>
        private static void CopyPanelLook(Image target)
        {
            target.color = new Color(0.95f, 0.91f, 0.82f, 1f);
            var modal = ModalScreen.instance;
            if (modal == null) return;
            Image best = null;
            float bestArea = 0f;
            foreach (var img in modal.GetComponentsInChildren<Image>(true))
            {
                if (img == null || img.sprite == null) continue;
                var r = img.rectTransform.rect;
                float area = Mathf.Abs(r.width * r.height);
                if (img.GetComponentInParent<CustomButton>() != null) continue;
                if (area > bestArea) { bestArea = area; best = img; }
            }
            if (best == null) return;
            target.sprite = best.sprite;
            target.type = best.type == Image.Type.Simple && best.sprite.border != Vector4.zero ? Image.Type.Sliced : best.type;
            target.pixelsPerUnitMultiplier = best.pixelsPerUnitMultiplier;
            target.color = best.color;
            target.material = best.material;
        }
    }
}
