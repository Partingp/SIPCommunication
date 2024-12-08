using KristofferStrube.Blazor.MediaCaptureStreams;
using Microsoft.JSInterop;
using Serilog;
using Serilog.Extensions.Logging;
using SIPSorcery.Media;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using SIPSorceryMedia.Abstractions;

public class SIPCallService : ISIPCallService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly IMediaDevicesService _mediaDevicesService;
    private IAudioEncoder _audioEncoder;

    SIPTransport _sipTransport;
    //TODO - Temp global until invokable works in WebAudioEndPoint
    WebAudioEndPoint2 webAudioPoint2;
    VoIPMediaSession _voipMediaSession;

    private static string DESTINATION = "aaron@127.0.0.1:5060";
    private static SIPEndPoint OUTBOUND_PROXY = null;

    private const string WELCOME_8K = "hellowelcome8k.raw";
    private const string GOODBYE_16K = "goodbye16k.raw";

    public SIPCallService(IJSRuntime jsRunttime, IMediaDevicesService mediaDevicesService)
    {
        _jsRuntime = jsRunttime;
        _mediaDevicesService = mediaDevicesService;

    }

    public async Task StartCall(IAudioEncoder audioEncoder)
    {
        await Task.Run(async () =>
        {
            Console.WriteLine("Starting call");
            AddConsoleLogger();
            _sipTransport = new SIPTransport();
            _sipTransport.EnableTraceLogs();
            _audioEncoder = audioEncoder;
            var userAgent = new SIPUserAgent(_sipTransport, OUTBOUND_PROXY);
            userAgent.ClientCallFailed += (uac, error, sipResponse) => Console.WriteLine($"Call failed {error}.");

            //TODO - Use AudioEncoder() param
            webAudioPoint2 = new WebAudioEndPoint2(_jsRuntime, _mediaDevicesService, _audioEncoder);
            _voipMediaSession = new VoIPMediaSession(webAudioPoint2.ToMediaEndPoints());
            _voipMediaSession.AcceptRtpFromAny = true;


            // Place the call and wait for the result.
            var callTask = userAgent.Call(DESTINATION, null, null, _voipMediaSession);

            bool callResult = await callTask;

            if (callResult)
            {
                Console.WriteLine($"Call to {DESTINATION} succeeded.");
                await _voipMediaSession.Start();
                //await webAudioPoint2.PauseAudio();
                //try
                //{
                //    await _voipMediaSession.AudioExtrasSource.StartAudio();

                //    //Console.WriteLine("Sending welcome message from 8KHz sample.");
                //    //await voipMediaSession.AudioExtrasSource.SendAudioFromStream(new FileStream(WELCOME_8K, FileMode.Open), AudioSamplingRatesEnum.Rate8KHz);

                //    //await Task.Delay(200, exitCts.Token);

                //    //Console.WriteLine("Sending sine wave.");
                //    //voipMediaSession.AudioExtrasSource.SetSource(AudioSourcesEnum.SineWave);

                //    //await Task.Delay(5000, exitCts.Token);

                //    //Console.WriteLine("Sending white noise signal.");
                //    //voipMediaSession.AudioExtrasSource.SetSource(AudioSourcesEnum.WhiteNoise);
                //    //await Task.Delay(2000, exitCts.Token);

                //    //Console.WriteLine("Sending pink noise signal.");
                //    //voipMediaSession.AudioExtrasSource.SetSource(AudioSourcesEnum.PinkNoise);
                //    //await Task.Delay(2000, exitCts.Token);

                //    //Console.WriteLine("Sending silence.");
                //    //voipMediaSession.AudioExtrasSource.SetSource(AudioSourcesEnum.Silence);

                //    //await Task.Delay(2000, exitCts.Token);

                //    Console.WriteLine("Playing music.");
                //    _voipMediaSession.AudioExtrasSource.SetSource(AudioSourcesEnum.Music);

                //    await Task.Delay(5000, CancellationToken.None);

                //    Console.WriteLine("Sending goodbye message from 16KHz sample.");
                //    await _voipMediaSession.AudioExtrasSource.SendAudioFromStream(new FileStream(GOODBYE_16K, FileMode.Open), AudioSamplingRatesEnum.Rate16KHz);

                //    _voipMediaSession.AudioExtrasSource.SetSource(AudioSourcesEnum.None);

                //    await _voipMediaSession.AudioExtrasSource.PauseAudio();

                //    await Task.Delay(200, CancellationToken.None);
                //}
                //catch (TaskCanceledException)
                //{ }

                //// Switch to the external microphone input source.
                //await webAudioPoint2.ResumeAudio();

                CancellationToken.None.WaitHandle.WaitOne();
            }
            else
            {
                Console.WriteLine($"Call to {DESTINATION} failed.");
            }

            Console.WriteLine("Exiting...");

            if (userAgent?.IsHangingUp == true)
            {
                Console.WriteLine("Waiting 1s for the call hangup or cancel to complete...");
                //await Task.Delay(1000);
            }

            // Clean up.
            _sipTransport.Shutdown();
        });
    }

    private static Microsoft.Extensions.Logging.ILogger AddConsoleLogger()
    {
        var serilogLogger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .MinimumLevel.Is(Serilog.Events.LogEventLevel.Debug)
            .WriteTo.Console()
            .CreateLogger();
        var factory = new SerilogLoggerFactory(serilogLogger);
        SIPSorcery.LogFactory.Set(factory);
        return factory.CreateLogger<Program>();
    }
}

public interface ISIPCallService
{
    public Task StartCall(IAudioEncoder audioEncoder);
}