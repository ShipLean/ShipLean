import { useEffect } from 'react';
import { applyAppearance } from '../../theme';
import { useMe } from './useShellQueries';

// Applies the signed-in user's theme and density once ['me'] resolves.
export function useAppearanceOnLoad() {
  const me = useMe().data;
  useEffect(() => {
    if (me) applyAppearance(me.theme, me.density);
  }, [me]);
}
