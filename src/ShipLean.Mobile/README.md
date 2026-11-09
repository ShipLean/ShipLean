# ShipLean phone app

An Expo (React Native) app, `shiplean-mobile`, in the pnpm workspace rooted at `src/`. It talks to a ShipLean server through `@shiplean/core` (`src/ShipLean.Core.Client`), the same API layer the web SPA uses. React and React Native follow the Expo SDK (SDK 58: React 19.3, React Native 0.88), so they can differ from the SPA's.

## Commands

Install once from the workspace root: `cd src && pnpm install`. Then, in `src/ShipLean.Mobile`:

| Command                 | What it does                                                                                                                                                         |
| ----------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `pnpm start`            | Starts Metro (`expo start`). Press `a` for Android, `i` for iOS.                                                                                                     |
| `pnpm run format:check` | Prettier. `pnpm run format` writes.                                                                                                                                  |
| `pnpm run lint`         | ESLint.                                                                                                                                                              |
| `pnpm run typecheck`    | `tsc --noEmit`, strict.                                                                                                                                              |
| `pnpm test`             | Vitest, plain Node: `src/**/*.test.ts`.                                                                                                                              |
| `pnpm run test:screens` | Jest with `jest-expo` and React Native Testing Library: `src/**/*.spec.tsx`.                                                                                         |
| `pnpm run bundle`       | A Metro Android export into `.expo/bundle-check` (gitignored). It proves every import resolves, without a device or an Android SDK. Memory-heavy: run it on its own. |

CI runs all six in the `build, lint, typecheck` job. On a pull request it skips them when the diff touches nothing under this folder, the core, the workspace files or `design/tokens.json`.

Filter a run with a path: `pnpm exec vitest run src/session`, `pnpm exec jest src/screens/SignIn`.

## Tests: two runners

- **Vitest** for pure modules (`*.test.ts`): no React Native, nothing native. Config in `vitest.config.ts`.
- **Jest** (`jest-expo` preset) for screens and UI primitives (`*.spec.tsx`). Config in `jest.config.js`, with a 15 s test timeout because busy CI runners blow the 5 s default.

The two patterns never overlap. Put specs next to the code they test under `src/`, **never under `app/`**: expo-router treats every file in `app/` as a route. Only `*.test.ts` and `*.spec.tsx` may import `src/test-support/` (a lint rule enforces it).

## Expo Go or a development build

- **Expo Go** (from the app store) runs the app with no native build: `pnpm start` and scan the QR code. It only works while this SDK's Expo Go is the one in the store.
- **A development build** has the app's own native code: `npx expo prebuild`, then `npx expo run:android` or `npx expo run:ios`. That needs the Android SDK or Xcode on your machine. `android/` and `ios/` are generated and gitignored. Store builds and signing (EAS) aren't set up yet.

## Running against a dev server

Start a server (`dotnet run --project src/ShipLean.LocalHost -- --port 5113`, see `docs/local-environment.md`) and type its address into the app's Server field. Sign in as the seeded admin, `joel@shiplean.local` (the password is in `docs/local-environment.md`).

- **Android emulator:** `http://10.0.2.2:<port>`. The emulator's `10.0.2.2` is the host's loopback.
- **iOS simulator:** `http://localhost:<port>`.
- **A phone on the LAN:** `http://<LAN IP>:<port>`, with the server bound to `0.0.0.0` and the port open in the firewall. LocalHost binds `localhost` only, so a phone can't reach it as it is.

Plain `http` works in development: iOS allows local networking (`NSAllowsLocalNetworking` in `app.json`), and debug Android builds allow cleartext. Android **release** builds block cleartext `http`, which is fine because real servers are `https`.

**Identity-proxy servers aren't supported.** A server behind an identity proxy blocks the app's bearer calls to `/api`, so it refuses to sign the app in and the app says so.
