using System;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityWebRTCForAMSTest;

public class WebRTCTest : MonoBehaviour
{
    [SerializeField] private Toggle publisherToggle;
    [SerializeField] private RawImage publisherDisplay;
    [SerializeField] private InputField publisherSendDataInputFeild;
    [SerializeField] private Button publisherSendDataButton;
    [SerializeField] private Toggle publisherSendDataIntavalToggle;
    [SerializeField] private Dropdown publisherSendDataIntervalDropdown;
    [SerializeField] private Scrollbar publisherLogListScrollbar;
    [SerializeField] private GameObject publisherLogListContent;

    [SerializeField] private Toggle playerToggle;
    [SerializeField] private RawImage playerDisplay;
    [SerializeField] private InputField playerSendDataInputFeild;
    [SerializeField] private Button playerSendDataButton;
    [SerializeField] private Toggle playerSendDataIntavalToggle;
    [SerializeField] private Dropdown playerSendDataIntervalDropdown;
    [SerializeField] private Scrollbar playerLogListScrollbar;
    [SerializeField] private GameObject playerLogListContent;

    [SerializeField] private Toggle logListClearOnConnectToggle;

    [SerializeField] private Dropdown assetTypeDropdown;
    [SerializeField] private InputField streamIdInputField;
    [SerializeField] private Dropdown videoResolutionDropdown;

    [SerializeField] private InputField signalingUrlInputField;
    [SerializeField] private Button connectButton;

    [SerializeField] private GameObject logItem;
    [SerializeField] private VideoPlayer videoPlayer;

    [SerializeField] private Button resetSettingsButton;
    [SerializeField] private Button logClearButton;

    private AssetType assetType;
    private int videoWidth;
    private int videoHeight;
    private int videoBitrate;
    private bool isRunning;
    private IEnumerator webrtcCoroutine;
    private SynchronizationContext context;

    AMSClient publisher;
    AMSClient player;

    void Start()
    {
        context = SynchronizationContext.Current;
        foreach (Transform item in publisherLogListContent.transform)
            Destroy(item.gameObject);
        foreach (Transform item in playerLogListContent.transform)
            Destroy(item.gameObject);

        resetSettingsButton.onClick.AddListener(resetSettings);
        logClearButton.onClick.AddListener(logClear);

        assetTypeDropdown.AddOptions(Enum.GetNames(typeof(AssetType)).ToList());
        assetTypeDropdown.onValueChanged.AddListener(i => assetType = (AssetType)i);
        videoResolutionDropdown.AddOptions(Enum.GetNames(typeof(VideoResolution)).Select(x => x.Replace("_", "")).ToList());
        videoResolutionDropdown.onValueChanged.AddListener(i => setVideoResolution((VideoResolution)i));

        signalingUrlInputField.onValidateInput = signalingUrlInputFieldValidate;
        streamIdInputField.onValidateInput = streamIdInputFieldValidate;
        connectButton.onClick.AddListener(onConnectButtonClick);

        publisher = createClient(ClientType.Publisher);
        publisher.OnOpen.AddListener(() =>
        {
            Task.Run(async () =>
            {
                await Task.Delay(3000);
                if (!isRunning) return;
                context.Post(_ =>
                {
                    if (playerToggle.isOn)
                        player.Connect(
                            assetType,
                            signalingUrlInputField.text,
                            streamIdInputField.text,
                            videoWidth,
                            videoHeight,
                            videoBitrate);
                }, null);
            });
        });
        player = createClient(ClientType.Player);

        loadSettings();

        assetType = (AssetType)assetTypeDropdown.value;
        setVideoResolution((VideoResolution)videoResolutionDropdown.value);
    }

    private void setVideoResolution(VideoResolution videoResolution)
    {
        switch (videoResolution)
        {
            case VideoResolution._2160p: videoWidth = 3840; videoHeight = 2160; videoBitrate = 6000000; break;
            case VideoResolution._1440p: videoWidth = 2560; videoHeight = 1440; videoBitrate = 4000000; break;
            case VideoResolution._1080p: videoWidth = 1920; videoHeight = 1080; videoBitrate = 2000000; break;
            case VideoResolution._720p: videoWidth = 1280; videoHeight = 720; videoBitrate = 1500000; break;
            case VideoResolution._480p: videoWidth = 854; videoHeight = 480; videoBitrate = 1000000; break;
            case VideoResolution._360p: videoWidth = 640; videoHeight = 360; videoBitrate = 800000; break;
            case VideoResolution._240p: videoWidth = 426; videoHeight = 240; videoBitrate = 500000; break;
        }
    }

