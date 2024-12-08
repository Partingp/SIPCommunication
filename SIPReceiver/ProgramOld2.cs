﻿////-----------------------------------------------------------------------------
//using Microsoft.Extensions.Logging;
//using Microsoft.Extensions.Logging.Abstractions;
//using Serilog;
//using Serilog.Extensions.Logging;
//using SIPSorcery;
//using SIPSorcery.Media;
//using SIPSorcery.Net;
//using SIPSorcery.SIP;
//using SIPSorcery.SIP.App;
//using SIPSorceryMedia.Abstractions;
//using System.Net;
//using System.Net.Sockets;

//namespace SIPReceiver
//{
//    class ProgramOld2
//    {
//        private static int SIP_LISTEN_PORT = 5060;
//        private static int SIPS_LISTEN_PORT = 5061;
//        //private static int SIP_WEBSOCKET_LISTEN_PORT = 80;
//        //private static int SIP_SECURE_WEBSOCKET_LISTEN_PORT = 443;
//        private static string SIPS_CERTIFICATE_PATH = "localhost.pfx";

//        private const string WELCOME_8K = "Sounds/hellowelcome8k.raw";
//        private const string GOODBYE_16K = "Sounds/goodbye16k.raw";

//        private static Microsoft.Extensions.Logging.ILogger Log = NullLogger.Instance;

//        static void Main(string[] args)
//        {
//            Console.WriteLine("SIPSorcery user agent server example.");
//            Console.WriteLine("Press h to hangup a call or ctrl-c to exit.");

//            Log = AddConsoleLogger();

//            IPAddress listenAddress = IPAddress.Any;
//            IPAddress listenIPv6Address = IPAddress.IPv6Any;
//            if (args != null && args.Length > 0)
//            {
//                if (!IPAddress.TryParse(args[0], out var customListenAddress))
//                {
//                    Log.LogWarning($"Command line argument could not be parsed as an IP address \"{args[0]}\"");
//                    listenAddress = IPAddress.Any;
//                }
//                else
//                {
//                    if (customListenAddress.AddressFamily == AddressFamily.InterNetwork)
//                    {
//                        listenAddress = customListenAddress;
//                    }
//                    if (customListenAddress.AddressFamily == AddressFamily.InterNetworkV6)
//                    {
//                        listenIPv6Address = customListenAddress;
//                    }
//                }
//            }

//            // Set up a default SIP transport.
//            var sipTransport = new SIPTransport();

//            //var localhostCertificate = new X509Certificate2(SIPS_CERTIFICATE_PATH);

//            // IPv4 channels.
//            sipTransport.AddSIPChannel(new SIPUDPChannel(new IPEndPoint(listenAddress, SIP_LISTEN_PORT)));
//            sipTransport.AddSIPChannel(new SIPTCPChannel(new IPEndPoint(listenAddress, SIP_LISTEN_PORT)));
//            // sipTransport.AddSIPChannel(new SIPTLSChannel(localhostCertificate, new IPEndPoint(listenAddress, SIPS_LISTEN_PORT)));
//            //sipTransport.AddSIPChannel(new SIPWebSocketChannel(IPAddress.Any, SIP_WEBSOCKET_LISTEN_PORT));
//            //sipTransport.AddSIPChannel(new SIPWebSocketChannel(IPAddress.Any, SIP_SECURE_WEBSOCKET_LISTEN_PORT, localhostCertificate));

//            // IPv6 channels.
//            sipTransport.AddSIPChannel(new SIPUDPChannel(new IPEndPoint(listenIPv6Address, SIP_LISTEN_PORT)));
//            sipTransport.AddSIPChannel(new SIPTCPChannel(new IPEndPoint(listenIPv6Address, SIP_LISTEN_PORT)));
//            //sipTransport.AddSIPChannel(new SIPTLSChannel(localhostCertificate, new IPEndPoint(listenIPv6Address, SIPS_LISTEN_PORT)));
//            //sipTransport.AddSIPChannel(new SIPWebSocketChannel(IPAddress.IPv6Any, SIP_WEBSOCKET_LISTEN_PORT));
//            //sipTransport.AddSIPChannel(new SIPWebSocketChannel(IPAddress.IPv6Any, SIP_SECURE_WEBSOCKET_LISTEN_PORT, localhostCertificate));

//            EnableTraceLogs(sipTransport);

//            string executableDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

//            // To keep things a bit simpler this example only supports a single call at a time and the SIP server user agent
//            // acts as a singleton
//            SIPServerUserAgent uas = null;
//            CancellationTokenSource rtpCts = null; // Cancellation token to stop the RTP stream.
//            VoIPMediaSession rtpSession = null;

