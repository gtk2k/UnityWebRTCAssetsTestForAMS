using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.Video;

namespace UnityWebRTCForAMSTest
{
    public class AMSClient
    {
        public UnityEvent OnOpen = new UnityEvent();
        public UnityEvent OnClose = new UnityEvent();

        private bool isRunning;
        private bool isLogClearOnConnect;
        private ClientType clientType;
        private bool sendIntervalFlg = false;
        private float sendInterval = 0f;
        private int sendIntervalCounter = 0;
        private float time = 0f;

        private Toggle clientToggle;
        private RawImage display;
        private VideoPlayer videoPlayer;
        private InputField sendDataInputField;
        private Button sendDataButton;
        private Toggle sendDataIntervalToggle;
        private Dropdown sendDataIntervalDropdown;
        private Scrollbar logListScrollbar;
        private GameObject logList;
        private GameObject logItem;

        private IWebRTCAsset amsClient;

        public AMSClient(
            WebRTCTest coroutineRunner,
            bool isLogClearOnConnect,
            ClientType clientType,
            VideoPlayer videoPlayer,
            RawImage display,
            Toggle clientToggle,
            InputField sendDataInputField,
            Button sendDataButton,
            Toggle sendDataIntervalToggle,
            Dropdown sendDataIntervalDropdown,
            Scrollbar logListScrollbar,
            GameObject logList,
            GameObject logItem)
        {
            this.isLogClearOnConnect = isLogClearOnConnect;
            this.clientType = clientType;

            this.clientToggle = clientToggle;
            this.display = display;
            this.videoPlayer = videoPlayer;
            this.sendDataInputField = sendDataInputField;
            this.sendDataButton = sendDataButton;
            this.sendDataIntervalToggle = sendDataIntervalToggle;
            this.sendDataIntervalDropdown = sendDataIntervalDropdown;
            this.logListScrollbar = logListScrollbar;
            this.logList = logList;
            this.logItem = logItem;

            this.clientToggle.onValueChanged.AddListener(flg => onToggle(flg));
            onToggle(this.clientToggle.isOn);
            this.sendDataIntervalToggle.isOn = false;
            this.sendDataIntervalToggle.onValueChanged.AddListener(flg =>
            {
                sendIntervalFlg = flg;
                if (!flg)
                    sendIntervalCounter = 0;
            });
            this.sendDataIntervalDropdown.AddOptions(Enum.GetNames(typeof(SendDataInterval)).Select(x => x.Replace("_", "")).ToList());
            this.sendDataIntervalDropdown.onValueChanged.AddListener(i => setSendDataInterval((SendDataInterval)i));
            this.sendDataIntervalDropdown.value = (int)SendDataInterval._1s;
            this.sendDataButton.onClick.AddListener(onSendDataClick);

        }

        private void onToggle(bool interactable)
        {
            logListScrollbar.interactable = interactable;
            display.color = interactable ? Color.white : Color.grey;
        }

        private void onDataChannelOpen()
        {
            sendDataInputField.interactable = true;
            sendDataButton.interactable = true;
            sendDataIntervalToggle.interactable = true;
            sendDataIntervalDropdown.interactable = true;
        }

        private void onSendDataClick()
        {
            if (!string.IsNullOrEmpty(sendDataInputField.text))
            {
                amsClient.SendDataChannelData(sendDataInputField.text);
            }
        }

        private void setSendDataInterval(SendDataInterval interval)
        {
            switch (interval)
            {
                case SendDataInterval._100ms: sendInterval = 0.1f; break;
                case SendDataInterval._1s: sendInterval = 1f; break;
                case SendDataInterval._5s: sendInterval = 5f; break;
                case SendDataInterval._10s: sendInterval = 10f; break;
            }
        }

