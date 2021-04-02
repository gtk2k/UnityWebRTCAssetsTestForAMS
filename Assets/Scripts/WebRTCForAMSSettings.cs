using System;
using UnityEngine;

namespace UnityWebRTCForAMSTest
{
    [Serializable]
    [CreateAssetMenu(menuName = "WebRTCForAMS/Settings", fileName = "WebRTCForAMSSettings")]
    public class WebRTCForAMSSettings : ScriptableObject
    {
        [SerializeField]
        public string SignalingUrl = "";
    }
}
