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

**Regenerating the theme.** The app's colours, sizes and spacing come from `design/tokens.json`. After editing it, run `pnpm run theme` and commit `src/theme/tokens.generated.ts`; never edit that file by hand. `pnpm test` fails (`src/theme/tokens.test.ts`) when it is stale, and CI runs that test.

CI runs all six in the `build, lint, typecheck` job. On a pull request it skips them when the diff touches nothing under this folder, the core, the workspace files or `design/tokens.json`.

Filter a run with a path: `pnpm exec vitest run src/session`, `pnpm exec jest src/screens/SignIn`.

## Tests: two runners

- **Vitest** for pure modules (`*.test.ts`): no React Native, nothing native. Config in `vitest.config.ts`.
- **Jest** (`jest-expo` preset) for screens and UI primitives (`*.spec.tsx`). Config in `jest.config.js`, with a 15 s test timeout because busy CI runners blow the 5 s default.

The two patterns never overlap. Put specs next to the code they test under `src/`, **never under `app/`**: expo-router treats every file in `app/` as a route. Only `*.test.ts` and `*.spec.tsx` may import `src/test-support/` (a lint rule enforces it).

## Session and sign-in gate

- **`src/session/sessionStore.ts`** (pure, vitest) holds `{ origin, email, secret, signedIn, pending }`. It reads storage synchronously when it is created, so the first render already knows which side of the gate the user is on. The secret lives only in expo-secure-store (Keychain or Keystore), bound to its server as JSON `{ origin, secret }`; the app is signed in only when that value parses, and the secret goes only to that origin. `commit` writes it last, so a crash part-way leaves the app signed out. The server and email also live in expo-sqlite's `kv-store`, only to fill in Sign in: they are kept after sign-out and never decide where a stored secret is sent (on iOS the Keychain outlives an uninstall, the `kv-store` doesn't). A sign-in in progress calls `beginSignIn(origin)`, then `setPendingSecret(secret)`: requests go to that server with that secret while the app still counts as signed out, until `commit(email)` or `abandon()`. `clearSecret()` never throws.
- **`src/session/session.ts`** binds the real storage; `useSession()` reads it with `useSyncExternalStore`.
- **`src/mobilePlatform.ts`** is the core's `Platform`: the kv store, NetInfo for offline, the session's server and `Authorization: Bearer` secret, `credentials: 'omit'`, and `lostSession()` on any 401 (or 403 `password_change_required`). `lostSession()` clears the query cache and the secret.
- **`app/_layout.tsx`** configures the platform at module level and gates the stack with `Stack.Protected`: `index` (Projects) and `account` (a sheet) when signed in, `sign-in` when not. Clearing the secret is all it takes to land on Sign in.
- **`app/+native-intent.ts`** keeps the browser handoff's `shiplean://signed-in?…` redirect away from the router. On Android it arrives as an ordinary deep link as well as reaching `openAuthSessionAsync`, and would otherwise show "Unmatched route".
- **`index.ts`** loads `src/polyfills.ts` before `expo-router/entry`, because the core reads `crypto.randomUUID` when it loads. `src/entry.spec.tsx` proves the order.
- **`src/links.ts`** is the only place a route path is written: `router.push(links.account())`.
- Screen root test ids are in `src/screens/testIds.ts`; a screen's own ids go in `src/screens/<screen>/testIds.ts`. UI primitives (`src/ui/`) and the theme (`src/theme/`) have their final props and shape; their looks come later.

## Test harness (`src/test-support/`)

Only `*.test.ts` and `*.spec.tsx` may import it.

- **`fakeApi(table)`** replaces `fetch`; the real core client runs. Keys are `"METHOD /path"`; a value is a body (200), `{ status, body }`, or a function of the request. Unlisted requests answer 404 `not_found`. `api.callsTo('GET /api/me')` lists the recorded requests (method, path, origin, headers, body). `apiError(status, code)` and `sessionEnded()` build error replies.
- **`signedInApi()`** is the table a signed-in app reads (`/api/me`, `/api/projects`, `/api/health`); spread it and override routes.
- **Builders:** `aMe`, `aProject`, `aHealth`, `aDeviceSession`, `aDeviceSignIn` (from the pinned fixtures in `src/ShipLean.TestSupport/DtoFixtures`), `TEST_ORIGIN` (`https://shiplean.test`) and `TEST_SECRET`.
- **`renderScreen(ui, { scheme })`** renders a screen on a 390×844 notched phone with its own query cache (`retry: false`, `gcTime: Infinity`).
- **`renderApp({ url, api, scheme, signedIn = true })`** renders the real `app/` through `expo-router/testing-library`, signed in to `https://shiplean.test` as `aMe()`. `signedIn: false` starts signed out with that server and email kept. It turns on jest's fake timers, as `renderRouter` does.
- **Jest setup** (`jest.setup.ts`) fakes expo-secure-store and kv-store in memory (`storage.ts`), expo-web-browser (`jest.mocked(openAuthSessionAsync)`), NetInfo (`setConnected(false)` from `netInfo.ts`), the colour scheme and the safe area.
- Specs assert root ids and test ids, never placeholder text.

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
