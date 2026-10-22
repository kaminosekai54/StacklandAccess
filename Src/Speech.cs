using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace StacklandAccessNS
{
    /// <summary>
    /// Speech output through Tolk (NVDA, JAWS, SAPI fallback...).
    /// The native DLLs are shipped in the "native" sub folder of the mod and loaded by full path,
    /// so nothing has to be copied next to Stacklands.exe.
    /// </summary>
    public static class Speech
    {
        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("Tolk", CallingConvention = CallingConvention.Cdecl)]
        private static extern void Tolk_Load();

        [DllImport("Tolk", CallingConvention = CallingConvention.Cdecl)]
        private static extern void Tolk_Unload();

        [DllImport("Tolk", CallingConvention = CallingConvention.Cdecl)]
        private static extern void Tolk_TrySAPI([MarshalAs(UnmanagedType.I1)] bool trySAPI);

        [DllImport("Tolk", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr Tolk_DetectScreenReader();

        [DllImport("Tolk", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool Tolk_Output([MarshalAs(UnmanagedType.LPWStr)] string str, [MarshalAs(UnmanagedType.I1)] bool interrupt);

        [DllImport("Tolk", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool Tolk_Silence();

        private static bool loaded;
        private static bool failed;

        public static string LastSpoken { get; private set; } = "";

        /// <summary>Narration on or off (setting "narration"). Off: the mod makes no sound at all, speech or beeps.</summary>
        public static bool Enabled = true;

        public static void Init(string modPath)
        {
            if (loaded || failed) return;
            try
            {
                string nativeDir = Path.Combine(modPath, "native");
                if (!Directory.Exists(nativeDir)) nativeDir = modPath;
                SetDllDirectory(nativeDir);
                IntPtr h = LoadLibrary(Path.Combine(nativeDir, "Tolk.dll"));
                if (h == IntPtr.Zero)
                {
                    Debug.LogError("[StacklandAccess] Could not load Tolk.dll from " + nativeDir + " (error " + Marshal.GetLastWin32Error() + ")");
                    failed = true;
                    return;
                }
                Tolk_TrySAPI(true);
                Tolk_Load();
                loaded = true;
                IntPtr namePtr = Tolk_DetectScreenReader();
                string name = namePtr == IntPtr.Zero ? "none" : Marshal.PtrToStringUni(namePtr);
                Debug.Log("[StacklandAccess] Tolk loaded, screen reader: " + name);
            }
            catch (Exception e)
            {
                failed = true;
                Debug.LogError("[StacklandAccess] Speech init failed: " + e);
            }
        }

        public static void Shutdown()
        {
            if (!loaded) return;
            try { Tolk_Unload(); } catch (System.Exception ex) { Log.Once(ex); }
            loaded = false;
        }

        /// <summary>Speak text. interrupt=true cuts the current speech (navigation), false queues it (events).</summary>
        public static void Say(string text, bool interrupt = true)
        {
            text = TextCleaner.Clean(text);
            if (string.IsNullOrWhiteSpace(text)) return;
            LastSpoken = text;
            if (!Enabled) return;
            if (!loaded)
            {
                Debug.Log("[StacklandAccess] (no speech) " + text);
                return;
            }
            try
            {
                Tolk_Output(text, interrupt);
                // An important event spoken just before was probably cut by this navigation message:
                // say it again once, after it.
                if (interrupt && pendingImportant != null)
                {
                    if (Time.unscaledTime - pendingImportantTime < 3f && pendingImportant != text)
                        Tolk_Output(pendingImportant, false);
                    pendingImportant = null;
                }
            }
            catch (Exception e) { Log.Once(e); }
        }

        private static string pendingImportant;
        private static float pendingImportantTime;

        /// <summary>Queued message that must not be lost if the player presses a key right after it.</summary>
        public static void SayImportant(string text)
        {
            text = TextCleaner.Clean(text);
            if (string.IsNullOrWhiteSpace(text) || !Enabled) return;
            Say(text, interrupt: false);
            pendingImportant = text;
            pendingImportantTime = Time.unscaledTime;
        }

        public static void Silence()
        {
            if (!loaded) return;
            try { Tolk_Silence(); } catch (System.Exception ex) { Log.Once(ex); }
        }
    }

    /// <summary>Turns TextMeshPro rich text into plain speakable text.</summary>
    public static class TextCleaner
    {
        private static readonly Regex SpriteTag = new Regex("<sprite=\"?([a-zA-Z_]+)\"?[^>]*>", RegexOptions.Compiled);
        private static readonly Regex AnyTag = new Regex("<[^>]*>", RegexOptions.Compiled);
        private static readonly Regex Spaces = new Regex("[ \\t]{2,}", RegexOptions.Compiled);
        private static readonly Regex Lines = new Regex("\\s*\\n+\\s*", RegexOptions.Compiled);

        public static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Replace("\\d", "\n").Replace("^", "\n");
            text = SpriteTag.Replace(text, m => " " + Loc.Icon(m.Groups[1].Value) + " ");
            text = AnyTag.Replace(text, "");
            text = text.Replace("\r", "");
            text = Lines.Replace(text, m => ". ");
            text = Spaces.Replace(text, " ");
            text = text.Replace(" .", ".").Replace("..", ".").Replace(". .", ".");
            text = text.Trim();
            if (text.StartsWith(". ")) text = text.Substring(2);
            return text;
        }

        /// <summary>Joins several fragments with ". ", skipping empty ones.</summary>
        public static string Join(params string[] parts)
        {
            var sb = new StringBuilder();
            foreach (var p in parts)
            {
                if (string.IsNullOrWhiteSpace(p)) continue;
                var s = p.Trim();
                if (sb.Length > 0)
                {
                    char last = sb[sb.Length - 1];
                    sb.Append(last == '.' || last == ',' || last == ':' || last == '!' || last == '?' ? " " : ". ");
                }
                sb.Append(s);
            }
            return sb.ToString();
        }
    }
}
