/** What a chapter calls to move the player: DemoPlayer fills it, DemoChapters uses it. */
export interface DemoPlayerHandle {
  /** Move the player to a second and play. */
  seekTo(seconds: number): void;
  /** Hear the player's position in seconds (a seek, or the file playing). Returns the way to stop listening. */
  subscribe(listener: (seconds: number) => void): () => void;
}
