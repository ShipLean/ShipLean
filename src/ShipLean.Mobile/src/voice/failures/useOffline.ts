import { platform } from '@shiplean/core/platform';
import NetInfo from '@react-native-community/netinfo';
import { useEffect, useState } from 'react';

// True while the phone has no connection; NetInfo says when it drops, onOnline when it is back.
export function useOffline(): boolean {
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
