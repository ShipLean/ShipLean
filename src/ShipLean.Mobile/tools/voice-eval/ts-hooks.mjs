// Lets Node run the eval's TypeScript straight from the app's source: Node strips the types itself, and this hook
// resolves the app's extensionless relative imports ('./contracts') to their .ts files.
// Use: node --import ./tools/voice-eval/ts-hooks.mjs tools/voice-eval/export.ts
import { registerHooks } from 'node:module';

const RELATIVE = /^\.{1,2}\//;
const HAS_EXTENSION = /\.[cm]?[jt]sx?$|\.json$/;

registerHooks({
  resolve(specifier, context, nextResolve) {
    if (!RELATIVE.test(specifier) || HAS_EXTENSION.test(specifier))
      return nextResolve(specifier, context);
    try {
      return nextResolve(`${specifier}.ts`, context);
    } catch {
      return nextResolve(`${specifier}/index.ts`, context);
    }
  },
});
