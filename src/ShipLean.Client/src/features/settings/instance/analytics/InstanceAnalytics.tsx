import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { Panel } from '../../draft/Panel';
import { AccessState } from '../AccessState';
import { FieldError } from '../integrations/FieldError';
import { errorField } from '../integrations/model';
import { errorText } from '../model';
import {
  analyticsKey,
  getAnalytics,
  putAnalytics,
  testAnalytics,
  type AnalyticsProvider,
  type AnalyticsSettings,
} from './analyticsApi';
import { settingsTestIds } from '../../testIds';

const input =
  'h-7 w-full max-w-md rounded-control border border-edge bg-panel px-2 text-[length:var(--text-sm)] text-ink disabled:opacity-60';

const providers: { value: AnalyticsProvider; label: string }[] = [
  { value: 'none', label: 'Off' },
  { value: 'umami', label: 'Umami' },
  { value: 'posthog', label: 'PostHog' },
];

const copy = {
  umami: {
    hostHint: 'Your Umami address, such as https://umami.example.com',
    keyLabel: 'Website ID',
  },
  posthog: {
    hostHint: 'https://eu.i.posthog.com or https://us.i.posthog.com',
    keyLabel: 'Project API key',
  },
} as const;

// The draft keeps host and key while the provider is Off, so switching back does not lose them.
interface Draft {
  provider: AnalyticsProvider;
  host: string;
  siteKey: string;
  sendNames: boolean;
}

const draftOf = (s: AnalyticsSettings): Draft => ({
  provider: s.provider,
  host: s.host ?? '',
  siteKey: s.siteKey ?? '',
  sendNames: s.sendNames,
});

const orNull = (text: string) => (text.trim() === '' ? null : text.trim());

// The C4 body: Off stores no host or key.
const bodyOf = (d: Draft): AnalyticsSettings =>
  d.provider === 'none'
    ? { provider: 'none', host: null, siteKey: null, sendNames: d.sendNames }
    : {
        provider: d.provider,
        host: orNull(d.host),
        siteKey: orNull(d.siteKey),
        sendNames: d.sendNames,
      };

const isDirty = (saved: AnalyticsSettings, d: Draft) => {
  const body = bodyOf(d);
  return (
    body.provider !== saved.provider ||
    body.host !== saved.host ||
    body.siteKey !== saved.siteKey ||
    body.sendNames !== saved.sendNames
  );
};

/** The instance admin's Analytics tab: where usage events are sent, and a button to prove it works. */
export function InstanceAnalytics() {
  const analytics = useQuery({ queryKey: analyticsKey, queryFn: getAnalytics });
  return (
    <AccessState pending={analytics.isPending} error={analytics.error}>
      {analytics.data ? <AnalyticsForm saved={analytics.data} /> : null}
    </AccessState>
  );
}

