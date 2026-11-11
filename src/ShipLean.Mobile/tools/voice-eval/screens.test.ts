import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { afterAll, describe, expect, it } from 'vitest';
import { designFeatures } from '../../src/test-support/backlog';
import { designOverview } from '../../src/test-support/features';
import { designInbox } from '../../src/test-support/inboxDesign';
import { designRetro } from '../../src/test-support/retro';
import { aFeatureItem } from '../../src/test-support/workItem';
import { exportPacks } from './export';
import { SCREENS } from './screens';

// What each screen's spec tests show (their own design data): the screen's context must carry all of it, or the
// eval would ask the model about a screen the test set wasn't written for.
const SHOWN: Record<string, () => string[]> = {
  inbox: () => designInbox().items.map((i) => i.key),
  workItem: () => [aFeatureItem().key],
  backlog: () => designFeatures().map((r) => r.key),
  features: () => designOverview().groups.flatMap((g) => g.rows.map((r) => r.key)),
  retro: () =>
    designRetro().categories.flatMap((c) => c.groups.flatMap((g) => g.points.map((p) => p.text))),
};

const SPEC_SCREENS = ['inbox', 'workItem', 'backlog', 'features', 'retro'];

describe('screens', () => {
  const outDir = mkdtempSync(join(tmpdir(), 'voice-eval-screens-'));
  afterAll(() => rmSync(outDir, { recursive: true, force: true }));
  const report = exportPacks({ outDir });

  it('registers the five screens that have a test set', () => {
    expect(Object.keys(SCREENS)).toEqual(expect.arrayContaining(SPEC_SCREENS));
    expect(report.unregistered).toEqual([]);
  });

  it.each(SPEC_SCREENS)("%s's context shows the data its spec's tests use", (screen) => {
    const context = JSON.stringify(SCREENS[screen]().context());
    const shown = SHOWN[screen]();
    expect(shown.length).toBeGreaterThan(0);
    expect(shown.filter((s) => !context.includes(JSON.stringify(s)))).toEqual([]);
  });
});
