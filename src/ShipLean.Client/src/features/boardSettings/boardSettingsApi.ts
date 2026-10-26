import type { QueryClient } from '@tanstack/react-query';
import type { BoardSettingsDto, BoardSettingsPut, BoardSettingsPutResult } from '../../api/boards';
import type { BoardSettingsJson } from '../../api/persisted';
import { api, ApiRequestError } from '../../api/client';
import type { SaveOutcome } from '../../components/SettingsDialog/types';
import { boardRootKey } from '../board/boardApi';

/** Keyed under the project like the rest of its data; a save sets it to the document the server returned. */
export const boardSettingsKey = (projectId: string, boardId: string) =>
  ['projects', projectId, 'boards', boardId, 'settings'] as const;

export const fetchBoardSettings = (boardId: string) =>
  api.get<BoardSettingsDto>(`/api/boards/${encodeURIComponent(boardId)}/settings`);

/**
 * PUTs the draft based on `base`, the document the frame's draft was made from (the cache may already hold a newer one
 * that the draft has not seen). A refused write (a validation error) comes back as a message; anything else is thrown.
 */
export async function saveBoardSettings(
  queryClient: QueryClient,
  projectId: string,
  boardId: string,
  draft: BoardSettingsJson,
  base: BoardSettingsJson,
): Promise<SaveOutcome<BoardSettingsJson>> {
  const body: BoardSettingsPut = { basedOn: base, settings: draft };
  try {
    const result = await api.put<BoardSettingsPutResult>(
      `/api/boards/${encodeURIComponent(boardId)}/settings`,
      body,
    );
    queryClient.setQueryData(boardSettingsKey(projectId, boardId), result.board);
    // This tab's own event is dropped by clientId, so the open board is told to read itself again here.
    void queryClient.invalidateQueries({ queryKey: boardRootKey(projectId, boardId) });
    return { ok: true, document: result.board.settings, mergedSections: result.mergedSections };
  } catch (error) {
    if (error instanceof ApiRequestError && error.code === 'validation') {
      return { ok: false, message: error.message };
    }
    throw error;
  }
}
