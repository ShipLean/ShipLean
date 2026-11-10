// N9's ModelStore over the injected download ports (C9). It picks the command model by the phone's memory, downloads
// Parakeet then the command model one file at a time, checks each file's SHA-256, waits for Wi-Fi unless mobile data
// was allowed for this download, stops with what it has when space runs out or the connection drops, and carries on
// after a restart. A model change downloads and checks the new model before the old one is removed (D11).
import type { KeyValueStore } from '@shiplean/core/platform';
import type { CommandModelId, ModelChoice, ModelStatus, ModelStore } from '../voice/contracts';
import { chooseModel, soldGb, type ModelFile } from '../voice/manifest';
import type { DownloadTask, ModelFiles, NetworkKind } from '../voice/native/ports';
import {
  loadModelSettings,
  saveModelSettings,
  type FileState,
  type ModelSettings,
} from './modelSettings';

export interface ModelStoreDeps {
  files: ModelFiles;
  network: NetworkKind;
  // Per phone, not per account.
  kv: KeyValueStore;
  // The phone's memory in bytes as it reports it (0 when it doesn't say).
  memoryBytes: number;
  manifest: readonly ModelFile[];
  clock: () => Date;
}

// N9 has no way to change Wi-Fi only, so the store the app builds adds one for the switch on the model screen and in
// Settings › Voice.
export interface LocalModelStore extends ModelStore {
  setWifiOnly(on: boolean): void;
}

type PauseReason = 'wifi' | 'space' | 'network' | 'user';

interface HeldTask {
  fileId: string;
  task: DownloadTask;
  settled: boolean;
}

const GIB = 1024 ** 3;

class LocalStore implements LocalModelStore {
  private readonly s: ModelSettings;
  private readonly listeners = new Set<() => void>();
  private readonly fileBytes = new Map<string, number>();
  // Files whose checksum failed once in this download: the second mismatch fails it.
  private readonly retried = new Set<string>();
  private readonly reportedGb: number;
  private run: 'idle' | 'downloading' | 'checking' = 'idle';
  private pausedFor: PauseReason | null = null;
  private failure: 'checksum' | 'memory' | null = null;
  private freeBytes = 0;
  private held: HeldTask | null = null;
  // Each start bumps it; a loop whose generation is gone stops at its next await.
  private gen = 0;
  private snapshot: ModelStatus;

  constructor(private readonly deps: ModelStoreDeps) {
    this.s = loadModelSettings(deps.kv);
    this.reportedGb = Math.round((deps.memoryBytes / GIB) * 10) / 10;
    if (this.targetFiles().some((f) => this.s.files[f.id] === 'failed')) this.failure = 'checksum';
    if (this.s.download?.paused === true) this.pausedFor = 'user';
    this.snapshot = this.compute();
    deps.network.subscribe(() => this.onNetwork());
    void this.refreshFree();
    if (this.s.download !== null && !this.s.download.paused && !this.s.skipped) this.start();
  }

  status(): ModelStatus {
    return this.snapshot;
  }

  subscribe(cb: () => void): () => void {
    this.listeners.add(cb);
    return () => {
      this.listeners.delete(cb);
    };
  }

  deviceMemory(): { reportedGb: number; soldGb: number } {
    return { reportedGb: this.reportedGb, soldGb: soldGb(this.reportedGb) };
  }

  choose(c: ModelChoice): void {
    if (c === 'qwen3-4b' && chooseModel(this.reportedGb).fourB === 'blocked') return;
    const before = this.target();
    let wanted = this.downloadWanted();
    this.s.choice = c;
    if (this.failure === 'memory') {
      // The model in use didn't fit: voice stays off until the new choice is downloaded and checked (P-S3).
      this.failure = null;
      this.s.inUse = null;
      wanted = true;
    }
    this.save();
    if (this.target() === before) {
      this.notify();
      return;
    }
    this.stopTask();
    this.held = null;
    this.retried.clear();
    if (this.failure === 'checksum') this.failure = null;
    if (this.pausedFor !== 'user') this.pausedFor = null;
    this.run = 'idle';
    if (wanted && this.s.download === null) this.s.download = this.newDownload();
    this.save();
    this.notify();
    void this.removePartials().then(() => {
      if (wanted && this.pausedFor !== 'user') this.start();
    });
  }

  download(): void {
    if (this.target() === null || this.failure === 'memory') return;
    if (this.failure === 'checksum') {
      this.failure = null;
      this.retried.clear();
      for (const f of this.targetFiles()) {
        if (this.s.files[f.id] === 'failed') this.s.files[f.id] = 'pending';
      }
    }
    this.s.skipped = false;
    this.s.download = { ...(this.s.download ?? this.newDownload()), paused: false };
    if (this.pausedFor === 'user') this.pausedFor = null;
    this.save();
    if (this.run === 'idle') this.start();
    else this.notify();
  }

