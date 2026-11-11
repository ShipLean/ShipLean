import * as Sentry from '@sentry/react-native';
import Constants from 'expo-constants';

// Crash reports to Sentry (README, "Crash reports"). index.ts imports this file first, so a crash while the rest of the
// app loads is reported too. The DSN is app.json's expo.extra.sentryDsn: unset or empty, nothing is sent. Development
// builds never send. Only errors and crashes go: no tracing, replay, logs or personal data.
const dsn: unknown = Constants.expoConfig?.extra?.sentryDsn;

if (!__DEV__ && typeof dsn === 'string' && dsn.length > 0) {
  Sentry.init({
    dsn,
    sendDefaultPii: false,
    tracesSampleRate: 0,
  });
}
