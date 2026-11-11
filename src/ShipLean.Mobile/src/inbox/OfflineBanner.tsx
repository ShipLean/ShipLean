import { platform } from '@shiplean/core/platform';
import NetInfo from '@react-native-community/netinfo';
import { useEffect, useState } from 'react';
import { Banner } from '../ui/Banner';
import { inboxIds } from './testIds';

export interface OfflineBannerProps {
  // The server's clock time of the Inbox as last loaded (InboxDto.loadedAtLabel).
  loadedAtLabel: string;
}

// True while the phone has no connection. NetInfo says when it drops, onOnline when it is back; both re-read the platform.
function useOffline(): boolean {
  const [offline, setOffline] = useState(() => platform().isOffline());
  useEffect(() => {
    const read = () => setOffline(platform().isOffline());
    read();
    const stopNet = NetInfo.addEventListener(read);
    const stopOnline = platform().onOnline(read);
    return () => {
      stopNet();
      stopOnline();
    };
  }, []);
  return offline;
}

// The Inbox as last loaded, shown only while offline (D12). The time is the server's label, as given.
export function OfflineBanner({ loadedAtLabel }: OfflineBannerProps) {
  const offline = useOffline();
  if (!offline) return null;
  return (
    <Banner
      testID={inboxIds.offlineBanner}
      tone="warn"
      text={`You're offline. Your Inbox as of ${loadedAtLabel}. Answers send when you're back.`}
    />
  );
}
