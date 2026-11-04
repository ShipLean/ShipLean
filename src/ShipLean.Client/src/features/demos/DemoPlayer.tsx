import { useCallback, useEffect, useState } from 'react';
import { Chip } from '../../components/Chip';
import { formatSize } from '../workItem/side/fileSize';
import type { DemoProps } from './DemoProps';
import { bindEmbed, bindVideo, reportTime } from './playerHandle';
import { demosTestIds as ids } from './testIds';

// The YouTube embed plays only when it is published and not private, so the server sets EmbedUrl for exactly that.
// A chapter click sets the start and autoplay on the embed.
function embedSource(embedUrl: string, start: number | null): string {
  if (start === null) return embedUrl;
  const url = new URL(embedUrl);
  url.searchParams.set('start', String(start));
  url.searchParams.set('autoplay', '1');
  return url.toString();
}

/**
 * The 16:9 frame: the YouTube embed when there is one, else the file ShipLean kept. Under it, the watch link, the note
 * for a private video and the file's facts. Nothing when the demo has no video.
 */
export function DemoPlayer({ demo, player }: DemoProps) {
  const { video, youTube } = demo;
  const embedUrl = youTube.embedUrl;
  const [start, setStart] = useState<number | null>(null);
  const bind = useCallback(
    (element: HTMLVideoElement | null) => bindVideo(player, element),
    [player],
  );

  useEffect(() => {
    if (embedUrl === null) return;
    bindEmbed(player, (seconds) => setStart(Math.floor(seconds)));
    return () => bindEmbed(player, null);
  }, [player, embedUrl]);

  if (video === null) return null;
  return (
    <div data-testid={ids.demoPlayer} className="min-w-0">
      <div className="relative aspect-video w-full overflow-hidden rounded-panel border border-edge bg-sunken">
        {embedUrl !== null ? (
          <iframe
            data-testid={ids.demoEmbed}
            title={`${demo.sprintName} demo on YouTube`}
            src={embedSource(embedUrl, start)}
            referrerPolicy="strict-origin-when-cross-origin"
            allow="autoplay; encrypted-media; picture-in-picture; fullscreen"
            allowFullScreen
            className="size-full border-0"
          />
        ) : (
          <video
            ref={bind}
            data-testid={ids.demoVideo}
            src={video.contentUrl}
            controls
            onTimeUpdate={(event) => reportTime(player, event.currentTarget.currentTime)}
            className="size-full bg-sunken"
          />
        )}
        <span className="pointer-events-none absolute top-2 left-2">
          <Chip>{embedUrl !== null ? 'YouTube' : 'Video file'}</Chip>
        </span>
      </div>
      <div className="mt-2 flex flex-wrap items-center gap-2 text-[length:var(--text-sm)]">
        {youTube.watchUrl !== null && (
          <a
            data-testid={ids.demoWatchOnYoutube}
            href={youTube.watchUrl}
            target="_blank"
            rel="noreferrer"
            className="inline-flex h-7 items-center rounded-control border border-edge bg-surface px-3 font-medium text-ink hover:bg-sunken"
          >
            Watch on YouTube ↗
          </a>
        )}
        {youTube.privacy === 'unlisted' && embedUrl !== null && (
          <span className="text-ink-muted">Unlisted: anyone with the link can watch.</span>
        )}
      </div>
      {youTube.state === 'published' && youTube.privacy === 'private' && (
        <p
          data-testid={ids.demoPrivateNote}
          className="mt-2 rounded-control border border-dashed border-edge px-3 py-2 text-[length:var(--text-sm)] text-ink-muted"
        >
          This video is private on YouTube, so only the channel owner can watch it there. Everyone
          here watches the file ShipLean kept.
        </p>
      )}
      <p className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-[length:var(--text-sm)] text-ink-muted">
        <span>
          Length <b className="font-medium text-ink">{video.durationLabel}</b>
        </span>
        <span>
          Size <b className="font-medium text-ink">{formatSize(video.sizeBytes)}</b>
        </span>
        <span className="min-w-0 break-all">
          File <b className="font-medium text-ink">{video.name}</b>
        </span>
        <span>{video.deliveredLabel}</span>
      </p>
    </div>
  );
}
