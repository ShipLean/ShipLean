import { afterEach, expect, test, vi } from 'vitest';
import page from '../../../ShipLean.TestSupport/DtoFixtures/prompts/prompts-page.json';
import history from '../../../ShipLean.TestSupport/DtoFixtures/prompts/prompt-history.json';
import assembled from '../../../ShipLean.TestSupport/DtoFixtures/prompts/prompt-assembled.json';
import saved from '../../../ShipLean.TestSupport/DtoFixtures/prompts/prompt-saved.json';
import {
  PROMPT_KEYS,
  PROMPT_LAYER_KINDS,
  PROMPT_STAGES,
  PROMPT_STATUSES,
  PROMPT_VERSION_SOURCES,
  getPromptAssembled,
  getPromptHistory,
  getPrompts,
  keepPromptMine,
  savePromptVersion,
  usePromptBuiltIn,
  usePromptVersion,
  type PromptAssembledDto,
  type PromptHistoryDto,
  type PromptSavedDto,
  type PromptsPageDto,
} from './prompts';
import { ok, sent, stubFetch } from '../test-support/fetchStub';

afterEach(() => {
  vi.unstubAllGlobals();
});

// Typecheck fails if the fixture lacks a field, has one of the wrong type, or has one the type doesn't declare.
type DeepWiden<T> = T extends string
  ? string
  : T extends number
    ? number
    : T extends boolean
      ? boolean
      : T extends readonly (infer U)[]
        ? DeepWiden<U>[]
        : T extends object
          ? { [K in keyof T]: DeepWiden<T[K]> }
          : T;

type NoExtraKeys<U, T> = U extends readonly (infer UE)[]
  ? T extends readonly (infer TE)[]
    ? NoExtraKeys<UE, TE>[]
    : U
  : U extends object
    ? T extends object
      ? { [K in keyof U]: K extends keyof T ? NoExtraKeys<U[K], T[K]> : never }
      : U
    : U;

function typed<T>() {
  return <U extends DeepWiden<T>>(value: U & NoExtraKeys<U, DeepWiden<T>>): DeepWiden<T> => value;
}

test('prompts_ShouldRoundTripEachFixture', () => {
  const typedPage = typed<PromptsPageDto>()(page);
  typed<PromptHistoryDto>()(history);
  const typedAssembled = typed<PromptAssembledDto>()(assembled);
  typed<PromptSavedDto>()(saved);

  const prompts = typedPage.stages.flatMap((stage) => stage.prompts);
  expect(typedPage.stages.map((stage) => stage.id)).toEqual(
    PROMPT_STAGES.slice(0, typedPage.stages.length),
  );
  expect(prompts.map((prompt) => prompt.key)).toEqual(
    PROMPT_KEYS.filter((key) => prompts.some((p) => p.key === key)),
  );
  for (const prompt of prompts) expect(PROMPT_STATUSES).toContain(prompt.status);
  for (const version of history.versions) expect(PROMPT_VERSION_SOURCES).toContain(version.source);
  for (const layer of typedAssembled.layers) expect(PROMPT_LAYER_KINDS).toContain(layer.kind);
  expect(saved.key).toBe('brief');
});

test('prompts_ShouldEncodeThePromptKeyInUrls', async () => {
  const calls = [
    [
      () => getPromptHistory('p/1', 'planReviewer'),
      '/api/projects/p%2F1/prompts/planReviewer/history',
      'GET',
      history,
    ],
    [
      () => getPromptAssembled('p/1', 'brief', 'claude code'),
      '/api/projects/p%2F1/prompts/brief/assembled?agent=claude%20code',
      'GET',
      assembled,
    ],
    [
      () => usePromptBuiltIn('p/1', 'sprintDemo'),
      '/api/projects/p%2F1/prompts/sprintDemo/use-built-in',
      'POST',
      saved,
    ],
    [
      () => usePromptVersion('p/1', 'brief', 3),
      '/api/projects/p%2F1/prompts/brief/versions/3/use',
      'POST',
      saved,
    ],
    [
      () => keepPromptMine('p/1', 'brief'),
      '/api/projects/p%2F1/prompts/brief/update/keep',
      'POST',
      saved,
    ],
  ] as const;
  for (const [call, url, method, body] of calls) {
    const fetchStub = stubFetch(ok(body));
    await call();
    expect(sent(fetchStub)).toMatchObject({ url, method });
  }
  const fetchStub = stubFetch(ok(page));
  await getPrompts('p/1');
  expect(sent(fetchStub).url).toBe('/api/projects/p%2F1/prompts');
});

test('savePromptVersion_ShouldPostTheTextNoteAndSource', async () => {
  const fetchStub = stubFetch(ok(saved));
  await savePromptVersion('p/1', 'brief', { text: 'T', note: 'N', source: 'builtInUpdate' });
  expect(sent(fetchStub)).toEqual({
    url: '/api/projects/p%2F1/prompts/brief/versions',
    method: 'POST',
    body: { text: 'T', note: 'N', source: 'builtInUpdate' },
  });
});
