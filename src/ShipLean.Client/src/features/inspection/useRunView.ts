import { useSearchParams } from 'react-router-dom';

// The run page's view state lives in the address, so a link reproduces it: ?turn=&errorsOnly=true&row=&q=.
export interface RunView {
  zoomTurn: number | null;
  errorsOnly: boolean;
  selectedRowId: string | null;
  query: string;
  setZoomTurn: (turn: number | null) => void;
  setErrorsOnly: (on: boolean) => void;
  selectRow: (rowId: string | null) => void;
  setQuery: (text: string) => void;
}

export function useRunView(): RunView {
  const [params, setParams] = useSearchParams();
  const turn = Number.parseInt(params.get('turn') ?? '', 10);
  // Zooming, errors-only and selecting are steps back can undo; typing in the find box is not.
  const write = (name: string, value: string | null, replace: boolean) => {
    const next = new URLSearchParams(params);
    if (value === null || value === '') next.delete(name);
    else next.set(name, value);
    setParams(next, replace ? { replace: true } : undefined);
  };
  return {
    zoomTurn: Number.isNaN(turn) ? null : turn,
    errorsOnly: params.get('errorsOnly') === 'true',
    selectedRowId: params.get('row') ?? null,
    query: params.get('q') ?? '',
    setZoomTurn: (value) => write('turn', value === null ? null : String(value), false),
    setErrorsOnly: (on) => write('errorsOnly', on ? 'true' : null, false),
    selectRow: (rowId) => write('row', rowId, true),
    setQuery: (text) => write('q', text, true),
  };
}
