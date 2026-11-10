// Microphone PCM, at whatever rate and channel count the phone gives, to the 16 kHz mono int16 the recognizer takes.
// The phone may deliver a rate other than the one asked for, so any rate is accepted. Channels are interleaved and
// averaged to mono. Going down, each output sample is the mean of the input it covers (a box filter, which keeps
// most of what is above 8 kHz out); going up, samples are interpolated linearly. float32 is read as -1..1.
export const TARGET_RATE = 16000;

export function resample(
  samples: Float32Array | Int16Array,
  fromRate: number,
  channels: number,
): Int16Array {
  if (!(fromRate > 0) || !Number.isInteger(channels) || channels < 1) {
    throw new Error(`resample: bad format (${fromRate} Hz, ${channels} channels)`);
  }
  const scale = samples instanceof Float32Array ? 32767 : 1;
  const frames = Math.floor(samples.length / channels);
  const mono = new Float64Array(frames);
  for (let f = 0; f < frames; f++) {
    let sum = 0;
    for (let c = 0; c < channels; c++) sum += samples[f * channels + c];
    mono[f] = (sum / channels) * scale;
  }
  if (fromRate === TARGET_RATE) return toInt16(mono);

  const outLength = Math.round((frames * TARGET_RATE) / fromRate);
  const out = new Float64Array(outLength);
  const step = fromRate / TARGET_RATE;
  if (step > 1) {
    for (let i = 0; i < outLength; i++)
      out[i] = average(mono, i * step, Math.min((i + 1) * step, frames));
  } else {
    for (let i = 0; i < outLength; i++) {
      const at = i * step;
      const left = Math.floor(at);
      const right = Math.min(left + 1, frames - 1);
      out[i] = mono[left] + (mono[right] - mono[left]) * (at - left);
    }
  }
  return toInt16(out);
}

// The mean of mono over [from, to), weighting the partly covered samples at each end.
function average(mono: Float64Array, from: number, to: number): number {
  let sum = 0;
  let weight = 0;
  for (let j = Math.floor(from); j < to; j++) {
    const w = Math.min(j + 1, to) - Math.max(j, from);
    sum += mono[j] * w;
    weight += w;
  }
  return weight > 0 ? sum / weight : 0;
}

function toInt16(values: Float64Array): Int16Array {
  const out = new Int16Array(values.length);
  for (let i = 0; i < values.length; i++)
    out[i] = Math.max(-32768, Math.min(32767, Math.round(values[i])));
  return out;
}
