using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace AntGarden
{
    /// <summary>
    /// The game's own data files (StreamingAssets: game_config.json, the nests' path drawings), read the same way on
    /// every platform. On Android (a standalone headset, QuestLook) they lie inside the app's package, where System.IO
    /// cannot open them: there they are read through UnityWebRequest, waited for (they are small, and read on the main
    /// thread while the world loads).
    /// </summary>
    public static class StreamingFiles
    {
        /// <summary>The file's text, or null when there is none.</summary>
        public static string Read(string path)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (UnityWebRequest req = UnityWebRequest.Get(path))
            {
                UnityWebRequestAsyncOperation op = req.SendWebRequest();
                while (!op.isDone) System.Threading.Thread.Sleep(1);
                return req.result == UnityWebRequest.Result.Success ? req.downloadHandler.text : null;
            }
#else
            return File.Exists(path) ? File.ReadAllText(path) : null;
#endif
        }
    }
}
