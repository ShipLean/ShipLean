// The model files on the phone: resumable downloads (expo-file-system's download task, a background session on
// iPhone), SHA-256 (expo-file-system's streamed native digest), free space and iCloud exclusion (the local
// modules/model-files module). Wi-Fi rules, retries and order are the model store's; this only moves bytes.
//
// Layout: <models root>/<entry.model>/<entry.fileName>, one directory per model, so sherpa-onnx finds a model's files
// by name. A download writes to <models root>/<entry.model>/<entry.id>.part and is renamed only when complete, so a
// scan never sees a half file.
import NetInfo from '@react-native-community/netinfo';
import {
  Directory,
  File,
  Paths,
  DownloadTask as FileDownloadTask,
  type DownloadPauseState,
  type DownloadTaskOptions,
} from 'expo-file-system';
import Storage from 'expo-sqlite/kv-store';
import { Platform } from 'react-native';
import { excludeFromBackup } from '../../../modules/model-files';
import { MODEL_FILES } from '../manifest';
import { kindOf } from './network';
import type { DownloadTask, ModelFile, ModelFiles } from './ports';

type Outcome = Awaited<DownloadTask['done']>;

// TODO: read entry.fileName straight from ModelFile once MODEL_FILES carries it (the pinned manifest).
type Entry = ModelFile & { fileName?: string };

// The paused download of a file, kept per phone so a restart carries on from it.
interface Saved {
  state: DownloadPauseState;
  bytes: number;
}

const savedKey = (id: string) => `shiplean.voice.resume.${id}`;

// "No space" in the platforms' words (ENOSPC on Android, the Cocoa write errors on iOS); every other failure is the
// network's. The raw message is never shown.
const NO_SPACE =
  /no space|enospc|out of space|not enough space|disk full|insufficient storage|cannot (write to|create) file/i;

function failure(error: unknown): Outcome {
  const message = error instanceof Error ? error.message : String(error);
  return { error: NO_SPACE.test(message) ? 'space' : 'network' };
}

