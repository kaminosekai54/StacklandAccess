using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace StacklandAccessNS
{
    /// <summary>
    /// Saves card icons as PNG files for the card page. Game textures are not readable from scripts,
    /// so each atlas is copied once through a render texture, then every sprite is cut out of it.
    /// </summary>
    public class CardImages
    {
        private readonly Dictionary<Texture, Texture2D> readable = new Dictionary<Texture, Texture2D>();
        private readonly Dictionary<(Sprite, Color32), string> saved = new Dictionary<(Sprite, Color32), string>();
        private readonly string folder;
        /// <summary>False when the game's content has not changed since the last export: existing pictures are kept.</summary>
        private readonly bool rewrite;

        public int Written { get; private set; }

        public CardImages(string folder, bool rewrite = true)
        {
            this.folder = folder;
            this.rewrite = rewrite;
            Directory.CreateDirectory(folder);
        }

        private Texture2D Readable(Texture tex)
        {
            if (readable.TryGetValue(tex, out var t)) return t;
            var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            Graphics.Blit(tex, rt);
            RenderTexture.active = rt;
            t = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
            t.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            readable[tex] = t;
            return t;
        }

        /// <summary>
        /// Writes the sprite as "name.png" (once per sprite and colour) and returns the file name, or null.
        /// Icons are drawn in one colour by the game: the shape (alpha) is kept and painted with the tint.
        /// </summary>
        public string Save(Sprite sprite, string name, Color? tint = null)
        {
            if (sprite == null || sprite.texture == null) return null;
            Color32 key = tint ?? new Color(0, 0, 0, 0);
            if (saved.TryGetValue((sprite, key), out var existing)) return existing;
            string file = Safe(name) + ".png";
            // Same game content as last time: the picture on disk is already right, nothing to extract.
            if (!rewrite && File.Exists(Path.Combine(folder, file)))
            {
                saved[(sprite, key)] = file;
                return file;
            }
            try
            {
                var src = Readable(sprite.texture);
                Rect r = sprite.textureRect;
                int x = Mathf.RoundToInt(r.x), y = Mathf.RoundToInt(r.y), w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
                if (w <= 0 || h <= 0) return null;
                var pixels = src.GetPixels(x, y, w, h);
                if (tint.HasValue)
                {
                    Color t = tint.Value;
                    for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(t.r, t.g, t.b, pixels[i].a);
                }
                var cut = new Texture2D(w, h, TextureFormat.RGBA32, false);
                cut.SetPixels(pixels);
                cut.Apply();
                File.WriteAllBytes(Path.Combine(folder, file), cut.EncodeToPNG());
                Written++;
                Object.Destroy(cut);
                saved[(sprite, key)] = file;
                return file;
            }
            catch (System.Exception ex)
            {
                Log.Once(ex);
                return null;
            }
        }

        private static string Safe(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s;
        }

        public void Dispose()
        {
            foreach (var t in readable.Values) if (t != null) Object.Destroy(t);
            readable.Clear();
        }
    }
}
