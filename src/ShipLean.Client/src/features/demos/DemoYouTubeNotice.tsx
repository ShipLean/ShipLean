import type { DemoProps } from './DemoProps';
import { demosTestIds as ids } from './testIds';

// TODO: the YouTube state notice with the retry button.
export function DemoYouTubeNotice({ demo }: DemoProps) {
  return <div data-testid={ids.demoYoutubeNotice}>YouTube: {demo.youTube.state}</div>;
}
