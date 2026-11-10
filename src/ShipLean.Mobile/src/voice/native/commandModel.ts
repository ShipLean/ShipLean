// Qwen3 (Q4_K_M GGUF) through llama.rn, constrained by the screen's grammar (buildGrammar). The model is loaded once
// the store says it is ready and the memory check passes; a model that doesn't fit isn't loaded and the store hears
// reportLoadFailure('memory'). Each screen's prefix (its system prompt: instructions and actions) is evaluated once and
// saved with llama.rn's saveSession under the cache directory; a parse on the screen whose prefix is in the KV cache
// reuses it (llama.rn keeps the common prefix of the cached tokens), and a parse on another prepared screen loads that
// screen's session first. The screen's context is read at each parse and goes in the user turn, after the prefix.
import { File, Paths } from 'expo-file-system';
import { initLlama, type LlamaContext } from 'llama.rn';
import { Platform } from 'react-native';
import { buildGrammar, buildSystemPrompt, parseModelOutput, userTurn } from '../commandPrompt';
import type {
  CommandModel,
  CommandModelId,
  ModelStore,
  ParsedCall,
  PreparedScreen,
  ScreenVoiceSpec,
  Transcript,
} from '../contracts';
import { MODEL_FILES } from '../manifest';
import { availableBytes, memoryCheck, N_CTX } from '../model/memoryCheck';
import type { ModelFiles } from './ports';

export interface CommandModelDeps {
  files: ModelFiles;
  models: ModelStore;
}

// The most tokens a command's calls take; a dozen calls fit.
const N_PREDICT = 512;

// Thinking off: enable_thinking false for the chat template (the prefix also ends with /no_think).
const CHAT = { jinja: true, enable_thinking: false } as const;

interface Prefix {
  // What the prefix bakes in: the system prompt and the grammar. A spec with the same key reuses the evaluated prefix.
  readonly key: string;
  readonly system: string;
  readonly grammar: string;
  readonly session: string;
}

class PreparedPrompt implements PreparedScreen {
  constructor(
    readonly screen: string,
    readonly prefix: Prefix,
    // Read at parse time for its context.
    readonly spec: ScreenVoiceSpec,
  ) {}
}

// One session file per model and screen, under the cache directory. llama.rn's saveSession takes a plain path.
function sessionPath(model: CommandModelId, screen: string): string {
  const name = `command-prefix-${model}-${screen.replace(/[^A-Za-z0-9_-]/g, '_')}.session`;
  return new File(Paths.cache, name).uri.replace(/^file:\/\//, '');
}

export class LlamaCommandModel implements CommandModel {
  private context: LlamaContext | null = null;
  private loaded: CommandModelId | null = null;
  // The model that failed the memory check, so the store's next change doesn't check it again.
  private refused: CommandModelId | null = null;
  // The evaluated prefix of each screen of the loaded model, and the one at the start of the KV cache.
  private readonly prefixes = new Map<string, Prefix>();
  private live: Prefix | null = null;
  // llama.rn runs one completion at a time on a context: loads, prepares and parses run in order.
  private queue: Promise<unknown> = Promise.resolve();

  // Without deps (nativeAdapters() before VW wires it) nothing is loaded and ready() stays false.
  constructor(deps?: CommandModelDeps) {
    if (deps === undefined) return;
    deps.models.subscribe(() => this.syncWith(deps));
    this.syncWith(deps);
  }

  ready(): boolean {
    return this.context !== null;
  }

  prepare(spec: ScreenVoiceSpec): Promise<PreparedScreen> {
    return this.run(async () => {
      const { context, model } = this.loadedModel();
      const system = buildSystemPrompt(spec);
      const grammar = buildGrammar(spec);
      const key = `${system}\n${grammar}`;
      let prefix = this.prefixes.get(spec.screen);
      if (prefix === undefined || prefix.key !== key) {
        prefix = { key, system, grammar, session: sessionPath(model, spec.screen) };
        // Evaluate the prefix alone into the KV cache, and keep it for when another screen has been used since.
        await context.completion({
          ...CHAT,
          messages: [{ role: 'system', content: system }],
          add_generation_prompt: false,
          n_predict: 0,
        });
        await context.saveSession(prefix.session);
        this.prefixes.set(spec.screen, prefix);
        this.live = prefix;
      }
      return new PreparedPrompt(spec.screen, prefix, spec);
    });
  }

  parse(p: PreparedScreen, t: Transcript, spec?: ScreenVoiceSpec): Promise<ParsedCall[]> {
    return this.run(async () => {
      if (!(p instanceof PreparedPrompt))
        throw new Error('This screen was prepared by another command model.');
      const { context } = this.loadedModel();
      const { prefix } = p;
      // Another screen's prefix is in the cache: load this one's. A prefix since replaced for its screen (or from
      // before a model change) has no session any more, and is evaluated with the command.
      if (this.live !== prefix && this.prefixes.get(p.screen) === prefix)
        await context.loadSession(prefix.session);
      this.live = prefix;
      const result = await context.completion({
        ...CHAT,
        messages: [
          { role: 'system', content: prefix.system },
          { role: 'user', content: userTurn(t, (spec ?? p.spec).context()) },
        ],
        add_generation_prompt: true,
        grammar: prefix.grammar,
        temperature: 0,
        n_predict: N_PREDICT,
      });
      return parseModelOutput(result.text);
    });
  }

  private loadedModel(): { context: LlamaContext; model: CommandModelId } {
    if (this.context === null || this.loaded === null)
      throw new Error('The command model is not loaded.');
    return { context: this.context, model: this.loaded };
  }

  private run<T>(step: () => Promise<T>): Promise<T> {
    const next = this.queue.then(step);
    this.queue = next.catch(() => undefined);
    return next;
  }

  // A load llama.rn rejects (a file it can't read) leaves the model unloaded, so ready() stays false; the store's next
  // change tries again. It isn't reported as memory: the memory check is what decides that.
  private syncWith(deps: CommandModelDeps): void {
    this.run(() => this.sync(deps)).catch(() => undefined);
  }

  // Load the store's resolved model once its file is checked, release it when the models are gone.
  private async sync({ files, models }: CommandModelDeps): Promise<void> {
    const status = models.status();
    if (status.phase === 'none' || status.phase === 'skipped') {
      await this.unload();
      return;
    }
    const want = status.resolved;
    if (status.phase !== 'ready' || want === null || want === this.loaded || want === this.refused)
      return;
    const file = MODEL_FILES.find((f) => f.model === want);
    if (file === undefined) return;
    const listed = status.files.find((f) => f.id === file.id);
    if (listed !== undefined && listed.state !== 'checked') return;

    await this.unload();
    this.refused = null;
    const check = memoryCheck(
      want,
      await files.bytesOnDisk(file.id),
      availableBytes(models.deviceMemory().reportedGb),
    );
    if (!check.fits) {
      this.refused = want;
      models.reportLoadFailure('memory');
      return;
    }
    this.context = await initLlama({
      model: files.path(file.id),
      n_ctx: N_CTX,
      use_mmap: true,
      use_mlock: false,
      // Metal on iOS; Q4_K_M runs on the CPU on Android (llama.rn's OpenCL path takes Q4_0 and Q6_K only).
      n_gpu_layers: Platform.OS === 'ios' ? 99 : 0,
    });
    this.loaded = want;
  }

  private async unload(): Promise<void> {
    const context = this.context;
    this.context = null;
    this.loaded = null;
    this.prefixes.clear();
    this.live = null;
    if (context !== null) await context.release();
  }
}
