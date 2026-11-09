// Polyfills first: the core reads crypto at load, and Hermes has neither randomUUID nor getRandomValues.
import './src/polyfills';
// expo-router's entry goes last, so everything above is in place before the root layout loads.
import 'expo-router/entry';
