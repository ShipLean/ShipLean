// Wi-Fi or mobile data, for the download's Wi-Fi only rule.
import type { NetworkKind } from './ports';

export class NetInfoNetworkKind implements NetworkKind {
  kind(): 'wifi' | 'cellular' | 'none' {
    // TODO: read NetInfo's connection type.
    throw new Error('TODO: the network kind');
  }

  subscribe(cb: () => void): () => void {
    void cb;
    // TODO: subscribe to NetInfo's changes.
    throw new Error('TODO: subscribe to network changes');
  }
}
