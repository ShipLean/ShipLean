// Mic tuning events. The app has no analytics pipeline yet, so each is a Sentry breadcrumb only.
import * as Sentry from '@sentry/react-native';
import type { MicTuningFrom } from '../../links';
import type { TuningResultKind } from './line';

export function micTuningOpened(from: MicTuningFrom): void {
  Sentry.addBreadcrumb({ category: 'mic_tuning', message: 'mic_tuning_opened', data: { from } });
}

export function micTuningResult(kind: TuningResultKind, platform: string): void {
  Sentry.addBreadcrumb({
    category: 'mic_tuning',
    message: 'mic_tuning_result',
    data: { kind, platform },
  });
}
