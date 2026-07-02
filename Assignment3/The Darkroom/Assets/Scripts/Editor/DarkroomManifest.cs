#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Darkroom
{
    /// Regenerates StreamingAssets/manifest.txt — the file list the WebGL runtime
    /// reads in place of Directory.GetFiles (WebGL can't enumerate a remote folder).
    /// Run this after adding or removing anything under StreamingAssets/art or /music.
    public static class DarkroomManifest
    {
        [MenuItem("Darkroom/Regenerate StreamingAssets Manifest")]
        public static void Regenerate()
        {
            string root = Path.Combine(Application.dataPath, "StreamingAssets");
            var rels = new List<string>();
            foreach (var full in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                string ext = Path.GetExtension(full).ToLowerInvariant();
                // only the assets the runtime actually fetches (images + audio)
                if (ext != ".png" && ext != ".jpg" && ext != ".jpeg"
                    && ext != ".ogg" && ext != ".wav" && ext != ".mp3") continue;
                rels.Add(full.Substring(root.Length + 1).Replace('\\', '/'));
            }
            rels.Sort(System.StringComparer.Ordinal);

            var sb = new StringBuilder();
            sb.AppendLine("# StreamingAssets manifest — WebGL has no filesystem and cannot list");
            sb.AppendLine("# directories, so the runtime loaders read this list instead.");
            sb.AppendLine("# One StreamingAssets-relative path per line. Regenerate via menu:");
            sb.AppendLine("#   Darkroom ▸ Regenerate StreamingAssets Manifest");
            foreach (var r in rels) sb.AppendLine(r);

            File.WriteAllText(Path.Combine(root, "manifest.txt"), sb.ToString());
            AssetDatabase.Refresh();
            Debug.Log("[Darkroom] manifest.txt regenerated — " + rels.Count + " assets listed.");
        }
    }
}
#endif