function AnalyticsForm({ saved }: { saved: AnalyticsSettings }) {
  const client = useQueryClient();
  const [draft, setDraft] = useState<Draft>(() => draftOf(saved));
  const save = useMutation({
    mutationFn: putAnalytics,
    onSuccess: (stored) => {
      client.setQueryData(analyticsKey, stored);
      setDraft(draftOf(stored));
    },
  });
  const test = useMutation({ mutationFn: testAnalytics });

  const dirty = isDirty(saved, draft);
  const locked = save.isPending;
  const blamed = errorField(save.error);
  const message = save.isError ? errorText(save.error) : undefined;
  const fieldMessage = (field: string) => (blamed === field ? message : undefined);
  const named = ['provider', 'host', 'siteKey'].includes(blamed ?? '');
  const change = (patch: Partial<Draft>) => {
    test.reset();
    setDraft((d) => ({ ...d, ...patch }));
  };
  const on = draft.provider === 'none' ? undefined : copy[draft.provider];

  return (
    <div data-testid={settingsTestIds.analyticsContainer} className="p-4">
      <Panel title="Analytics">
        <label className="grid gap-1 text-[length:var(--text-sm)] font-medium text-ink">
          Provider
          <select
            data-testid={settingsTestIds.analyticsProviderSelect}
            className={input}
            value={draft.provider}
            disabled={locked}
            onChange={(e) => change({ provider: e.target.value as AnalyticsProvider })}
          >
            {providers.map((p) => (
              <option key={p.value} value={p.value}>
                {p.label}
              </option>
            ))}
          </select>
          <span className="font-normal text-ink-muted">
            Usage events from people and agents are sent from this server. Nothing is loaded in
            browsers.
          </span>
          <FieldError
            message={fieldMessage('provider')}
            testId={settingsTestIds.analyticsProviderError}
          />
        </label>
        {on ? (
          <>
            <label className="grid gap-1 text-[length:var(--text-sm)] font-medium text-ink">
              Host
              <input
                data-testid={settingsTestIds.analyticsHostInput}
                className={`${input} font-mono`}
                value={draft.host}
                spellCheck={false}
                autoComplete="off"
                disabled={locked}
                onChange={(e) => change({ host: e.target.value })}
              />
              <span className="font-normal text-ink-muted">{on.hostHint}</span>
              <FieldError
                message={fieldMessage('host')}
                testId={settingsTestIds.analyticsHostError}
              />
            </label>
            <label className="grid gap-1 text-[length:var(--text-sm)] font-medium text-ink">
              {on.keyLabel}
              <input
                data-testid={settingsTestIds.analyticsSiteKeyInput}
                className={`${input} font-mono`}
                value={draft.siteKey}
                spellCheck={false}
                autoComplete="off"
                disabled={locked}
                onChange={(e) => change({ siteKey: e.target.value })}
              />
              <FieldError
                message={fieldMessage('siteKey')}
                testId={settingsTestIds.analyticsSiteKeyError}
              />
            </label>
          </>
        ) : null}
        <label className="grid gap-1 text-[length:var(--text-sm)] font-medium text-ink">
          <span className="flex items-center gap-2">
            <input
              type="checkbox"
              data-testid={settingsTestIds.analyticsSendNamesToggle}
              checked={draft.sendNames}
              disabled={locked}
              onChange={(e) => change({ sendNames: e.target.checked })}
            />
            Send people&apos;s names
          </span>
          <span className="font-normal text-ink-muted">
            Off: people are sent as ids only. On: their names and emails are sent too.
          </span>
        </label>
      </Panel>
      {message !== undefined && !named ? (
        <p
          role="alert"
          data-testid={settingsTestIds.analyticsFieldError}
          className="m-0 mb-3 text-[length:var(--text-sm)] text-alert"
        >
          {message}
        </p>
      ) : null}
      <div className="flex items-center gap-2 text-[length:var(--text-sm)]">
        <button
          type="button"
          data-testid={settingsTestIds.analyticsTestButton}
          className="btn-ghost"
          disabled={dirty || locked || test.isPending}
          onClick={() => test.mutate()}
        >
          Send test event
        </button>
        {dirty ? <span className="text-ink-muted">Save first</span> : null}
        {test.data ? (
          <span
            role={test.data.ok ? 'status' : 'alert'}
            data-testid={settingsTestIds.analyticsTestResult}
            className={test.data.ok ? 'text-ok' : 'text-alert'}
          >
            {test.data.message}
          </span>
        ) : null}
        {test.isError ? (
          <span
            role="alert"
            data-testid={settingsTestIds.analyticsTestResult}
            className="text-alert"
          >
            {errorText(test.error)}
          </span>
        ) : null}
        <span className="flex-1" />
        <button
          type="button"
          data-testid={settingsTestIds.analyticsSaveButton}
          className="btn-signal"
          disabled={!dirty || locked}
          onClick={() => save.mutate(bodyOf(draft))}
        >
          Save
        </button>
      </div>
    </div>
  );
}
