import { useState } from 'react';
import { Link } from 'react-router-dom';
import type {
  YouTubeDisconnectDto,
  YouTubeProjectDto,
  YouTubeVisibility,
} from '@shiplean/core/api/demos';
import type { ProjectDto } from '@shiplean/core/api/projects';
import { Chip } from '../../../components/Chip';
import { showToast } from '../../../components/toastStore';
import { routePath } from '../../../routes';
import { useCheckYouTube, useDisconnectYouTube, useStartYouTube } from '../../demos/demosApi';
import { demosTestIds as ids } from '../../demos/testIds';
import { Panel } from '../draft/Panel';
import { DisconnectControl } from './youtube/DisconnectControl';
import { useYouTubeReturn, youTubeReturnMessages } from './youtube/useYouTubeReturn';
import { VisibilityChoice } from './youtube/VisibilityChoice';

export interface YouTubePanelProps {
  project: ProjectDto;
  youtube: YouTubeProjectDto;
  readOnly: boolean;
}

const row = 'grid gap-1 text-[length:var(--text-sm)]';
const label = 'font-medium text-ink';
const hint = 'text-[length:var(--text-xs)] text-ink-muted';
const notice = 'max-w-2xl border-l-[3px] px-2.5 py-1.5';

/**
 * Connecting a YouTube channel: off, not connected, pending (choose a visibility and check), refused (the server's reason,
 * the choices kept) and connected. Nothing is connected until the check passes. The check is exercised against the fake and
 * the stub, not yet against Google.
 */
