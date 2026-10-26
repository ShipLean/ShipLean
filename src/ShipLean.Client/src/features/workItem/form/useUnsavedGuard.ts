import { useEffect } from 'react';
import { useBlocker } from 'react-router-dom';
import { showToast } from '../../../components/toastStore';

export const UNSAVED_MESSAGE = 'You have unsaved changes. Save or discard them before leaving.';

// While `dirty`, in-app navigation is refused with a toast (the router blocks it and the form stays) and closing or
// reloading the tab asks the browser to confirm.
export function useUnsavedGuard(dirty: boolean) {
  const blocker = useBlocker(dirty);
  useEffect(() => {
    if (blocker.state === 'blocked') {
      showToast(UNSAVED_MESSAGE, { tone: 'warn' });
      blocker.reset();
    }
  }, [blocker]);
  useEffect(() => {
    if (!dirty) return undefined;
    const onBeforeUnload = (event: BeforeUnloadEvent) => event.preventDefault();
    window.addEventListener('beforeunload', onBeforeUnload);
    return () => window.removeEventListener('beforeunload', onBeforeUnload);
  }, [dirty]);
}
