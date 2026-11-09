import { useMemo } from 'react';
import type { SprintDemoDto } from '@shiplean/core/api/demos';
import { ToastHost } from '../../../components/ToastHost';
import { DemoStatusBand } from '../DemoStatusBand';
import { newPlayerHandle } from '../playerHandle';

// The band alone, at the width the tab gives it, with the toasts its failures show.
export function DemoBandHarness({ demo }: { demo: SprintDemoDto }) {
  const player = useMemo(() => newPlayerHandle(), []);
  return (
    <div className="max-w-[1280px] p-4">
      <DemoStatusBand demo={demo} player={player} />
      <ToastHost />
    </div>
  );
}
