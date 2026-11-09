import { Sheet } from '../ui/Sheet';
import { screenIds } from './testIds';

// TODO: placeholder; the Account sheet (name, email, server, version, profile link, Sign out) replaces it.
export function AccountSheet() {
  return (
    <Sheet title="Account" testID={screenIds.account}>
      {null}
    </Sheet>
  );
}
