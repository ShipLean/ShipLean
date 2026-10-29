import { useQuery } from '@tanstack/react-query';
import { fetchHolidayCountries, holidayCountriesKey } from '../settingsApi';
import { settingsTestIds } from '../testIds';
import { FieldErrorText } from './FieldErrorText';
import { Panel } from './Panel';
import type { TabProps } from './tabProps';

const select =
  'h-7 w-fit max-w-full rounded-control border border-edge bg-panel px-1 text-[length:var(--text-sm)] text-ink disabled:opacity-60';

/** The country whose public holidays the project's sprints skip: None first, then every country the feed has listed. */
export function HolidaysPanel({ draft, setDraft, readOnly, errors }: TabProps) {
  const query = useQuery({ queryKey: holidayCountriesKey, queryFn: fetchHolidayCountries });
  const countries = query.data ?? [];
  const unavailable = !query.isPending && countries.length === 0;
  // A saved country the feed no longer lists stays selectable, so the select never shows a value it cannot hold.
  const saved =
    draft.holidayCountry !== '' && !countries.some((c) => c.code === draft.holidayCountry);
  return (
    <Panel title="Public holidays" testId={settingsTestIds.holidaysPanel}>
      <label className="grid gap-1 text-[length:var(--text-sm)] font-medium text-ink">
        Country
        <select
          id="settings-field-holidayCountry"
          data-testid={settingsTestIds.holidayCountry}
          className={select}
          value={draft.holidayCountry}
          disabled={readOnly}
          onChange={(e) => setDraft((d) => ({ ...d, holidayCountry: e.target.value }))}
        >
          <option value="">None</option>
          {saved ? <option value={draft.holidayCountry}>{draft.holidayCountry}</option> : null}
          {countries.map((c) => (
            <option key={c.code} value={c.code}>
              {c.name}
            </option>
          ))}
        </select>
        <FieldErrorText errors={errors} field="holidayCountry" />
        {unavailable ? (
          <span
            className="font-normal text-ink-muted"
            data-testid={settingsTestIds.holidaysUnavailable}
          >
            Holiday data isn't available on this server.
          </span>
        ) : (
          <span className="font-normal text-ink-muted">
            Sprint capacity skips this country's public holidays.
          </span>
        )}
      </label>
    </Panel>
  );
}
