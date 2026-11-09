// The jest mock of @react-native-community/netinfo. Online until a spec says otherwise:
// await act(() => setConnected(false)).
type Listener = (state: NetState) => void;

interface NetState {
  type: 'wifi' | 'none';
  isConnected: boolean;
  isInternetReachable: boolean;
  details: null;
}

const listeners = new Set<Listener>();
let state: NetState = online(true);

function online(connected: boolean): NetState {
  return {
    type: connected ? 'wifi' : 'none',
    isConnected: connected,
    isInternetReachable: connected,
    details: null,
  };
}

export function setConnected(connected: boolean): void {
  state = online(connected);
  for (const listener of [...listeners]) listener(state);
}

export const netInfoMock = {
  __esModule: true,
  default: {
    addEventListener(listener: Listener) {
      listeners.add(listener);
      listener(state);
      return () => {
        listeners.delete(listener);
      };
    },
    fetch: async () => state,
    refresh: async () => state,
    configure: () => undefined,
  },
};
