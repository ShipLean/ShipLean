import { NetInfoNetworkKind } from './network';

type Listener = (state: { type: string }) => void;

// NetInfo with every connection type: the shared mock (test-support/netInfo.ts) only knows Wi-Fi and none.
const mockNet = { listeners: new Set<Listener>(), type: 'unknown' };

jest.mock('@react-native-community/netinfo', () => ({
  __esModule: true,
  default: {
    addEventListener: (listener: Listener) => {
      mockNet.listeners.add(listener);
      listener({ type: mockNet.type });
      return () => mockNet.listeners.delete(listener);
    },
  },
}));

function connect(type: string): void {
  mockNet.type = type;
  for (const listener of [...mockNet.listeners]) listener({ type });
}

beforeEach(() => {
  mockNet.listeners.clear();
  mockNet.type = 'unknown';
});

describe('networkAdapter', () => {
  it('maps NetInfo wifi, cellular and none, and notifies on change', () => {
    connect('wifi');
    const network = new NetInfoNetworkKind();
    const changes: string[] = [];
    const unsubscribe = network.subscribe(() => changes.push(network.kind()));
    expect(network.kind()).toBe('wifi');

    connect('cellular');
    expect(network.kind()).toBe('cellular');
    connect('none');
    expect(network.kind()).toBe('none');
    connect('ethernet');
    expect(network.kind()).toBe('none');
    connect('wifi');
    expect(changes).toEqual(['wifi', 'cellular', 'none', 'wifi']);

    unsubscribe();
    connect('cellular');
    expect(changes).toEqual(['wifi', 'cellular', 'none', 'wifi']);
    expect(network.kind()).toBe('cellular');
  });
});
