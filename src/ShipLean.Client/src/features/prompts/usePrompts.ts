// The Prompts tab's hooks. Every read lives under ['projects', projectId, 'prompts'], so a write invalidates the page and
// history together. A write rejects with the core client's ApiRequestError (`field`, `message`) for the caller to show.
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getPromptAssembled,
  getPromptHistory,
  getPrompts,
  keepPromptMine,
  savePromptVersion,
  usePromptBuiltIn as postUseBuiltIn,
  usePromptVersion as postUseVersion,
  type PromptKey,
  type PromptSaveDto,
} from '@shiplean/core/api/prompts';

export const promptsRootKey = (projectId: string) => ['projects', projectId, 'prompts'] as const;
export const promptsPageKey = (projectId: string) =>
  [...promptsRootKey(projectId), 'page'] as const;
export const promptHistoryKey = (projectId: string, key: PromptKey) =>
  [...promptsRootKey(projectId), 'history', key] as const;
export const promptAssembledKey = (projectId: string, key: PromptKey, agent: string) =>
  [...promptsRootKey(projectId), 'assembled', key, agent] as const;

export function usePromptsPage(projectId: string) {
  return useQuery({
    queryKey: promptsPageKey(projectId),
    queryFn: () => getPrompts(projectId),
  });
}

/** The history of one prompt; read only while `enabled` (its tab, the editor's Changes, the update tab). */
export function usePromptHistory(projectId: string, key: PromptKey, enabled: boolean) {
  return useQuery({
    queryKey: promptHistoryKey(projectId, key),
    queryFn: () => getPromptHistory(projectId, key),
    enabled,
  });
}

export function usePromptAssembled(
  projectId: string,
  key: PromptKey,
  agent: string,
  enabled: boolean,
) {
  return useQuery({
    queryKey: promptAssembledKey(projectId, key, agent),
    queryFn: () => getPromptAssembled(projectId, key, agent),
    enabled,
  });
}

function useWrite<TVariables>(
  projectId: string,
  write: (variables: TVariables) => ReturnType<typeof keepPromptMine>,
) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: write,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: promptsRootKey(projectId) }),
  });
}

export function useSavePromptVersion(projectId: string, key: PromptKey) {
  return useWrite(projectId, (body: PromptSaveDto) => savePromptVersion(projectId, key, body));
}

export function useUseBuiltIn(projectId: string, key: PromptKey) {
  return useWrite(projectId, () => postUseBuiltIn(projectId, key));
}

export function useUseVersion(projectId: string, key: PromptKey) {
  return useWrite(projectId, (number: number) => postUseVersion(projectId, key, number));
}

export function useKeepMine(projectId: string, key: PromptKey) {
  return useWrite(projectId, () => keepPromptMine(projectId, key));
}
