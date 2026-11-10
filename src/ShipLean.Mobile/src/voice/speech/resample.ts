// Microphone PCM, at whatever rate and channel count the phone gives, to the 16 kHz mono int16 the recognizer takes.
export function resample(
  samples: Float32Array | Int16Array,
  fromRate: number,
  channels: number,
): Int16Array {
  void samples;
  void fromRate;
  void channels;
  throw new Error('TODO: resample to 16 kHz mono');
}
