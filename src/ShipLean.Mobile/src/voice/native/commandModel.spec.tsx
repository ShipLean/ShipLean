import { waitFor } from '@testing-library/react-native';
import { initLlama } from 'llama.rn';
import { buildGrammar, buildSystemPrompt } from '../commandPrompt';
import type { ScreenVoiceSpec } from '../contracts';
import { FakeModelFiles } from '../../test-support/modelFiles';
import { aTranscript, FakeModelStore } from '../../test-support/voice';
import { LlamaCommandModel } from './commandModel';

interface MockCompletionParams {
  messages: { role: string; content: string }[];
  n_predict?: number;
  grammar?: string;
  temperature?: number;
  enable_thinking?: boolean;
  jinja?: boolean;
  add_generation_prompt?: boolean;
}

interface MockContext {
  completion: jest.Mock<Promise<{ text: string }>, [MockCompletionParams]>;
  saveSession: jest.Mock<Promise<number>, [string]>;
  loadSession: jest.Mock<Promise<{ tokens_loaded: number; prompt: string }>, [string]>;
  release: jest.Mock<Promise<void>, []>;
}

// T0v's llama.rn mock has no saveSession or loadSession, so this spec mocks the calls it asserts on.
const mockLlama = { output: '[]', contexts: [] as MockContext[] };

jest.mock('llama.rn', () => ({
  __esModule: true,
  initLlama: jest.fn(async () => {
    const context: MockContext = {
      completion: jest.fn<Promise<{ text: string }>, [MockCompletionParams]>(async () => ({
        text: mockLlama.output,
      })),
      saveSession: jest.fn<Promise<number>, [string]>(async () => 1),
      loadSession: jest.fn<Promise<{ tokens_loaded: number; prompt: string }>, [string]>(
        async () => ({ tokens_loaded: 1, prompt: '' }),
      ),
      release: jest.fn(async () => undefined),
    };
    mockLlama.contexts.push(context);
    return context;
  }),
}));

const QWEN_17 = 'qwen3-1.7b-q4_k_m';
const QWEN_4 = 'qwen3-4b-q4_k_m';

function aSpec(
  screen: string,
  context: () => Record<string, unknown> = () => ({}),
): ScreenVoiceSpec {
  return {
    screen,
    actions: [
      {
        name: 'open',
        description: 'Open an item.',
        args: { type: 'object', properties: { key: { type: 'string' } }, required: ['key'] },
      },
      {
        name: 'addNote',
        description: 'Post a note on the item.',
        args: { type: 'object', properties: { text: { type: 'string' } }, required: ['text'] },
        noteArg: 'text',
      },
    ],
    context,
    hotwords: () => [],
    toActions: () => ({ ok: false, failure: { kind: 'nothingHeard', detail: {} } }),
  };
}

// A loaded model: the store is ready on 1.7B (an 8 GB phone) and its file is on disk.
async function loadedModel() {
  const models = new FakeModelStore();
  const files = new FakeModelFiles();
  files.place(QWEN_17);
  const model = new LlamaCommandModel({ files, models });
  await waitFor(() => expect(model.ready()).toBe(true));
  return { model, models, files, context: mockLlama.contexts[0] };
}

const prefixOnly = (c: MockContext) =>
  c.completion.mock.calls.filter(([params]) => params.n_predict === 0);

afterEach(() => {
  mockLlama.output = '[]';
  mockLlama.contexts = [];
  jest.mocked(initLlama).mockClear();
});

