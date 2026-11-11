// Expo's default Metro config plus Sentry's, which stamps each bundle and its source map with a debug ID so crash
// reports can be mapped back to the source (README, "Crash reports").
const { getSentryExpoConfig } = require('@sentry/react-native/metro');

module.exports = getSentryExpoConfig(__dirname);
