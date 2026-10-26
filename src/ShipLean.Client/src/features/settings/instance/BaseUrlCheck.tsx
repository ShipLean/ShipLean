import { settingsTestIds } from '../testIds';
import { checkBaseUrl } from './model';

/** What the typed base URL says about the address this page is open on, and about https. */
export function BaseUrlCheck({ baseUrl, origin }: { baseUrl: string; origin: string }) {
  const { matchesAddressInUse, insecure } = checkBaseUrl(baseUrl, origin);
  return (
    <>
      {insecure ? (
        <span
          data-testid={settingsTestIds.instanceUrlInsecure}
          className="text-[length:var(--text-sm)] text-caution"
        >
          ⚠ Use https: sign-in cookies are only sent securely over https.
        </span>
      ) : null}
      {matchesAddressInUse ? (
        <span
          data-testid={settingsTestIds.instanceUrlMatch}
          className="text-[length:var(--text-sm)] text-good"
        >
          ✓ Matches the address you’re using now
        </span>
      ) : (
        <span
          data-testid={settingsTestIds.instanceUrlMismatch}
          className="text-[length:var(--text-sm)] text-caution"
        >
          ⚠ You’re using {origin}. Links ShipLean builds will point somewhere else.
        </span>
      )}
    </>
  );
}
