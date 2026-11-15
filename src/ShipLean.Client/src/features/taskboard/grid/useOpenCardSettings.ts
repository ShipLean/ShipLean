import { useSearchParams } from 'react-router-dom';

/** Opens the card settings dialog on its Fields pane (`?settings=fields`), keeping the rest of the query. */
export function useOpenCardSettings(): () => void {
  const [params, setParams] = useSearchParams();
  return () => {
    const next = new URLSearchParams(params);
    next.set('settings', 'fields');
    setParams(next);
  };
}
