using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// Film only (-objectfilm &lt;dir&gt;, 11-04; the presentation video's S30): for each of the visitors' things -objectitems
    /// names (their numbers in the manifest, in that order; all of this nest's by default), seen as the ant stands on the way
    /// a few steps off, seen from just behind it (still, a slow push in): nothing there, then the thing gathers out of specks
    /// scattered round where it stands (each speck on its way at its own moment, ParticleSolid's _ExhibitForm) and its ring
    /// lights; then the ant walks up, touches it with
    /// its antennae, its card opens and its words are typed out a letter at a time. -objectzh N: then thing N once more,
    /// its card in the other language (the language preference is put back after). -objectplain: as in the game, the things
    /// there from the start and each card whole at once (held as long as typing it would take). Frames at 30 a second into
    /// &lt;dir&gt;/item_NNN[_plain][_zh]/ on StoryFilm's 1920×1080 screen, only the card of the screen's words shown; parts.txt says
    /// where each part starts and ends. Nothing here runs in play: without the flag _ExhibitForm stays 0 and the rings 1.
    /// </summary>
    public partial class Uploads
    {
        float[] ringShown;
        readonly Vector4[] forms = new Vector4[8];
        static readonly int FormId = Shader.PropertyToID("_ExhibitForm");

        /// <summary>How much of a thing's ring shows (1 always, but while -objectfilm brings the thing in).</summary>
        float RingShown(int i) => ringShown == null || i >= ringShown.Length ? 1f : ringShown[i];

        /// <summary>A thing gathering: unformed 1, none of it there; 0, whole, as always. Its specks start up to spread away.</summary>
        void Form(int i, float unformed, float spread)
        {
            if (i < 0 || i >= forms.Length) return;
            forms[i] = new Vector4(Mathf.Clamp01(unformed), spread, 1.4f, 0f);
            Shader.SetGlobalVectorArray(FormId, forms);
        }

        static float Spread(Thing t) => Mathf.Clamp(t.sphere.w * 1.4f, 1.4f, 4.5f);

        IEnumerator ObjectFilm(Game game, string dir, List<Vector2> track)
        {
            Directory.CreateDirectory(dir);
            while (!game.Ready) yield return null;
            StoryFilm.Begin(game);
            string langPref = PlayerPrefs.GetString("antgarden.lang", "zh");
            var order = new List<int>();
            foreach (string s in (Game.Arg("-objectitems") ?? "").Split(','))
                if (int.TryParse(s.Trim(), out int n)) { int i = things.FindIndex(x => x.index == n); if (i >= 0 && !order.Contains(i)) order.Add(i); }
            if (order.Count == 0) for (int i = 0; i < things.Count; i++) order.Add(i);
            ringShown = new float[things.Count];
            for (int i = 0; i < things.Count; i++) ringShown[i] = 1f;
            // (-objectplain: as in the game, the things there from the start, their cards whole at once)
            bool plain = Game.Arg("-objectplain") != null;
            // none of them there yet, nor their rings (nor the nest's other visitors' things, not in this film: one of them
            // stood large at the edge of another's frame)
            if (!plain) for (int i = 0; i < things.Count && i < forms.Length; i++) { ringShown[i] = 0f; Form(i, 1f, Spread(things[i])); }
            yield return StoryFilm.Settle(6f);   // shaders compile, the ants spread out
            var parts = new StringBuilder();
            foreach (int i in order) yield return Take(game, dir, track, i, !plain, parts, !plain);
            if (int.TryParse(Game.Arg("-objectzh") ?? "", out int again))
            {
                int j = things.FindIndex(x => x.index == again);
                if (j >= 0)
                {
                    game.ToggleLanguage();
                    yield return StoryFilm.Settle(0.5f);
                    yield return Take(game, dir, track, j, false, parts, !plain);
                    PlayerPrefs.SetString("antgarden.lang", langPref);
                    PlayerPrefs.Save();
                }
            }
            File.WriteAllText(Path.Combine(dir, "parts.txt"), parts.ToString());
            File.WriteAllText(Path.Combine(dir, "done.txt"), "ok\n");
            Debug.Log($"[AntGarden] objectfilm: done, {order.Count} things");
            Time.captureFramerate = 0;
            Application.Quit();
        }

        /// <summary>One thing: (gather) it comes into being, its ring lights; then the ant walks up and reads it.</summary>
        IEnumerator Take(Game game, string dir, List<Vector2> track, int i, bool gather, StringBuilder parts, bool typeOut)
        {
            const int Fps = DemoMode.Fps;
            Thing t = things[i];
            bool zh = !Loc.En;
            string sub = Path.Combine(dir, $"item_{t.index:000}{(typeOut ? "" : "_plain")}{(zh ? "_zh" : "")}");
            Directory.CreateDirectory(sub);
            var at = new Vector2(t.pos.x, t.pos.z);
            int near = 0;
            for (int j = 1; j < track.Count; j++) if ((track[j] - t.from).sqrMagnitude < (track[near] - t.from).sqrMagnitude) near = j;
            int start = near;
            for (float run = 0f; start > 0 && run < 6f; start--) run += (track[start] - track[start - 1]).magnitude;
            shown = -1;
            // the camera in the sight line kept clear of plants from six units back along the way to the thing (Add: where one
            // coming along the way first sees it), a little behind its start and up, looking at the thing; still, but for a
            // slow push in (behind the ant as in play, a plant beside the way filled a third of the frame); the ant on that
            // line a step ahead of it, facing the thing
            Vector2 seen = track[start], toward = (at - seen).sqrMagnitude > 1e-4f ? (at - seen).normalized : Vector2.up;
            Camera cam = game.cam;
            float fov = cam.fieldOfView;
            var aim = new Vector3(t.pos.x, t.pos.y + t.top * 0.42f, t.pos.z);
            // (far enough back that the thing takes about half the frame's height: close to the record's tall sleeve it filled
            // the frame; of a few spots on and beside that line, the one that sees it clearest, a plant at the lens filled an edge)
            float want = Mathf.Max(t.top, 0.8f) / (0.9f * Mathf.Tan(21f * Mathf.Deg2Rad)), baseD = Mathf.Max((at - seen).magnitude + 1.4f, t.reach + 2.2f + 2.3f);
            var tall = StoryFilm.TallNear(game, t.pos, Mathf.Max(want, baseD) + 10f);
            tall.RemoveAll(x => new Vector2(x.foot.x - t.pos.x, x.foot.z - t.pos.z).magnitude < 0.8f);   // (not the thing itself)
            Vector2 side = new Vector2(-toward.y, toward.x), c0 = seen - toward * 1.4f;
            float bestScore = float.MaxValue;
            foreach (float extra in new[] { 0f, 1f, 2f, 3f, 4.5f })
                foreach (float lat in new[] { 0f, 0.8f, -0.8f, 1.6f, -1.6f })
                {
                    float d = Mathf.Max(baseD, want) + extra;
                    Vector2 c = at - toward * d + side * lat;
                    if (!game.LabNest.OnFloor(c, 0.3f)) continue;
                    var cp = new Vector3(c.x, game.world.Height(c.x, c.y) + 1.7f, c.y);
                    float score = 4f * StoryFilm.Blocked(game, cp, aim, tall) + 0.12f * extra + 0.1f * Mathf.Abs(lat) + Mathf.Abs(d - want) / want;
                    if (score < bestScore) { bestScore = score; c0 = c; }
                }
            var camFrom = new Vector3(c0.x, game.world.Height(c0.x, c0.y) + 1.7f, c0.y);
            float camFov = Mathf.Clamp(2f * Mathf.Atan(Mathf.Max(t.top, 0.8f) / (0.9f * Vector3.Distance(camFrom, aim))) * Mathf.Rad2Deg, 36f, 52f);
            Vector3 camTo = camFrom + (aim - camFrom).normalized * 0.6f;
            // (out of its antennae's reach, or it read the thing while it was still gathering: the record stood close to the way)
            Vector2 stand = at - toward * Mathf.Max((at - seen).magnitude - 0.9f, t.reach + 2.2f);
            DemoMode.PlaceFacing(game, stand, at);
            game.rig.manual = true;
            cam.fieldOfView = camFov;
            cam.transform.SetPositionAndRotation(camFrom, Quaternion.LookRotation(aim - camFrom));
            game.demoMove = Vector2.zero;
            FilmScreen.OnlyWords(game, "VisitorCard");
            yield return StoryFilm.Settle(1.2f);
            yield return SceneShots.ShellSettles();
            yield return StoryFilm.Settle(0.3f);
            DemoMode.PlaceFacing(game, stand, at);
            int takeN = 0;   // the push runs over the whole take (both parts)
            void Push()
            {
                Vector3 p = Vector3.Lerp(camFrom, camTo, Mathf.Clamp01(takeN++ / (30f * Fps)));
                cam.transform.SetPositionAndRotation(p, Quaternion.LookRotation(aim - p));
            }
            var end = new WaitForEndOfFrame();
            int f = 0;
            if (gather)
            {
                // nothing there; it gathers (2.2 s); its ring lights (1.2 s); a moment more
                int empty = Mathf.RoundToInt(0.8f * Fps), gatherN = Mathf.RoundToInt(2.2f * Fps), ringN = Mathf.RoundToInt(1.2f * Fps), holdN = Mathf.RoundToInt(1.4f * Fps);
                for (int k = 0, n = empty + gatherN + ringN + holdN; k < n; k++, f++)
                {
                    float g = Mathf.SmoothStep(0f, 1f, (k - empty) / (float)gatherN);
                    Form(i, 1f - g, Spread(t));
                    ringShown[i] = Mathf.SmoothStep(0f, 1f, (k - empty - gatherN * 0.85f) / ringN);
                    Push();
                    FilmScreen.OnlyWords(game, "VisitorCard");
                    StoryFilm.AntsOffLens(game);
                    yield return end;
                    FilmScreen.Save(StoryFilm.Frame(sub, f));
                    yield return null;
                }
                Form(i, 0f, 0f);
                ringShown[i] = 1f;
                parts.Append($"{t.index:000} gather 0 {f}\n");
            }
            // the ant walks up, its antennae reach out, the card opens and its words are typed out
            int cardFrom = f;
            float opened = -1f, typed = -1f;
            string full = null;
            float[] when = null;
            for (int k = 0, max = 28 * Fps; k < max; k++, f++)
            {
                Vector2 pos = DemoMode.Pos(game);
                float toThing = (at - pos).magnitude - t.reach;
                if (shown == i && opened < 0f) { opened = 0f; full = cardBody.text; when = Typing(full, zh); }
                if (opened < 0f && k == 11 * Fps) Read(i, game.hud);   // (kept from it by something: read all the same)
                if (opened >= 0f)
                {
                    game.demoMove = Vector2.zero;
                    opened += 1f / Fps;
                    int c = 0;
                    while (c < when.Length && when[c] <= opened - 0.45f) c++;
                    if (typeOut) cardBody.text = c >= full.Length ? full : full.Substring(0, c) + "<color=#00000000>" + full.Substring(c) + "</color>";
                    if (c >= full.Length && typed < 0f) typed = opened;   // (whole at once: held as long as typing it would take)
                }
                else
                {
                    // (after a moment, along the clear sight line straight to it)
                    Vector2 d = at - pos;
                    game.demoMove = k < Mathf.RoundToInt(0.5f * Fps) || d.sqrMagnitude < 0.04f ? Vector2.zero : d.normalized * (toThing < 1.5f ? 0.5f : 1f);
                    if (toThing < 1.2f) game.player.talkTimer = Mathf.Max(game.player.talkTimer, 0.9f);   // its antennae reach out
                }
                Push();
                FilmScreen.OnlyWords(game, "VisitorCard");
                StoryFilm.AntsOffLens(game);
                yield return end;
                FilmScreen.Save(StoryFilm.Frame(sub, f));
                yield return null;
                if (typed >= 0f && opened - typed >= 2.6f) { f++; break; }
            }
            StoryFilm.AntsBack();
            cam.fieldOfView = fov;
            game.rig.manual = false;
            if (full != null) cardBody.text = full;
            parts.Append($"{t.index:000} {(typeOut ? "card" : "plain")}{(zh ? "_zh" : "")} {cardFrom} {f}\n");
            Debug.Log($"[AntGarden] objectfilm: #{t.index} {(gather ? "gathered, " : "")}card {(zh ? "zh" : "en")}, {f} frames");
            shown = -1;
            game.demoMove = Vector2.zero;
            yield return StoryFilm.Settle(1.5f);   // its card fades (not filmed)
        }

        /// <summary>When each letter of the card's words shows (seconds from the first): an even hand, a breath after a
        /// stop or a comma.</summary>
        static float[] Typing(string s, bool zh)
        {
            var when = new float[s.Length];
            float t = 0f, per = zh ? 1f / 11f : 1f / 30f;
            bool tag = false;
            for (int k = 0; k < s.Length; k++)
            {
                char c = s[k];
                if (c == '<') tag = true;   // (rich text's tags take no time)
                if (!tag && c != '\n') t += per;
                when[k] = t;
                if (c == '>') tag = false;
                if (tag) continue;
                if (".!?。！？…".IndexOf(c) >= 0) t += zh ? 0.35f : 0.28f;
                else if (",;:，；：、".IndexOf(c) >= 0) t += zh ? 0.14f : 0.08f;
            }
            return when;
        }
    }
}
