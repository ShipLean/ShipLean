// The model files on the phone: resumable downloads (expo-file-system), SHA-256, free space and iCloud exclusion
// (the local modules/model-files module).
import type { DownloadTask, ModelFile, ModelFiles } from './ports';

export class ExpoModelFiles implements ModelFiles {
  freeBytes(): Promise<number> {
    // TODO: free space on the app's storage volume.
    return Promise.reject(new Error('TODO: free space'));
  }

  bytesOnDisk(id: string): Promise<number> {
    void id;
    // TODO: the size of a model file, partial or whole.
    return Promise.reject(new Error('TODO: bytes on disk'));
  }

  sha256(id: string): Promise<string> {
    void id;
    // TODO: hash the file natively; expo-file-system can't.
    return Promise.reject(new Error('TODO: sha256 of a model file'));
  }

  remove(id: string): Promise<void> {
    void id;
    // TODO: delete a model file and its partial download.
    return Promise.reject(new Error('TODO: remove a model file'));
  }

  path(id: string): string {
    void id;
    // TODO: the model directory under the app's documents, excluded from backup.
    throw new Error('TODO: the path of a model file');
  }

  download(
    file: ModelFile,
    opts: { onProgress(bytes: number): void; cellular: boolean },
  ): DownloadTask {
    void file;
    void opts;
    // TODO: a resumable download that carries on in the background and resumes after a dropped connection.
    throw new Error('TODO: download a model file');
  }
}
