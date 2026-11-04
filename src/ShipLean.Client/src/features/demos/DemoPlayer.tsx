import { useCallback } from 'react';
import type { DemoProps } from './DemoProps';
import { bindVideo } from './playerHandle';
import { demosTestIds as ids } from './testIds';

// TODO: the framed player with the YouTube embed and the watch link.
export function DemoPlayer({ demo, player }: DemoProps) {
  const bind = useCallback((video: HTMLVideoElement | null) => bindVideo(player, video), [player]);
  return (
    <div data-testid={ids.demoPlayer}>
      {demo.video !== null && (
        <video ref={bind} data-testid={ids.demoVideo} src={demo.video.contentUrl} controls />
      )}
    </div>
  );
}
