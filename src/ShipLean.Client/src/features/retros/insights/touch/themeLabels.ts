import type { ChipTone } from '../../../../components/Chip';
import type { RetroThemeStatus, RetroVerdict } from '@shiplean/core/api/retros';

// The words and tones of a theme's chips for the theme sheet, which spells out what each one means. The chips on the
// cards and in the table are StatusChip and VerdictChip; their tap opens "What these mean", which the sheet replaces.
export const statusLabels: Record<RetroThemeStatus, { label: string; tone: ChipTone }> = {
  recurring: { label: 'Recurring', tone: 'warn' },
  beingAddressed: { label: 'Being addressed', tone: 'accent' },
  resolved: { label: 'Resolved', tone: 'ok' },
  quiet: { label: 'Quiet', tone: 'muted' },
};

export const verdictLabels: Record<RetroVerdict, { label: string; tone: ChipTone }> = {
  helped: { label: 'Helped', tone: 'ok' },
  someEffect: { label: 'Some effect', tone: 'warn' },
  noEffect: { label: 'No effect', tone: 'danger' },
  tooEarly: { label: 'Too early', tone: 'muted' },
};
