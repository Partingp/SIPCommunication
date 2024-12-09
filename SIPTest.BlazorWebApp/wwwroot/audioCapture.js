let audioContext;
let mediaStreamSource;
let scriptProcessor;
async function startMicrophoneCapture(dotNetRef) {
    // Request access to the microphone
    mediaStream = await navigator.mediaDevices.getUserMedia({ audio: true });
    audioContext = new AudioContext({ sampleRate: 8000 }); // Set to 8kHz for G.711 compatibility

    // Connect microphone stream to the audio context
    const source = audioContext.createMediaStreamSource(mediaStream);

    // Create a ScriptProcessorNode for audio processing
    processor = audioContext.createScriptProcessor(1024, 1, 1); // Buffer size 1024 samples
    source.connect(processor);
    processor.connect(audioContext.destination);

    // Process audio data
    processor.onaudioprocess = (event) => {
        const inputBuffer = event.inputBuffer.getChannelData(0); // Get PCM data (float32)
        const g711Data = convertToG711(inputBuffer);
        console.log(g711Data);
        dotNetRef.invokeMethodAsync("OnAudioFrameCaptured", Array.from(g711Data));
    };
}


function convertToG711(pcmData) {
    const g711Data = new Uint8Array(pcmData.length); // Each 32-bit float -> 8-bit G.711
    for (let i = 0; i < pcmData.length; i++) {
        const sample = Math.max(-1, Math.min(1, pcmData[i])); // Clamp to [-1, 1]
        const intSample = Math.round(sample * 32767); // Convert to 16-bit PCM
        g711Data[i] = linearToMuLaw(intSample); // Encode to µ-law
    }
    return g711Data;
}

function linearToMuLaw(sample) {
    const MAX = 32767;
    const BIAS = 132;
    const MASK = 0x7F;

    let sign = (sample >> 8) & 0x80; // Get sign bit
    if (sign !== 0) sample = -sample; // Make sample positive
    sample = Math.min(MAX, sample + BIAS);

    let exponent = 7;
    for (let expMask = 0x4000; (sample & expMask) === 0 && exponent > 0; expMask >>= 1) {
        exponent--;
    }

    const mantissa = (sample >> (exponent + 3)) & 0x0F;
    const ulawByte = ~(sign | (exponent << 4) | mantissa);
    return ulawByte & 0xFF;
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