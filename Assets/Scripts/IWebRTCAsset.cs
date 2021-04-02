using UnityEngine;
using UnityEngine.Events;

namespace UnityWebRTCForAMSTest
{
    interface IWebRTCAsset
    {
        UnityWebRTCForAMSLogEvent OnLogEvent { get; }
        UnityWebRTCForAMSLogEvent OnWarningEvent { get; }
        UnityWebRTCForAMSLogEvent OnErrorEvent { get; }
        UnityEvent OnOpen { get; }
        UnityEvent OnClose { get; }
        VideoTrackEvent OnVideoTrack { get; }
        UnityEvent OnDataChannelOpen { get; }

        void Connect(
            string signalingUrl,
            ClientType clientType,
            string streamId,
            int videoWidth,
            int videoHeight,
            int videoBitrate,
            RenderTexture renderTexture
        );
        void Update();
        void SendDataChannelData(string msg);
        void Close();

        void Dispose();
    }
}