export function YouTubePanel({ project, youtube, readOnly }: YouTubePanelProps) {
  const start = useStartYouTube(project.id);
  const check = useCheckYouTube(project.id);
  const disconnect = useDisconnectYouTube(project.id);
  const { returned, dismiss } = useYouTubeReturn();
  const [choice, setChoice] = useState<YouTubeVisibility | null>(youtube.visibility);
  const [disconnected, setDisconnected] = useState<YouTubeDisconnectDto | null>(null);

  // The latest check's answer is the freshest word on the channel; the settings read catches up behind it.
  const current = check.data ?? youtube;
  const busy = start.isPending || check.isPending || disconnect.isPending;
  const locked = readOnly || busy;

  const fail = (failure: Error) => showToast(failure.message, { tone: 'danger' });

  const connect = () => {
    dismiss();
    setDisconnected(null);
    start.mutate(undefined, {
      onSuccess: ({ url }) => window.location.assign(url),
      onError: fail,
    });
  };

  const runCheck = (visibility: YouTubeVisibility) => {
    dismiss();
    check.mutate(
      { visibility },
      {
        // A connected channel whose new choice could not be checked goes back to the one it passed with.
        onError: (failure) => {
          if (current.status === 'connected') setChoice(current.visibility);
          fail(failure);
        },
      },
    );
  };

  const choose = (visibility: YouTubeVisibility) => {
    setChoice(visibility);
    // A connected channel is checked again at once with a different choice; a pending one waits for the button.
    if (current.status === 'connected' && visibility !== current.visibility) runCheck(visibility);
  };

  const leave = () => {
    dismiss();
    disconnect.mutate(undefined, {
      onSuccess: (result) => {
        check.reset();
        setChoice(null);
        setDisconnected(result);
      },
      onError: fail,
    });
  };

  return (
    <Panel title="YouTube" testId={ids.youtubePanel}>
      <p className="text-[length:var(--text-sm)] text-ink-muted">
        Also publish each demo to your channel. The file kept here plays either way.
      </p>
      {returned === null ? null : (
        <div
          role={returned === 'pending' ? 'status' : 'alert'}
          className={`${notice} ${returned === 'pending' ? 'border-good text-ink' : 'border-alert text-alert'}`}
        >
          {youTubeReturnMessages[returned]}
        </div>
      )}
      {disconnected === null ? null : (
        <div role="status" className={`${notice} border-caution text-ink`}>
          {disconnected.revoked
            ? 'Disconnected. Google confirmed that ShipLean no longer has access.'
            : (disconnected.message ??
              "Disconnected, but Google did not confirm it. Remove ShipLean's access in your Google account.")}
        </div>
      )}
      {current.status === 'off' ? (
        <div className={row}>
          <span className={label}>
            Status <Chip>Off</Chip>
          </span>
          <span data-testid={ids.youtubeOff}>
            YouTube isn&apos;t set up on this ShipLean server, so demos play from the file kept
            here.
          </span>
          <span className={hint}>
            An instance admin turns it on by adding a YouTube OAuth client. That is separate from
            Sign in with Google.
          </span>
          <span>
            <Link className="btn-ghost" to={routePath('instanceSettings', { tab: 'integrations' })}>
              Instance settings › Integrations
            </Link>
          </span>
        </div>
      ) : current.status === 'notConnected' ? (
        <div className={row}>
          <span className={label}>
            Channel <Chip>Not connected</Chip>
          </span>
          <span>
            Demos play from the file kept here. Connect a channel to publish each one to YouTube as
            well.
          </span>
          {readOnly ? (
            <span className={hint}>A project admin connects the channel.</span>
          ) : (
            <span>
              <button
                type="button"
                data-testid={ids.youtubeConnect}
                className="btn-signal"
                disabled={locked}
                onClick={connect}
              >
                Connect YouTube
              </button>
            </span>
          )}
          <span className={hint}>
            Google asks you to let ShipLean upload to, read and delete videos on one channel.
            Nothing is saved as connected until the check passes. Use a throwaway channel while you
            try it.
          </span>
        </div>
      ) : (
        <div className="grid gap-3">
          <div className={row}>
            <span className={label}>
              Channel{' '}
              {current.status === 'connected' ? (
                <span data-testid={ids.youtubeConnected}>
                  <Chip tone="ok">Connected</Chip>
                </span>
              ) : (
                <Chip tone="warn">Pending check</Chip>
              )}
            </span>
            <span className="flex flex-wrap items-center gap-3">
              <b data-testid={ids.youtubeChannel} className="text-ink">
                {current.channelTitle}
              </b>
              {readOnly ? null : (
                <DisconnectControl
                  channelTitle={current.channelTitle ?? 'this channel'}
                  busy={busy}
                  onConfirm={leave}
                />
              )}
            </span>
            <span className={hint}>
              Disconnecting asks Google to revoke ShipLean&apos;s access. If Google can&apos;t be
              reached, ShipLean still forgets the channel and tells you to remove access in your
              Google account.
            </span>
          </div>
          <div className={row}>
            <span className={label}>Visibility</span>
            <VisibilityChoice value={choice} disabled={locked} onChange={choose} />
            <span className={hint}>
              {current.status === 'connected'
                ? 'Choosing a different one runs the check again.'
                : 'Choose the visibility demos are published at.'}
            </span>
          </div>
          <div className={row}>
            <span className={label}>Check</span>
            {current.status === 'connected' ? (
              <span>
                Passed: YouTube kept the test video {current.visibility}.{' '}
                {current.checkedLabel === null ? null : (
                  <span className="text-ink-muted">{current.checkedLabel}</span>
                )}
              </span>
            ) : (
              <span>{current.refusal === null ? 'Not checked yet.' : 'Not passed.'}</span>
            )}
            {current.refusal === null ? null : current.status === 'connected' ? (
              // A passed check whose test video could not be deleted: connected, and the result names the video.
              <div role="status" className={`${notice} border-caution text-ink`}>
                {current.refusal}
              </div>
            ) : (
              <div
                data-testid={ids.youtubeRefusal}
                role="alert"
                className={`${notice} border-alert text-alert`}
              >
                {current.refusal}
              </div>
            )}
            {readOnly ? null : (
              <span>
                <button
                  type="button"
                  data-testid={ids.youtubeCheck}
                  className="btn-signal"
                  disabled={locked || choice === null}
                  onClick={() => choice !== null && runCheck(choice)}
                >
                  {check.isPending
                    ? 'Checking…'
                    : current.status === 'connected'
                      ? 'Check again'
                      : 'Check and connect'}
                </button>
              </span>
            )}
            <span className={hint}>
              Uploads a 2-second test video at the visibility you chose, reads back what YouTube
              made of it, then deletes it. It uses one of the Google project&apos;s 100 uploads a
              day.
            </span>
          </div>
        </div>
      )}
    </Panel>
  );
}
