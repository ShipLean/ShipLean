import { useContext } from 'react';
import { RetroActionDialogContext, type RetroActionDialogApi } from './RetroActionDialogContext';

/** Opens the action dialog and tells a retro component its project. Needs a `RetroActionDialogProvider` above it. */
export function useRetroActionDialog(): RetroActionDialogApi {
  const api = useContext(RetroActionDialogContext);
  if (!api) throw new Error('useRetroActionDialog needs a RetroActionDialogProvider above it.');
  return api;
}
