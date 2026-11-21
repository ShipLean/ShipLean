/** The server's message for a rejected write, or the fallback when the error carries none. */
export function errorText(error: unknown, fallback: string): string {
  return error instanceof Error && error.message !== '' ? error.message : fallback;
}
