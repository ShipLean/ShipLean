import { QueryClient } from '@tanstack/react-query';
import { expect, test, vi } from 'vitest';
import type { BoardSettingsDto } from '../../api/boards';
import type { BoardSettingsJson } from '../../api/persisted';
import { boardKey, boardRootKey } from '../board/boardApi';
import { saveBoardSettings } from './boardSettingsApi';

const result = { board: { settings: {} } as BoardSettingsDto, mergedSections: [] };
vi.mock('../../api/client', async (original) => ({
  ...(await original<typeof import('../../api/client')>()),
  api: { put: vi.fn(async () => result) },
}));

test('saveBoardSettings_WhenSaved_ShouldInvalidateTheSavedBoardOnly', async () => {
  const queryClient = new QueryClient();
  const open = boardKey('p1', 'b1', {});
  const expanded = boardKey('p1', 'b1', { older: ['done:l1'] });
  const other = boardKey('p1', 'b2', {});
  for (const key of [open, expanded, other]) queryClient.setQueryData(key, {});

  const draft = {} as BoardSettingsJson;
  const outcome = await saveBoardSettings(queryClient, 'p1', 'b1', draft, draft);

  expect(outcome.ok).toBe(true);
  expect(queryClient.getQueryState(open)?.isInvalidated).toBe(true);
  expect(queryClient.getQueryState(expanded)?.isInvalidated).toBe(true);
  expect(queryClient.getQueryState(other)?.isInvalidated).toBe(false);
  expect(boardRootKey('p1', 'b1')).toEqual(open.slice(0, 4));
});