//            // Because this is a server user agent the SIP transport must start listening for client user agents.
//            sipTransport.SIPTransportRequestReceived += async (localSIPEndPoint, remoteEndPoint, sipRequest) =>
//            {
//                try
//                {
//                    if (sipRequest.Method == SIPMethodsEnum.INVITE)
//                    {
//                        Log.LogInformation($"Incoming call request: {localSIPEndPoint}<-{remoteEndPoint} {sipRequest.URI}.");

//                        // Check there's a codec we support in the INVITE offer.
//                        var offerSdp = SDP.ParseSDPDescription(sipRequest.Body);
//                        IPEndPoint dstRtpEndPoint = SDP.GetSDPRTPEndPoint(sipRequest.Body);

//                        if (offerSdp.Media.Any(x => x.Media == SDPMediaTypesEnum.audio && x.MediaFormats.Any(x => x.Key == (int)SDPWellKnownMediaFormatsEnum.PCMU)))
//                        {
//                            Log.LogDebug($"Client offer contained PCMU audio codec.");
//                            //AudioExtrasSource extrasSource = new AudioExtrasSource(new AudioEncoder(), new AudioSourceOptions { AudioSource = AudioSourcesEnum.Music });
//                            //rtpSession = new VoIPMediaSession(new MediaEndPoints { AudioSource = extrasSource });
//                            rtpSession = new VoIPMediaSession();
//                            rtpSession.AcceptRtpFromAny = true;

//                            var setResult = rtpSession.SetRemoteDescription(SdpType.offer, offerSdp);

//                            if (setResult != SetDescriptionResultEnum.OK)
//                            {
//                                // Didn't get a match on the codecs we support.
//                                SIPResponse noMatchingCodecResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.NotAcceptableHere, setResult.ToString());
//                                await sipTransport.SendResponseAsync(noMatchingCodecResponse);
//                            }
//                            else
//                            {
//                                // If there's already a call in progress hang it up. Of course this is not ideal for a real softphone or server but it 
//                                // means this example can be kept simpler.
//                                if (uas?.IsHungup == false)
//                                {
//                                    uas?.Hangup(false);
//                                }
//                                rtpCts?.Cancel();
//                                rtpCts = new CancellationTokenSource();

//                                UASInviteTransaction uasTransaction = new UASInviteTransaction(sipTransport, sipRequest, null);
//                                uas = new SIPServerUserAgent(sipTransport, null, uasTransaction, null);
//                                uas.CallCancelled += (uasAgent, canelReq) =>
//                                {
//                                    rtpCts?.Cancel();
//                                    rtpSession.Close(null);
//                                };
//                                rtpSession.OnRtpClosed += (reason) => uas?.Hangup(false);
//                                uas.Progress(SIPResponseStatusCodesEnum.Trying, null, null, null, null);
//                                await Task.Delay(100);
//                                uas.Progress(SIPResponseStatusCodesEnum.Ringing, null, null, null, null);
//                                await Task.Delay(100);

//                                var answerSdp = rtpSession.CreateAnswer(null);
//                                uas.Answer(SDP.SDP_MIME_CONTENTTYPE, answerSdp.ToString(), null, SIPDialogueTransferModesEnum.NotAllowed);

//                                await rtpSession.Start();
//                            }
//                        }
//                    }
//                    else if (sipRequest.Method == SIPMethodsEnum.BYE)
//                    {
//                        Log.LogInformation("Call hungup.");
//                        SIPResponse byeResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.Ok, null);
//                        await sipTransport.SendResponseAsync(byeResponse);
//                        uas?.Hangup(true);
//                        rtpSession?.Close(null);
//                        rtpCts?.Cancel();
//                    }
//                    else if (sipRequest.Method == SIPMethodsEnum.SUBSCRIBE)
//                    {
//                        SIPResponse notAllowededResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.MethodNotAllowed, null);
//                        await sipTransport.SendResponseAsync(notAllowededResponse);
//                    }
//                    else if (sipRequest.Method == SIPMethodsEnum.OPTIONS || sipRequest.Method == SIPMethodsEnum.REGISTER)
//                    {
//                        SIPResponse optionsResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.Ok, null);
//                        await sipTransport.SendResponseAsync(optionsResponse);
//                    }
//                }
//                catch (Exception reqExcp)
//                {
//                    Log.LogWarning($"Exception handling {sipRequest.Method}. {reqExcp.Message}");
//                }
//            };

//            ManualResetEvent exitMre = new ManualResetEvent(false);

//            Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e)
//            {
//                e.Cancel = true;

//                Log.LogInformation("Exiting...");

//                Hangup(uas).Wait();

//                rtpSession?.Close(null);
//                rtpCts?.Cancel();

//                if (sipTransport != null)
//                {
//                    Log.LogInformation("Shutting down SIP transport...");
//                    sipTransport.Shutdown();
//                }

//                exitMre.Set();
//            };

//            // Task to handle user key presses.
//            Task.Run(async () =>
//            {
//                try
//                {
//                    while (!exitMre.WaitOne(0))
//                    {
//                        var keyProps = Console.ReadKey();

