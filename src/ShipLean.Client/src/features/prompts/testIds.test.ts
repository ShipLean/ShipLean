import { describe, expect, it } from 'vitest';
import { assembledTestIds } from './testIds/assembled';
import { changesTestIds } from './testIds/changes';
import { detailTestIds } from './testIds/detail';
import { editorTestIds } from './testIds/editor';
import { navTestIds } from './testIds/nav';
import { pageTestIds } from './testIds/page';
import { searchTestIds } from './testIds/search';

const areas = {
  page: pageTestIds,
  nav: navTestIds,
  search: searchTestIds,
  detail: detailTestIds,
  changes: changesTestIds,
  assembled: assembledTestIds,
  editor: editorTestIds,
};

describe('prompts test ids', () => {
  it('TestIds_ShouldHaveNoDuplicateKeysOrValuesAcrossAreas', () => {
    const entries = Object.values(areas).flatMap((ids) => Object.entries(ids));
    const keys = entries.map(([key]) => key);
    const values = entries.map(([, value]) => value);

    expect(keys.length).toBeGreaterThan(0);
    expect(new Set(keys).size).toBe(keys.length);
    expect(new Set(values).size).toBe(values.length);
  });
});