  pause(): void {
    if (this.s.download === null) return;
    this.stopTask();
    this.s.download = { ...this.s.download, paused: true };
    this.pauseFor('user');
  }

  useMobileDataOnce(): void {
    if (this.s.download === null) return;
    this.s.download = { ...this.s.download, mobileDataOnce: true };
    this.save();
    if (this.pausedFor === 'wifi') this.start();
    else this.notify();
  }

  setWifiOnly(on: boolean): void {
    this.s.wifiOnly = on;
    this.save();
    this.notify();
    this.onNetwork();
  }

  skip(): void {
    this.stopTask();
    this.s.skipped = true;
    this.s.download = null;
    this.pausedFor = null;
    this.run = 'idle';
    this.resetDownloadingFiles();
    this.save();
    this.notify();
  }

  async deleteAll(): Promise<void> {
    this.stopTask();
    this.held = null;
    this.run = 'idle';
    this.pausedFor = null;
    this.failure = null;
    this.retried.clear();
    for (const f of this.deps.manifest) await this.removeIfThere(f);
    this.fileBytes.clear();
    this.s.files = {};
    this.s.inUse = null;
    this.s.download = null;
    this.s.skipped = false;
    this.save();
    await this.refreshFree();
  }

  reportLoadFailure(reason: 'memory'): void {
    this.failure = reason;
    this.notify();
  }

  // The command model the choice resolves to on this phone; null when voice can't be set up here (D6).
  private target(): CommandModelId | null {
    const advice = chooseModel(this.reportedGb);
    if (advice.auto === null) return null;
    return this.s.choice === 'auto' ? advice.auto : this.s.choice;
  }

  // Parakeet first, then the command model.
  private targetFiles(): ModelFile[] {
    const target = this.target();
    if (target === null) return [];
    const files = this.deps.manifest;
    return [
      ...files.filter((f) => f.model === 'parakeet'),
      ...files.filter((f) => f.model === target),
    ];
  }

  // What this download fetches: during a model change, only the files the model in use doesn't share.
  private planFiles(): ModelFile[] {
    const inUse = this.s.inUse;
    if (inUse === null || inUse === this.target()) return this.targetFiles();
    const shared = new Set(
      this.deps.manifest
        .filter((f) => f.model === 'parakeet' || f.model === inUse)
        .map((f) => f.id),
    );
    return this.targetFiles().filter((f) => !shared.has(f.id));
  }

  private downloadWanted(): boolean {
    if (this.s.skipped) return false;
    return this.s.download !== null || this.s.inUse !== null || this.failure === 'checksum';
  }

  private newDownload(): NonNullable<ModelSettings['download']> {
    return { startedAt: this.deps.clock().getTime(), mobileDataOnce: false, paused: false };
  }

  private start(): void {
    this.pausedFor = null;
    const gen = ++this.gen;
    this.notify();
    void this.loop(gen);
  }

  private stopTask(): void {
    this.gen += 1;
    const held = this.held;
    if (held !== null && !held.settled) held.task.pause();
  }

  private async loop(gen: number): Promise<void> {
    const live = () => gen === this.gen;
    for (const file of this.targetFiles()) {
      if (this.s.files[file.id] === 'checked') continue;
      for (;;) {
        const net = this.deps.network.kind();
        if (net === 'none') return this.pauseFor('network');
        if (net === 'cellular' && !this.mobileDataAllowed()) return this.pauseFor('wifi');
        const onDisk = await this.deps.files.bytesOnDisk(file.id);
        if (!live()) return;
        this.freeBytes = await this.deps.files.freeBytes();
        if (!live()) return;
        if (this.freeBytes < file.bytes - onDisk) return this.pauseFor('space');

        this.fileBytes.set(file.id, onDisk);
        this.setFile(file.id, 'downloading');
        this.run = 'downloading';
        this.save();
        this.notify();
        const outcome = await this.taskFor(file, net === 'cellular', live).done;
        if (!live()) return;
        if (outcome === 'paused') return this.pauseFor('network');
        if (typeof outcome === 'object') return this.pauseFor(outcome.error);

        this.run = 'checking';
        this.notify();
        const sum = await this.deps.files.sha256(file.id);
        if (!live()) return;
        if (sum === file.sha256) {
          this.setFile(file.id, 'checked');
          this.save();
          break;
        }
        await this.deps.files.remove(file.id);
        this.fileBytes.set(file.id, 0);
        if (!live()) return;
        if (this.retried.has(file.id)) return this.failChecksum(file.id);
        // One more download of the same file.
        this.retried.add(file.id);
        this.setFile(file.id, 'pending');
      }
    }
    await this.finish(gen);
  }

  private taskFor(file: ModelFile, cellular: boolean, live: () => boolean): DownloadTask {
    const held = this.held;
    if (held !== null && held.fileId === file.id && !held.settled) {
      held.task.resume();
      return held.task;
    }
    const task = this.deps.files.download(file, {
      cellular,
      onProgress: (bytes) => {
        if (!live()) return;
        this.fileBytes.set(file.id, bytes);
        this.notify();
      },
    });
    const next: HeldTask = { fileId: file.id, task, settled: false };
    this.held = next;
    void task.done.then(() => {
      next.settled = true;
    });
    return task;
  }

