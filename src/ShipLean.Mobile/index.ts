// Crash reports first, so a crash while anything below loads is reported.
import './src/crashReports';
// Polyfills next: the core reads crypto at load, and Hermes has neither randomUUID nor getRandomValues.
import './src/polyfills';
import { systemNow } from './src/actions/clock';
import { kvStore } from './src/session/session';
import { configureEngines } from './src/voice/engines';
import { MODEL_FILES } from './src/voice/manifest';
import { nativeAdapters } from './src/voice/native';
import { buildEngines } from './src/voice/wiring/buildEngines';
// expo-router's entry goes last, so everything above is in place before the root layout loads.
import 'expo-router/entry';

// The real voice engines, once. This runs when the bundle has loaded, before the native side starts the app and the
// first render reads engines(). kvStore, not platform(): the root layout configures the platform after this runs.
configureEngines(
  buildEngines(nativeAdapters(), { kv: kvStore, manifest: MODEL_FILES, clock: systemNow }),
);
