import { Chip } from '../../components/Chip';
import { showToast } from '../../components/toastStore';
import { useRetryYouTube } from './demosApi';
import type { DemoProps } from './DemoProps';
import { demosTestIds as ids } from './testIds';

/**
 * What YouTube is doing with the file, when it isn't playing there: sending, or a failure with its reason and Retry.
 * The file plays above either way. Nothing when YouTube isn't in play or is published.
 */
export function DemoYouTubeNotice({ demo }: DemoProps) {
  const retry = useRetryYouTube(demo.sprintId);
  const { youTube, actions } = demo;
  if (youTube.state === 'pending') {
    return (
      <div
        data-testid={ids.demoYoutubeNotice}
        className="flex flex-wrap items-center gap-3 rounded-control border border-edge border-l-[3px] border-l-notice bg-surface px-3 py-2 text-[length:var(--text-sm)] touch:order-first touch:w-full touch:flex-col touch:items-start touch:gap-2"
      >
        <Chip tone="info">Sending to YouTube</Chip>
        <span className="min-w-60 flex-1 touch:min-w-0">
          ShipLean is sending the file to YouTube. It plays here meanwhile.
        </span>
      </div>
    );
  }
  if (youTube.state !== 'failed') return null;
  return (
    <div
      data-testid={ids.demoYoutubeNotice}
      className="flex flex-wrap items-center gap-3 rounded-control border border-edge border-l-[3px] border-l-alert bg-surface px-3 py-2 text-[length:var(--text-sm)] touch:order-first touch:w-full touch:flex-col touch:items-start touch:gap-2"
    >
      <Chip tone="danger">YouTube upload failed</Chip>
      <span className="min-w-60 flex-1 touch:min-w-0">
        {youTube.failureMessage} The file is safe here and plays above.
      </span>
      {actions.canRetryYouTube && (
        <button
          type="button"
          data-testid={ids.demoYoutubeRetry}
          disabled={retry.isPending}
          onClick={() =>
            retry.mutate(undefined, {
              onError: (failure) => showToast(failure.message, { tone: 'danger' }),
            })
          }
          className="inline-flex h-7 items-center rounded-control border border-edge bg-surface px-3 font-medium hover:bg-sunken disabled:opacity-50 touch:h-[var(--touch-target)] touch:w-full touch:justify-center"
        >
          Retry upload
        </button>
      )}
    </div>
  );
}
