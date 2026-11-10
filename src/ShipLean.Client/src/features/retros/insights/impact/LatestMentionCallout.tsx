import type { RetroPointDto } from '@shiplean/core/api/retros';
import { ActionChip } from '../../../retroShared/ActionChip';

/** For an action that did not help: the latest mention of the theme and the follow-up action item, if one was raised. */
export function LatestMentionCallout({ mention }: { mention: RetroPointDto }) {
  return (
    <div
      data-callout="latest"
      className="rounded-control border border-edge border-l-[3px] border-l-caution bg-panel px-3 py-2 text-[length:var(--text-sm)]"
    >
      <b className="block">Didn&apos;t help</b>
      Agents still hit it: &ldquo;{mention.text}&rdquo;.
      {mention.action ? (
        <span className="mt-1 flex flex-wrap items-center gap-1.5" data-follow-up="">
          Follow-up <ActionChip action={mention.action} />
          {mention.sprint ? <span>raised in {mention.sprint.name}</span> : null}
        </span>
      ) : null}
    </div>
  );
}
