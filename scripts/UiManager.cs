using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NativeWebSocket;
using UnityEngine.Audio;

public class UiManager : MonoBehaviour
{
    string authenticationToken = null;

    GameObject sceneController;
    public WebSocketClient webSocketClient;
    public VolumeController volumeController;
    public TMP_Dropdown microphoneDropdown;
    public AudioMixerGroup audioGroup = null;

    public Slider volumeGainScrollbarDisplay;
    public Scrollbar volumeGainScrollbar;
    public Scrollbar volumeThresoldScrollbar;

    public Scrollbar frequencyGainScrollbar;
    public Slider frequencyGainScrollbarDisplay;

    public Button connButton;
    public Button desConnButton;
     
    float frequencyGainScrollbarDefaultValue = 0;
    float volumeGainScrollbarDefaultValue = 0;
    float volumeThresholdScrollbarDefaultValue = 0;

    float currentMicVolume = 0;
    float currentMicFreq = 0;

    float targetMicVolume = 0;
    float targetMicFreq = 0;

    float lastMicVolume = 0;
    float lastmicFreq = 0;

    private float timer = 0f;
    private readonly float interval = 0.05f;

    void Start(){
        Application.runInBackground = true;

        sceneController = FindObjectOfType<GameObject>();

        PopulateDropdownWithMicrophones();

        if (microphoneDropdown != null)
            microphoneDropdown.onValueChanged.AddListener(OnDropdownValueChanged);

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
        currentMicVolume = ((volumeController.GetVolume() / 80) + 1);
        currentMicFreq = volumeController.GetFrequency();

        targetMicVolume = currentMicVolume;
        targetMicFreq = currentMicFreq;

        timer += Time.deltaTime;
        if (timer >= interval){
            targetMicVolume = Mathf.Clamp(targetMicVolume, 0, 1);

            targetMicFreq *= 1 + frequencyGainScrollbar.value;
            targetMicFreq = Mathf.Clamp(targetMicFreq, 0, 1);

            if (targetMicVolume < (volumeThresoldScrollbar.value))
            {
                targetMicVolume = 0;
                targetMicFreq = 0;
            }

            if (targetMicVolume > 0)
                targetMicVolume += volumeGainScrollbar.value;

            frequencyGainScrollbarDisplay.value = targetMicFreq;
            volumeGainScrollbarDisplay.value = targetMicVolume;


            if (lastMicVolume != targetMicVolume) {
                webSocketClient.ParamSendData("PetVolume", ""+(targetMicVolume*100));
                lastMicVolume = targetMicVolume;
            }
            if (lastmicFreq != targetMicFreq) {
                webSocketClient.ParamSendData("PetFrequency", ""+(targetMicVolume*100));
                lastmicFreq = targetMicFreq;
            }
            timer = 0f;
        }
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
        
        string savedMic = PlayerPrefs.GetString("MicrophoneDefaultValue", null);
        int currentMic = 0;
        for (int i = 0; i < microphoneDropdown.options.Count; i++) {
            if (microphoneDropdown.options[i].text.Equals(savedMic))
            {
                currentMic = i;
                break;
            }
        }

        if (currentMic >= 0 && currentMic < options.Count){
            microphoneDropdown.value = currentMic;
        } else {
            microphoneDropdown.value = 0;
        }

        microphoneDropdown.RefreshShownValue();

        if (currentMic == 0)
            PlayerPrefs.SetString("MicrophoneDefaultValue", microphoneDropdown.options[currentMic].text);

        volumeController = gameObject.AddComponent<VolumeController>();

    }
    void OnDropdownValueChanged(int value)
    {
        Destroy(volumeController);
        PlayerPrefs.SetString("MicrophoneDefaultValue", microphoneDropdown.options[value].text);
        volumeController = gameObject.AddComponent<VolumeController>();

    }
}
