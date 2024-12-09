let audioContext;
let mediaStreamSource;
let scriptProcessor;

//async function startMicrophoneCapture(dotNetRef) {
//    const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
//    const audioContext = new AudioContext();
//    const mediaStreamSource = audioContext.createMediaStreamSource(stream);

//    const scriptProcessor = audioContext.createScriptProcessor(1024, 1, 1);
//    scriptProcessor.onaudioprocess = async function (event) {
//        const inputBuffer = event.inputBuffer;

//        // Resample the audio to 16,000 Hz
//        const resampledBuffer = await resampleAudioBuffer(inputBuffer, 16000);

//        console.log("Resampled Sample Rate:", resampledBuffer.sampleRate);
//        console.log("Number of Channels:", resampledBuffer.numberOfChannels);
//        for (let channel = 0; channel < resampledBuffer.numberOfChannels; channel++) {
//            const channelData = resampledBuffer.getChannelData(channel);

//            console.log(`Channel ${channel} Data (First 10 Samples):`);
//            console.log(channelData.slice(0, 10));  // Display first 10 samples of the channel
//        }

//        //const muLawBytes = convertToMuLawBytes(resampledBuffer);
//        Need to figure out how to get the data to .Net and then encode in .Net

//        console.log("G.711 µ-Law Encoded Bytes (First 10):", muLawBytes.slice(0, 10));

//        // Example: Pass resampled audio data back to .NET
//        dotNetRef.invokeMethodAsync("OnAudioFrameCaptured", Array.from(new Uint8Array(muLawBytes)));
//    };

//    mediaStreamSource.connect(scriptProcessor);
//    scriptProcessor.connect(audioContext.destination);
//}

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
        const g711Data = convertToG711(inputBuffer); // Convert to G.711 µ-law
        console.log(g711Data); // Handle the encoded G.711 data
        dotNetRef.invokeMethodAsync("OnAudioFrameCaptured", Array.from(g711Data));
    };
}

// Convert Float32 PCM data to G.711 µ-law
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

//async function resampleAudioBuffer(inputBuffer, targetSampleRate) {
//    const offlineAudioContext = new OfflineAudioContext(
//        inputBuffer.numberOfChannels,
//        inputBuffer.duration * targetSampleRate,
//        targetSampleRate
//    );

//    const source = offlineAudioContext.createBufferSource();
//    source.buffer = inputBuffer;

//    source.connect(offlineAudioContext.destination);
//    source.start(0);

//    return await offlineAudioContext.startRendering();
//}

//function floatToInt16(value) {
//    // Convert the floating-point value to 16-bit signed integer
//    return Math.max(-32768, Math.min(32767, Math.floor(value * 32768.0)));
//}


//function linearToMuLaw(sample) {
//    // Apply G.711 µ-law encoding (µ-Law algorithm)
//    const SIGN_BIT = 0x80;
//    const QUANT_MASK = 0xF;
//    const SEG_MASK = 0x70;
//    const BIAS = 0x84;
//    const CLIP = 32635;

//    // Take absolute value and apply clipping
//    let sampleAbs = Math.abs(sample);
//    if (sampleAbs > CLIP) sampleAbs = CLIP;

//    // Add bias
//    sampleAbs += BIAS;

//    // Determine the segment (0-7)
//    let segment = Math.min(Math.floor(sampleAbs / 0x1000), 7);

//    // Extract the mantissa (the rest of the bits)
//    let shift = (segment + 1) << 4;
//    let mantissa = (sampleAbs >> (segment + 3)) & QUANT_MASK;

//    // Calculate the µ-Law encoded byte
//    let result = SIGN_BIT | (segment << 4) | mantissa;

//    return result;
//}

//function convertToMuLawBytes(channelData) {
//    const muLawBytes = new Uint8Array(channelData.length);

//    for (let i = 0; i < channelData.length; i++) {
//        console.log(`Raw Sample: ${channelData[i]}`);
//        let int16Sample = floatToInt16(channelData[i]);
//        console.log(`Int16 Sample: ${int16Sample}`);
//        muLawBytes[i] = linearToMuLaw(int16Sample);
//    }

//    return muLawBytes;
//}

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


/*TODO - Sunday
https://github.com/sipsorcery-org/SIPSorceryMedia.Windows/blob/master/src/WindowsAudioEndPoint.cs#L305
- Implmenting the above class for web microphone
- Cannot send byte[] from js to .Net
- May have to look into using Blazor.WebAudio again to get microphone audio

*/
