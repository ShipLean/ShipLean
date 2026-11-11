import { platform } from '@shiplean/core/platform';
import { useRouter } from 'expo-router';
import { useEffect } from 'react';
import { links } from '../links';
import { markSetupOffered, shouldOfferSetup } from './firstRun';

// Called by Projects, the first screen after sign-in: opens the voice setup once on a phone that was never offered it.
export function useSetupGate(): void {
  const router = useRouter();
  useEffect(() => {
    const kv = platform().persistentStore();
    if (!shouldOfferSetup(kv)) return;
    // Marked before it opens, so coming back to Projects, or a restart in the middle, never opens it again.
    markSetupOffered(kv);
    router.push(links.setup('models'));
  }, [router]);
}
