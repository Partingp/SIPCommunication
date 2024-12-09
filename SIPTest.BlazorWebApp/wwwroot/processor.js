// processor.js - AudioWorkletProcessor

class G711Processor extends AudioWorkletProcessor {
    constructor() {
        super();
    }

    // Called for each audio block
    process(inputs, outputs, parameters) {
        const input = inputs[0]; // Get the input (audio data from the microphone)
        const output = outputs[0]; // Get the output (where to send processed data)

        // Iterate over the input channels (only 1 in your case)
        for (let channel = 0; channel < input.length; channel++) {
            const inputData = input[channel]; // Get PCM data (float32)
            const g711Data = this.convertToG711(inputData); // Convert to G.711 format
            this.port.postMessage(g711Data); // Send G.711 data to the main thread
        }

        // Return true to continue processing (keep the worklet alive)
        return true;
    }

    // Convert PCM (float32) to G.711 (A-law or μ-law)
 convertToG711(pcmData) {
    const g711Data = new Uint8Array(pcmData.length); // Each 32-bit float -> 8-bit G.711
    for (let i = 0; i < pcmData.length; i++) {
        const sample = Math.max(-1, Math.min(1, pcmData[i])); // Clamp to [-1, 1]
        const intSample = Math.round(sample * 32767); // Convert to 16-bit PCM
        g711Data[i] = this.linearToMuLaw(intSample); // Encode to µ-law
    }
    return g711Data;
}

 linearToMuLaw(sample) {
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
}

// Register the processor
registerProcessor('g711-processor', G711Processor);