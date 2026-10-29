using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace AntGarden.EditorTools
{
    /// <summary>
    /// The four-layer palettes NestPalette makes for all twelve segments of the colour wheel, and v15's own colours
    /// (NestTheme.Cyan) to check segment 6 against, as JSON for Tools/palette/palette_cards.py. Batch mode:
    /// -executeMethod AntGarden.EditorTools.PaletteDump.Run -paletteOut &lt;file&gt;
    /// </summary>
    public static class PaletteDump
    {
        public static void Run()
        {
            string path = "../07_Outputs/配色/palettes.json";
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-paletteOut") path = args[i + 1];
            var ci = CultureInfo.InvariantCulture;
            string Rgb(Color c) => string.Format(ci, "[{0:F4},{1:F4},{2:F4}]", c.r, c.g, c.b);
            var sb = new StringBuilder("{\"segments\":[");
            for (int seg = 0; seg < 12; seg++)
            {
                NestPalette.Palette p = NestPalette.For(seg, NestTheme.RecipeFor(seg));
                if (seg > 0) sb.Append(',');
                sb.Append(string.Format(ci, "{{\"segment\":{0},\"hue\":{1:F2},\"warm\":{2:F3},\"temperature\":{3:F2},\"groundGain\":{4:F3},\"exposure\":{5:F2},\"swatches\":[",
                    seg, p.hue, p.warm, p.temperature, p.groundGain, p.exposure));
                for (int k = 0; k < p.swatches.Count; k++)
                {
                    NestPalette.Swatch s = p.swatches[k];
                    if (k > 0) sb.Append(',');
                    sb.Append($"{{\"role\":\"{s.role}\",\"layer\":{s.layer},\"rgb\":{Rgb(s.colour)}}}");
                }
                sb.Append("]}");
            }
            sb.Append("],\"v15\":{");
            bool first = true;
            foreach (string role in NestPalette.Roles)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append($"\"{role}\":{Rgb(NestPalette.Get(NestTheme.Cyan, role))}");
            }
            sb.Append("}}");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllText(path, sb.ToString());
            Debug.Log("[AntGarden] palettes written to " + Path.GetFullPath(path));
        }
    }
}
