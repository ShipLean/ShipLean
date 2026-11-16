import type { RunLinkSource } from '@shiplean/core/api/inspection';

/** 1234 -> "1.2k", 2_700_000 -> "2.7M". Null (the agent reported none) is a dash, never zero. */
export function formatTokens(n: number | null): string {
  if (n === null) return '—';
  if (n < 1000) return String(n);
  if (n < 1_000_000) return `${(n / 1000).toFixed(1).replace(/\.0$/, '')}k`;
  return `${(n / 1_000_000).toFixed(1).replace(/\.0$/, '')}M`;
}

/** In plus out, or null when the agent reported neither. */
export function sumTokens(tokensIn: number | null, tokensOut: number | null): number | null {
  return tokensIn === null && tokensOut === null ? null : (tokensIn ?? 0) + (tokensOut ?? 0);
}

/** Long durations for the header ("1 h 29 m"); short ones read as the waterfall's. */
export function formatTotalDuration(ms: number): string {
  const totalMin = Math.round(ms / 60_000);
  if (totalMin < 60) return totalMin === 0 ? `${Math.round(ms / 1000)} s` : `${totalMin} m`;
  const h = Math.floor(totalMin / 60);
  const m = totalMin % 60;
  return m === 0 ? `${h} h` : `${h} h ${m} m`;
}

export const LINK_SOURCE_LABEL: Record<RunLinkSource, string> = {
  tagged: 'Tagged',
  fromToolCall: 'From pick_up',
  shipleanFile: '.shiplean file',
};

export const LINK_SOURCE_WHY: Record<RunLinkSource, string> = {
  tagged: 'shiplean.item on the run',
  fromToolCall: 'a ShipLean tool call that names the item',
  shipleanFile: "the .shiplean file read by Cursor's hook script",
};

export const SETUP_AGENTS = [
  { agent: 'claude-code', label: 'Claude Code' },
  { agent: 'codex-cli', label: 'Codex CLI' },
  { agent: 'cursor', label: 'Cursor' },
] as const;
