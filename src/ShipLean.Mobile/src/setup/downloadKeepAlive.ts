// Holds the download's keep-alive (Android's foreground service) up while the voice models download, so leaving the
// app doesn't freeze the download: while a file downloads or is checked, and while a dropped connection waits for the
// store's retry. Anything else (done, paused by the person, waiting for Wi-Fi or space, failed) stops it.
import type { ModelStatus, ModelStore } from '../voice/contracts';
import type { DownloadKeepAlive } from '../voice/native/ports';

function wanted(s: ModelStatus): boolean {
  return (
    s.phase === 'downloading' ||
    s.phase === 'checking' ||
    (s.phase === 'paused' && s.reason === 'network')
  );
}

function percentOf(s: ModelStatus): number {
  return s.bytesTotal > 0 ? Math.floor((s.bytesDone / s.bytesTotal) * 100) : -1;
}

// Follows the store from now on; returns the unsubscribe. The keep-alive hears only whole-percent changes, so the
// notification updates at most about a hundred times a download.
export function keepDownloadAlive(models: ModelStore, keepAlive: DownloadKeepAlive): () => void {
  let on = false;
  // The percentage last sent, null when nothing was sent since the last stop.
  let sent: number | null = null;
  const sync = () => {
    const status = models.status();
    if (!wanted(status)) {
      if (on) keepAlive.stop();
      on = false;
      sent = null;
      return;
    }
    const percent = percentOf(status);
    if (percent === sent) return;
    sent = percent;
    // Refused (the app is in the background): the next percentage tries again.
    on = keepAlive.start(percent) || on;
  };
  sync();
  return models.subscribe(sync);
}