//                        if (keyProps.KeyChar == 'w')
//                        {
//                            Console.WriteLine();
//                            Console.WriteLine("Welcome requested by user...");

//                            if (rtpSession?.IsStarted == true &&
//                                rtpSession?.IsClosed == false)
//                            {
//                                await rtpSession.AudioExtrasSource.SendAudioFromStream(new FileStream(WELCOME_8K, FileMode.Open), AudioSamplingRatesEnum.Rate8KHz);
//                            }
//                        }

//                        if (keyProps.KeyChar == 'h' || keyProps.KeyChar == 'q')
//                        {
//                            Console.WriteLine();
//                            Console.WriteLine("Hangup requested by user...");

//                            if (rtpSession?.IsStarted == true &&
//                                rtpSession?.IsClosed == false)
//                            {
//                                await rtpSession.AudioExtrasSource.SendAudioFromStream(new FileStream(GOODBYE_16K, FileMode.Open), AudioSamplingRatesEnum.Rate16KHz);
//                            }

//                            Hangup(uas).Wait();

//                            rtpSession?.Close(null);
//                            rtpCts?.Cancel();
//                        }

//                        if (keyProps.KeyChar == 'q')
//                        {
//                            Log.LogInformation("Quitting...");

//                            if (sipTransport != null)
//                            {
//                                Log.LogInformation("Shutting down SIP transport...");
//                                sipTransport.Shutdown();
//                            }

//                            exitMre.Set();
//                        }
//                    }
//                }
//                catch (Exception excp)
//                {
//                    Log.LogError($"Exception Key Press listener. {excp.Message}.");
//                }
//            });

//            exitMre.WaitOne();
//        }

//        /// <summary>
//        /// Hangs up the current call.
//        /// </summary>
//        /// <param name="uas">The user agent server to hangup the call on.</param>
//        private static async Task Hangup(SIPServerUserAgent uas)
//        {
//            try
//            {
//                if (uas?.IsHungup == false)
//                {
//                    uas?.Hangup(false);

//                    // Give the BYE or CANCEL request time to be transmitted.
//                    Log.LogInformation("Waiting 1s for call to hangup...");
//                    await Task.Delay(1000);
//                }
//            }
//            catch (Exception excp)
//            {
//                Log.LogError($"Exception Hangup. {excp.Message}");
//            }
//        }

//        /// <summary>
//        /// Enable detailed SIP log messages.
//        /// </summary>
//        private static void EnableTraceLogs(SIPTransport sipTransport)
//        {
//            sipTransport.SIPRequestInTraceEvent += (localEP, remoteEP, req) =>
//            {
//                Log.LogDebug($"Request received: {localEP}<-{remoteEP}");
//                Log.LogDebug(req.ToString());
//            };

//            sipTransport.SIPRequestOutTraceEvent += (localEP, remoteEP, req) =>
//            {
//                Log.LogDebug($"Request sent: {localEP}->{remoteEP}");
//                Log.LogDebug(req.ToString());
//            };

//            sipTransport.SIPResponseInTraceEvent += (localEP, remoteEP, resp) =>
//            {
//                Log.LogDebug($"Response received: {localEP}<-{remoteEP}");
//                Log.LogDebug(resp.ToString());
//            };

//            sipTransport.SIPResponseOutTraceEvent += (localEP, remoteEP, resp) =>
//            {
//                Log.LogDebug($"Response sent: {localEP}->{remoteEP}");
//                Log.LogDebug(resp.ToString());
//            };

//            sipTransport.SIPRequestRetransmitTraceEvent += (tx, req, count) =>
//            {
//                Log.LogDebug($"Request retransmit {count} for request {req.StatusLine}, initial transmit {DateTime.Now.Subtract(tx.InitialTransmit).TotalSeconds.ToString("0.###")}s ago.");
//            };

//            sipTransport.SIPResponseRetransmitTraceEvent += (tx, resp, count) =>
//            {
//                Log.LogDebug($"Response retransmit {count} for response {resp.ShortDescription}, initial transmit {DateTime.Now.Subtract(tx.InitialTransmit).TotalSeconds.ToString("0.###")}s ago.");
//            };
//        }

//        /// <summary>
//        /// Adds a console logger. Can be omitted if internal SIPSorcery debug and warning messages are not required.
//        /// </summary>
//        private static Microsoft.Extensions.Logging.ILogger AddConsoleLogger()
//        {
//            var serilogLogger = new LoggerConfiguration()
//                .Enrich.FromLogContext()
//                .MinimumLevel.Is(Serilog.Events.LogEventLevel.Debug)
//                .WriteTo.Console()
//                .CreateLogger();
//            var factory = new SerilogLoggerFactory(serilogLogger);
//            LogFactory.Set(factory);
//            return factory.CreateLogger<ProgramOld2>();
//        }
//    }
//}