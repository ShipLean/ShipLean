// Qwen3 (Q4_K_M GGUF) through llama.rn, constrained by the screen's grammar (buildGrammar).
import type { LlamaContext } from 'llama.rn';
import type {
  CommandModel,
  ParsedCall,
  PreparedScreen,
  ScreenVoiceSpec,
  Transcript,
} from '../contracts';

export class LlamaCommandModel implements CommandModel {
  // TODO: the loaded llama.rn context for the chosen model, created once its file is checked and memory allows.
  private context: LlamaContext | null = null;

  ready(): boolean {
    return this.context !== null;
  }

  prepare(spec: ScreenVoiceSpec): Promise<PreparedScreen> {
    void spec;
    // TODO: process the screen's fixed prompt prefix once and keep it, with the screen's grammar.
    return Promise.reject(new Error('TODO: prepare the command model for a screen'));
  }

  parse(p: PreparedScreen, t: Transcript): Promise<ParsedCall[]> {
    void p;
    void t;
    // TODO: complete the user turn under the grammar and parse the output with parseModelOutput.
    return Promise.reject(new Error('TODO: parse a transcript with the command model'));
  }
}
