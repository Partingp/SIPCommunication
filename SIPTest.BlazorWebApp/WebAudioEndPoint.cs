using Microsoft.JSInterop;
using SIPSorceryMedia.Abstractions;

public class WebAudioEndPoint : IAudioSource, IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;

    private bool _isStarted;
    private bool _isPaused;
    private bool _isDisposed;

    private AudioFormat _currentFormat;
    private Func<AudioFormat, bool>? _formatFilter;

    /// <summary>
    /// Event that gets raised when encoded audio samples are available.
    /// </summary>
    public event EncodedSampleDelegate OnAudioSourceEncodedSample;

    /// <summary>
    /// Event that gets raised when raw audio samples are available.
    /// </summary>
    public event RawAudioSampleDelegate OnAudioSourceRawSample;

    /// <summary>
    /// Event that gets raised when an error occurs in the audio source.
    /// </summary>
    public event SourceErrorDelegate OnAudioSourceError;

    public WebAudioEndPoint(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;

        // Set a default audio format: PCM, 16kHz, 1 channel (mono)
        _currentFormat = new AudioFormat(AudioCodecsEnum.PCMU, 0, 16000, 1);
    }

    /// <summary>
    /// Starts capturing audio from the microphone.
    /// </summary>
    public async Task StartAudio()
    {
        if (_isStarted)
            throw new InvalidOperationException("Audio capture is already running.");

        try
        {
            _isStarted = true;
            _isPaused = false;
            var dotNetRef = DotNetObjectReference.Create(this);
            await _jsRuntime.InvokeVoidAsync("startMicrophoneCapture", dotNetRef);
        }
        catch (Exception ex)
        {
            _isStarted = false;
            OnAudioSourceError?.Invoke($"Failed to start audio capture: {ex.Message}");
        }
    }

    /// <summary>
    /// Stops capturing audio.
    /// </summary>
    public async Task CloseAudio()
    {
        if (_isStarted)
        {
            await _jsRuntime.InvokeVoidAsync("stopMicrophoneCapture");
            _isStarted = false;
        }
    }

    /// <summary>
    /// Pauses audio capture by disconnecting the microphone.
    /// </summary>
    public async Task PauseAudio()
    {
        if (!_isStarted || _isPaused)
            return;

        _isPaused = true;
        if (_jsRuntime != null)
        {
            await _jsRuntime.InvokeVoidAsync("pauseMicrophoneCapture");
        }
    }

    /// <summary>
    /// Resumes audio capture after being paused.
    /// </summary>
    public async Task ResumeAudio()
    {
        if (!_isStarted || !_isPaused)
            return;

        _isPaused = false;
        if (_jsRuntime != null)
        {
            await _jsRuntime.InvokeVoidAsync("resumeMicrophoneCapture");
        }
    }

    /// <summary>
    /// Gets a list of audio formats supported by the audio source.
    /// </summary>
    public List<AudioFormat> GetAudioSourceFormats()
    {
        return new List<AudioFormat>
        {
            new AudioFormat(AudioCodecsEnum.PCMU, 0, 16000, 1),
            new AudioFormat(AudioCodecsEnum.PCMU, 0, 8000, 1)
        };
    }

    /// <summary>
    /// Sets the current audio format to be used for audio capture.
    /// </summary>
    public void SetAudioSourceFormat(AudioFormat audioFormat)
    {
        _currentFormat = audioFormat;
    }

    /// <summary>
    /// Restricts the formats to those matching the provided filter.
    /// </summary>
    public void RestrictFormats(Func<AudioFormat, bool> filter)
    {
        _formatFilter = filter;
    }

    /// <summary>
    /// Supplies external raw audio samples to this source.
    /// </summary>
    public void ExternalAudioSourceRawSample(AudioSamplingRatesEnum samplingRate, uint durationMilliseconds, short[] sample)
    {
        OnAudioSourceRawSample?.Invoke(samplingRate, durationMilliseconds, sample);
    }

    /// <summary>
    /// Checks if there are subscribers to the encoded audio sample event.
    /// </summary>
    public bool HasEncodedAudioSubscribers()
    {
        return OnAudioSourceEncodedSample != null && OnAudioSourceEncodedSample.GetInvocationList().Any();
    }

    /// <summary>
    /// Checks if the audio source is currently paused.
    /// </summary>
    public bool IsAudioSourcePaused()
    {
        return _isPaused;
    }

    /// <summary>
    /// Handles audio data received from JavaScript.
    /// </summary>
    /// <param name="pcmData">Raw PCM audio data (Int16).</param>
    //TODO - Figure out how to get this invokable to work
    [JSInvokable("OnAudioFrameCaptured")]
    public async void OnAudioFrameCaptured(byte[] pcmData)
    {
        if (_isStarted && !_isPaused)
        {
            short[] shortPcmData = new short[pcmData.Length / 2];
            for (int i = 0; i < shortPcmData.Length; i++)
            {
                shortPcmData[i] = (short)(pcmData[2 * i] | (pcmData[2 * i + 1] << 8));
            }
            // Notify raw sample subscribers
            OnAudioSourceRawSample?.Invoke(AudioSamplingRatesEnum.Rate16KHz, (uint)(pcmData.Length / (_currentFormat.ClockRate / 1000)), shortPcmData);

            // Notify encoded sample subscribers (in this case, passing PCM as-is)
            OnAudioSourceEncodedSample?.Invoke((uint)_currentFormat.ClockRate, pcmData);
        }
    }

    public MediaEndPoints ToMediaEndPoints()
    {
        return new MediaEndPoints
        {
            AudioSource = this,
            AudioSink = null
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (!_isDisposed)
        {

            _isDisposed = true;
        }
        //    _dotNetReference?.Dispose();
    }
}
