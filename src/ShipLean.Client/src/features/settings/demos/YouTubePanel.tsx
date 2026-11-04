import type { YouTubeProjectDto } from '../../../api/demos';
import type { ProjectDto } from '../../../api/projects';
import { demosTestIds as ids } from '../../demos/testIds';

export interface YouTubePanelProps {
  project: ProjectDto;
  youtube: YouTubeProjectDto;
  readOnly: boolean;
}

// TODO: connect, check, visibility and disconnect.
export function YouTubePanel({ youtube }: YouTubePanelProps) {
  return <section data-testid={ids.youtubePanel}>YouTube: {youtube.status}</section>;
}
