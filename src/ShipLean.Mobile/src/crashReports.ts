import * as Sentry from '@sentry/react-native';

// Crash reports to Sentry (README, "Crash reports"). The options, DSN included, are sentry.options.json: native code
// starts Sentry from it before React Native loads (the plugin's useNativeInit), so a crash at launch is reported, and
// Metro bundles it into JavaScript for this call. index.ts imports this file first. Only errors and crashes go: no
// tracing, replay, logs or personal data. Development builds turn it off here, once JavaScript runs.
Sentry.init({ enabled: !__DEV__ });
