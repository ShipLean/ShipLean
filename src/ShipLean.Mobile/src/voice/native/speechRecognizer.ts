// Parakeet TDT 0.6B through sherpa-onnx (react-native-sherpa-onnx), fed 16 kHz mono PCM from expo-audio's stream.
// No audio is kept: the PCM stays in memory for recognition only.
import type { SttEngine } from 'react-native-sherpa-onnx/stt';
import type { RecognitionSession, SpeechRecognizer } from '../contracts';

export class NativeSpeechRecognizer implements SpeechRecognizer {
  // TODO: the loaded offline recognizer (nemo_transducer, hotwords file), created once the model files are checked.
  private engine: SttEngine | null = null;

  ready(): boolean {
    return this.engine !== null;
  }

  inputName(): string | null {
    // TODO: the microphone in use, from the audio session's current input.
    return null;
  }

  start(opts: { hotwords: string[] }): Promise<RecognitionSession> {
    void opts;
    // TODO: stream PCM from expo-audio into the recognizer, with partials, the level meter and the final transcript.
    return Promise.reject(new Error('TODO: native speech recognition'));
  }
}
