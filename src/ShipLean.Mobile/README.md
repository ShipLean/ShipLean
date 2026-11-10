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
- **A development build** has the app's own native code: `npx expo prebuild`, then `npx expo run:android` or `npx expo run:ios`. That needs the Android SDK or Xcode on your machine. `android/` and `ios/` are generated and gitignored. To run it on a real phone over USB, use `npx expo run:android --device` or `npx expo run:ios --device`; `docs/device-check.md` is the script for that. Store builds and signing (EAS) aren't set up yet.

## Running against a dev server

Start a server (`dotnet run --project src/ShipLean.LocalHost -- --port 5113`, see `docs/local-environment.md`) and type its address into the app's Server field. Sign in as the seeded admin, `joel@shiplean.local` (the password is in `docs/local-environment.md`).

- **Android emulator:** `http://10.0.2.2:<port>`. The emulator's `10.0.2.2` is the host's loopback.
- **iOS simulator:** `http://localhost:<port>`.
- **A phone on the LAN:** `http://<LAN IP>:<port>`, with the server bound to `0.0.0.0` and the port open in the firewall. LocalHost binds `localhost` only, so a phone can't reach it as it is.

Plain `http` works in development: iOS allows local networking (`NSAllowsLocalNetworking` in `app.json`), and debug Android builds allow cleartext. Android **release** builds block cleartext `http`, which is fine because real servers are `https`.

**Identity-proxy servers aren't supported.** A server behind an identity proxy blocks the app's bearer calls to `/api`, so it refuses to sign the app in and the app says so.

## Manual run against a local server

