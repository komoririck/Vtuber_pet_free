using UnityEngine;
using NativeWebSocket;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections;

public class WebSocketClient : MonoBehaviour {
    public GameObject alertPopup;

    public WebSocket websocket;
    public string authenticationToken = null;
    public bool waitingForARequest = false;

    string base64String;
    string jsonAuthRequest;

    float timer;

    [Flags]
	public enum ConnectionState : byte{
		None = 0,
        OpenWebSocket = 1,
        CallForToken = 2,
        WaitForToken = 3,
        CallForAuth = 4,
        WaitForAuth = 5,
        CallForParam = 6,
        WaitForParam = 7,
        ReadyToWork = 8
    }

    public ConnectionState connectionState;

    void Start() {
        Application.runInBackground = true;
        connectionState = ConnectionState.None;

        authenticationToken = PlayerPrefs.GetString("authKey", null);
        base64String = ImageToBase64("Assets/Icons/my_icon.png");
        websocket = new WebSocket("ws://localhost:8001");
    }

    void Update() {
        if (websocket != null) {
            #if !UNITY_WEBGL || UNITY_EDITOR
                        websocket.DispatchMessageQueue();
            #endif
        }

        if (connectionState < ConnectionState.CallForAuth) {
            CheckForGenAuth();
            return;
        }
        if (connectionState < ConnectionState.CallForParam) { 
            CheckForAuth();
            return;
        }
        if (connectionState < ConnectionState.ReadyToWork) { 
            CheckForParameters();
            return;
        }
       
    }
    
    public void ConnectToVtubeStudio() {
        if (!(connectionState == ConnectionState.OpenWebSocket)) {
            Debug.Log("Connection state: " + ConnectionState.OpenWebSocket);
            return;
        }

        websocket.OnOpen += () => {
            Debug.Log("WebSocket connection open!");
        };

        websocket.OnError += (e) => {
            Debug.LogError("WebSocket error: " + e);
        };

        websocket.OnClose += (e) => {
            Debug.Log("WebSocket connection closed!");
        };

        try {
            websocket.Connect();

            timer = 0f;
            while (!(websocket.State == WebSocketState.Open)) {
                timer += Time.deltaTime;
                if (timer >= 3000000){
                    timer = 0f;
                    return;
                }
            }

            if (websocket.State == WebSocketState.Open) {
                if (string.IsNullOrEmpty(authenticationToken)) { 
                    connectionState = ConnectionState.CallForToken;
                    CallForGenAuth();
                } else {
                    connectionState = ConnectionState.CallForAuth;
                    CallForAuth();
                }
            }
        } catch (Exception e) {
            Debug.LogWarning("WebSocket connection error: " + e.Message);
        }
        return;
    }

    public void ParamSendData (string ParamId, string ParamValue){
        if (!(connectionState == ConnectionState.ReadyToWork)) {
            //Debug.Log("Connection state: " + ConnectionState.ReadyToWork);
            return;
        }

        string jsonParamsRequest = @"
            {
	            ""apiName"": ""VTubeStudioPublicAPI"",
	            ""apiVersion"": ""1.0"",
	            ""requestID"": ""6969696969699696969"",
	            ""messageType"": ""InjectParameterDataRequest"",
	            ""data"": {
		            ""faceFound"": false,
		            ""mode"": ""set"",
		            ""parameterValues"": [
			            {
				            ""id"": """ + ParamId + @""",
				            ""value"": """ + ParamValue + @"""
			            }
		            ]
	            }
            }        
        ";
        SendWebSocketMessage(jsonParamsRequest);
    }

    private void CallForParameters() {
        if (!(connectionState == ConnectionState.CallForParam)) {
            Debug.Log("Connection state: " + ConnectionState.CallForParam);
            return;
        }

        string jsonParamsRequest = @"{
	        ""apiName"": ""VTubeStudioPublicAPI"",

            ""apiVersion"": ""1.0"",
	        ""requestID"": ""6969696969699696969"",
	        ""messageType"": ""ParameterCreationRequest"",
	        ""data"": {
                ""parameterName"": ""PetVolume"",
		        ""explanation"": ""this paramter controls the input of the volume for the pet microphone."",
		        ""min"": 0,
		        ""max"": 100,
		        ""defaultValue"": 0
            }
        }
     ";
        SendWebSocketMessage(jsonParamsRequest);
        jsonParamsRequest = @"{
	        ""apiName"": ""VTubeStudioPublicAPI"",

