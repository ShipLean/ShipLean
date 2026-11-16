import { useQuery } from '@tanstack/react-query';
import { timeZoneChoices } from '@shiplean/core/api/me';
import { fetchMe, meKey } from '../../../../queries/shared';
import { browserZone } from '../../../shell/timeZone/browserZone';
import { settingsTestIds } from '../../testIds';
import { FormRow } from '../identity/FormRow';
import { errorText } from './errorText';
import { PanelFrame } from './PanelFrame';
import { useTimeZoneSave } from './useTimeZoneSave';

const CHOICES_KEY = ['time-zone-choices'] as const;
const SELECT_ID = 'profile-time-zone';

/** The zone every date and time is shown in: the server's label for it, a pick from the server's list, or this device's zone. */
export function TimeZonePanel() {
  const me = useQuery({ queryKey: meKey, queryFn: fetchMe }).data;
  const choices = useQuery({ queryKey: CHOICES_KEY, queryFn: timeZoneChoices });
  const { save, saving, refusal } = useTimeZoneSave();
  if (me === undefined) return null;
  const device = browserZone();
  return (
    <PanelFrame
      title="Time zone"
      meta="Every date and time in ShipLean is shown in this zone"
      testId={settingsTestIds.timeZonePanel}
    >
      <div className="grid grid-cols-[130px_minmax(0,1fr)] touch:grid-cols-1">
        <FormRow label="Zone" htmlFor={SELECT_ID}>
          <span
            data-testid={settingsTestIds.timeZoneCurrent}
            className={me.timeZone === null ? 'text-ink-muted' : 'text-ink'}
          >
            {me.timeZoneLabel}
          </span>
          {choices.isError ? (
            <span
              data-testid={settingsTestIds.timeZoneChoicesError}
              role="alert"
              className="text-[length:var(--text-xs)] text-alert"
            >
              {errorText(choices.error, 'The list of time zones could not be loaded.')}
            </span>
          ) : (
            <select
              id={SELECT_ID}
              data-testid={settingsTestIds.timeZoneSelect}
              className="field w-70 max-w-full font-sans touch:h-9 touch:w-full"
              value={me.timeZone ?? ''}
              disabled={choices.data === undefined || saving}
              onChange={(e) => save(e.target.value)}
            >
              {me.timeZone === null ? (
                <option value="" disabled>
                  Choose a time zone
                </option>
              ) : null}
              {choices.data?.choices.map((choice) => (
                <option key={choice.id} value={choice.id}>
                  {choice.label}
                </option>
              ))}
            </select>
          )}
          {device !== null && device !== me.timeZone ? (
            <button
              type="button"
              data-testid={settingsTestIds.timeZoneUseDevice}
              className="btn-ghost h-9 touch:h-[var(--touch-target)]"
              disabled={saving}
              onClick={() => save(device)}
            >
              Use this device&apos;s zone ({device})
            </button>
          ) : null}
          {refusal !== null ? (
            <span
              data-testid={settingsTestIds.timeZoneError}
              role="alert"
              className="text-[length:var(--text-xs)] text-alert"
            >
              {refusal}
            </span>
          ) : null}
        </FormRow>
      </div>
    </PanelFrame>
  );
}
