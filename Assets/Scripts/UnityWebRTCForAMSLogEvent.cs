using UnityEngine;
using UnityEngine.Events;

namespace UnityWebRTCForAMSTest
{
    public class UnityWebRTCForAMSLogEvent : UnityEvent<string, string> { }

    public class VideoTrackEvent : UnityEvent<Texture> { }
}
