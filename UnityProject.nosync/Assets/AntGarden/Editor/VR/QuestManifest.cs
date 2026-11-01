#if UNITY_ANDROID   // (the Android build support is only there with Android the active target)
using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace AntGarden.EditorTools
{
    /// <summary>
    /// The headset app's Android manifest without eye tracking. Unity's OpenXR 1.14 (Meta Quest Support) asks for it in
    /// every app, required, whatever the app does: a Quest 3 has no eye tracking and the game never looks at the eyes, so
    /// the player is never asked for it and the app does not claim a feature the headset lacks. (OpenXR 1.10, before
    /// Meta's XR Core SDK came in on 11-01, did not ask.) Run after Unity's and Meta's own changes to the manifests.
    /// </summary>
    class QuestManifest : IPostGenerateGradleAndroidProject
    {
        const string Android = "http://schemas.android.com/apk/res/android";
        static readonly string[] EyeTracking = { "oculus.software.eye_tracking", "com.oculus.permission.EYE_TRACKING" };

        public int callbackOrder => 10000;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            int removed = 0;
            foreach (string file in new[] { Path.Combine(path, "xrmanifest.androidlib", "AndroidManifest.xml"), Path.Combine(path, "src", "main", "AndroidManifest.xml") })
            {
                if (!File.Exists(file)) continue;
                var doc = new XmlDocument();
                doc.Load(file);
                XmlNodeList nodes = doc.SelectNodes("/manifest/uses-feature | /manifest/uses-permission");
                int before = removed;
                for (int i = nodes.Count - 1; i >= 0; i--)
                {
                    if (!(nodes[i] is XmlElement e) || System.Array.IndexOf(EyeTracking, e.GetAttribute("name", Android)) < 0) continue;
                    e.ParentNode.RemoveChild(e);
                    removed++;
                }
                if (removed > before) doc.Save(file);
            }
            Debug.Log($"[AntGarden] VR (Android): eye tracking taken out of the manifest ({removed} entries)");
        }
    }
}
#endif
