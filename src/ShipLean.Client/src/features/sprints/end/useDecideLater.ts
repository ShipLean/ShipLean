import { useState } from 'react';

const key = (sprintId: string) => `shiplean.sprintEndLater.${sprintId}`;

/**
 * Whether this viewer postponed the sprint's rollover in this browser. A convenience only: it survives a page change,
 * a new tab and a restart, and when storage is unavailable the dialog simply comes back.
 */
export function useDecideLater(sprintId: string | undefined): [boolean, (later: boolean) => void] {
  const read = () => {
    try {
      return sprintId !== undefined && localStorage.getItem(key(sprintId)) === '1';
    } catch {
      return false;
    }
  };
  const [later, setLater] = useState(read);
  return [
    later,
    (next) => {
      setLater(next);
      if (sprintId === undefined) return;
      try {
        if (next) localStorage.setItem(key(sprintId), '1');
        else localStorage.removeItem(key(sprintId));
      } catch {
        // storage is blocked: the choice lasts until the page changes
      }
    },
  ];
}
