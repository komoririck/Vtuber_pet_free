using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NativeWebSocket;

public class UiManager : MonoBehaviour
{
    string authenticationToken = null;

    GameObject sceneController;
    public WebSocketClient webSocketClient;
    public VolumeController volumeController;
    public TMP_Dropdown microphoneDropdown;

    Slider volumeGainScrollbarDisplay;
    Scrollbar volumeGainScrollbar;
    Scrollbar volumeThresoldScrollbar;

    Scrollbar frequencyGainScrollbar;
    Slider frequencyGainScrollbarDisplay;

    Button connButton;
    Button desConnButton;

    float frequencyGainScrollbarDefaultValue = 0;
    float volumeGainScrollbarDefaultValue = 0;
    float volumeThresholdScrollbarDefaultValue = 0;

    float micVolume;
    float micFreq;
    float lastMicVolume = 0;
    float lastmicFreq = 0;

    private float timer = 0f;
    private readonly float interval = 0.2f;

    void Start(){
        Application.runInBackground = true;

        sceneController = FindObjectOfType<GameObject>();
        if (sceneController == null){
            Debug.LogWarning("SceneController not found in the scene.");
        }

        PopulateDropdownWithMicrophones();

        if (microphoneDropdown != null)
            microphoneDropdown.onValueChanged.AddListener(OnDropdownValueChanged);

        volumeGainScrollbarDisplay = GameObject.Find("VolumeGainScrollbarDisplay").GetComponent<Slider>();
        volumeGainScrollbar = GameObject.Find("VolumeGainScrollbar").GetComponent<Scrollbar>();
        volumeThresoldScrollbar = GameObject.Find("VolumeThresoldScrollbar").GetComponent<Scrollbar>();
        frequencyGainScrollbar = GameObject.Find("FrequencyGainScrollbar").GetComponent<Scrollbar>();
        frequencyGainScrollbarDisplay = GameObject.Find("FrequencyGainScrollbarDisplay").GetComponent<Slider>();

        connButton = GameObject.Find("ButtonConnect").GetComponent<Button>();
        desConnButton = GameObject.Find("ButtonDesconnect").GetComponent<Button>();
        desConnButton.enabled = false;

        frequencyGainScrollbar.onValueChanged.AddListener(FrequencyGainOnScrollValueChanged);
        volumeGainScrollbar.onValueChanged.AddListener(VolumeGainOnScrollValueChanged);
        volumeThresoldScrollbar.onValueChanged.AddListener(VolumeThresoldOnScrollValueChanged);

        frequencyGainScrollbarDefaultValue = PlayerPrefs.GetFloat("frequencyGainScrollbarDefaultValue", 0.5f);
        volumeGainScrollbarDefaultValue = PlayerPrefs.GetFloat("volumeGainScrollbarDefaultValue", 0.5f);
        volumeThresholdScrollbarDefaultValue = PlayerPrefs.GetFloat("volumeThresholdScrollbarDefaultValue", 0.5f);

        frequencyGainScrollbar.value = frequencyGainScrollbarDefaultValue;
        volumeGainScrollbar.value = volumeGainScrollbarDefaultValue;
        volumeThresoldScrollbar.value = volumeThresholdScrollbarDefaultValue;
    }

    void OnDestroy(){
        frequencyGainScrollbar.onValueChanged.RemoveListener(FrequencyGainOnScrollValueChanged);
        volumeGainScrollbar.onValueChanged.RemoveListener(VolumeGainOnScrollValueChanged);
        volumeThresoldScrollbar.onValueChanged.RemoveListener(VolumeThresoldOnScrollValueChanged);
    }

    void FrequencyGainOnScrollValueChanged(float value){
        PlayerPrefs.SetFloat("frequencyGainScrollbarDefaultValue", frequencyGainScrollbar.value);
    }
    void VolumeGainOnScrollValueChanged(float value){
        PlayerPrefs.SetFloat("volumeGainScrollbarDefaultValue", volumeGainScrollbar.value);
    }
    void VolumeThresoldOnScrollValueChanged(float value){
        PlayerPrefs.SetFloat("volumeThresholdScrollbarDefaultValue", volumeThresoldScrollbar.value);
    }
    void Update() {
        micVolume = ((volumeController.GetVolume() / 80) + 1);
        micVolume = Mathf.Clamp(micVolume, 0, 1);

        micFreq = volumeController.GetFrequency();
        micFreq *= 1 + frequencyGainScrollbar.value;
        micFreq = Mathf.Clamp(micFreq, 0, 1);

        if (micVolume < (volumeThresoldScrollbar.value)){
            micVolume = 0;
            micFreq = 0;
        }

        if (micVolume > 0)
            micVolume += volumeGainScrollbar.value;

        frequencyGainScrollbarDisplay.value = micFreq;
        volumeGainScrollbarDisplay.value = micVolume;

        timer += Time.deltaTime;
        if (timer >= interval){
            if (lastMicVolume != micVolume) {
                webSocketClient.ParamSendData("PetVolume", ""+(micVolume*100));
                lastMicVolume = micVolume;
            }
            if (lastmicFreq != micFreq) {
                webSocketClient.ParamSendData("PetFrequency", ""+(micVolume*100));
                lastmicFreq = micFreq;
            }
            timer = 0f;
        }
    }

    void OnDropdownValueChanged(int value){
        volumeController.SetMicrophoneDevice(value);
        PlayerPrefs.SetInt("MicrophoneDefaultValue", value);
        volumeController.ChangeMicrophone();
    }

    public void OpenDevPage() {
        Application.OpenURL("https://www.youtube.com/@rickkomoribr?sub_confirmation=1");
    }

    public async void ButtonConnectionCall(){
        connButton.enabled = false;
        desConnButton.enabled = true;
        authenticationToken = PlayerPrefs.GetString("authKey", null);
        webSocketClient.connectionState = WebSocketClient.ConnectionState.OpenWebSocket;

        if ((webSocketClient.connectionState == WebSocketClient.ConnectionState.OpenWebSocket))
            webSocketClient.ConnectToVtubeStudio();

        if (webSocketClient.websocket.State == WebSocketState.Closed) {
            connButton.enabled = true;
            desConnButton.enabled = false;
            return;
        }
    }

    public void ButtonDesconnectionCall(){
        if (webSocketClient != null){
            webSocketClient.OnApplicationQuit();
        }
        else{
            Debug.LogWarning("WebSocketClient is not assigned.");
        }
        webSocketClient.connectionState = WebSocketClient.ConnectionState.None;

        connButton.enabled = true;
        desConnButton.enabled = false;
    }

    public void ResetToken(){
        if (webSocketClient != null){
            webSocketClient.authenticationToken = null;
            PlayerPrefs.SetString("authKey", null);
            ButtonDesconnectionCall();
        }
    }
    public void PopulateDropdownWithMicrophones(){
        string[] microphones = Microphone.devices;

        List<TMP_Dropdown.OptionData> options = new();

        foreach (string mic in microphones)
            options.Add(new TMP_Dropdown.OptionData(mic));
        
        microphoneDropdown.ClearOptions();
        microphoneDropdown.AddOptions(options);

        int defaultMicIndex = PlayerPrefs.GetInt("MicrophoneDefaultValue", -1);

        if (defaultMicIndex >= 0 && defaultMicIndex < options.Count){
            microphoneDropdown.value = defaultMicIndex;
        } else {
            microphoneDropdown.value = 0;
        }

        microphoneDropdown.RefreshShownValue();
    }
}
