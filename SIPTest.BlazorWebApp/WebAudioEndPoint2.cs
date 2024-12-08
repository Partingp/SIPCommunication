using KristofferStrube.Blazor.DOM;
using KristofferStrube.Blazor.FileAPI;
using KristofferStrube.Blazor.MediaCaptureStreams;
using KristofferStrube.Blazor.MediaStreamRecording;
using KristofferStrube.Blazor.WebAudio;
using Microsoft.JSInterop;
using SIPSorceryMedia.Abstractions;

public class WebAudioEndPoint2 : IAudioSource, IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly IMediaDevicesService _mediaDevicesService;
    private readonly IAudioEncoder _audioEncoder;
    private MediaFormatManager<AudioFormat> _audioFormatManager;

    private bool _isStarted;
    private bool _isPaused;
    private bool _isDisposed;

    private MediaStream? mediaStream;
    MediaRecorder? recorder;
    EventListener<BlobEvent>? dataAvailableEventListener;
    private MediaDevices? mediaDevices;

    private AudioFormat _currentFormat;
    private Func<AudioFormat, bool>? _formatFilter;
    private AudioContext? context;

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

    public WebAudioEndPoint2(IJSRuntime jsRuntime, IMediaDevicesService mediaDevicesService, IAudioEncoder audioEncoder)
    {
        _jsRuntime = jsRuntime;
        _mediaDevicesService = mediaDevicesService;
        _audioEncoder = audioEncoder;

        // Set a default audio format: PCM, 16kHz, 1 channel (mono)
        _audioFormatManager = new MediaFormatManager<AudioFormat>(audioEncoder.SupportedFormats);
        _currentFormat = new AudioFormat(AudioCodecsEnum.PCMU, 0, 16000, 1);
    }

    //public async Task OpenAudio()
    //{
    //    //await StopAudioTrack();

    //    //await InvokeAsync(StateHasChanged);

    //    try
    //    {
    //        if (context is null)
    //        {
    //            context = await AudioContext.CreateAsync(_jsRuntime);
    //        }
    //        if (mediaDevices is null)
    //        {
    //            mediaDevices = await _mediaDevicesService.GetMediaDevicesAsync();
    //        }

    //        MediaTrackConstraints mediaTrackConstraints = new MediaTrackConstraints
    //        {
    //            EchoCancellation = true,
    //            NoiseSuppression = true,
    //            AutoGainControl = false,
    //            //DeviceId = selectedAudioSource is null ? null : new ConstrainDomString(selectedAudioSource)
    //        };
    //        mediaStream = await mediaDevices.GetUserMediaAsync(new MediaStreamConstraints() { Audio = mediaTrackConstraints });

    //        //var deviceInfos = await mediaDevices.EnumerateDevicesAsync();
    //        //audioOptions.Clear();
    //        //foreach (var device in deviceInfos)
    //        //{
    //        //    if (await device.GetKindAsync() is MediaDeviceKind.AudioInput)
    //        //    {
    //        //        audioOptions.Add((await device.GetLabelAsync(), await device.GetDeviceIdAsync()));
    //        //    }
    //        //}

    //        //analyser = await context.CreateAnalyserAsync();
    //        await using MediaStreamAudioSourceNode mediaStreamAudioSourceNode = await context.CreateMediaStreamSourceAsync(mediaStream);
    //        //await mediaStreamAudioSourceNode.ConnectAsync(analyser);
    //    }
    //    catch (WebIDLException ex)
    //    {
    //        Console.WriteLine($"{ex.GetType().Name}: {ex.Message}");
    //    }
    //    catch (Exception ex)
    //    {
    //        Console.WriteLine($"An unexpected error of type '{ex.GetType().Name}' happened.");
    //    }
    //}

    /// <summary>
    /// Starts capturing audio from the microphone.
    /// </summary>
    public async Task StartAudio()
    {
        if (_isStarted)
            throw new InvalidOperationException("Audio capture is already running.");

        try
        {
            context = await AudioContext.CreateAsync(_jsRuntime);
            mediaDevices = await _mediaDevicesService.GetMediaDevicesAsync();
            MediaTrackConstraints mediaTrackConstraints = new MediaTrackConstraints
            {
                EchoCancellation = true,
                NoiseSuppression = true,
                AutoGainControl = false,
                SampleRate = 8000,
                SampleSize = 16,
                ChannelCount = 1,
                //DeviceId = selectedAudioSource is null ? null : new ConstrainDomString(selectedAudioSource)
            };
            mediaStream = await mediaDevices.GetUserMediaAsync(new MediaStreamConstraints() { Audio = mediaTrackConstraints });

            _isStarted = true;

            //var source = await context.CreateMediaStreamSourceAsync(mediaStream);
            //var panner = await context.CreateChannelMergerAsync(1);
            //await source.ConnectAsync(panner);
            //var description = await context.GetDestinationAsync();
            //await panner.ConnectAsync(description);
            //var test = await source.GetMediaStreamAsync();

            // Create new MediaRecorder from some existing MediaStream.
            recorder = await MediaRecorder.CreateAsync(_jsRuntime, mediaStream, new MediaRecorderOptions
            {
                MimeType = "audio/webm",
            });

            // Add event listener for when each data part is available.
            dataAvailableEventListener =
                await EventListener<BlobEvent>.CreateAsync(_jsRuntime, async (BlobEvent e) =>
                {
                    Blob blob = await e.GetDataAsync();
                    byte[] audioData = await blob.ArrayBufferAsync();
                    //var audioBuffer = await context.DecodeAudioDataAsync(audioData);
                    //var offlineContext = await OfflineAudioContext.CreateAsync(_jsRuntime, 1, (ulong)audioData.Length, 16000);
                    //var source = await offlineContext.CreateBufferSourceAsync();
                    //await source.SetBufferAsync(audioData);
                    //source.Connect(offlineContext.GetDestinationAsync());
                    //source.StartAsync();
                    OnAudioFrameCaptured(audioData);

                });
            await recorder.AddOnDataAvailableEventListenerAsync(dataAvailableEventListener);

            // Starts Recording
            await recorder.StartAsync(20);
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
            if (recorder is null)
                return;

            _isStarted = false;

            // Stops recording
            await recorder.StopAsync();

            //TODO - This code has been moved to dispose, might need to make a seperate StopAudioTrack instead
            //await StopAudioTrack();
        }
    }

    public async Task StopAudioTrack()
    {
        if (mediaStream is null) return;
        var audioTrack = (await mediaStream.GetAudioTracksAsync()).FirstOrDefault();
        if (audioTrack is not null)
        {
            await audioTrack.StopAsync();
        }
    }

    /// <summary>
    /// Pauses audio capture by disconnecting the microphone.
    /// </summary>
    public async Task PauseAudio()
    {
        if (!_isStarted || _isPaused || recorder is null)
            return;

        _isPaused = true;
        await recorder.PauseAsync();
    }

    /// <summary>
    /// Resumes audio capture after being paused.
    /// </summary>
    public async Task ResumeAudio()
    {
        if (!_isStarted || !_isPaused || recorder is null)
            return;

        _isPaused = false;
        await recorder.ResumeAsync();
    }

    /// <summary>
    /// Gets a list of audio formats supported by the audio source.
    /// </summary>
    public List<AudioFormat> GetAudioSourceFormats()
    {
        return new List<AudioFormat>
        {
            new AudioFormat(SDPWellKnownMediaFormatsEnum.PCMU),
            new AudioFormat(SDPWellKnownMediaFormatsEnum.PCMA),
            new AudioFormat(SDPWellKnownMediaFormatsEnum.G722),
            new AudioFormat(SDPWellKnownMediaFormatsEnum.G729),
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
    public async void OnAudioFrameCaptured(byte[] pcmData)
    {
        if (_isStarted && !_isPaused)
        {
            short[] shortPcmData = new short[pcmData.Length / 2];
            byte[] encodedSample = _audioEncoder.EncodeAudio(shortPcmData, _audioFormatManager.SelectedFormat);
            OnAudioSourceEncodedSample?.Invoke((uint)encodedSample.Length, encodedSample);
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
        if (mediaStream is null) return;
        var audioTrack = (await mediaStream.GetAudioTracksAsync()).FirstOrDefault();
        if (audioTrack is not null)
        {
            await audioTrack.StopAsync();
        }
    }
}
