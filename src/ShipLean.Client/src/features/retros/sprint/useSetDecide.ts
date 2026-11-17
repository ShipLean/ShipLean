import { useSearchParams } from 'react-router-dom';
import type { SprintRetroDto } from '@shiplean/core/api/retros';
import { parseSprintRetroAddress, withSprintRetroValue } from './sprintRetroAddress';

/**
 * Turns To decide on or off in the address. Turning it on while the picked category has nothing to decide (Went well
 * never has) moves to the first category that has points to decide (D7).
 */
export function useSetDecide() {
  const [params, setParams] = useSearchParams();
  const { cat } = parseSprintRetroAddress(params);
  return (retro: SprintRetroDto, on: boolean) => {
    let next = withSprintRetroValue(params, 'decide', on ? 'open' : 'all');
    const shown = retro.categories.find((c) => c.category === cat);
    const first = retro.categories.find((c) => c.toDecide > 0);
    if (on && (shown?.toDecide ?? 0) === 0 && first)
      next = withSprintRetroValue(next, 'cat', first.category);
    setParams(next);
  };
}
