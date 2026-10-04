using System;
using System.IO;
using UnityEngine;

namespace StacklandAccessNS
{
    /// <summary>
    /// Short sounds played by the mod. The "villager free" sound is read from Sounds/villager_free.wav
    /// (a short rising "ready" chime, Kenney CC0); a synthesized fallback is used if the file is missing.
    /// </summary>
    public static class Beeper
    {
        public static string ModPath;
        private static AudioSource source;
        private static AudioClip freeClip;

        private static AudioSource Source()
        {
            if (source != null) return source;
            var host = AccessController.Instance != null ? AccessController.Instance.gameObject : new GameObject("StacklandAccessSound");
            source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            return source;
        }

        public static void Play()
        {
            try
            {
                if (freeClip == null) freeClip = LoadWav("villager_free.wav") ?? MakeFreeClip();
                Source().PlayOneShot(freeClip, AccessController.BeepVolume);
            }
            catch (Exception ex) { Log.Once(ex); }
        }

        /// <summary>Reads an uncompressed PCM WAV (8, 16, 24 or 32 bits, any rate/channels) into an AudioClip.</summary>
        private static AudioClip LoadWav(string fileName)
        {
            try
            {
                if (string.IsNullOrEmpty(ModPath)) return null;
                string path = System.IO.Path.Combine(ModPath, "Sounds", fileName);
                if (!File.Exists(path)) return null;
                byte[] b = File.ReadAllBytes(path);
                if (b.Length < 44 || b[0] != 'R' || b[1] != 'I' || b[2] != 'F' || b[3] != 'F') return null;

                int channels = 0, rate = 0, bits = 0, format = 0, dataPos = -1, dataLen = 0;
                int pos = 12;
                while (pos + 8 <= b.Length)
                {
                    string id = System.Text.Encoding.ASCII.GetString(b, pos, 4);
                    int len = BitConverter.ToInt32(b, pos + 4);
                    int body = pos + 8;
                    if (id == "fmt ")
                    {
                        format = BitConverter.ToInt16(b, body);
                        channels = BitConverter.ToInt16(b, body + 2);
                        rate = BitConverter.ToInt32(b, body + 4);
                        bits = BitConverter.ToInt16(b, body + 14);
                    }
                    else if (id == "data")
                    {
                        dataPos = body;
                        dataLen = Math.Min(len, b.Length - body);
                        break;
                    }
                    pos = body + len + (len & 1);
                }
                // 1 = PCM, 0xFFFE = extensible (PCM inside).
                if (dataPos < 0 || channels <= 0 || rate <= 0 || (format != 1 && format != -2)) return null;

                int bytesPer = bits / 8;
                if (bytesPer < 1 || bytesPer > 4) return null;
                int count = dataLen / bytesPer;
                var data = new float[count];
                for (int i = 0, p = dataPos; i < count; i++, p += bytesPer)
                {
                    switch (bytesPer)
                    {
                        case 1: data[i] = (b[p] - 128) / 128f; break;
                        case 2: data[i] = BitConverter.ToInt16(b, p) / 32768f; break;
                        case 3: data[i] = ((b[p] | (b[p + 1] << 8) | ((sbyte)b[p + 2] << 16))) / 8388608f; break;
                        default: data[i] = BitConverter.ToInt32(b, p) / 2147483648f; break;
                    }
                }
                var clip = AudioClip.Create("sa_" + fileName, count / channels, channels, rate, false);
                clip.SetData(data, 0);
                return clip;
            }
            catch (Exception ex)
            {
                Log.Once(ex);
                return null;
            }
        }

        /// <summary>Fallback: a short rising arpeggio, synthesized.</summary>
        private static AudioClip MakeFreeClip()
        {
            const int rate = 44100;
            float[] notes = { 523f, 659f, 784f, 1047f }; // C5 E5 G5 C6
            float noteLen = 0.07f;
            int perNote = (int)(rate * noteLen);
            int total = notes.Length * perNote + (int)(rate * 0.15f);
            var data = new float[total];
            for (int k = 0; k < notes.Length; k++)
            {
                float f = notes[k];
                int start = k * perNote;
                int len = k == notes.Length - 1 ? total - start : perNote;
                for (int n = 0; n < len; n++)
                {
                    float t = n / (float)rate;
                    float env = Mathf.Min(1f, n / (rate * 0.004f)) * Mathf.Exp(-t * (k == notes.Length - 1 ? 12f : 25f));
                    data[start + n] += 0.35f * env * (Mathf.Sin(2f * Mathf.PI * f * t) + 0.25f * Mathf.Sin(4f * Mathf.PI * f * t));
                }
            }
            var clip = AudioClip.Create("sa_villager_free", total, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
