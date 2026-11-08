export interface CronPreset {
  label: string;
  cron: string;
}

/** The one-click schedules offered above the field (all UTC). */
export const cronPresets: readonly CronPreset[] = [
  { label: 'Daily 02:00', cron: '0 2 * * *' },
  { label: 'Every 6 h', cron: '0 */6 * * *' },
  { label: 'Sundays 02:00', cron: '0 2 * * 0' },
  { label: 'Hourly', cron: '0 * * * *' },
];

/** How long typing must pause before the expression is sent for a preview, in ms. */
export const CRON_PREVIEW_DEBOUNCE_MS = 300;
