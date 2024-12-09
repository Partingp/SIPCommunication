let audioContext;
let mediaStreamSource;
let scriptProcessor;
async function startMicrophoneCapture(dotNetRef) {
    // Request access to the microphone
    const mediaStream = await navigator.mediaDevices.getUserMedia({ audio: true });
    const audioContext = new AudioContext({ sampleRate: 8000 }); // Set to 8kHz for G.711 compatibility

    // Add an AudioWorkletProcessor to the AudioContext
    await audioContext.audioWorklet.addModule('processor.js'); // Define the processor in a separate file (explained below)

    // Create the AudioWorkletNode using the custom processor
    const workletNode = new AudioWorkletNode(audioContext, 'g711-processor');

    // Connect microphone stream to the audio context
    const source = audioContext.createMediaStreamSource(mediaStream);

    // Connect the source to the AudioWorkletNode and then to the audio context destination
    source.connect(workletNode);
    workletNode.connect(audioContext.destination);

    // Pass captured audio data to .NET
    workletNode.port.onmessage = (event) => {
        const g711Data = event.data;
        //console.log(g711Data);
        dotNetRef.invokeMethodAsync("OnAudioFrameCaptured", Array.from(g711Data));
    };
}

async function stopMicrophoneCapture() {
    if (audioContext) {
        audioContext.close();
        audioContext = null;
    }
    if (mediaStreamSource) {
        mediaStreamSource.disconnect();
        mediaStreamSource = null;
    }
    if (scriptProcessor) {
        scriptProcessor.disconnect();
        scriptProcessor = null;
    }
}

async function pauseMicrophoneCapture() {
    if (audioContext) {
        audioContext.suspend();
    }
}

async function resumeMicrophoneCapture() {
    if (audioContext) {
        audioContext.resume();
    }
}