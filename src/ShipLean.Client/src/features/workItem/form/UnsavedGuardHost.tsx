import { useEffect } from 'react';
import { useBlocker, type BlockerFunction } from 'react-router-dom';
import { showToast } from '../../../components/toastStore';
import { UNSAVED_MESSAGE } from './useUnsavedGuard';
import { hasDirtyForm } from './unsavedRegistry';

const itemOf = (search: string) => new URLSearchParams(search).get('item') ?? '';

// A dirty page form blocks leaving the page (the path changes); opening or closing a modal over it only changes `item`.
// A dirty modal form also blocks closing the modal or swapping its item.
const shouldBlock: BlockerFunction = ({ currentLocation, nextLocation }) => {
  const leavesPage = currentLocation.pathname !== nextLocation.pathname;
  const changesItem = itemOf(currentLocation.search) !== itemOf(nextLocation.search);
  return (
    (hasDirtyForm('page') && leavesPage) || (hasDirtyForm('modal') && (leavesPage || changesItem))
  );
};

// The app's only router blocker (react-router allows one): refuses the navigation with a toast and stays.
export function UnsavedGuardHost() {
  const blocker = useBlocker(shouldBlock);
  useEffect(() => {
    if (blocker.state === 'blocked') {
      showToast(UNSAVED_MESSAGE, { tone: 'warn' });
      blocker.reset();
    }
  }, [blocker]);
  return null;
}
