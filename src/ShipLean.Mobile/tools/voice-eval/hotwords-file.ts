// Prints the hotwords file the phone gives Parakeet for a screen pack: toHotwords(pack.hotwords). run.py's audio tier
// calls it, so the host eval and the phone encode the same lines. Run from src/ShipLean.Mobile:
//   node --import ./tools/voice-eval/ts-hooks.mjs tools/voice-eval/hotwords-file.ts <out>/<screen>.json
import { readFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';
import { toHotwords } from '../../src/voice/speech/hotwords';

export function hotwordsFile(packJson: string): string {
  const pack = JSON.parse(packJson) as { hotwords: string[] };
  return toHotwords(pack.hotwords);
}

if (process.argv[1] !== undefined && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const file = process.argv[2];
  if (file === undefined) throw new Error('usage: hotwords-file.ts <out>/<screen>.json');
  process.stdout.write(hotwordsFile(readFileSync(file, 'utf8')));
}
