using System;
using UnityEngine;
using UnityEngine.Events;

namespace UnityWebRTCForAMSTest
{
    public class AMSUnitySDKClient : IWebRTCAsset, IDisposable
    {
        private Sora sora;
        private uint trackId;
        private RenderTexture receiveTexture;
        private ClientType? clientType = null;

        private UnityWebRTCForAMSLogEvent logEvent = new UnityWebRTCForAMSLogEvent();
        private UnityWebRTCForAMSLogEvent warningEvent = new UnityWebRTCForAMSLogEvent();
        private UnityWebRTCForAMSLogEvent errorEvent = new UnityWebRTCForAMSLogEvent();
        private UnityEvent openEvent = new UnityEvent();
        private UnityEvent closeEvent = new UnityEvent();
        private VideoTrackEvent videoTrackEvent = new VideoTrackEvent();
        private UnityEvent dataChannelOpenEvent = new UnityEvent();
        public UnityWebRTCForAMSLogEvent OnLogEvent => logEvent;
        public UnityWebRTCForAMSLogEvent OnWarningEvent => warningEvent;
        public UnityWebRTCForAMSLogEvent OnErrorEvent => errorEvent;
        public UnityEvent OnOpen => openEvent;
        public UnityEvent OnClose => closeEvent;
        public VideoTrackEvent OnVideoTrack => videoTrackEvent;
        public UnityEvent OnDataChannelOpen => dataChannelOpenEvent;

        public AMSUnitySDKClient()
        {
        }

        public void Connect(
            string signalingUrl,
            ClientType clientType,
            string streamId,
            int videoWidth,
            int videoHeight,
            int videoBitrate,
            RenderTexture renderTexture)
        {
            try
            {
                sora = new Sora();

                this.clientType = clientType;

                var config = new Sora.Config
                {
                    SignalingUrl = signalingUrl,
                    ChannelId = streamId,
                    VideoWidth = videoWidth,
                    VideoHeight = videoHeight,
                    VideoBitrate = videoBitrate,
                    AudioOnly = false,
                    Multistream = false,
                    Role = clientType == ClientType.Publisher ? Sora.Role.Sendonly : Sora.Role.Recvonly,
                    CapturerType = Sora.CapturerType.UnityRenderTexture,
                    UnityRenderTexture = renderTexture
                };

                sora.OnAddTrack = (trackId) =>
                {
                    this.trackId = trackId;
                    OnLogEvent.Invoke("OnAddTrack", $"trackId: {trackId}");
                    //Debug.Log($"OnAddTrack ptr:{receiveTexture.GetNativeTexturePtr()}");
                    //OnVideoTrack.Invoke(receiveTexture);
                };
                if (clientType == ClientType.Player)
                {
                    receiveTexture = renderTexture;

                    sora.OnRemoveTrack = (trackId) =>
                    {
                        this.trackId = 0;
                        OnLogEvent.Invoke("OnRemoveTrack", $"trackId: {trackId}");
                    };
                }
                sora.OnNotify = (msg) =>
                {
                    OnLogEvent.Invoke("OnNotify", $"\"{msg}\"");
                };

                var isSuccess = sora.Connect(config);

                if (isSuccess)
                {
                    OnOpen?.Invoke();
                    OnLogEvent.Invoke("Sora Connect", "success");
                    OnDataChannelOpen?.Invoke();
                }
                else
                    OnErrorEvent.Invoke("sora connect error", "");
            }
            catch (Exception ex)
            {
                OnErrorEvent.Invoke("OnError", ex.Message);
            }
        }

        public void Update()
        {
            if (sora != null)
            {
                sora.DispatchEvents();
                sora.OnRender();
                if (clientType == ClientType.Player)
                {
                    if (trackId != 0 && receiveTexture != null)
                    {
                        sora.RenderTrackToTexture(trackId, receiveTexture);
                        OnVideoTrack.Invoke(receiveTexture);
                    }
                }
            }
        }

        public void SendDataChannelData(string msg)
        {
            sora.SendDataChannelMessage(msg);
        }

        public void Close()
        {
            logEvent?.RemoveAllListeners();
            warningEvent?.RemoveAllListeners();
            errorEvent?.RemoveAllListeners();
            dataChannelOpenEvent?.RemoveAllListeners();
            logEvent = null;
            warningEvent = null;
            errorEvent = null;
            dataChannelOpenEvent = null;

            if (sora != null)
            {
                sora.OnAddTrack = null;
                sora.OnRemoveTrack = null;
                sora.OnNotify = null;
                sora.Dispose();
                sora = null;
            }
        }

        public void Dispose()
        {
            Close();
        }
    }
}
