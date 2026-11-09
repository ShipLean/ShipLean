import { useMemo } from 'react';
import { ToastHost } from '../../../components/ToastHost';
import type { SprintDemoDto } from '@shiplean/core/api/demos';
import fixtureUrl from '../../../../../ShipLean.TestSupport/DemoFixtures/demo-fixture.mp4?url';
import { DemoChapters } from '../DemoChapters';
import { DemoFeatures } from '../DemoFeatures';
import { DemoPlayer } from '../DemoPlayer';
import { DemoScript } from '../DemoScript';
import { DemoYouTubeNotice } from '../DemoYouTubeNotice';
import { newPlayerHandle } from '../playerHandle';

// The ready state as the Demo tab lays it out (the player and its notice, the side lists, the script), with the
// real 8-second fixture the CT bundle serves in place of the fixture JSON's attachment url.
export function DemoPlayerHarness({ demo }: { demo: SprintDemoDto }) {
  const player = useMemo(() => newPlayerHandle(), []);
  const played: SprintDemoDto = {
    ...demo,
    video: demo.video === null ? null : { ...demo.video, contentUrl: fixtureUrl },
  };
  return (
    <div className="grid max-w-[1280px] gap-3 p-4">
      <div className="grid grid-cols-[minmax(0,1fr)_360px] items-start gap-4">
        <div className="grid min-w-0 gap-2">
          <DemoPlayer demo={played} player={player} />
          <DemoYouTubeNotice demo={played} player={player} />
        </div>
        <div className="grid min-w-0 gap-3">
          <DemoChapters demo={played} player={player} />
          <DemoFeatures demo={played} player={player} />
        </div>
      </div>
      <DemoScript demo={played} player={player} />
      <ToastHost />
    </div>
  );
}