        public void Connect(
            AssetType assetType,
            string signalingUrl,
            string streamId,
            int videoWidth,
            int videoHeight,
            int videoBitrate)
        {
            isRunning = true;

            if (!clientToggle.isOn) return;

            clientToggle.interactable = false;

            RenderTexture tex = null;
            //if (display.texture != null)
            //{
            //    tex = (RenderTexture)display.texture;
            //    display.texture = null;
            //    UnityEngine.Object.Destroy(tex);
            //}
            if (clientType == ClientType.Publisher)
            {
                tex = new RenderTexture(videoWidth, videoHeight, 24, RenderTextureFormat.BGRA32);
                var cb = tex.colorBuffer;
                videoPlayer.targetTexture = tex;
                videoPlayer.Play();
                display.texture = tex;
            }
            else if (assetType == AssetType.AMS)
            {
                tex = new RenderTexture(videoWidth, videoHeight, 16, RenderTextureFormat.ARGB32);
                var cb = tex.colorBuffer;
                display.texture = tex;
            }

            switch (assetType)
            {
                case AssetType.AMS: amsClient = new AMSUnitySDKClient(); break;
                case AssetType.Unity: amsClient = new UnityWebRTCTest(); break;
            }
            amsClient.OnOpen.AddListener(() => OnOpen.Invoke());
            amsClient.OnClose.AddListener(() => OnClose.Invoke());
            amsClient.OnLogEvent.AddListener((name, msg) => AddLog(LogLevel.Log, name, msg));
            amsClient.OnWarningEvent.AddListener((name, msg) => AddLog(LogLevel.Warning, name, msg));
            amsClient.OnErrorEvent.AddListener((name, msg) => AddLog(LogLevel.Error, name, msg));
            amsClient.OnDataChannelOpen.AddListener(onDataChannelOpen);

            amsClient.Connect(signalingUrl, clientType, streamId, videoWidth, videoHeight, videoBitrate, tex);

            if (clientType == ClientType.Player)
                amsClient.OnVideoTrack.AddListener(texture =>
                {
                    display.texture = texture;
                });
        }

        public void Close()
        {
            isRunning = false;
            clientToggle.interactable = true;

            videoPlayer?.Stop();
            amsClient?.Dispose();
            amsClient = null;

            if (display.texture != null)
            {
                var tex = display.texture;
                display.texture = null;
                UnityEngine.Object.Destroy(tex);
            }
        }

        public void Update()
        {
            amsClient?.Update();
            if (isRunning && sendIntervalFlg)
            {
                if (!string.IsNullOrEmpty(sendDataInputField.text))
                {
                    time += Time.deltaTime;
                    if (time >= sendInterval)
                    {
                        time = 0f;
                        sendIntervalCounter++;
                        if (sendIntervalCounter == int.MaxValue)
                            sendIntervalCounter = 0;
                        var msg = $"{sendDataInputField.text} ({sendIntervalCounter})";
                        AddLog(LogLevel.Log, "SendData", $"\"{msg}\"");
                        amsClient.SendDataChannelData(msg);
                    }
                }
            }
        }

        public void AddLog(LogLevel level, string name, string msg)
        {
            var item = UnityEngine.Object.Instantiate(logItem);
            var msgColor = level == LogLevel.Log ? "white" : level == LogLevel.Warning ? "yellow" : "red";
            item.GetComponent<TextMeshProUGUI>().text = $"<mspace=0.6em>{DateTime.Now.ToString("HH:mm:ss.fff")}</mspace> <color={msgColor}>{name}: {msg}</color>";
            item.transform.SetParent(logList.transform);
            item.transform.SetAsFirstSibling();
        }

        public void Dispose()
        {
            clientToggle = null;
            display = null;
            videoPlayer?.Stop();
            videoPlayer = null;
            sendDataInputField = null;
            sendDataButton = null;
            sendDataIntervalToggle = null;
            sendDataIntervalDropdown = null;
            logListScrollbar = null;
            logList = null;
            logItem = null;
        }
    }
}