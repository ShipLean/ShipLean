// An agent stamp as the server writes it: "Claude Code (claude.ai)", "Codex (unverified)". The app shows the name only.
export function agentDisplayName(stamp: string): string {
  return stamp.replace(/\s*\([^)]*\)\s*$/, '').trim();
}
