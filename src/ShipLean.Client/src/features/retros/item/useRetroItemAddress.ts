import { useSearchParams } from 'react-router-dom';
import type { RetroPointDto } from '@shiplean/core/api/retros';
import { parseRetroItemAddress, withRetroItemFilters } from './retroItemAddress';

/** The item page's address and the ways to change it. `onAdded` clears the filter that would hide a point just added. */
export function useRetroItemAddress() {
  const [params, setParams] = useSearchParams();
  const address = parseRetroItemAddress(params);
  return {
    address,
    setRetro: (retro: string | undefined) => setParams(withRetroItemFilters(params, { retro })),
    setDecide: (decide: boolean) => setParams(withRetroItemFilters(params, { decide })),
    onAdded: (point: RetroPointDto) =>
      setParams(
        withRetroItemFilters(params, {
          ...(address.retro !== undefined && point.retroId !== address.retro
            ? { retro: undefined }
            : {}),
          ...(address.decide && point.category === 'wentWell' ? { decide: false } : {}),
        }),
      ),
  };
}
