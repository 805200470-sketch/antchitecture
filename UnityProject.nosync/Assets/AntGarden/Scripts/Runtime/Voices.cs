using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// What the world says, in both languages: lines some NPC ants share when you touch antennae with them,
    /// the preset stories the memory plants hold (placeholders until visitors leave their own), and each
    /// relic's short script (one line per touch). Plus a soft procedural chime, so every reply is heard.
    /// </summary>
    public static class Voices
    {
        public static readonly (string zh, string en)[] AntLines =
        {
            ("东边那片苔藓下面，有人埋了一首歌。", "Under the moss to the east, someone buried a song."),
            ("我们从不一个人走路。", "We never walk alone."),
            ("气味还新鲜，刚有一只从这里回家。", "The scent is fresh. One of us just went home this way."),
            ("上一位客人在大杯子旁边坐了很久。", "The last visitor sat by the big cup for a long time."),
            ("琥珀里睡着我们的祖先，别吵醒她。", "An ancestor sleeps in the amber. Don't wake her."),
            ("你的触角有点凉，你是从上面来的吧？", "Your antennae are cold. You came from above, didn't you?"),
            ("这条路，是三百只蚂蚁一起走出来的。", "Three hundred of us walked this path into being."),
            ("碰一碰会发光的那些，它们记得事情。", "Touch the glowing ones. They remember things."),
            ("我搬过一粒比我大四倍的种子。", "I once carried a seed four times my size."),
            ("下雨那天，整个巢都在唱歌。", "The day it rained, the whole nest sang."),
            ("迷路了，就跟着气味最浓的那条走。", "If you get lost, follow the strongest scent."),
            ("有人在这里留下了一个名字，我替你记着。", "Someone left a name here. I'm keeping it for you."),
        };

        /// <summary>Preset stories (written for the demo, until visitors leave their own).</summary>
        public static readonly (string zh, string en)[] Stories =
        {
            ("小时候我蹲在院子里看蚂蚁搬家，一看就是一下午。", "As a child I watched ants move house in the yard for a whole afternoon."),
            ("希望我也能像它们一样，不着急，一点一点地走。", "I hope I can be like them: no hurry, step by step."),
            ("给外婆：你种的那盆花还活着。", "To my grandmother: the plant you grew is still alive."),
            ("今天是我到伦敦的第一百天。", "Today is my hundredth day in London."),
            ("我们在这里相遇，然后各自回家。", "We met here, and then each of us went home."),
            ("如果你读到这句话，说明这株蘑菇还亮着。", "If you are reading this, this mushroom is still glowing."),
            ("一群很小的东西，也能做很大的事。", "Very small things, together, can do very big things."),
            ("我把一个秘密留在这里了，蚂蚁会帮我保管。", "I left a secret here. The ants will keep it for me."),
        };

        public class Script
        {
            public string zhName, enName;
            public (string zh, string en)[] lines;
            public float pitch = 1;
        }

        public static readonly System.Collections.Generic.Dictionary<string, Script> Relics = new System.Collections.Generic.Dictionary<string, Script>
        {
            ["relic_teacup"] = new Script { zhName = "巨人的茶杯", enName = "The giant's teacup", pitch = 1.0f, lines = new[] {
                ("……还温着。", "...still warm."),
                ("很久以前，有人每天下午都用它喝茶。", "Long ago, someone drank tea from it every afternoon."),
                ("后来那个人搬走了，它留在了花园里。", "Then they moved away, and it stayed in the garden."),
                ("现在，它是蚂蚁的大厅。", "Now it is a hall for the ants.") } },
            ["relic_key"] = new Script { zhName = "生锈的钥匙", enName = "The rusted key", pitch = 0.8f, lines = new[] {
                ("咔哒。", "Click."),
                ("它已经打不开任何一扇门了。", "It opens no door anymore."),
                ("可是蚂蚁把它当成一座桥。", "But the ants use it as a bridge.") } },
            ["relic_coins"] = new Script { zhName = "铜钱台地", enName = "The coin terraces", pitch = 1.35f, lines = new[] {
                ("叮——", "Ting—"),
                ("每一枚上面，都刻着一个年份。", "Each one carries a year."),
                ("最底下那枚，比这个蚁穴还要老。", "The one at the bottom is older than this nest.") } },
            ["relic_button"] = new Script { zhName = "纽扣广场", enName = "The button plaza", pitch = 1.15f, lines = new[] {
                ("四个孔，四条路。", "Four holes, four roads."),
                ("蚂蚁们在这里交换消息。", "The ants trade their news here.") } },
            ["temple_spire"] = new Script { zhName = "陶土尖塔", enName = "A clay spire", pitch = 0.9f, lines = new[] {
                ("螺旋一圈一圈往上，像在数年份。", "The spiral climbs ring by ring, like counting years."),
                ("每一圈，都是一代蚂蚁垒上去的。", "Each ring was laid by one generation.") } },
            ["temple_arch"] = new Script { zhName = "刻着蚁路的拱门", enName = "The carved arch", pitch = 0.75f, lines = new[] {
                ("这些纹路，是一张很老的路网。", "These lines are a very old map of paths."),
                ("和你脚下这一张，几乎一样。", "Almost the same as the one under your feet.") } },
            ["temple_cells"] = new Script { zhName = "六边形巢室", enName = "Hexagon cells", pitch = 1.25f, lines = new[] {
                ("空的……这一格也是空的。", "Empty... this one too."),
                ("它们都搬去别的地方了。", "They have all moved somewhere else.") } },
            ["amber_shrine"] = new Script { zhName = "琥珀里的祖先", enName = "The ancestor in amber", pitch = 0.6f, lines = new[] {
                ("……", "..."),
                ("你走了很远的路。", "You have come a long way."),
                ("我们也曾在这里，搬土，筑塔，碰触角。", "We were here too once: carrying earth, raising spires, touching antennae."),
                ("把你的故事留下吧，下一只蚂蚁会听到。", "Leave your story here. The next ant will hear it.") } },
        };

        public static string T((string zh, string en) line) => Loc.T(line.zh, line.en);

        static AudioClip chime;
        static AudioSource[] voices;
        static int nextVoice;

        /// <summary>A soft bell (a few inharmonic partials, slow decay) at a position, pitched per speaker.</summary>
        public static void Chime(Vector3 at, float pitch, float volume = 0.5f)
        {
            if (chime == null)
            {
                const int rate = 44100;
                int n = (int)(rate * 2.2f);
                var data = new float[n];
                float[] f = { 523.25f, 1309.4f, 2093f, 2853f };
                float[] a = { 1f, 0.45f, 0.22f, 0.1f };
                float[] k = { 2.2f, 3.5f, 5f, 7f };
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)rate, v = 0;
                    for (int j = 0; j < f.Length; j++) v += a[j] * Mathf.Exp(-k[j] * t) * Mathf.Sin(2 * Mathf.PI * f[j] * t);
                    data[i] = v * 0.22f * Mathf.Min(1, t * 400);
                }
                chime = AudioClip.Create("Chime", n, 1, rate, false);
                chime.SetData(data, 0);
                voices = new AudioSource[4];
                for (int i = 0; i < voices.Length; i++)
                {
                    var go = new GameObject("ChimeVoice");
                    Object.DontDestroyOnLoad(go);
                    voices[i] = go.AddComponent<AudioSource>();
                    voices[i].spatialBlend = 0.65f;
                    voices[i].rolloffMode = AudioRolloffMode.Linear;
                    voices[i].maxDistance = 40;
                }
            }
            AudioSource s = voices[nextVoice++ % voices.Length];
            s.transform.position = at;
            s.pitch = pitch;
            s.PlayOneShot(chime, volume);
        }
    }
}
