// N9: the voice state and the provider seam the flows build against. Pinned: MicStatus, VoiceSource, VoiceUiState,
// VoiceApi and VoiceInternals.
import { createContext, useContext } from 'react';
import type { ActionRunner, PreviewLine } from '../actions/types';
import type {
  AnswerSheetContent,
  ParsedCall,
  ResolveResult,
  ScreenVoiceSpec,
  Transcript,
  VoiceEngines,
  VoiceFailure,
} from './contracts';

export type MicStatus = 'granted' | 'undetermined' | 'deniedCanAsk' | 'deniedFinal';
export type VoiceSource = 'voice' | 'typed';

export type DisabledReason = 'downloading' | 'skipped' | 'micOff' | 'noModels';

export type VoiceUiState =
  | { kind: 'idle'; permission: MicStatus }
  | { kind: 'disabled'; reason: DisabledReason; permission: MicStatus }
  | { kind: 'listening'; partial: string; screen: string; hotwords: string[] }
  | { kind: 'thinking'; heard: string; source: VoiceSource }
  | {
      kind: 'preview';
      heard: string;
      source: VoiceSource;
      lines: PreviewLine[];
      removed: string[];
      marks: { lineId: string; note: string }[];
      fixing: boolean;
    }
  | { kind: 'failure'; failure: VoiceFailure; heard: string | null; source: VoiceSource }
  | { kind: 'answer'; content: AnswerSheetContent }
  | {
      kind: 'applyFailed';
      batchId: string;
      heard: string;
      source: VoiceSource;
      lines: PreviewLine[];
      reason: string;
      retryable: boolean;
    }
  | { kind: 'applying'; batchId: string };

export type VoiceUiStateOf<K extends VoiceUiState['kind']> = Extract<VoiceUiState, { kind: K }>;

// A one-tap correction: a different value for a span of the heard text, or the whole of it as a note.
export type RetryChange =
  { span: { start: number; end: number }; value: string } | { asNote: true };

export interface VoiceApi {
  state: VoiceUiState;
  holdStart(): void;
  holdEnd(): void;
  cancel(): void;
  apply(lineIds?: string[]): void;
  fix(): void;
  typeInstead(): void;
  submitTyped(text: string): void;
  retryWith(change: RetryChange): void;
  dismiss(): void;
}

export interface VoiceInternals {
  state(): VoiceUiState;
  set(s: VoiceUiState): void;
  spec(): ScreenVoiceSpec | null;
  // Calls cb each time a screen registers its spec or renders with it again (so spec() holds the data the screen has
  // now). Returns the unsubscribe.
  watchSpec(cb: () => void): () => void;
  engines(): VoiceEngines;
  runner: ActionRunner;
  last(): { calls: ParsedCall[]; transcript: Transcript; result: ResolveResult } | null;
  resolve(t: Transcript, opts: { source: VoiceSource }): Promise<void>;
  resolveCalls(calls: ParsedCall[], t: Transcript): ResolveResult;
  dispatch(lineIds?: string[]): string;
}

export const VoiceInternalsContext = createContext<VoiceInternals | null>(null);

// The provider's internals, for the flows and the sheets host.
export function useVoiceInternals(): VoiceInternals {
  const internals = useContext(VoiceInternalsContext);
  if (internals === null) throw new Error('useVoiceInternals needs a VoiceProvider above it.');
  return internals;
}
