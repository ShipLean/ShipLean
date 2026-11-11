import type { BacklogRowDto } from '@shiplean/core/api/views';
import { useEffect, useRef, useState } from 'react';
import { rankMoves, ranksOf, type RankMove } from './filters';

export const HIGHLIGHT_MS = 3000;

const NONE: ReadonlyMap<string, RankMove> = new Map();

// The rows whose rank changed since the last read of the same list (`scope` names the level and its filters), for 3 s.
// The first read of a list, and the first after `scope` changes, marks nothing.
export function useRankMoves(scope: string, rows: BacklogRowDto[] | undefined) {
  const last = useRef<{ scope: string; ranks: Map<string, number> } | null>(null);
  const [moves, setMoves] = useState(NONE);
  useEffect(() => {
    if (rows === undefined) return;
    const before = last.current;
    last.current = { scope, ranks: ranksOf(rows) };
    const changed = before?.scope === scope ? rankMoves(before.ranks, rows) : NONE;
    setMoves(changed);
    if (changed.size === 0) return;
    const timer = setTimeout(() => setMoves(NONE), HIGHLIGHT_MS);
    return () => clearTimeout(timer);
  }, [scope, rows]);
  return moves;
}
