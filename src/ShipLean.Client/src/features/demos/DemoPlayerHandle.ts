/** What a chapter calls to move the player: DemoPlayer fills it, DemoChapters uses it. */
export interface DemoPlayerHandle {
  seekTo(seconds: number): void;
}
