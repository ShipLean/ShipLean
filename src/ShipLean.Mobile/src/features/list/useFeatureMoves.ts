import type { FeatureRowDto } from '@shiplean/core/api/featuresOverview';
import { useEffect, useRef, useState } from 'react';
import { rankMoves, ranksOf, type RankMove } from './filters';

export const HIGHLIGHT_MS = 3000;

const NONE: ReadonlyMap<string, RankMove> = new Map();

// The Features whose rank changed since the last read of the overview, for 3 s. The first read marks nothing.
export function useFeatureMoves(rows: FeatureRowDto[] | undefined) {
  const last = useRef<Map<string, number> | null>(null);
  const [moves, setMoves] = useState(NONE);
  useEffect(() => {
    if (rows === undefined) return;
    const before = last.current;
    last.current = ranksOf(rows);
    const changed = before === null ? NONE : rankMoves(before, rows);
    setMoves(changed);
    if (changed.size === 0) return;
    const timer = setTimeout(() => setMoves(NONE), HIGHLIGHT_MS);
    return () => clearTimeout(timer);
  }, [rows]);
  return moves;
}
