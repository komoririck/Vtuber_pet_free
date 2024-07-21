using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VolumeController : MonoBehaviour
{

    public string microphoneDevice;

    private AudioSource audioSource;

    private float timer = 0f;
    private readonly float updateInterval = 0.00f;


    float rmsVal;
    public float dbVal;
    public float pitchVal;

    private readonly float minFrequency = 00.0f;
    private readonly float maxFrequency = 400.0f;
    readonly float scaleMax = 1000.0f;

    private const int QSamples = 1024;
    private const float RefValue = 0.1f;
    private const float Threshold = 0.00f;

    float[] _samples;
    private float[] _spectrum;
    private float _fSample;


    void Start()
    {
        _samples = new float[QSamples];
        _spectrum = new float[QSamples];
        _fSample = AudioSettings.outputSampleRate;

        Application.runInBackground = true;

        microphoneDevice = PlayerPrefs.GetString("MicrophoneDefaultValue", null);
        microphoneDevice = Microphone.devices.Length > 0 ? Microphone.devices[0] : null;

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = Microphone.Start(microphoneDevice, true, 10, AudioSettings.outputSampleRate);
        while (!(Microphone.GetPosition(null) > 0)) { }
        audioSource.loop = true;
        audioSource.Play();
    }

    void Update(){
        timer += Time.deltaTime;
        if (timer >= updateInterval)
        {
            AnalyzeAudio();
            timer = 0f;
        }
    }


    private void AnalyzeAudio(){
        GetComponent<AudioSource>().GetOutputData(_samples, 0); // Fill array with samples

        float sum = 0;
        for (int i = 0; i < QSamples; i++)
        {
            sum += _samples[i] * _samples[i]; // Sum squared samples
        }
        rmsVal = Mathf.Sqrt(sum / QSamples); // RMS = square root of average
        float dbValCal = 20 * Mathf.Log10(rmsVal / RefValue); // Calculate dB
        if (dbValCal < -80)
            dbValCal = -80;
        if (dbValCal > 0)
            dbValCal = 0;

        dbVal = dbValCal;


        GetComponent<AudioSource>().GetSpectrumData(_spectrum, 0, FFTWindow.BlackmanHarris);

        float maxV = 0;
        var maxN = 0;
        for (int i = 0; i < QSamples; i++)
        {
            if (!(_spectrum[i] > maxV) || !(_spectrum[i] > Threshold))
            {
                continue;
            }

            maxV = _spectrum[i];
            maxN = i; // maxN is the index of max
        }

        float freqN = maxN; // pass the index to a float variable
        if (maxN > 0 && maxN < QSamples - 1)
        { // interpolate index using neighbours
            var dL = _spectrum[maxN - 1] / _spectrum[maxN];
            var dR = _spectrum[maxN + 1] / _spectrum[maxN];
            freqN += 0.5f * (dR * dR - dL * dL);
        }
        pitchVal = FreqToScale((freqN * (_fSample / 2) / QSamples), minFrequency, maxFrequency, scaleMax);

    }
    public float GetVolume()
    {
        return dbVal;
    }
    public float GetFrequency()
    {
        return pitchVal;
    }

    float FreqToScale(float value, float inputMin, float inputMax, float outputMax){
        float clampedValue = Mathf.Clamp(value, inputMin, inputMax);

        if (inputMin == clampedValue)
            return 0;

        float mappedValue = clampedValue / (inputMax * 2);

        return mappedValue;
    }

    void OnDisable()
    {
        Microphone.End(microphoneDevice);
        if (audioSource != null)
        {
            Destroy(audioSource);
        }
    }

    public void SetMicrophoneDevice(int device)
    {
        microphoneDevice = Microphone.devices[device];
    }
    public void ChangeMicrophone() {
        Microphone.End(microphoneDevice);
        string newDevice = PlayerPrefs.GetString("MicrophoneDefaultValue", null);
        audioSource.clip = Microphone.Start(newDevice, true, 10, AudioSettings.outputSampleRate);
        while (!(Microphone.GetPosition(null) > 0)) { }
        audioSource.loop = true;
        audioSource.Play();
    }
}
