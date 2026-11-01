using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// The prefabs of Meta's XR Core SDK that the headset's input layer draws (MetaDevices): its controllers' models
    /// (OVRControllerPrefab: every Touch controller, the Quest 3's Touch Plus among them) and a hand (OVRHandPrefab: the
    /// headset's own hand mesh on its tracked skeleton). They live in the SDK's package, where a built game cannot load
    /// them, so they are named here, in Resources/AntGarden/VR/MetaRefs.asset (made by VRSetup.ConfigureQuest).
    /// </summary>
    public class MetaRefs : ScriptableObject
    {
        public GameObject controller;
        public GameObject hand;
    }
}
