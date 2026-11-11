// Wi-Fi or mobile data, for the download's Wi-Fi only rule.
import NetInfo from '@react-native-community/netinfo';
import type { NetworkKind } from './ports';

export type Kind = ReturnType<NetworkKind['kind']>;

// NetInfo's connection type as the store sees it: Wi-Fi, mobile data, or anything else as none.
export function kindOf(state: { type: string }): Kind {
  return state.type === 'wifi' || state.type === 'cellular' ? state.type : 'none';
}

export class NetInfoNetworkKind implements NetworkKind {
  private current: Kind = 'none';
  private readonly listeners = new Set<() => void>();
  private watching = false;

  kind(): Kind {
    this.watch();
    return this.current;
  }

  subscribe(cb: () => void): () => void {
    this.listeners.add(cb);
    this.watch();
    return () => {
      this.listeners.delete(cb);
    };
  }

  // One NetInfo listener for the app's life, from the first read: kind() answers synchronously from it.
  private watch(): void {
    if (this.watching) return;
    this.watching = true;
    NetInfo.addEventListener((state) => {
      const next = kindOf(state);
      if (next === this.current) return;
      this.current = next;
      for (const cb of [...this.listeners]) cb();
    });
  }
}