describe('commandModelAdapter', () => {
  it('prepare evaluates the prefix once; two parses reuse it', async () => {
    const { model, context } = await loadedModel();
    const spec = aSpec('workItem');

    const prepared = await model.prepare(spec);
    await model.parse(prepared, aTranscript({ text: 'open SHIP-1' }));
    await model.parse(prepared, aTranscript({ text: 'open SHIP-2' }));

    const system = buildSystemPrompt(spec);
    // The prefix alone, evaluated once into the cache and saved.
    expect(prefixOnly(context)).toHaveLength(1);
    expect(prefixOnly(context)[0][0].messages).toEqual([{ role: 'system', content: system }]);
    expect(prefixOnly(context)[0][0].add_generation_prompt).toBe(false);
    expect(context.saveSession).toHaveBeenCalledTimes(1);
    // Each parse starts with the same prefix, so llama.rn reuses the cached tokens; nothing is loaded again.
    const parses = context.completion.mock.calls.filter(([params]) => params.n_predict !== 0);
    expect(parses).toHaveLength(2);
    for (const [params] of parses)
      expect(params.messages[0]).toEqual({ role: 'system', content: system });
    expect(context.loadSession).not.toHaveBeenCalled();

    // The screen rebuilt its spec (same screen and actions): no new evaluation.
    await model.prepare(aSpec('workItem'));
    expect(prefixOnly(context)).toHaveLength(1);
  });

  it('a parse on a screen prepared before another loads its saved prefix instead of evaluating it again', async () => {
    const { model, context } = await loadedModel();

    const inbox = await model.prepare(aSpec('inbox'));
    await model.prepare(aSpec('backlog'));
    await model.parse(inbox, aTranscript({ text: 'open SHIP-1' }));
    await model.parse(inbox, aTranscript({ text: 'open SHIP-2' }));

    expect(prefixOnly(context)).toHaveLength(2);
    const [inboxSession, backlogSession] = context.saveSession.mock.calls.map(([path]) => path);
    expect(inboxSession).not.toEqual(backlogSession);
    expect(inboxSession.startsWith('file://')).toBe(false);
    expect(context.loadSession.mock.calls).toEqual([[inboxSession]]);
  });

  it('completion is called with the grammar, enable_thinking false and temperature 0', async () => {
    const { model, context } = await loadedModel();
    const spec = aSpec('workItem');
    mockLlama.output = '[{"action":"addNote","args":{},"noteSpan":{"start":9,"end":14}}]';

    const prepared = await model.prepare(spec);
    const calls = await model.parse(prepared, aTranscript({ text: 'add note hello' }));

    const [params] = context.completion.mock.calls[context.completion.mock.calls.length - 1];
    expect(params.grammar).toBe(buildGrammar(spec));
    expect(params.enable_thinking).toBe(false);
    expect(params.temperature).toBe(0);
    expect(params.jinja).toBe(true);
    expect(params.add_generation_prompt).toBe(true);
    expect(params.messages[1].content.endsWith('add note hello')).toBe(true);
    expect(calls).toEqual([{ action: 'addNote', args: {}, noteSpan: { start: 9, end: 14 } }]);
  });

  it('reads the screen context at each parse: a change between two parses is seen without preparing again', async () => {
    const { model, context } = await loadedModel();
    let focus = 'SHIP-1';
    const prepared = await model.prepare(aSpec('workItem', () => ({ focus })));

    await model.parse(prepared, aTranscript({ text: 'open it' }));
    focus = 'SHIP-2';
    await model.parse(prepared, aTranscript({ text: 'open it' }));

    const users = context.completion.mock.calls
      .filter(([params]) => params.n_predict !== 0)
      .map(([params]) => params.messages[1].content);
    expect(users[0]).toContain('"focus":"SHIP-1"');
    expect(users[1]).toContain('"focus":"SHIP-2"');
    expect(prefixOnly(context)).toHaveLength(1);
    expect(prefixOnly(context)[0][0].messages[0].content).not.toContain('SHIP-');
  });

  it('reads the context from the spec passed to parse, when a screen rebuilt it after prepare', async () => {
    const { model, context } = await loadedModel();
    const prepared = await model.prepare(aSpec('workItem', () => ({ focus: 'SHIP-1' })));

    await model.parse(
      prepared,
      aTranscript({ text: 'open it' }),
      aSpec('workItem', () => ({ focus: 'SHIP-7' })),
    );

    const users = context.completion.mock.calls
      .filter(([params]) => params.n_predict !== 0)
      .map(([params]) => params.messages[1].content);
    expect(users[0]).toContain('"focus":"SHIP-7"');
    expect(prefixOnly(context)).toHaveLength(1);
  });

  it('a model that fails the memory check is not loaded and reports memory', async () => {
    // An 8 GB phone (7.6 GB reported) that chose 4B: ~3.3 GB for the app, and 4B needs ~3.6.
    const models = new FakeModelStore();
    models.set({ resolved: 'qwen3-4b' });
    const files = new FakeModelFiles();
    files.place(QWEN_4);
    files.place(QWEN_17);

    const model = new LlamaCommandModel({ files, models });

    await waitFor(() =>
      expect(models.calls).toEqual([{ method: 'reportLoadFailure', reason: 'memory' }]),
    );
    expect(initLlama).not.toHaveBeenCalled();
    expect(model.ready()).toBe(false);
    await expect(model.prepare(aSpec('workItem'))).rejects.toThrow('not loaded');

    // Switching to 1.7B (the sheet's offer) loads it on the same phone: the check is about memory.
    models.set({ resolved: 'qwen3-1.7b' });
    await waitFor(() => expect(model.ready()).toBe(true));
    expect(initLlama).toHaveBeenCalledTimes(1);
    expect(jest.mocked(initLlama).mock.calls[0][0]).toMatchObject({
      model: files.path(QWEN_17),
      n_ctx: 4096,
    });
    expect(models.calls).toHaveLength(1);
  });

  it('loads nothing until the model file is checked, and releases the model when the models are deleted', async () => {
    const models = new FakeModelStore();
    models.set({ files: [{ id: QWEN_17, state: 'downloading', bytes: 10 }] });
    const files = new FakeModelFiles();
    files.place(QWEN_17);

    const model = new LlamaCommandModel({ files, models });
    models.set({ phase: 'ready' });
    await model.prepare(aSpec('workItem')).catch(() => undefined);
    expect(initLlama).not.toHaveBeenCalled();

    models.set({ files: [{ id: QWEN_17, state: 'checked', bytes: 1_100_000_000 }] });
    await waitFor(() => expect(model.ready()).toBe(true));

    models.set({ phase: 'none', resolved: null });
    await waitFor(() => expect(model.ready()).toBe(false));
    expect(mockLlama.contexts[0].release).toHaveBeenCalledTimes(1);
  });

  it('without its store and files it loads nothing and is not ready', async () => {
    const model = new LlamaCommandModel();

    expect(model.ready()).toBe(false);
    await expect(model.prepare(aSpec('workItem'))).rejects.toThrow('not loaded');
    expect(initLlama).not.toHaveBeenCalled();
  });
});
