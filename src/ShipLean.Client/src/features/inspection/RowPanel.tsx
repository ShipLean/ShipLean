import { useEffect } from 'react';
import { ApiRequestError } from '@shiplean/core/api/client';
import { postInspectionUsage, type RunRowKind } from '@shiplean/core/api/inspection';
import { useProjectByKey } from '../../realtime/useProjectByKey';
import { useRunRow } from './inspectionApi';
import { inspectionTestIds as ids } from './testIds';
import { formatDuration } from './waterfall/geometry';
import './waterfall/waterfall.css';

export interface RowPanelProps {
  project: string;
  itemKey: string;
  runId: string;
  rowId: string;
}

const KIND_NAME: Record<RunRowKind, string> = {
  modelCall: 'Model call',
  tool: 'Tool',
  shipleanTool: 'ShipLean tool',
  subagent: 'Subagent',
  event: 'Event',
};
const KIND_LETTER: Record<RunRowKind, string> = {
  modelCall: 'M',
  tool: 'T',
  shipleanTool: 'SL',
  subagent: 'S',
  event: '•',
};

// The side panel of the selected row; it fetches the row's detail itself, once per selected row.
export function RowPanel({ project, itemKey, runId, rowId }: RowPanelProps) {
  const row = useRunRow(project, itemKey, runId, rowId);
  // The usage POST wants the project's id; the by-key query is the one the page already holds.
  const projectId = useProjectByKey(project).data?.id;
  useEffect(() => {
    if (projectId === undefined) return;
    postInspectionUsage(projectId, { action: 'rowSelected', itemKey }).catch(() => undefined);
  }, [projectId, itemKey, rowId]);

  const detail = row.data;
  if (detail === undefined) {
    return (
      <aside className="rp" data-testid={ids.runPanel}>
        {row.error instanceof ApiRequestError && row.error.status === 409 ? (
          <p
            className="rp-note"
            data-testid={ids.runPanelLoadFailed}
            data-reason="store-unreachable"
          >
            <b>Store unreachable.</b> {row.error.message}
          </p>
        ) : row.isError ? (
          <p className="rp-note" data-testid={ids.runPanelLoadFailed}>
            Couldn&apos;t load this row. Select it again to retry.
          </p>
        ) : (
          <p className="rp-note" data-testid={ids.runPanelLoading}>
            Loading…
          </p>
        )}
      </aside>
    );
  }

  const copyLink = () => {
    navigator.clipboard.writeText(window.location.href).catch(() => undefined);
  };
  const ranFor =
    detail.durationMs !== null && detail.waitMs !== null ? detail.durationMs - detail.waitMs : null;
  return (
    <aside className="rp" data-testid={ids.runPanel}>
      <div className="rp-h">
        <div className="t">
          <span className={`rw-kd ${detail.kind}`}>{KIND_LETTER[detail.kind]}</span>
          <span className="nm">{detail.name}</span>
        </div>
        <div className="rp-meta">
          <span data-testid={ids.runPanelKind}>{KIND_NAME[detail.kind]}</span>·
          <span data-testid={ids.runPanelStart}>{detail.startLabel}</span>·
          <span data-testid={ids.runPanelDuration}>
            {detail.durationMs === null ? 'point in time' : formatDuration(detail.durationMs)}
          </span>
          ·
          <span data-testid={ids.runPanelOffset}>
            {formatDuration(detail.offsetMs)} into the run
          </span>
        </div>
        {detail.waitMs === null || detail.waitMs === 0 ? null : (
          <div className="rp-meta" data-testid={ids.runPanelWait}>
            <span className="rw-pill warn">
              Waited {formatDuration(detail.waitMs)} for permission
            </span>
            {ranFor === null ? null : <span>then ran for {formatDuration(ranFor)}</span>}
          </div>
        )}
        <div className="rp-acts">
          <button
            type="button"
            className="btn-ghost"
            data-testid={ids.runPanelCopyLink}
            onClick={copyLink}
          >
            Copy link
          </button>
          {detail.grafanaUrl === null ? null : (
            <a
              className="btn-ghost"
              data-testid={ids.runPanelGrafana}
              href={detail.grafanaUrl}
              target="_blank"
              rel="noreferrer"
            >
              Open in Grafana ↗
            </a>
          )}
        </div>
      </div>
      <div className="rp-b">
        {detail.filePath === null ? null : (
          <div className="rp-sec">
            <span className="rp-over">Edited file</span>
            <div className="rp-file">
              <code data-testid={ids.runPanelFilePath}>{detail.filePath}</code>
              {detail.commitSha === null ? null : (
                <code className="text-ink-muted" data-testid={ids.runPanelCommit}>
                  {detail.commitSha}
                </code>
              )}
            </div>
          </div>
        )}
        {detail.contentHidden ? (
          <div className="rp-hidden" data-testid={ids.runPanelContentHidden}>
            Prompts, responses and tool input and output are hidden in {project}. A project admin
            can show them in Project settings › Inspection; the agent or the team&apos;s collector
            decides whether they&apos;re captured at all.
          </div>
        ) : (
          <>
            {detail.input === null ? null : (
              <div className="rp-sec">
                <span className="rp-over">Input</span>
                <pre data-testid={ids.runPanelInput}>{detail.input}</pre>
              </div>
            )}
            {detail.output === null ? null : (
              <div className="rp-sec">
                <span className="rp-over">Output</span>
                <pre data-testid={ids.runPanelOutput}>{detail.output}</pre>
                {detail.outputTruncated ? (
                  <span
                    className="text-[length:var(--text-xs)] text-ink-muted"
                    data-testid={ids.runPanelOutputTruncated}
                  >
                    Showing the first 40 lines.
                  </span>
                ) : null}
              </div>
            )}
          </>
        )}
        <div className="rp-sec">
          <span className="rp-over">Attributes</span>
          <div className="rp-kvbox" data-testid={ids.runPanelAttributes}>
            <table className="rp-kv">
              <tbody>
                {detail.attributes.map((a) => (
                  <tr key={a.key} data-testid={ids.runPanelAttribute}>
                    <td>{a.key}</td>
                    <td>{a.value}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
        {detail.logs.length === 0 ? null : (
          <div className="rp-sec">
            <span className="rp-over">Logs · {detail.logs.length}</span>
            <div className="rp-logs" data-testid={ids.runPanelLogs}>
              {detail.logs.map((log, i) => (
                <div key={`${log.timeLabel}-${i}`} className="rp-lr" data-testid={ids.runPanelLog}>
                  <span className="rp-tm">{log.timeLabel}</span>
                  <span className={`rp-sv ${log.severity}`}>{log.severity}</span>
                  <span className="rp-bd">
                    {log.body}{' '}
                    {log.attributes.map((a) => (
                      <span key={a.key}>
                        {a.key}={a.value}{' '}
                      </span>
                    ))}
                  </span>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>
    </aside>
  );
}