// A file:// URI as the path native libraries open (sherpa-onnx, llama.cpp).
function nativePath(uri: string): string {
  return decodeURI(uri.replace(/^file:\/\//, ''));
}

// Where the models live: storage the OS never clears. On iPhone that is Library/Application Support (not Caches, and
// not Documents, which the person can see), excluded from backup. On Android it is the app's files directory; the
// app's backup is off.
function modelsRoot(): Directory {
  return Platform.OS === 'ios'
    ? new Directory(Paths.document, '..', 'Library', 'Application Support', 'models')
    : new Directory(Paths.document, 'models');
}

function entryOf(id: string): Entry {
  const entry = MODEL_FILES.find((f) => f.id === id);
  if (entry === undefined) throw new Error(`No model file ${id}`);
  return entry;
}

function fileNameOf(entry: Entry): string {
  if (!entry.fileName) throw new Error(`Model file ${entry.id} has no file name`);
  return entry.fileName;
}

function readSaved(id: string): Saved | null {
  const raw = Storage.getItemSync(savedKey(id));
  return raw === null ? null : (JSON.parse(raw) as Saved);
}

function clearSaved(id: string): void {
  Storage.removeItemSync(savedKey(id));
}

function modelDir(entry: Entry): Directory {
  return new Directory(modelsRoot(), entry.model);
}

function finalFile(entry: Entry): File {
  return new File(modelDir(entry), fileNameOf(entry));
}

function partFile(entry: Entry): File {
  return new File(modelDir(entry), `${entry.id}.part`);
}

// Creates the models root (excluded from backup with everything in it) and the model's directory on first use.
async function ensureDirs(entry: Entry): Promise<void> {
  const root = modelsRoot();
  if (!root.exists) {
    root.create({ intermediates: true, idempotent: true });
    await excludeFromBackup(root.uri);
  }
  const dir = modelDir(entry);
  if (!dir.exists) dir.create({ intermediates: true, idempotent: true });
}

export class ExpoModelFiles implements ModelFiles {
  freeBytes(): Promise<number> {
    return Promise.resolve(Paths.availableDiskSpace);
  }

  // A whole file's size, else the partial download's: the part file, or the bytes an iPhone's paused session holds.
  bytesOnDisk(id: string): Promise<number> {
    const entry = entryOf(id);
    const whole = finalFile(entry);
    if (whole.exists) return Promise.resolve(whole.size);
    const part = partFile(entry);
    const partBytes = part.exists ? part.size : 0;
    return Promise.resolve(Math.max(partBytes, readSaved(id)?.bytes ?? 0));
  }

  sha256(id: string): Promise<string> {
    return finalFile(entryOf(id)).digest('SHA-256');
  }

  remove(id: string): Promise<void> {
    const entry = entryOf(id);
    for (const file of [finalFile(entry), partFile(entry)]) {
      if (file.exists) file.delete();
    }
    clearSaved(id);
    return Promise.resolve();
  }

  path(id: string): string {
    return nativePath(finalFile(entryOf(id)).uri);
  }

  download(
    file: ModelFile,
    opts: { onProgress(bytes: number): void; cellular: boolean },
  ): DownloadTask {
    return new ModelDownload(file, opts);
  }
}

// One file's download. done is the outcome of the current run: a pause settles it 'paused', and resume() carries the
// same task on with a new run.
class ModelDownload implements DownloadTask {
  private task: FileDownloadTask | null = null;
  private run: Promise<Outcome>;
  private running = false;
  private finished = false;
  private pauseWanted = false;
  private bytes = 0;

  constructor(
    private readonly entry: Entry,
    private readonly opts: { onProgress(bytes: number): void; cellular: boolean },
  ) {
    this.run = this.go(() => this.start());
  }

  get done(): Promise<Outcome> {
    return this.run;
  }

  pause(): void {
    this.pauseWanted = true;
    if (this.task?.state === 'active') this.task.pause();
  }

  resume(): void {
    if (this.running || this.finished) return;
    this.pauseWanted = false;
    const task = this.task;
    this.run = this.go(() => (task === null ? this.start() : task.resumeAsync()));
  }

  private async go(transfer: () => Promise<File | null>): Promise<Outcome> {
    this.running = true;
    try {
      // On mobile data with mobile data not allowed: stay paused rather than start.
      if (!this.opts.cellular && kindOf(await NetInfo.fetch()) === 'cellular') return 'paused';
      if (this.pauseWanted) return 'paused';
      const result = await transfer();
      if (result === null) {
        this.save();
        return 'paused';
      }
      await this.complete();
      return 'complete';
    } catch (error) {
      // A restored task that fails leaves nothing to resume from: the next download starts again (see start()).
      clearSaved(this.entry.id);
      this.finished = true;
      return failure(error);
    } finally {
      this.running = false;
    }
  }

  private options(): DownloadTaskOptions {
    return {
      // iOS: the background session (expo-file-system's default) carries on while the app is suspended; completion
      // is acknowledged once the file is in place.
      sessionType: 'background',
      deferBackgroundSessionCompletion: true,
      onProgress: ({ bytesWritten }) => {
        this.bytes = bytesWritten;
        this.opts.onProgress(bytesWritten);
      },
    };
  }

  // A new task, or the saved one after a restart. Without a saved pause, Android carries on from the part file with
  // a Range request (its resume data is the byte offset); iOS starts again from zero.
  private async start(): Promise<File | null> {
    await ensureDirs(this.entry);
    if (this.pauseWanted) return null;
    const part = partFile(this.entry);
    if (part.exists && this.entry.bytes > 0 && part.size === this.entry.bytes) return part;
    const saved = readSaved(this.entry.id);
    const state =
      saved?.state ??
      (Platform.OS === 'android' && part.exists && part.size > 0
        ? {
            url: this.entry.url,
            fileUri: part.uri,
            isDirectory: false,
            resumeData: String(part.size),
          }
        : null);
    if (state !== null) {
      try {
        this.task = FileDownloadTask.fromSavable(state, this.options());
        this.bytes = saved?.bytes ?? part.size;
        return await this.task.resumeAsync();
      } catch (error) {
        // Saved state that can't be rebuilt: start again below. A failed transfer is the network's.
        if (this.task !== null) throw error;
        clearSaved(this.entry.id);
      }
    }
    if (part.exists) part.delete();
    this.task = File.createDownloadTask(this.entry.url, part, this.options());
    return this.task.downloadAsync();
  }

  private save(): void {
    if (this.task === null) return;
    const saved: Saved = { state: this.task.savable(), bytes: this.bytes };
    Storage.setItemSync(savedKey(this.entry.id), JSON.stringify(saved));
  }

  // The part file becomes the model file, kept out of backup.
  private async complete(): Promise<void> {
    try {
      const whole = finalFile(this.entry);
      if (whole.exists) whole.delete();
      partFile(this.entry).rename(fileNameOf(this.entry));
      await excludeFromBackup(whole.uri);
      clearSaved(this.entry.id);
      this.finished = true;
    } finally {
      await this.task?.acknowledgeBackgroundCompletionAsync();
    }
  }
}
