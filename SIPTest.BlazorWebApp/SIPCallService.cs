using KristofferStrube.Blazor.MediaCaptureStreams;
using Microsoft.JSInterop;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using SIPSorceryMedia.Abstractions;
using System.Net;

public class SIPCallService : ISIPCallService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly IMediaDevicesService _mediaDevicesService;
    private IAudioEncoder _audioEncoder;

    SIPTransport _sipTransport;
    //TODO - Temp global until invokable works in WebAudioEndPoint
    WebAudioEndPoint2 webAudioPoint2;
    VoIPMediaSession _voipMediaSession;
    SIPClientUserAgent _userAgent;

    private static string DESTINATION = "aaron@127.0.0.1:5060";
    private static readonly string DEFAULT_DESTINATION_SIP_URI = "sip:aaron@127.0.0.1:5060";
    SIPURI callUri = SIPURI.ParseSIPURI(DEFAULT_DESTINATION_SIP_URI);
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

            //userAgent.ClientCallFailed += (uac, error, sipResponse) => Console.WriteLine($"Call failed {error}.");

            //TODO - Use AudioEncoder() param
            webAudioPoint2 = new WebAudioEndPoint2(_jsRuntime, _mediaDevicesService, _audioEncoder);
            //var audioSession = new WindowsAudioEndPoint(new AudioEncoder())
            _voipMediaSession = new VoIPMediaSession(webAudioPoint2.ToMediaEndPoints());
            var offerSDP = _voipMediaSession.CreateOffer(IPAddress.Any);

            _userAgent = new SIPClientUserAgent(_sipTransport, OUTBOUND_PROXY);
            _userAgent.CallTrying += (uac, resp) => Console.WriteLine($"{uac.CallDescriptor.To} Trying: {resp.StatusCode} {resp.ReasonPhrase}.");
            _userAgent.CallRinging += async (uac, resp) =>
            {
                Console.WriteLine($"{uac.CallDescriptor.To} Ringing: {resp.StatusCode} {resp.ReasonPhrase}.");
                if (resp.Status == SIPResponseStatusCodesEnum.SessionProgress)
                {
                    if (resp.Body != null)
                    {
                        var result = _voipMediaSession.SetRemoteDescription(SdpType.answer, SDP.ParseSDPDescription(resp.Body));
                        if (result == SetDescriptionResultEnum.OK)
                        {
                            await _voipMediaSession.Start();
                            Console.WriteLine($"Remote SDP set from in progress response. RTP session started.");
                        }
                    }
                }
            };
            _userAgent.CallFailed += (uac, err, resp) =>
            {
                Console.WriteLine($"Call attempt to {uac.CallDescriptor.To} Failed: {err}");
                //hasCallFailed = true;
            };
            _userAgent.CallAnswered += async (iuac, resp) =>
            {
                if (resp.Status == SIPResponseStatusCodesEnum.Ok)
                {
                    Console.WriteLine($"{iuac.CallDescriptor.To} Answered: {resp.StatusCode} {resp.ReasonPhrase}.");

                    if (resp.Body != null)
                    {
                        var result = _voipMediaSession.SetRemoteDescription(SdpType.answer, SDP.ParseSDPDescription(resp.Body));
                        if (result == SetDescriptionResultEnum.OK)
                        {
                            await _voipMediaSession.Start();
                        }
                        else
                        {
                            Console.WriteLine($"Failed to set remote description {result}.");
                            _userAgent.Hangup();
                        }
                    }
                    else if (!_voipMediaSession.IsStarted)
                    {
                        Console.WriteLine($"Failed to set get remote description in session progress or final response.");
                        _userAgent.Hangup();
                    }
                }
                else
                {
                    Console.WriteLine($"{iuac.CallDescriptor.To} Answered: {resp.StatusCode} {resp.ReasonPhrase}.");
                }
            };

            _sipTransport.SIPTransportRequestReceived += async (localSIPEndPoint, remoteEndPoint, sipRequest) =>
            {
                if (sipRequest.Method == SIPMethodsEnum.BYE)
                {
                    SIPResponse okResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.Ok, null);
                    await _sipTransport.SendResponseAsync(okResponse);

                    if (_userAgent.IsUACAnswered)
                    {
                        Console.WriteLine("Call was hungup by remote server.");
                        //isCallHungup = true;
                        //exitMre.Set();
                    }
                }
            };
            //_voipMediaSession.AcceptRtpFromAny = true;


            // Place the call and wait for the result.
            SIPCallDescriptor callDescriptor = new SIPCallDescriptor(
                 SIPConstants.SIP_DEFAULT_USERNAME,
                 null,
                 callUri.ToString(),
                 SIPConstants.SIP_DEFAULT_FROMURI,
                 callUri.CanonicalAddress,
                 null, null, null,
                 SIPCallDirection.Out,
                 SDP.SDP_MIME_CONTENTTYPE,
                 offerSDP.ToString(),
                 null);

            _userAgent.Call(callDescriptor, null);

            CancellationToken.None.WaitHandle.WaitOne();
        });
    }


    public async Task EndCall()
    {
        await webAudioPoint2.CloseAudio();
        Console.WriteLine("Exiting...");

        _voipMediaSession.Close(null);

        if (_userAgent != null)
        {
            if (_userAgent.IsUACAnswered)
            {
                Console.WriteLine($"Hanging up call to {_userAgent.CallDescriptor.To}.");
                _userAgent.Hangup();
            }
            //else if (!hasCallFailed)
            //{
            //    Console.WriteLine($"Cancelling call to {userAgent.CallDescriptor.To}.");
            //    userAgent.Cancel();
            //}

            // Give the BYE or CANCEL request time to be transmitted.
            Console.WriteLine("Waiting 1s for call to clean up...");
            Task.Delay(1000).Wait();
        }

        if (_sipTransport != null)
        {
            Console.WriteLine("Shutting down SIP transport...");
            _sipTransport.Shutdown();
        }
    }

    //public void OnAudioFrameCaptured(byte[] pcmData)
    //{
    //    webAudioPoint2.OnAudioFrameCaptured(pcmData);
    //}

    //private static Microsoft.Extensions.Logging.ILogger AddConsoleLogger()
    //{
    //    var serilogLogger = new LoggerConfiguration()
    //        .Enrich.FromLogContext()
    //        .MinimumLevel.Is(Serilog.Events.LogEventLevel.Debug)
    //        .WriteTo.Console()
    //        .CreateLogger();
    //    var factory = new SerilogLoggerFactory(serilogLogger);
    //    SIPSorcery.LogFactory.Set(factory);
    //    return factory.CreateLogger<Program>();
    //}

    private static Microsoft.Extensions.Logging.ILogger AddConsoleLogger(
    LogEventLevel logLevel = LogEventLevel.Debug)
    {
        var serilogLogger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .MinimumLevel.Is(logLevel)
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
    public Task EndCall();

    //public void OnAudioFrameCaptured(byte[] pcmData);
}