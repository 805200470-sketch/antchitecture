using System.Collections.Generic;
using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// What some of the NPC ants say when the player touches antennae with them (E or A beside one, or reaching out with the
    /// antennae: Game.Interact), nest by nest (11-04: the user wanted some ants, not all, to say short things tied to the
    /// story). The words are data, not code: Resources/AntGarden/Text/npc_lines.json, a few quiet lines per nest in 中文 and
    /// English, each of its nest (the fire long ago and the ancestors' carved stump in the temple, the resin that keeps
    /// things in the amber nest, the granary's seeds and honey, the leafcutters' garden, the moss forest's slowness, the
    /// blue forest's clean air and the paths the ants dug), the last of each telling where its light leads, the next nest
    /// round the ring. The file also says who is named as speaking (一只蚂蚁 / An ant: no model number), about how many of a
    /// nest's ants have something to say (share; the rest only touch antennae back), how many lines one ant says (perAnt)
    /// and how long a line just said stays when the same ant is touched again at once (again). The user reviews the same
    /// lines as tables in Docs/文本/NPC台词_草稿.md.
    /// A nest's lines are dealt from one shuffled deck kept for the whole session (a nest left and come back to goes on where
    /// it stopped), so a line comes round again only after all the others, and never twice running. The speakers are spread
    /// evenly through a nest's ants (they come shuffled, Game.SpeciesMix), the first always among them. Places the file names
    /// no lines for (the old archipelago, the ruins, the nests left out of the ring) keep the world's old lines
    /// (Voices.AntLines), said by the ants NpcSettings.messengerShare picked, as before.
    /// </summary>
    public static class NpcLines
    {
        [System.Serializable] class Line { public string zh, en; }
        [System.Serializable] class Nest { public string nest, name; public Line[] lines; }
        [System.Serializable]
        class Table
        {
            public Line speaker;
            public float share = 0.5f;
            public int perAnt = 2;
            public float again = 4f;
            public Nest[] nests;
        }

        /// <summary>One ant with something to say: the deck its lines come from, and how many it has said.</summary>
        public sealed class Voice
        {
            internal Deck deck;
            internal int said;
            /// <summary>Whose lines (a nest's biome; "*" the world's old lines).</summary>
            public string From => deck.nest;
            public int Said => said;
        }

        /// <summary>A nest's lines in a shuffled order, dealt one by one and shuffled again when all are dealt.</summary>
        internal sealed class Deck
        {
            public readonly string nest;
            readonly (string zh, string en)[] lines;
            readonly int[] order;
            readonly System.Random rng;
            int next, last = -1;

            public Deck(string nest, (string zh, string en)[] lines, int seed)
            {
                this.nest = nest;
                this.lines = lines;
                order = new int[lines.Length];
                rng = new System.Random(seed);
                Shuffle();
            }

            void Shuffle()
            {
                for (int i = 0; i < order.Length; i++) order[i] = i;
                for (int i = order.Length - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (order[i], order[j]) = (order[j], order[i]);
                }
                if (order.Length > 1 && order[0] == last) (order[0], order[1]) = (order[1], order[0]);   // never the same line twice running
                next = 0;
            }

            public (string zh, string en) Draw()
            {
                if (next >= order.Length) Shuffle();
                last = order[next++];
                return lines[last];
            }
        }

        const string Asset = "AntGarden/Text/npc_lines";   // (Resources/…/npc_lines.json)
        static Table table;
        static readonly Dictionary<string, Deck> decks = new Dictionary<string, Deck>();
        static Voice lastVoice;
        static float lastAt = -100f;

        static Table Data
        {
            get
            {
                if (table != null) return table;
                TextAsset asset = Resources.Load<TextAsset>(Asset);
                try { table = asset != null ? JsonUtility.FromJson<Table>(asset.text) : null; }
                catch (System.Exception e) { Debug.LogWarning($"[AntGarden] npc lines: {Asset}.json unreadable ({e.Message}); the old lines everywhere"); }
                if (table == null) table = new Table();
                if (table.nests == null) table.nests = new Nest[0];
                int lines = 0;
                foreach (Nest n in table.nests) lines += n.lines != null ? n.lines.Length : 0;
                Debug.Log($"[AntGarden] npc lines: {table.nests.Length} nests, {lines} lines, share {table.share:F2}, {table.perAnt} an ant");
                return table;
            }
        }

        /// <summary>The nest's entry with lines in it, or null.</summary>
        static Nest Find(string nest)
        {
            if (string.IsNullOrEmpty(nest)) return null;
            foreach (Nest n in Data.nests)
                if (n.nest == nest && n.lines != null && n.lines.Length > 0) return n;
            return null;
        }

        /// <summary>
        /// What the i-th of a nest's ants has to say: a voice drawing on its nest's lines, or null when it only touches
        /// antennae back. nest: the region's biome in a particle nest (null elsewhere); messenger: whether the world's old
        /// draw (NpcSettings.messengerShare) picked it, which decides where the file has no lines for the nest.
        /// </summary>
        public static Voice For(string nest, int i, bool messenger)
        {
            Nest n = Find(nest);
            if (n == null) return messenger ? new Voice { deck = DeckOf("*", null) } : null;
            float share = Mathf.Clamp01(Data.share);
            bool speaks = Mathf.FloorToInt((i + 1) * share + 0.5f) > Mathf.FloorToInt(i * share + 0.5f);   // evenly spread, the first among them
            return speaks ? new Voice { deck = DeckOf(n.nest, n) } : null;
        }

        static Deck DeckOf(string nest, Nest n)
        {
            if (decks.TryGetValue(nest, out Deck d)) return d;
            var lines = new List<(string zh, string en)>();
            if (n == null) lines.AddRange(Voices.AntLines);
            else
                foreach (Line l in n.lines)
                    if (l != null && !string.IsNullOrEmpty(l.zh)) lines.Add((l.zh, string.IsNullOrEmpty(l.en) ? l.zh : l.en));
            if (lines.Count == 0) lines.AddRange(Voices.AntLines);
            // the same order in every check run (its stills stay comparable); another in each session played
            int seed = Hash(nest) ^ (Tutorial.CheckRun ? 0 : System.Environment.TickCount);
            decks[nest] = d = new Deck(nest, lines.ToArray(), seed);
            Debug.Log($"[AntGarden] npc lines: {nest}, {lines.Count} to deal");
            return d;
        }

        static int Hash(string s)
        {
            int h = 17;
            foreach (char c in s) h = h * 31 + c;
            return h;
        }

        /// <summary>
        /// The line an ant says as antennae touch, in both languages; null when it has none, has said its share, or is
        /// touched again at once while the line it just said is still up (that line stays).
        /// </summary>
        public static (string zh, string en)? Say(Voice v, bool lineShowing)
        {
            if (v == null) return null;
            if (v == lastVoice && lineShowing && Time.unscaledTime - lastAt < Data.again) return null;
            if (Data.perAnt > 0 && v.said >= Data.perAnt) return null;
            v.said++;
            lastVoice = v;
            lastAt = Time.unscaledTime;
            return v.deck.Draw();
        }

        /// <summary>Who is named as speaking, in both languages.</summary>
        public static (string zh, string en) Speaker
        {
            get
            {
                Line s = Data.speaker;
                if (s == null || string.IsNullOrEmpty(s.zh)) return ("一只蚂蚁", "An ant");
                return (s.zh, string.IsNullOrEmpty(s.en) ? s.zh : s.en);
            }
        }

        /// <summary>A nest's lines as the file has them (checks); empty when it has none.</summary>
        public static List<(string zh, string en)> Of(string nest)
        {
            var all = new List<(string zh, string en)>();
            Nest n = Find(nest);
            if (n != null) foreach (Line l in n.lines) if (l != null && !string.IsNullOrEmpty(l.zh)) all.Add((l.zh, string.IsNullOrEmpty(l.en) ? l.zh : l.en));
            return all;
        }
    }
}
