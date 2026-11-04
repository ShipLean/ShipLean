import { demosTestIds } from '../../../demos/testIds';
import { Panel } from '../../draft/Panel';
import { FieldError } from './FieldError';
import { youTubeIdOf, type IntegrationsEdit, type IntegrationsSettingsDto } from './model';

const input =
  'h-7 w-full max-w-md rounded-control border border-edge bg-panel px-2 font-mono text-[length:var(--text-sm)] text-ink disabled:opacity-60';

interface YouTubePanelProps {
  saved: IntegrationsSettingsDto;
  edit: IntegrationsEdit;
  disabled: boolean;
  idError?: string;
  secretError?: string;
  onChange: (patch: Partial<IntegrationsEdit>) => void;
}

/** The instance's YouTube OAuth client, a separate one from sign-in's. The secret is write-only. */
export function YouTubePanel({
  saved,
  edit,
  disabled,
  idError,
  secretError,
  onChange,
}: YouTubePanelProps) {
  const lastFour = saved.youTubeClientSecretLastFour;
  const secretHint =
    lastFour === null
      ? 'Paste the secret'
      : `${saved.youTubeFromEnv ? 'From the environment' : 'Saved'}${lastFour === '' ? '' : `, ending ${lastFour}`}. Leave blank to keep it`;
  return (
    <Panel title="YouTube">
      <p className="m-0 text-[length:var(--text-sm)] text-ink-muted">
        Lets project admins publish sprint demos to their own channel.
      </p>
      <label
        htmlFor={demosTestIds.instanceYoutubeClientId}
        className="grid gap-1 text-[length:var(--text-sm)] font-medium text-ink"
      >
        Client id
        <input
          id={demosTestIds.instanceYoutubeClientId}
          data-testid={demosTestIds.instanceYoutubeClientId}
          className={input}
          value={youTubeIdOf(saved, edit)}
          placeholder="123456789-abc.apps.googleusercontent.com"
          spellCheck={false}
          autoComplete="off"
          disabled={disabled}
          onChange={(e) => onChange({ youTubeClientId: e.target.value })}
        />
        <FieldError message={idError} />
      </label>
      <label
        htmlFor={demosTestIds.instanceYoutubeClientSecret}
        className="grid gap-1 text-[length:var(--text-sm)] font-medium text-ink"
      >
        Client secret
        <input
          id={demosTestIds.instanceYoutubeClientSecret}
          data-testid={demosTestIds.instanceYoutubeClientSecret}
          type="password"
          className={input}
          value={edit.youTubeSecret}
          placeholder={secretHint}
          autoComplete="off"
          spellCheck={false}
          disabled={disabled}
          onChange={(e) => onChange({ youTubeSecret: e.target.value })}
        />
        <span className="font-normal text-ink-muted">
          Stored encrypted. Once saved it isn&apos;t shown again.
        </span>
        <FieldError message={secretError} />
      </label>
      <p
        data-testid={demosTestIds.instanceYoutubeFromEnv}
        className="m-0 text-[length:var(--text-sm)] text-ink-muted"
      >
        {saved.youTubeFromEnv
          ? 'Set by environment. Saving here overrides it.'
          : 'Not set by environment. SHIPLEAN_YOUTUBE_CLIENT_ID and SHIPLEAN_YOUTUBE_CLIENT_SECRET are read when nothing is saved here.'}
      </p>
      <p className="m-0 text-[length:var(--text-sm)] text-ink">
        Not Sign in with Google. This is a separate OAuth client from the one for signing in. It is
        only used when a project admin connects a channel in a project&apos;s Settings, Demos, and
        with no client here that panel says YouTube is off.
      </p>
      <p className="m-0 text-[length:var(--text-sm)] text-ink-muted">
        In Google Cloud, enable YouTube Data API v3 and add this redirect:{' '}
        <code className="font-mono">{`${window.location.origin}/api/youtube/callback`}</code>
      </p>
    </Panel>
  );
}
