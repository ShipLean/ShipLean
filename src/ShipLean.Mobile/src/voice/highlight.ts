// The listening sheet's partial text, split so the words that match a hotword can be shown matched.
export interface HighlightPart {
  text: string;
  matched: boolean;
}

// TODO: split the partial at each hotword (keys, names, states) and mark those parts matched.
export function highlight(partial: string, hotwords: string[]): HighlightPart[] {
  void hotwords;
  return [{ text: partial, matched: false }];
}
