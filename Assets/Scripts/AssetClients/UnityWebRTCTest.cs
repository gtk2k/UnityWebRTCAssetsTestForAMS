using System;
using System.Collections;
using System.Text;
using System.Threading;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.Events;

namespace UnityWebRTCForAMSTest
{
    class UnityWebRTCTest : IWebRTCAsset, IDisposable
    {
        private SynchronizationContext context;
        private ClientType clientType;
        private int videoWidth;
        private int videoHeight;
        private int videoBitrate;
        private RenderTexture renderTexture;
        private IEnumerator webrtcUpdate;
        private IEnumerator coroutine;
        private IEnumerator coroutine2;
        private string streamId;
        private AntMediaSignaling signaling;
        private RTCPeerConnection peer;
        private RTCDataChannel dataChannel;
        private RTCConfiguration peerConfig = new RTCConfiguration
        {
            iceServers = new[] { new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } } }
        };
        private RTCOfferOptions offerOption = new RTCOfferOptions
        {
            iceRestart = false,
            offerToReceiveAudio = false,
            offerToReceiveVideo = true
        };
        private RTCAnswerOptions answerOptions = new RTCAnswerOptions
        {
            iceRestart = false
        };

        private UnityWebRTCForAMSLogEvent logEvent = new UnityWebRTCForAMSLogEvent();
        private UnityWebRTCForAMSLogEvent warningEvent = new UnityWebRTCForAMSLogEvent();
        private UnityWebRTCForAMSLogEvent errorEvent = new UnityWebRTCForAMSLogEvent();
        private VideoTrackEvent videoTrackEvent = new VideoTrackEvent();
        private UnityEvent openEvent = new UnityEvent();
        private UnityEvent closeEvent = new UnityEvent();
        private UnityEvent dataChannelOpenEvent = new UnityEvent();
        public UnityWebRTCForAMSLogEvent OnLogEvent => logEvent;
        public UnityWebRTCForAMSLogEvent OnWarningEvent => warningEvent;
        public UnityWebRTCForAMSLogEvent OnErrorEvent => errorEvent;
        public VideoTrackEvent OnVideoTrack => videoTrackEvent;
        public UnityEvent OnOpen => openEvent;
        public UnityEvent OnClose => closeEvent;
        public UnityEvent OnDataChannelOpen => dataChannelOpenEvent;

        public void Connect(
            string signalingUrl,
            ClientType clientType,
            string streamId,
            int videoWidth,
            int videoHeight,
            int videoBitrate,
            RenderTexture renderTexture)
        {
            context = SynchronizationContext.Current;

            this.clientType = clientType;
            this.streamId = streamId;
            this.videoWidth = videoWidth;
            this.videoHeight = videoHeight;
            this.videoBitrate = videoBitrate;
            this.renderTexture = renderTexture;
            try
            {
                signaling = new AntMediaSignaling(signalingUrl);
                signaling.OnOpen += Signaling_OnOpen;
                signaling.OnStart += Signaling_OnStart;
                signaling.OnAnswer += Signaling_OnAnswer;
                signaling.OnOffer += Signaling_OnOffer;
                signaling.OnIceCandidate += Signaling_OnIceCandidate;
                signaling.OnClose += Signaling_OnClose;
                signaling.OnWSError += Signaling_OnWSError;
                signaling.OnSignalingError += Signaling_OnSignalingError;

                signaling.Connect();
            }
            catch (Exception ex)
            {
                OnErrorEvent.Invoke("Connect() Error", ex.Message);
            }
        }

        private void connectPeer()
        {
            OnLogEvent.Invoke("new RTCPeerConnection", "");
            peer = new RTCPeerConnection(ref peerConfig);
            peer.OnConnectionStateChange = connectionState =>
            {
                OnLogEvent.Invoke("OnConnectionStateChange", connectionState.ToString());
            };
            peer.OnDataChannel = channel =>
            {
                dataChannel = channel;
                setupDataChannelEventHandler();
                OnLogEvent.Invoke("OnDataChannel", channel.Label);
            };
            peer.OnIceCandidate = candidate =>
            {
                OnLogEvent.Invoke("OnIceCandidate", "");
                OnLogEvent.Invoke("Send IceCandidate", "");
                signaling.SendIceCandidate(streamId, candidate.Candidate, candidate.SdpMLineIndex.Value, candidate.SdpMid);
            };
            peer.OnIceGatheringStateChange = state =>
            {
                OnLogEvent.Invoke("OnIceGatheringStateChange", state.ToString());
            };
            peer.OnNegotiationNeeded = () =>
            {
                OnLogEvent.Invoke("OnNegotiationNeeded", "");
            };
            peer.OnTrack = evt =>
            {
                OnLogEvent.Invoke("OnTrack", evt.Track.Kind.ToString());
                if (evt.Track is VideoStreamTrack track)
                {
                    var texture = track.InitializeReceiver(videoWidth, videoHeight);
                    OnVideoTrack.Invoke(texture);
                }
            };

            var dcOptions = new RTCDataChannelInit();
            OnLogEvent.Invoke("CreateDataChannel", "testDC");
            dataChannel = peer.CreateDataChannel("testDC", dcOptions);
            setupDataChannelEventHandler();
            if (clientType == ClientType.Publisher)
            {
                var videoTrack = new VideoStreamTrack("VideoTrack", renderTexture);
                peer.AddTrack(videoTrack);
                coroutine = sendDesc(RTCSdpType.Offer);
            }
        }

        private void Signaling_OnOpen()
        {
            logEvent.Invoke("Signaling OnOpen", "");

            if (clientType == ClientType.Publisher)
            {
                OnLogEvent.Invoke("Send Publish Command", streamId);
                signaling.Publish(streamId);
            }
            else
            {
                OnLogEvent.Invoke("Send Play Command", streamId);
                signaling.Play(streamId);
            }
        }

        private void Signaling_OnStart(string streamId)
        {
            OnLogEvent.Invoke("Signaling OnStart", "");
            connectPeer();
        }

        private void Signaling_OnAnswer(AntMediaSignalingMessage msg)
        {
            OnLogEvent.Invoke("Signaling OnAnswer", "");
            coroutine = setRemoteDesc(RTCSdpType.Answer, msg.sdp);
        }

        private void Signaling_OnOffer(AntMediaSignalingMessage msg)
        {
            OnLogEvent.Invoke("Signaling OnOffer", "");
            connectPeer();
            coroutine = setRemoteDesc(RTCSdpType.Offer, msg.sdp);
        }

        private void Signaling_OnIceCandidate(AntMediaSignalingMessage msg)
        {
            OnLogEvent.Invoke("Signaling OnIceCandidate", "");
            OnLogEvent.Invoke("AddIceCandidate", "");
            var candidate = new RTCIceCandidate(new RTCIceCandidateInit
            {
                candidate = msg.candidate,
                sdpMLineIndex = msg.label,
                sdpMid = msg.id
            });
            peer.AddIceCandidate(candidate);
        }

        private void Signaling_OnClose()
        {
            logEvent.Invoke("Signaling OnClose", "");
        }

        private void Signaling_OnWSError(string errorMessage)
        {
            errorEvent.Invoke("Signaling OnWSError", errorMessage);
        }

        private void Signaling_OnSignalingError(string errorMessage)
        {
            errorEvent.Invoke("Signaling OnSignalingError", errorMessage);
        }

        private void setupDataChannelEventHandler()
        {
            dataChannel.OnOpen = () =>
            {
                OnLogEvent.Invoke("DC_OnOpen", "");
                OnDataChannelOpen.Invoke();
            };
            dataChannel.OnMessage = evt =>
            {
                var msg = Encoding.UTF8.GetString(evt);
                OnLogEvent.Invoke("DC_OnMessage", $"\"{msg}\"");
            };
            dataChannel.OnClose = () =>
            {
                OnLogEvent.Invoke("DC_OnClose", "");
            };
        }

        private IEnumerator sendDesc(RTCSdpType type)
        {

            RTCSessionDescriptionAsyncOperation opCreate = null;
            if (type == RTCSdpType.Offer)
            {
                OnLogEvent.Invoke("CreateOffer()", "");
                opCreate = peer.CreateOffer(ref offerOption);
            }
            else
            {
                OnLogEvent.Invoke("CreateAnswer()", "");
                opCreate = peer.CreateAnswer(ref answerOptions);
            }

            yield return opCreate;
            if (opCreate.IsError)
            {
                OnErrorEvent.Invoke($"Create {opCreate.Desc.type}", opCreate.Error.message);
                yield break;
            }
            else
            {
                OnLogEvent.Invoke($"Create {opCreate.Desc.type}", "");
            }

            OnLogEvent.Invoke($"SetLocalDescription {type}", $"{opCreate.Desc.sdp.Substring(0, 10)} ...");
            var desc = opCreate.Desc;
            var opSet = peer.SetLocalDescription(ref desc);
            yield return opSet;
            if (opSet.IsError)
            {
                OnErrorEvent.Invoke($"SetLocalDescription {type}", opSet.Error.message);
                yield break;
            }

            OnLogEvent.Invoke($"Send {type}", "");
            signaling.SendDesc(streamId, desc.type.ToString().ToLower(), desc.sdp);
        }

        private IEnumerator setRemoteDesc(RTCSdpType type, string sdp)
        {
            var desc = new RTCSessionDescription
            {
                type = type,
                sdp = sdp
            };

            OnLogEvent.Invoke($"SetRemoteDescription {type}", "");
            var opSetDesc = peer.SetRemoteDescription(ref desc);
            yield return opSetDesc;

            if (opSetDesc.IsError)
            {
                OnErrorEvent.Invoke($"SetRemoteDescription {type}", opSetDesc.Error.message);
                yield break;
            }

            if (type == RTCSdpType.Offer)
            {
                coroutine2 = sendDesc(RTCSdpType.Answer);
                yield return coroutine2.Current;
            }
            else
                OnOpen.Invoke();
        }

        public void Update()
        {
            //if (webrtcUpdate != null)
            //{
            //    webrtcUpdate.MoveNext();
            //}

            if (coroutine != null)
            {
                bool result = coroutine.MoveNext();
                if (result)
                {
                    Debug.Log("Next.");
                }
                else
                {
                    Debug.Log("End.");
                    coroutine = null;
                }
            }

            if(coroutine2 != null)
            {
                bool result = coroutine2.MoveNext();
                if (result)
                {
                    Debug.Log("Next.");
                }
                else
                {
                    Debug.Log("End.");
                    coroutine2 = null;
                }
            }
        }

        public void SendDataChannelData(string msg)
        {
            if (dataChannel != null)
            {
                dataChannel.Send(msg);
            }
        }

        public void Close()
        {
            coroutine = null;
            webrtcUpdate = null;

            try
            {
                signaling?.Close();
                signaling = null;
            }
            catch (Exception ex)
            {
                OnErrorEvent.Invoke("signaling.close", ex.Message);
            }

            try
            {
                if (dataChannel != null)
                {
                    dataChannel.OnOpen = null;
                    dataChannel.OnMessage = null;
                    dataChannel.OnClose = null;
                    dataChannel.Close();
                    dataChannel = null;
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent.Invoke("dataChannel dispose", ex.Message);
            }

            try
            {
                peer.OnConnectionStateChange = null;
                peer.OnDataChannel = null;
                peer.OnIceCandidate = null;
                peer.OnIceGatheringStateChange = null;
                peer.OnNegotiationNeeded = null;
                peer.OnTrack = null;
                peer.Close();
                peer.Dispose();
                peer = null;
            }
            catch (Exception ex)
            {
                OnErrorEvent.Invoke("peer dispose", ex.Message);
            }
        }

        public void Dispose()
        {
            Close();
        }
    }
}