1. Start the server (needs Docker, see `docs/local-environment.md`): `dotnet run --project src/ShipLean.LocalHost -- --port 5113`. It binds loopback only. `LocalHost` has no switch for `0.0.0.0` yet (`AppHostOptions.ListenOnAllInterfaces` exists but isn't a flag), so a phone on the LAN can't reach it as it is; use an emulator or simulator, or for a real phone point the app at a server on your LAN or at the hosted one (see `docs/device-check.md`).
2. Open the app in a **development build**, not Expo Go: Expo Go doesn't register the `shiplean://` scheme, so on Android the sign-in redirect never reaches the app. Android emulator: `npx expo run:android` (needs the Android SDK; server `http://10.0.2.2:5113`). iOS simulator: `npx expo run:ios` (server `http://localhost:5113`).
3. Type the server address; Sign in enables once the server check passes.
4. The browser opens the confirm page. Sign in as `joel@shiplean.local` (password in `docs/local-environment.md`), check the device name, tap Sign in, then Open the app.
5. The app shows the projects. Open Account and check the signed-in name.
6. On the web, open Your profile > Signed-in devices: the phone is listed. Sign it out there and confirm.
7. In the app, pull down on Projects: the next request answers `session_ended` and the app returns to Sign in.
8. Sign in again, then sign out from the app's Account (it asks first) and check the web list no longer shows the phone.

## Inbox

The Inbox tab lists everything waiting on you in a project, and the Answered segment lists what you answered in the last 7 days. It shows four kinds of ask: decisions (with a recommended answer), questions, mentions that need a reply or a Seen, and a Feature's status line once it is 3 days old. Rows run longest waiting first. The tab badge counts open asks, not items. An answer goes on the work item; nothing in the app promises a notification and nothing is read aloud.

Answers wait 5 seconds so Undo can catch them, then send. With no connection they stay on the phone (Answered says "Sends when you're online"), survive a restart, and send one by one when the phone is back online. A send that gets no response is checked against a fresh Inbox before it is ever sent again.

**Signing in as Tom Okafor.** Tom owns the seeded asks. Follow "Manual run against a local server", but sign in as `tom@shiplean.local` with `tom-dev-password` (the seeded dev passwords are in `src/ShipLean.TestSupport/SeedIds.cs`, `Passwords`). In the Inbox he should see:

- SHIP-124, his Story: decisions D1 to D4 and questions Q1 and Q2, all open;
- one open mention, a comment from Priya naming him (the other mention is already seen, so it is not listed);
- one stale status line, on SHIP-103 (a Feature whose status line was verified 5 days ago).

**Manual checklist** (dev build on an emulator, as above):

- [ ] Answer each kind: approve or change a decision, answer a question, reply to a mention and mark one Seen, and confirm the status line is still true.
- [ ] Undo inside 5 seconds: the ask comes back to Waiting and nothing is sent.
- [ ] Airplane mode on, answer a decision, wait 5 seconds: Answered shows "Sends when you're online" and the offline banner shows. Force-stop and reopen the app in airplane mode: the Inbox and the waiting answer are both there. Then airplane mode off: the answer sends once (check the item on the web: one confirmation, not two), and Answered shows who picked it up.
- [ ] The Inbox tab badge drops by one when you answer, and comes back if the send fails.
- [ ] Dark mode: set the phone to dark and check the rows, the sheets and the banner.
- [ ] Tap a row's header: the app's Work item screen opens for that item.

## Native voice modules

Voice runs on the phone, so the app needs a development build (`npx expo prebuild`); Expo Go can't load these. The adapters live in `src/voice/native/` behind N9's interfaces (`src/voice/contracts.ts`) and the ports in `src/voice/native/ports.ts`; jest mocks every one of these modules in `src/test-support/jest.setup.ts`, with settable state in `src/test-support/nativeMocks.ts`.

| Package                    | Version | For                                                                                                                      |
| -------------------------- | ------- | ------------------------------------------------------------------------------------------------------------------------ |
| `llama.rn`                 | 0.12.9  | The command model (Qwen3 Q4_K_M GGUF, GBNF grammars). Its postinstall downloads the release's prebuilt native libraries. |
| `react-native-sherpa-onnx` | 0.4.4   | Speech to text: Parakeet TDT through sherpa-onnx 1.12.35, offline `nemo_transducer` decoding with a hotwords file.       |
| `expo-audio`               | 58.0.11 | The microphone permission and a 16 kHz mono PCM stream (`AudioModule.AudioStream`) while the button is held. No file.    |
| `expo-file-system`         | 58.0.13 | Model files and the resumable download (`createDownloadResumable`, background session on iOS).                           |

**Why `react-native-sherpa-onnx` and not a local module (checked 10 Nov 2026).** sherpa-onnx has no official React Native binding. `react-native-sherpa-onnx` 0.4.4 (XDcobra, released 8 Sep 2026, releases since Jan 2026) is a TurboModule for Android and iOS. It bundles sherpa-onnx 1.12.35 (TDT hotwords need 1.12.24 or later), decodes `nemo_transducer` models offline, and takes `hotwordsFile` and `hotwordsScore` on the recognizer and a hotwords string per stream. Android gets its native libraries from a Maven AAR at Gradle time and iOS from a release xcframework when the pod installs; there is no npm postinstall. Its download and extraction helpers need two optional peers (`@dr.pogodin/react-native-fs`, `@kesha-antonov/react-native-background-downloader`); pnpm installs them as auto-installed peers; `react-native.config.js` keeps them out of autolinking; import only `react-native-sherpa-onnx/stt`. `@siteed/sherpa-onnx.rn` 2.0.1 was the other candidate: it decodes `nemo_transducer` too, but doesn't pass a hotwords file to transducer models yet. The CI job `mobile-android-build` is the proof that the prebuild compiles.

**App config (`app.json`).**

- `expo-audio`: the microphone purpose string; background playback and background recording off (voice is input only and listens only while held).
- `llama.rn`'s plugin with `enableEntitlements: false`: it adds C++20 to the Podfile and Android's optional `libOpenCL.so`. The iOS entitlement `com.apple.developer.kernel.increased-memory-limit` is set in `ios.entitlements` for every build, development builds included, rather than by the plugin for production profiles only.
- Android: `RECORD_AUDIO`, `FOREGROUND_SERVICE` and `FOREGROUND_SERVICE_DATA_SYNC`, and `allowBackup: false`, so Android's auto backup never copies the model directory (or anything else; the secret already stays out of it). iCloud exclusion on iOS is per file, at run time.
- No `expo-background-task`. It schedules short periodic work at the system's choosing (15 minutes at the soonest) and can't keep a multi-GB download running. The download carries on in the background through `expo-file-system`'s background session on iOS, and through a data-sync foreground service on Android.

`npx expo prebuild` rewrites the `android` and `ios` scripts in `package.json`; don't commit that change.

## Voice

Voice is input only: the app never reads anything aloud, plays a sound for it or keeps a recording. "Native voice modules" above says which packages are used and what `app.json` sets; this section says how the pieces fit and how they are checked.

**What runs where.** Both models run on the phone and nothing is uploaded.

- **Speech to text:** Parakeet TDT 0.6B through sherpa-onnx (`react-native-sherpa-onnx`), fed 16 kHz PCM held in memory while the mic is held. Hotwords (the screen's keys and names) are passed to the recognizer.
- **Commands:** Qwen3 (1.7B, or 4B on phones that report 11 GB or more) through llama.rn, constrained by a GBNF grammar to the screen's actions. The words become the same `AppAction`s a tap makes, so Undo, the offline queue and conflicts work the same way.
- **The model manifest** is `src/voice/manifest.ts` (`MODEL_FILES`): each file's name, size and SHA-256, and which model and licence it belongs to. Setup downloads from it and checks every file; Settings › Voice › Model licences is built from it. The licences are CC-BY-4.0 (Parakeet) and Apache 2.0 (Qwen3).

**Why a development build.** The two native modules aren't in Expo Go, and the iPhone needs the increased-memory-limit entitlement, which `app.json` sets for every build. Build with `npx expo prebuild`, then `npx expo run:android --device` or `npx expo run:ios --device`.

**How it is tested here.**

- **Vitest and Jest with fakes.** Every native module is mocked in `src/test-support/jest.setup.ts`, and the voice screens run against fake engines. This proves the app's behaviour, not the models.
- **The host eval.** `tools/voice-eval` runs the test set in `src/voice/testset/*.jsonl` against the real models on a computer; see its README for the export and fetch commands. The numbers it produces are posted on SHIP-916 by the eval run; they are not in this README.
- **The Android CI build.** The `mobile-android-build` job runs `expo prebuild` and Gradle `assembleDebug`, which proves the native modules compile and link on Android.

**What isn't tested here.** iOS isn't built in CI, and no CI job runs a model on a phone. Whether the models load, how fast they answer, what the memory check should allow, the background download, and the microphone permission paths are checked by hand on a Galaxy S22, a Galaxy S24 and an iPhone with `docs/device-check.md`.

**Known gaps** (the script marks them **Not built**): Android has no download foreground service or notification, so a download runs only while the process lives (`FOREGROUND_SERVICE_DATA_SYNC` is declared in `app.json` for a later service); on iPhone a killed app restarts a file that had no saved pause from zero; the iOS available-memory check isn't built, so `APP_SHARE` in `src/voice/model/memoryCheck.ts` (0.5 of the reported memory) is a first guess to calibrate against the device runs.
