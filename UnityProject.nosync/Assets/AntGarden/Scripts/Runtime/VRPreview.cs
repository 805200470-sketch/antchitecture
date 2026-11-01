using System.Collections;
using System.IO;
using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// The headset's view on a computer with no headset (-vrpreview &lt;dir&gt;, with -quest &lt;quest.json&gt; for the
    /// headset's own values): the VR rig is made as a headset makes it (VRPlayer.Active), the head held still where the
    /// player stands, looking ahead, about as wide as a Quest 3's eye sees (a window shaped as one eye is: -screen-width
    /// 1032 -screen-height 1104); stills of it go to dir (vr_1.png, vr_2.png, …, every few seconds once the nest is
    /// ready), then the game quits. For when no headset can be put on (11-01: the headset, left in a dark room, could
    /// not see where it was, and no game would start). Never on the headset itself.
    /// </summary>
    public class VRPreview : MonoBehaviour
    {
        const float VerticalFov = 96f;      // a Quest 3's eye, top to bottom (degrees)
        const int Stills = 4;
        const float First = 3f, Every = 4f; // the first still this long after the nest is ready, then one every (seconds)

        /// <summary>Where the stills go (null: no preview).</summary>
        public static string Dir => Application.platform == RuntimePlatform.Android ? null : Game.Arg("-vrpreview");
        public static bool On => !string.IsNullOrEmpty(Dir);

        public void Init(Camera cam)
        {
            cam.fieldOfView = VerticalFov;
            cam.transform.localPosition = Vector3.zero;
            cam.transform.localRotation = Quaternion.identity;
            StartCoroutine(Run(Dir));
        }

        IEnumerator Run(string dir)
        {
            Directory.CreateDirectory(dir);
            yield return new WaitForSeconds(First);
            for (int i = 1; i <= Stills; i++)
            {
                yield return new WaitForEndOfFrame();
                Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(dir, $"vr_{i}.png"), tex.EncodeToPNG());
                Destroy(tex);
                Debug.Log($"[AntGarden] VR preview: still {i} of {Stills}");
                if (i < Stills) yield return new WaitForSeconds(Every);
            }
            Application.Quit();
        }
    }
}
