import { describe, expect, it } from 'vitest';
import { MODEL_FILES, type ModelFile } from '../voice/manifest';
import { chosenModel, formatGb, modelChoiceView, totalBytes } from './modelChoice';

// The manifest's sizes are all 0 until they are pinned: sized entries, with Parakeet split over two files as the
// pinned manifest may have it.
const SIZES = { parakeet: 640_000_000, 'qwen3-1.7b': 1_100_000_000, 'qwen3-4b': 2_500_000_000 };
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

const view = (reportedGb: number, soldGb: number) => modelChoiceView({ reportedGb, soldGb }, FILES);

describe('modelChoice', () => {
  it('12 GB picks 4B, 8 GB picks 1.7B with 4B warned, 6 GB greys 4B with the reason, a phone under 6 GB offers no voice', () => {
    const twelve = view(11.2, 12);
    expect(twelve.voice).toBe(true);
    expect(twelve.auto).toEqual({
      model: 'qwen3-4b',
      name: 'Qwen3 4B',
      line: 'This phone: 12 GB → Qwen3 4B',
    });
    expect(twelve.commandRows.map((r) => [r.id, r.availability, r.warning])).toEqual([
      ['qwen3-1.7b', 'ok', null],
      ['qwen3-4b', 'ok', null],
    ]);

    const eight = view(7.6, 8);
    expect(eight.auto?.model).toBe('qwen3-1.7b');
    const fourB8 = eight.commandRows[1];
    expect(fourB8.availability).toBe('warn');
    expect(fourB8.warning).toBe(
      'Not recommended with 8 GB: slow, and may be closed when memory runs low.',
    );
    expect(fourB8.blockedReason).toBeNull();

    const six = view(5.6, 6);
    expect(six.auto?.model).toBe('qwen3-1.7b');
    const fourB6 = six.commandRows[1];
    expect(fourB6.availability).toBe('blocked');
    expect(fourB6.blockedReason).toBe('Needs 8 GB of memory. This phone has 6 GB.');
    expect(chosenModel(six, 'qwen3-4b')).toBeNull();

    const four = view(3.7, 4);
    expect(four.voice).toBe(false);
    expect(four.auto).toBeNull();
    expect(four.commandRows).toEqual([]);
    expect(four.noVoiceReason).toMatch(/needs 6 GB of memory or more.*has 4 GB/i);
  });

  it('sums the download from the manifest: the speech model, whatever its files, plus the chosen command model', () => {
    const twelve = view(11.2, 12);
    expect(twelve.speech.bytes).toBe(SIZES.parakeet);
    expect(totalBytes(twelve, 'auto')).toBe(SIZES.parakeet + SIZES['qwen3-4b']);
    expect(totalBytes(twelve, 'qwen3-1.7b')).toBe(SIZES.parakeet + SIZES['qwen3-1.7b']);
    expect(totalBytes(view(5.6, 6), 'auto')).toBe(SIZES.parakeet + SIZES['qwen3-1.7b']);
    expect(totalBytes(view(5.6, 6), 'qwen3-4b')).toBeNull();
    expect(totalBytes(view(3.7, 4), 'auto')).toBeNull();
  });

  it('gives an estimated time per command by phone class, and none where the model cannot be picked', () => {
    expect(view(11.2, 12).commandRows.map((r) => r.detail)).toEqual([
      'About 1 s a command here. Best for short ones.',
      'About 2 s a command here. Better with long ones that make several changes.',
    ]);
    expect(view(7.6, 8).commandRows[1].detail).toMatch(/^About 12 s a command here/);
    expect(view(5.6, 6).commandRows[1].detail).toBe(
      'Better with long ones that make several changes.',
    );
    expect(view(7.6, 8).estimateNote).toMatch(/estimates/);
  });

  it('writes sizes in decimal gigabytes', () => {
    expect(formatGb(1_100_000_000)).toBe('1.1 GB');
    expect(formatGb(3_140_000_000)).toBe('3.1 GB');
  });
});