    private void onConnectButtonClick()
    {
        isRunning = !isRunning;
        if (isRunning)
            connect();
        else
            close();
    }

    private char signalingUrlInputFieldValidate(string text, int charIndex, char addedChar)
    {
        if (Regex.IsMatch(addedChar.ToString(), "[0-9A-Za-z\\–\\._/:~]"))
            return addedChar;
        else
            return '\0';
    }

    private char streamIdInputFieldValidate(string text, int charIndex, char addedChar)
    {
        if (Regex.IsMatch(addedChar.ToString(), "[a-zA-Z0-9_]"))
            return addedChar;
        else
            return '\0';
    }

    private AMSClient createClient(ClientType clientType)
    {
        return new AMSClient(
            this,
            logListClearOnConnectToggle.isOn,
            clientType,
            videoPlayer,
            clientType == ClientType.Publisher ? publisherDisplay : playerDisplay,
            clientType == ClientType.Publisher ? publisherToggle : playerToggle,
            clientType == ClientType.Publisher ? publisherSendDataInputFeild : playerSendDataInputFeild,
            clientType == ClientType.Publisher ? publisherSendDataButton : playerSendDataButton,
            clientType == ClientType.Publisher ? publisherSendDataIntavalToggle : playerSendDataIntavalToggle,
            clientType == ClientType.Publisher ? publisherSendDataIntervalDropdown : playerSendDataIntervalDropdown,
            clientType == ClientType.Publisher ? publisherLogListScrollbar : playerLogListScrollbar,
            clientType == ClientType.Publisher ? publisherLogListContent : playerLogListContent,
            logItem);
    }

    private void connect()
    {
        connectButton.GetComponentInChildren<Text>().text = "Close";

        if (logListClearOnConnectToggle.isOn)
            logClear();

        assetTypeDropdown.interactable = false;
        streamIdInputField.interactable = false;
        videoResolutionDropdown.interactable = false;
        logListClearOnConnectToggle.interactable = false;

        var signalingUrl = signalingUrlInputField.text;
        if (string.IsNullOrEmpty(signalingUrl.Trim()))
        {
            publisher.AddLog(LogLevel.Error, "SignalingUrl is empty", "");
            return;
        }

        var streamId = streamIdInputField.text;
        if (string.IsNullOrEmpty(streamId.Trim()))
        {
            publisher.AddLog(LogLevel.Error, "StreamId is empty", "");
            return;
        }

        if (assetType == AssetType.Unity)
        {
            WebRTC.Initialize(EncoderType.Software);
            webrtcCoroutine = WebRTC.Update();
            StartCoroutine(webrtcCoroutine);
        }

        if (publisherToggle.isOn)
            publisher.Connect(
                assetType,
                signalingUrlInputField.text,
                streamIdInputField.text,
                videoWidth,
                videoHeight,
                videoBitrate);
        else if (playerToggle.isOn)
            player.Connect(
                assetType,
                signalingUrlInputField.text,
                streamIdInputField.text,
                videoWidth,
                videoHeight,
                videoBitrate);
    }

    private void close()
    {
        connectButton.GetComponentInChildren<Text>().text = "Connect";

        assetTypeDropdown.interactable = true;
        streamIdInputField.interactable = true;
        videoResolutionDropdown.interactable = true;
        logListClearOnConnectToggle.interactable = true;

        publisher.Close();
        player.Close();

        if (assetType == AssetType.Unity)
        {
            StopCoroutine(webrtcCoroutine);
            WebRTC.Dispose();
        }
    }

    public void RunCoroutine(IEnumerator croutine)
    {
        StartCoroutine(croutine);
    }

    private void Update()
    {
        publisher?.Update();
        player?.Update();
    }

    private void OnDestroy()
    {
        saveSettings();

        publisher?.Dispose();
        player?.Dispose();
        publisher = null;
        player = null;
    }

    //private void OnApplicationQuit()
    //{
    //    OnDestroy();
    //}

