let audioContext;
let mediaStreamSource;
let scriptProcessor;

async function startMicrophoneCapture(dotNetRef) {
    const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
    audioContext = new AudioContext();
    mediaStreamSource = audioContext.createMediaStreamSource(stream);

    scriptProcessor = audioContext.createScriptProcessor(1024, 1, 1);
    scriptProcessor.onaudioprocess = function (event) {
        const audioData = event.inputBuffer.getChannelData(0);

        // Convert Float32Array to Int16Array
        const int16Data = new Int16Array(audioData.length);
        for (let i = 0; i < audioData.length; i++) {
            int16Data[i] = Math.min(1, Math.max(-1, audioData[i])) * 32767; // Scale to Int16
        }

        // Send PCM data to C# using JS Interop
        const audioBytes = new Uint8Array(int16Data.buffer);
        dotNetRef.invokeMethodAsync("OnAudioFrameCaptured", Array.from(audioBytes));
        //dotNetRef.invokeMethodAsync("OnAudioFrameCaptured");
    };

    mediaStreamSource.connect(scriptProcessor);
    scriptProcessor.connect(audioContext.destination);
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


/*TODO - Sunday
https://github.com/sipsorcery-org/SIPSorceryMedia.Windows/blob/master/src/WindowsAudioEndPoint.cs#L305
- Implmenting the above class for web microphone
- Cannot send byte[] from js to .Net
- May have to look into using Blazor.WebAudio again to get microphone audio

*/
