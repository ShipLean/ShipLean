import { describe, expect, it } from 'vitest';
import type { ModelStatus } from '../voice/contracts';
import { MODEL_FILES, type ModelFile } from '../voice/manifest';
import { modelChoiceView } from './modelChoice';
import {
  accountVoiceValue,
  changeSummary,
  changeUnderWay,
  commandModelLine,
  licenceEntries,
  licencesLine,
  storageView,
  voiceStatusView,
} from './voiceSettingsView';

// The manifest's sizes are all 0 until they are pinned: sized entries, with Parakeet split over two files as the
// pinned manifest may have it.
const SIZES = { parakeet: 600_000_000, 'qwen3-1.7b': 1_100_000_000, 'qwen3-4b': 2_500_000_000 };
const PARAKEET_FILES = MODEL_FILES.filter((f) => f.model === 'parakeet').length;
const FILES: ModelFile[] = [
  ...MODEL_FILES.filter((f) => f.model === 'parakeet').flatMap((f) => [
    { ...f, id: `${f.id}-a`, bytes: SIZES.parakeet / (2 * PARAKEET_FILES) },
    { ...f, id: `${f.id}-b`, bytes: SIZES.parakeet / (2 * PARAKEET_FILES) },
  ]),
  ...MODEL_FILES.filter((f) => f.model !== 'parakeet').map((f) => ({
    ...f,
    bytes: SIZES[f.model],
  })),
];
const idsOf = (model: string) => FILES.filter((f) => f.model === model).map((f) => f.id);

const PHONE = { model: 'Galaxy S24 Ultra', memoryGb: 12 };
const view = modelChoiceView({ reportedGb: 11.2, soldGb: 12 }, FILES);

function status(partial: Partial<ModelStatus> = {}): ModelStatus {
  return {
    phase: 'ready',
    choice: 'auto',
    resolved: 'qwen3-4b',
    bytesDone: 0,
    bytesTotal: 0,
    freeBytes: 32_000_000_000,
    wifiOnly: true,
    reason: null,
    files: [
      ...idsOf('parakeet').map((id) => ({
        id,
        state: 'checked' as const,
        bytes: SIZES.parakeet / (2 * PARAKEET_FILES),
      })),
      ...idsOf('qwen3-4b').map((id) => ({
        id,
        state: 'checked' as const,
        bytes: SIZES['qwen3-4b'],
      })),
    ],
    ...partial,
  };
}

describe('voiceSettingsView', () => {
  it('the status line and the Account value for ready, downloading, skipped and mic off', () => {
    const ready = status();
    expect(voiceStatusView(ready, 'granted', PHONE).line).toBe(
      'Voice is ready · Galaxy S24 Ultra · 12 GB memory',
    );
    expect(accountVoiceValue(ready, 'granted', FILES)).toBe('Ready · Qwen3 4B');

    const downloading = status({ phase: 'downloading', bytesDone: 650, bytesTotal: 1000 });
    expect(voiceStatusView(downloading, 'granted', PHONE)).toMatchObject({
      kind: 'downloading',
      line: 'Downloading voice models · 65%',
      percent: 65,
      setUp: false,
    });
    expect(accountVoiceValue(downloading, 'granted', FILES)).toBe('Downloading · 65%');

    const skipped = status({ phase: 'skipped', resolved: null });
    expect(voiceStatusView(skipped, 'granted', PHONE)).toMatchObject({
      kind: 'off',
      line: 'Voice is off',
      setUp: true,
    });
    expect(accountVoiceValue(skipped, 'granted', FILES)).toBe('Off · Set up');

    expect(voiceStatusView(ready, 'deniedFinal', PHONE).line).toBe(
      'Voice is ready, but the microphone is off',
    );
    expect(accountVoiceValue(ready, 'deniedFinal', FILES)).toBe('Ready · mic off');
    expect(accountVoiceValue(ready, 'undetermined', FILES)).toBe('Ready · mic off');
  });

  it('storage is per model from what the store holds, and the delete line frees their sum', () => {
    const s = storageView(status(), FILES);

    expect(s.rows.map((r) => [r.name, r.bytes])).toEqual([
      ['Parakeet TDT 0.6B', 600_000_000],
      ['Qwen3 4B', 2_500_000_000],
    ]);
    expect(s.deleteLine).toBe('Frees 3.1 GB. Voice turns off; your choices are kept.');
    expect(storageView(status({ phase: 'skipped', files: [] }), FILES).totalBytes).toBe(0);
  });

  it('a change keeps voice ready on the old model and reports the new download', () => {
    const changing = status({
      choice: 'qwen3-1.7b',
      bytesDone: 440_000_000,
      bytesTotal: 1_100_000_000,
      reason: 'wifi',
      files: [
        ...status().files,
        { id: idsOf('qwen3-1.7b')[0], state: 'downloading', bytes: 440_000_000 },
      ],
    });

    expect(changeUnderWay(changing, FILES)).toEqual({
      name: 'Qwen3 1.7B',
      percent: 40,
      reason: 'wifi',
    });
    expect(changeUnderWay(status(), FILES)).toBeNull();
    expect(commandModelLine(changing, view, FILES)).toBe('Qwen3 4B');
    expect(
      commandModelLine(
        status({ phase: 'skipped', resolved: null, choice: 'qwen3-1.7b' }),
        view,
        FILES,
      ),
    ).toBe('Qwen3 1.7B');
    expect(commandModelLine(status(), view, FILES)).toBe('Auto · Qwen3 4B');
  });

  it('picking 1.7B on a 4B phone downloads 1.1 GB, keeps 4B until checked and frees 2.5 GB', () => {
    const s = changeSummary(status(), view, 'qwen3-1.7b', FILES);

    expect(s.lines).toEqual([
      'Qwen3 1.7B downloads 1.1 GB on Wi-Fi.',
      'Voice keeps using Qwen3 4B until Qwen3 1.7B is checked.',
      'Then Qwen3 4B is deleted to free 2.5 GB.',
    ]);
    expect(s.downloadLabel).toBe('Download 1.1 GB');
    expect(changeSummary(status(), view, 'auto', FILES)).toMatchObject({
      current: true,
      downloadLabel: null,
    });
  });

  it('the licences come from the manifest: one entry per model, grouped by author on the row', () => {
    expect(licenceEntries(FILES).map((e) => e.name)).toEqual([
      'Parakeet TDT 0.6B',
      'Qwen3 1.7B',
      'Qwen3 4B',
    ]);
    expect(licencesLine(FILES)).toBe('Parakeet TDT 0.6B (CC-BY-4.0) · Qwen3 (Apache 2.0)');
  });
});