    private void loadSettings()
    {
        signalingUrlInputField.text = PlayerPrefs.GetString("WebRTCForAMS_SignalingUrl", "");
        assetTypeDropdown.value = (int)Enum.Parse(typeof(AssetType), PlayerPrefs.GetString("WebRTCForAMS_AssetType", "AMS"));
        streamIdInputField.text = PlayerPrefs.GetString("WebRTCForAMS_StreamId", "WebRTCTest");
        videoResolutionDropdown.value = (int)Enum.Parse(typeof(VideoResolution), PlayerPrefs.GetString("WebRTCForAMS_VideoResolution", "_720p"));
        setVideoResolution((VideoResolution)videoResolutionDropdown.value);
        logListClearOnConnectToggle.isOn = PlayerPrefs.GetString("WebRTCForAMS_LogListClearOnConnect", "True") == "True";

        publisherToggle.isOn = PlayerPrefs.GetString("WebRTCForAMS_Publisher_Enable", "True") == "True";
        publisherSendDataInputFeild.text = PlayerPrefs.GetString("WebRTCForAMS_Publisher_SendData", "foo");
        publisherSendDataIntavalToggle.isOn = PlayerPrefs.GetString("WebRTCForAMS_Publisher_SendDataInterval", "False") == "True";
        publisherSendDataIntervalDropdown.value = (int)Enum.Parse(typeof(SendDataInterval), PlayerPrefs.GetString("WebRTCForAMS_Publisher_SendDataIntervalTime", "_1s"));

        playerToggle.isOn = PlayerPrefs.GetString("WebRTCForAMS_Player_Enable", "False") == "True";
        playerSendDataInputFeild.text = PlayerPrefs.GetString("WebRTCForAMS_Player_SendData", "bar");
        playerSendDataIntavalToggle.isOn = PlayerPrefs.GetString("WebRTCForAMS_Player_SendDataInterval", "False") == "True";
        playerSendDataIntervalDropdown.value = (int)Enum.Parse(typeof(SendDataInterval), PlayerPrefs.GetString("WebRTCForAMS_Player_SendDataIntervalTime", "_1s"));
    }

    private void saveSettings()
    {
        PlayerPrefs.SetString("WebRTCForAMS_SignalingUrl", signalingUrlInputField.text);
        PlayerPrefs.SetString("WebRTCForAMS_AssetType", ((AssetType)assetTypeDropdown.value).ToString());
        PlayerPrefs.SetString("WebRTCForAMS_StreamId", streamIdInputField.text);
        PlayerPrefs.SetString("WebRTCForAMS_VideoResolution", ((VideoResolution)videoResolutionDropdown.value).ToString());
        PlayerPrefs.SetString("WebRTCForAMS_LogListClearOnConnect", logListClearOnConnectToggle.isOn.ToString());

        PlayerPrefs.SetString("WebRTCForAMS_Publisher_Enable", publisherToggle.isOn.ToString());
        PlayerPrefs.SetString("WebRTCForAMS_Publisher_SendData", publisherSendDataInputFeild.text);
        PlayerPrefs.SetString("WebRTCForAMS_Publisher_SendDataInterval", publisherSendDataIntavalToggle.isOn.ToString());
        PlayerPrefs.SetString("WebRTCForAMS_Publisher_SendDataIntervalTime", ((SendDataInterval)publisherSendDataIntervalDropdown.value).ToString());

        PlayerPrefs.SetString("WebRTCForAMS_Player_Enable", playerToggle.isOn.ToString());
        PlayerPrefs.SetString("WebRTCForAMS_Player_SendData", playerSendDataInputFeild.text);
        PlayerPrefs.SetString("WebRTCForAMS_Player_SendDataInterval", playerSendDataIntavalToggle.isOn.ToString());
        PlayerPrefs.SetString("WebRTCForAMS_Player_SendDataIntervalTime", ((SendDataInterval)playerSendDataIntervalDropdown.value).ToString());

        PlayerPrefs.Save();
    }

    private void resetSettings()
    {
        signalingUrlInputField.text = "";
        assetTypeDropdown.value = (int)AssetType.AMS;
        streamIdInputField.text = "WebRTCTest";
        videoResolutionDropdown.value = (int)VideoResolution._720p;
        logListClearOnConnectToggle.isOn = true;

        publisherToggle.isOn = true;
        publisherSendDataIntavalToggle.isOn = false;
        publisherSendDataInputFeild.text = "foo";
        publisherSendDataIntervalDropdown.value = (int)SendDataInterval._1s;

        playerToggle.isOn = false;
        playerSendDataIntavalToggle.isOn = false;
        playerSendDataInputFeild.text = "bar";
        playerSendDataIntervalDropdown.value = (int)SendDataInterval._1s;

        setVideoResolution((VideoResolution)videoResolutionDropdown.value);

        PlayerPrefs.DeleteAll();
    }


    private void logClear()
    {
        foreach (Transform item in publisherLogListContent.transform)
            Destroy(item.gameObject);
        foreach (Transform item in playerLogListContent.transform)
            Destroy(item.gameObject);
    }
}