            ""apiVersion"": ""1.0"",
	        ""requestID"": ""6969696969699696969"",
	        ""messageType"": ""ParameterCreationRequest"",
	        ""data"": {
                ""parameterName"": ""PetFrequency"",
		        ""explanation"": ""this paramter  controls the input of the frequency for the pet microphone."",
		        ""min"": 0,
		        ""max"": 100,
		        ""defaultValue"": 0
            }
        }
     ";
        SendWebSocketMessage(jsonParamsRequest);
        connectionState = ConnectionState.WaitForParam;
    }

    private void CheckForParameters(){
        if (!(connectionState == ConnectionState.WaitForParam) || waitingForARequest) {
            return;
        }

        waitingForARequest = true;
        websocket.OnMessage += (bytes) => {
            string jsonString = System.Text.Encoding.UTF8.GetString(bytes);
            try{
                var responseData = JsonUtility.FromJson<CreateParamResponse>(jsonString);
                if (responseData != null && responseData.messageType == "ParameterCreationResponse"){
                    connectionState = ConnectionState.ReadyToWork;
                    waitingForARequest = false;
                    Debug.Log("Parameters done!");
                } 
            }
            catch (Exception e){
                Debug.LogError("Error parsing JSON response: " + e.Message);
            }
        };
    }


    public void CallForAuth() {
        if (!(connectionState == ConnectionState.CallForAuth)) {
            Debug.Log("Connection state: " + ConnectionState.CallForAuth);
            return;
        }

        jsonAuthRequest = @"{
	    ""apiName"": ""VTubeStudioPublicAPI"",
	    ""apiVersion"": ""1.0"",
	    ""requestID"": ""18931111151315189"",
	    ""messageType"": ""AuthenticationRequest"",
	    ""data"": {
		    ""pluginName"": ""Vtuber_Pet_Free"",
		    ""pluginDeveloper"": ""rickkomori"",
		    ""authenticationToken"": """ + authenticationToken + @"""
	        }
        }";
        SendWebSocketMessage(jsonAuthRequest);
        connectionState = ConnectionState.WaitForAuth;
    }

    public void CheckForAuth() {
        if (!(connectionState == ConnectionState.WaitForAuth) || waitingForARequest) {
            //Debug.Log("Connection state: " + ConnectionState.WaitForAuth);
            return;
        }

        waitingForARequest = true;
        websocket.OnMessage += (bytes) =>{
            string jsonString = System.Text.Encoding.UTF8.GetString(bytes);
            try{
                var responseData = JsonUtility.FromJson<AuthenticationResponse>(jsonString);
                if (responseData != null && responseData.messageType == "AuthenticationResponse"){
                    string authStatus = responseData.data.authenticated;
                    if (authStatus.Equals("true")) {
                        connectionState = ConnectionState.CallForParam;
                        CallForParameters();
                        waitingForARequest = false;
                        Debug.Log("Autentication done!");
                    }
                }
            }
            catch (Exception e){
                Debug.LogError("Error parsing JSON response: " + e.Message);
            }
        };
    }

    public void CallForGenAuth(){
        if (!(connectionState == ConnectionState.CallForToken)) {
            Debug.Log("Connection state: " + ConnectionState.CallForToken);
            return;
        }

        jsonAuthRequest = @"{
            ""apiName"": ""VTubeStudioPublicAPI"",
            ""apiVersion"": ""1.0"",
            ""requestID"": ""18931111151315189"",
            ""messageType"": ""AuthenticationTokenRequest"",
            ""data"": {
                ""pluginName"": ""Vtuber_Pet_Free"",
                ""pluginDeveloper"": ""rickkomori"",
                ""pluginIcon"": """ + base64String + @"""
            }
        }";
        SendWebSocketMessage(jsonAuthRequest);
        connectionState = ConnectionState.WaitForToken;
    }

    public void CheckForGenAuth() {
        if (!(connectionState == ConnectionState.WaitForToken) || waitingForARequest)     {
            return; 
        }

        waitingForARequest = true;
        alertPopup.SetActive(true);

        float timeoutDuration = 60f;
        float timer = 0f;

        waitingForARequest = true;
        websocket.OnMessage += (bytes) =>{
            string jsonString = System.Text.Encoding.UTF8.GetString(bytes);
            try{
                var responseData = JsonUtility.FromJson<AuthenticationTokenResponse>(jsonString);
                if (responseData != null && responseData.messageType == "AuthenticationTokenResponse"){
                    authenticationToken = responseData.data.authenticationToken;
                    PlayerPrefs.SetString("authKey", authenticationToken);
                    PlayerPrefs.Save();
                    connectionState = ConnectionState.CallForAuth;
                    CallForAuth();
                    waitingForARequest = false;
                    alertPopup.SetActive(false);
                    Debug.Log("Key Gen done!");
                    return;
                }
            }
            catch (Exception e){
                Debug.LogError("Error parsing JSON response: " + e.Message);
            }
        };
    }

    

    async void SendWebSocketMessage(string request) {

        if (websocket.State == WebSocketState.Open) {
            await websocket.SendText(request);
        }
        else {
            Debug.LogWarning("WebSocket connection is not open. Unable to send message.");
        }
    }
    
    string ImageToBase64(string imagePath) {
        try
        {
            byte[] imageBytes = File.ReadAllBytes(imagePath);
            string base64String = Convert.ToBase64String(imageBytes);
            return base64String;
        }
        catch (Exception e)
        {
            Debug.LogError("Error converting image to Base64: " + e.Message);
            return null;
        }
    }

    public async void OnApplicationQuit() {
        if (websocket != null) {
            await websocket.Close();
        }
    }

    [Serializable]
    public class AuthenticationTokenResponse{
        public string apiName;
        public string apiVersion;
        public long timestamp;
        public string requestID;
        public string messageType;
        public AuthenticationTokenData data;
    }

    [Serializable]
    public class AuthenticationResponse{
        public string apiName;
        public string apiVersion;
        public long timestamp;
        public string requestID;
        public string messageType;
        public AuthenticationData data;
    }
    [Serializable]
    public class CreateParamResponse{
        public string apiName;
        public string apiVersion;
        public long timestamp;
        public string requestID;
        public string messageType;
        public CreateParamResponseData data;
    }

    [Serializable]
    public class AuthenticationTokenData{
        public string authenticationToken;
    }

    [Serializable]
    public class AuthenticationData{
        public string authenticated;
        public string reason;
    }
    [Serializable]
    public class CreateParamResponseData{
        public string parameterName;
    }
}