import { useRef } from 'react';
import { useLocation, useNavigate, useNavigationType, useSearchParams } from 'react-router-dom';
import type { RetroSearchDto } from '@shiplean/core/api/retros';

export interface StepThroughResults {
  /** The open point's place among the results, from 1; 0 when it is not one of them. */
  position: number;
  total: number;
  canPrevious: boolean;
  canNext: boolean;
  previous: () => void;
  next: () => void;
  /** Closes the open retro: back one entry when this view pushed it in this session, else `selected` and `point` leave the address. */
  close: () => void;
}

/**
 * Steps the open retro through `search.rows` in order. Each step replaces the address entry (writing `selected` and `point`
 * and keeping every other parameter), so Back leaves the open retro however many were visited.
 */
export function useStepThroughResults(
  search: RetroSearchDto,
  pointId: string | undefined,
): StepThroughResults {
  const [, setParams] = useSearchParams();
  const navigate = useNavigate();
  const location = useLocation();
  const navigationType = useNavigationType();
  // Whether the entry this view opened on was pushed in this session: only then does Back lead out of it.
  const pushed = useRef(navigationType === 'PUSH' && location.key !== 'default');

  const { rows } = search;
  const retroId = search.selected?.id;
  const exact = rows.findIndex((row) => row.id === pointId && row.retroId === retroId);
  const index = exact >= 0 ? exact : rows.findIndex((row) => row.retroId === retroId);

  const go = (to: number) => {
    const row = rows[to];
    if (!row) return;
    setParams(
      (params) => {
        const written = new URLSearchParams(params);
        written.set('selected', row.retroId);
        written.set('point', row.id);
        return written;
      },
      { replace: true },
    );
  };

  const close = () => {
    if (pushed.current) {
      navigate(-1);
      return;
    }
    setParams(
      (params) => {
        const written = new URLSearchParams(params);
        written.delete('selected');
        written.delete('point');
        return written;
      },
      { replace: true },
    );
  };

  return {
    position: index + 1,
    total: rows.length,
    canPrevious: index > 0,
    canNext: index >= 0 && index < rows.length - 1,
    previous: () => go(index - 1),
    next: () => go(index + 1),
    close,
  };
}
