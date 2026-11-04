import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';

/** What Google's sign-in sent the browser back with: `?youtube=pending|denied|error`. */
export type YouTubeReturn = 'pending' | 'denied' | 'error';

export const youTubeReturnMessages: Record<YouTubeReturn, string> = {
  pending:
    'Signed in to Google. Choose a visibility below and check the channel to finish connecting it.',
  denied: 'You did not allow ShipLean on that channel in Google, so nothing was connected.',
  error: 'ShipLean could not connect that channel and saved nothing. Try Connect YouTube again.',
};

function parse(value: string | null): YouTubeReturn | null {
  return value === 'pending' || value === 'denied' || value === 'error' ? value : null;
}

/**
 * Reads the return from Google once, then clears the query by replacing the entry, so a reload or Back doesn't show it
 * again. The answer stays in the panel until the next thing the person does.
 */
export function useYouTubeReturn() {
  const [params, setParams] = useSearchParams();
  const [returned, setReturned] = useState<YouTubeReturn | null>(() =>
    parse(params.get('youtube')),
  );
  const present = params.has('youtube');

  useEffect(() => {
    if (!present) return;
    setParams(
      (current) => {
        const next = new URLSearchParams(current);
        next.delete('youtube');
        return next;
      },
      { replace: true },
    );
  }, [present, setParams]);

  return { returned, dismiss: () => setReturned(null) };
}
