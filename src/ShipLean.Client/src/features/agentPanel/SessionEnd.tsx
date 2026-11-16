import type { AgentSessionDto } from '@shiplean/core/api/agentSessions';
import { formatSize } from '../workItem/side/fileSize';
import { agentPanelTestIds as ids } from './testIds';

// Why a session that is not live ended: the sentence the server built, the detail when this viewer may see it, and the
// run log link when there is one. The server already applied who sees what, so this shows exactly what it is given. The
// detail and the sentence come from agents and receivers, so they render as text.
export function SessionEnd({ session }: { session: AgentSessionDto }) {
  if (session.live || !session.endText) return null;
  const { endText, endDetail, runLog } = session;
  // endText can already end with the detail (a failed delivery does); say it once.
  const showDetail = endDetail && !endText.includes(endDetail);
  return (
    <section
      data-testid={ids.sessionEnd}
      data-reason={session.endReason ?? undefined}
      aria-label="Why it ended"
      className="grid min-w-0 gap-1 rounded-control border-l-2 border-l-edge bg-sunken px-2 py-1.5 text-[length:var(--text-sm)]"
    >
      <div className="text-[length:var(--text-2xs)] font-semibold uppercase tracking-wide text-ink-muted">
        Why it ended
      </div>
      <div className="min-w-0 break-words">{endText}</div>
      {showDetail ? (
        <div className="min-w-0 whitespace-pre-wrap break-words text-[length:var(--text-xs)] text-ink-muted">
          {endDetail}
        </div>
      ) : null}
      {runLog ? (
        <a
          data-testid={ids.sessionEndRunLog}
          href={runLog.url}
          className="w-fit max-w-full break-all text-signal underline"
        >
          Run log ({formatSize(runLog.sizeBytes)})
        </a>
      ) : null}
    </section>
  );
}
