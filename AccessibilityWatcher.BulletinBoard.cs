using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace DateEverythingAccess
{
    // BULLETIN BOARD (first step: find out how it is built). The office bulletin board collects
    // photos of datables, with a little flavour text each, as the game goes on (the Ink story
    // counts them in a variable called "bulletins"). Before the mod can read them it has to know
    // where the photos and their text live, so the first time the player looks at the board in a
    // session (and again whenever the photo count changes), the board's whole make-up is written
    // to the log: every piece, whether it is showing, and any text, image or material names on it.
    internal sealed partial class AccessibilityWatcher
    {
        private float _bulletinNextCheck;
        private string _bulletinDumpedFor;   // "bulletins" value at the last dump, or null

        private void DiagnoseBulletinBoardIfNeeded()
        {
            if (Time.unscaledTime < _bulletinNextCheck) return;
            _bulletinNextCheck = Time.unscaledTime + 0.5f;
            try
            {
                InteractableManager manager = Singleton<InteractableManager>.Instance;
                if (manager == null || manager.activeObject == null) return;
                Transform board = FindNamedAncestor(manager.activeObject.transform, "BulletinBoard");
                if (board == null) return;
                string count = GetInkVariableString("bulletins") ?? "?";
                if (string.Equals(count, _bulletinDumpedFor, StringComparison.Ordinal)) return;
                _bulletinDumpedFor = count;
                DumpBulletinBoard(board, count);
            }
            catch (Exception ex)
            {
                if (Main.Log != null) Main.Log.LogWarning("BulletinBoard diagnostic failed: " + ex.Message);
            }
        }

        private static Transform FindNamedAncestor(Transform t, string name)
        {
            for (int depth = 0; t != null && depth < 16; depth++, t = t.parent)
                if (string.Equals(t.name, name, StringComparison.OrdinalIgnoreCase))
                    return t;
            return null;
        }

        private static void DumpBulletinBoard(Transform board, string count)
        {
            if (Main.Log == null) return;
            var sb = new StringBuilder();
            sb.Append("[bulletin] board=").Append(RuntimeBlockerProbe.PathOf(board.gameObject))
              .Append(" bulletins=").Append(count)
              .Append(" realized=").Append(GetInkVariableString("realized") ?? "?");
            Main.Log.LogInfo(sb.ToString());
            int lines = 0;
            DumpTransform(board, 0, ref lines);

            // Photos may also live elsewhere in the scene and be placed on the board at runtime.
            int extra = 0;
            foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t == null || t.gameObject.scene.name == null || t.IsChildOf(board)) continue;
                string n = t.name;
                if (n.IndexOf("bulletin", StringComparison.OrdinalIgnoreCase) < 0 &&
                    n.IndexOf("polaroid", StringComparison.OrdinalIgnoreCase) < 0 &&
                    n.IndexOf("photo", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                Main.Log.LogInfo("[bulletin] elsewhere: " + RuntimeBlockerProbe.PathOf(t.gameObject) +
                    " active=" + t.gameObject.activeInHierarchy + Describe(t));
                if (++extra >= 60) break;
            }
            Main.Log.LogInfo("[bulletin] dump done lines=" + lines + " elsewhere=" + extra);
        }

        private static void DumpTransform(Transform t, int depth, ref int lines)
        {
            if (t == null || lines >= 500 || depth > 10) return;
            lines++;
            Main.Log.LogInfo("[bulletin] " + new string(' ', depth * 2) + t.name +
                " active=" + t.gameObject.activeSelf + Describe(t));
            for (int i = 0; i < t.childCount; i++)
                DumpTransform(t.GetChild(i), depth + 1, ref lines);
        }

        // Component types plus any text, image or material names on this piece.
        private static string Describe(Transform t)
        {
            var sb = new StringBuilder();
            var types = new List<string>();
            foreach (Component c in t.GetComponents<Component>())
            {
                if (c == null || c is Transform) continue;
                types.Add(c.GetType().Name);
                if (c is TMP_Text tmp && !string.IsNullOrWhiteSpace(tmp.text))
                    sb.Append(" text=\"").Append(tmp.text.Replace("\n", " / ")).Append('"');
                else if (c is SpriteRenderer sr && sr.sprite != null)
                    sb.Append(" sprite=").Append(sr.sprite.name);
                else if (c is Renderer r && r.sharedMaterials != null)
                {
                    foreach (Material m in r.sharedMaterials)
                    {
                        if (m == null) continue;
                        sb.Append(" mat=").Append(m.name);
                        if (m.mainTexture != null) sb.Append("/tex=").Append(m.mainTexture.name);
                    }
                }
                else if (c is InteractableObj io)
                    sb.Append(" ink=").Append(io.inkFileName ?? "").Append(" main=\"").Append(io.mainText ?? "").Append('"');
            }
            if (types.Count > 0) sb.Insert(0, " [" + string.Join(",", types.ToArray()) + "]");
            return sb.ToString();
        }
    }
}