  // Every file is checked: voice switches to the new model, then the files nothing needs any more are removed.
  private async finish(gen: number): Promise<void> {
    this.s.inUse = this.target();
    this.s.download = null;
    this.run = 'idle';
    this.held = null;
    this.retried.clear();
    this.save();
    this.notify();
    const keep = new Set(this.targetFiles().map((f) => f.id));
    for (const f of this.deps.manifest) {
      if (gen !== this.gen) return;
      if (!keep.has(f.id)) await this.removeIfThere(f);
    }
    this.save();
    await this.refreshFree();
  }

  // Partial downloads of a model no longer chosen go first, so the new choice has the space.
  private async removePartials(): Promise<void> {
    const keep = new Set(this.targetFiles().map((f) => f.id));
    for (const f of this.deps.manifest) {
      if (keep.has(f.id) || this.s.files[f.id] === 'checked') continue;
      await this.removeIfThere(f);
    }
    this.save();
    await this.refreshFree();
  }

  private async removeIfThere(f: ModelFile): Promise<void> {
    const known = this.s.files[f.id] !== undefined;
    if (!known && (await this.deps.files.bytesOnDisk(f.id)) === 0) return;
    await this.deps.files.remove(f.id);
    delete this.s.files[f.id];
    this.fileBytes.delete(f.id);
  }

  private mobileDataAllowed(): boolean {
    return !this.s.wifiOnly || this.s.download?.mobileDataOnce === true;
  }

  private onNetwork(): void {
    const net = this.deps.network.kind();
    if (this.run === 'downloading' && net === 'cellular' && !this.mobileDataAllowed()) {
      this.stopTask();
      this.pauseFor('wifi');
      return;
    }
    if ((this.pausedFor === 'wifi' || this.pausedFor === 'network') && net !== 'none') this.start();
  }

  private pauseFor(reason: PauseReason): void {
    this.run = 'idle';
    this.pausedFor = reason;
    this.resetDownloadingFiles();
    this.save();
    this.notify();
  }

  private failChecksum(fileId: string): void {
    this.setFile(fileId, 'failed');
    this.failure = 'checksum';
    this.run = 'idle';
    this.held = null;
    this.s.download = null;
    this.save();
    this.notify();
  }

  private resetDownloadingFiles(): void {
    for (const [id, state] of Object.entries(this.s.files)) {
      if (state === 'downloading') this.s.files[id] = 'pending';
    }
  }

  private setFile(id: string, state: FileState): void {
    this.s.files[id] = state;
  }

  private async refreshFree(): Promise<void> {
    this.freeBytes = await this.deps.files.freeBytes();
    this.notify();
  }

  private save(): void {
    saveModelSettings(this.deps.kv, this.s);
  }

  private notify(): void {
    this.snapshot = this.compute();
    for (const cb of [...this.listeners]) cb();
  }

  private compute(): ModelStatus {
    const target = this.target();
    const rows = this.targetFiles().map((f) => {
      const state = this.s.files[f.id] ?? 'pending';
      return {
        id: f.id,
        state,
        bytes: state === 'checked' ? f.bytes : (this.fileBytes.get(f.id) ?? 0),
      };
    });
    const plan = new Set(this.planFiles().map((f) => f.id));
    const planned = rows.filter((r) => plan.has(r.id));
    const [phase, reason] = this.phase();
    return {
      phase,
      choice: this.s.choice,
      resolved: this.s.inUse ?? target,
      bytesDone: planned.reduce((sum, r) => sum + r.bytes, 0),
      bytesTotal: this.planFiles().reduce((sum, f) => sum + f.bytes, 0),
      freeBytes: this.freeBytes,
      wifiOnly: this.s.wifiOnly,
      reason,
      files: rows,
    };
  }

  private phase(): [ModelStatus['phase'], ModelStatus['reason']] {
    if (this.failure === 'memory') return ['failed', 'memory'];
    const paused = this.pausedFor === null || this.pausedFor === 'user' ? null : this.pausedFor;
    // A model change runs behind a working voice: the phase stays ready, and the reason says why the change waits.
    if (this.s.inUse !== null) return ['ready', this.failure ?? paused];
    if (this.s.skipped) return ['skipped', null];
    if (this.failure === 'checksum') return ['failed', 'checksum'];
    if (this.pausedFor !== null) return ['paused', paused];
    if (this.run === 'checking') return ['checking', null];
    if (this.run === 'downloading' || this.s.download !== null) return ['downloading', null];
    return ['none', null];
  }
}

export function createModelStore(deps: ModelStoreDeps): LocalModelStore {
  return new LocalStore(deps);
}
