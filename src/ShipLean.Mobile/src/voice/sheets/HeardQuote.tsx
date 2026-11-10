import { View } from 'react-native';
import type { VoiceSource } from '../internals';

export interface HeardQuoteProps {
  // What was heard or typed, word for word.
  text: string;
  source: VoiceSource;
  // Spans of text that didn't match, underlined.
  misses: { start: number; end: number }[];
  // The sentence before a Fix, shown with the new one.
  fixedFrom?: string;
  // heardQuoteId unless a sheet shows more than one.
  testID: string;
}

// The heard sentence on every preview and failure sheet: the words are never dropped.
// TODO: the quoted text with the misses underlined, the typed/heard label and the fixed-from line replace this.
export function HeardQuote({ text, source, misses, fixedFrom, testID }: HeardQuoteProps) {
  void text;
  void source;
  void misses;
  void fixedFrom;
  return <View testID={testID} />;
}
