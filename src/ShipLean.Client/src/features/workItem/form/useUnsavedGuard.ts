import { useEffect, useId } from 'react';
import { useInItemPeek } from '../../itemPeek/ItemPeekContext';
import { setFormDirty } from './unsavedRegistry';

export const UNSAVED_MESSAGE = 'You have unsaved changes. Save or discard them before leaving.';

// While `dirty`, the form joins the registry that the one UnsavedGuardHost blocks navigation from (the router allows a
// single blocker), under the scope it is in: the page, or the item modal over a page. Closing or reloading the tab
// asks the browser to confirm.
export function useUnsavedGuard(dirty: boolean) {
  const id = useId();
  const scope = useInItemPeek() ? 'modal' : 'page';
  useEffect(() => {
    setFormDirty(id, scope, dirty);
    return () => setFormDirty(id, scope, false);
  }, [id, scope, dirty]);
  useEffect(() => {
    if (!dirty) return undefined;
    const onBeforeUnload = (event: BeforeUnloadEvent) => event.preventDefault();
    window.addEventListener('beforeunload', onBeforeUnload);
    return () => window.removeEventListener('beforeunload', onBeforeUnload);
  }, [dirty]);
}
