import { assembledTestIds } from './testIds/assembled';
import { changesTestIds } from './testIds/changes';
import { detailTestIds } from './testIds/detail';
import { editorTestIds } from './testIds/editor';
import { navTestIds } from './testIds/nav';
import { pageTestIds } from './testIds/page';
import { searchTestIds } from './testIds/search';

export const promptsTestIds = {
  ...pageTestIds,
  ...navTestIds,
  ...searchTestIds,
  ...detailTestIds,
  ...changesTestIds,
  ...assembledTestIds,
  ...editorTestIds,
} as const;
