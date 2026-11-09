import { useQuery } from '@tanstack/react-query';
import { Fragment, useState } from 'react';
import { getMcpUrl } from '@shiplean/core/api/agents';
import { mcpUrlKey } from '../agents/connected/ConnectedAgentsPanel';
import { errorText } from '../settings/profile/account/errorText';
import { DeviceRow } from './DeviceRow';
import { DeviceSignOutConfirm } from './DeviceSignOutConfirm';
import { devicesTestIds as ids } from './testIds';
import { useDeviceSessions, useSignOutDevice } from './useDeviceSessions';

// "Signed-in devices" on Your profile: each phone signed in to the ShipLean app as you, with Sign out.
export function SignedInDevicesPanel() {
  const devices = useDeviceSessions();
  const [asking, setAsking] = useState<string | null>(null);
  const signOut = useSignOutDevice(() => setAsking(null));
  const mcp = useQuery({ queryKey: mcpUrlKey, queryFn: () => getMcpUrl() });
  // The host the app signs in to is the configured one, which can differ from the address this page was opened on.
  const host = mcp.data ? new URL(mcp.data.mcpUrl).host : window.location.host;

  return (
    <section data-testid={ids.slotDevices} className="panel mt-3 max-w-[560px] bg-panel">
      <header className="flex min-h-11 flex-wrap items-center gap-2 border-b border-edge px-3 py-2">
        <h2 className="m-0 text-[length:var(--text-md)] font-semibold">Signed-in devices</h2>
        <span className="text-[length:var(--text-xs)] text-ink-muted">
          Phones signed in to the ShipLean app as you
        </span>
      </header>
      <div data-testid={ids.panel}>
        {devices.isPending ? null : devices.isError ? (
          <p
            data-testid={ids.failed}
            className="m-0 p-3 text-[length:var(--text-sm)] text-ink-muted"
          >
            Couldn&apos;t load your signed-in devices. Reload the page to try again.
          </p>
        ) : devices.data.length === 0 ? (
          <p
            data-testid={ids.empty}
            className="m-0 p-3 text-[length:var(--text-sm)] text-ink-muted"
          >
            No phones are signed in.
          </p>
        ) : (
          <ul className="m-0 list-none p-0 [&>li:last-child]:border-b-0">
            {devices.data.map((device) => (
              <Fragment key={device.id}>
                <DeviceRow
                  device={device}
                  disabled={signOut.isPending}
                  onSignOut={() => {
                    signOut.reset();
                    setAsking(device.id);
                  }}
                />
                {asking === device.id ? (
                  <DeviceSignOutConfirm
                    device={device}
                    pending={signOut.isPending}
                    onCancel={() => setAsking(null)}
                    onConfirm={() => signOut.mutate(device)}
                  />
                ) : null}
              </Fragment>
            ))}
          </ul>
        )}
        {signOut.isError ? (
          <p
            data-testid={ids.signOutError}
            role="alert"
            className="m-0 border-t border-edge px-3 py-2 text-[length:var(--text-xs)] text-alert"
          >
            {errorText(signOut.error, "Couldn't sign that phone out. Try again.")}
          </p>
        ) : null}
      </div>
      <footer
        data-testid={ids.addLine}
        className="rounded-b-panel border-t border-edge bg-canvas px-3 py-2 text-[length:var(--text-xs)] text-ink-muted"
      >
        To add a phone, sign in to <b>{host}</b> from the ShipLean app.
      </footer>
    </section>
  );
}
