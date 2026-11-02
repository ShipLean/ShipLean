import { useListKeys } from './useListKeys';

// Mounts the list keys inside the selection provider, where they read the live selection.
export function ListKeysHost() {
  useListKeys();
  return null;
}
