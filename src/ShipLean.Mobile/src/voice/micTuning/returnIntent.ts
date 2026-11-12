// "Type instead" from the Mic tuning page. The page sits at the root, outside the project's VoiceProvider, so it
// leaves a one-shot flag and goes back; the provider takes it when its screen regains focus.
let wanted = false;

export function requestTypeInstead(): void {
  wanted = true;
}

export function takeTypeInstead(): boolean {
  const was = wanted;
  wanted = false;
  return was;
}
