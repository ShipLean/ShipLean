import { createPortal } from 'react-dom';
import {
  useEffect,
  useId,
  useImperativeHandle,
  useRef,
  useState,
  type KeyboardEvent,
  type MouseEvent,
  type Ref,
} from 'react';
import { useModalFocus } from '../../components/useModalFocus';
import { QuickSearchFooter } from './QuickSearchFooter';
import { ActionRow } from './rows/ActionRow';
import { ItemRow } from './rows/ItemRow';
import { ProjectRow } from './rows/ProjectRow';
import { SprintRow } from './rows/SprintRow';
import { quickSearchTestIds as ids } from './testIds';
import type { QuickSearchItem, QuickSearchResults, QuickSearchRow } from './types';

export interface QuickSearchDialogProps {
  query: string;
  onQueryChange(query: string): void;
  results: QuickSearchResults;
  onOpenItem(item: QuickSearchItem): void;
  onClose(): void;
  onDone(): void;
  /** Reaches the input, so the host can focus and select it again. */
  inputRef?: Ref<HTMLInputElement>;
}

function rowDataAttributes(row: QuickSearchRow) {
  if (row.kind === 'item') return { 'data-key': row.item.key };
  if (row.kind === 'project') return { 'data-key': row.key };
  return { 'data-id': row.id };
}

/** The quick search dialog: a combobox over a listbox of grouped rows. It owns only the highlight. */
export function QuickSearchDialog({
  query,
  onQueryChange,
  results,
  onOpenItem,
  onClose,
  onDone,
  inputRef,
}: QuickSearchDialogProps) {
  const panel = useRef<HTMLDivElement>(null);
  const input = useRef<HTMLInputElement>(null);
  const listId = useId();
  useModalFocus(panel);
  useImperativeHandle(inputRef, () => input.current as HTMLInputElement, []);
  // Typing over the last query: the text is selected when the dialog opens.
  useEffect(() => input.current?.select(), []);

  const rows = results.groups.flatMap((group) => group.rows);
  // The highlight belongs to one query and row list; when either changes it is back on the first row.
  const signature = `${query}\n${rows.map((row) => row.id).join('\n')}`;
  const [highlight, setHighlight] = useState({ signature, index: 0 });
  const index = highlight.signature === signature ? highlight.index : 0;
  const move = (to: number) => setHighlight({ signature, index: to });
  const optionId = (at: number) => `${listId}-option-${at}`;

  useEffect(() => {
    document.getElementById(`${listId}-option-${index}`)?.scrollIntoView({ block: 'nearest' });
  }, [listId, index, signature]);

  const pick = (row: QuickSearchRow) => {
    if (row.kind === 'item') onOpenItem(row.item);
    else if (row.kind === 'action') row.run();
    else row.open();
    onDone();
  };

  const onPanelKeyDown = (event: KeyboardEvent) => {
    if (event.key === 'Escape' && !event.defaultPrevented) {
      event.stopPropagation();
      onClose();
    }
  };

  // Only the input moves the highlight and picks: Enter on the Retry button must press Retry.
  const onInputKeyDown = (event: KeyboardEvent) => {
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      if (rows.length === 0) return;
      const step = event.key === 'ArrowDown' ? 1 : -1;
      move((index + step + rows.length) % rows.length);
    } else if (event.key === 'Enter') {
      const row = rows[index];
      if (!row) return;
      event.preventDefault();
      pick(row);
    }
  };

  // A drag that starts in the panel (selecting text) and ends on the scrim is not a click on the scrim.
  const pressedScrim = useRef(false);
  const onScrimMouseDown = (event: MouseEvent) => {
    pressedScrim.current = event.target === event.currentTarget;
    if (pressedScrim.current) event.preventDefault();
  };

  const nothingMatches =
    query.trim() !== '' &&
    results.status === 'ready' &&
    !results.groups.some((group) => group.id !== 'actions' && group.rows.length > 0);

  // Each group's first row's place in the flat list, for the highlight and the option ids.
  const shown = results.groups
    .filter((group) => group.rows.length > 0)
    .map((group, groupAt, all) => ({
      group,
      start: all.slice(0, groupAt).reduce((sum, earlier) => sum + earlier.rows.length, 0),
    }));
  return createPortal(
    <div
      data-testid={ids.scrim}
      onMouseDown={onScrimMouseDown}
      onClick={(event) => {
        if (event.target === event.currentTarget && pressedScrim.current) onClose();
      }}
      className="fixed inset-0 z-50 flex items-center justify-center p-4 max-[899px]:items-start max-[899px]:p-2"
      style={{ background: 'var(--scrim)' }}
    >
      <div
        ref={panel}
        data-testid={ids.dialog}
        role="dialog"
        aria-modal="true"
        aria-label="Quick search"
        tabIndex={-1}
        onKeyDown={onPanelKeyDown}
        style={{ boxShadow: 'var(--shadow-dialog)' }}
        className="flex max-h-[calc(100dvh-96px)] w-[min(640px,100vw-32px)] flex-col overflow-hidden rounded-frame border border-edge bg-panel max-[899px]:max-h-[calc(100dvh-16px)] max-[899px]:w-[calc(100vw-16px)]"
      >
        <div className="relative flex h-[46px] flex-none items-center gap-2.5 border-b border-edge px-3.5">
          <svg
            aria-hidden="true"
            viewBox="0 0 16 16"
            className="size-[15px] flex-none text-ink-muted"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.6"
            strokeLinecap="round"
          >
            <circle cx="7" cy="7" r="4.5" />
            <path d="m10.5 10.5 3 3" />
          </svg>
          <input
            ref={input}
            autoFocus
            data-testid={ids.input}
            type="text"
            role="combobox"
            aria-expanded="true"
            aria-controls={listId}
            aria-autocomplete="list"
            aria-activedescendant={rows.length > 0 ? optionId(index) : undefined}
            aria-label="Search"
            autoComplete="off"
            spellCheck={false}
            placeholder="Search work items, sprints and projects"
            value={query}
            onChange={(event) => onQueryChange(event.target.value)}
            onKeyDown={onInputKeyDown}
            className="min-w-0 flex-1 bg-transparent text-[length:var(--text-md)] text-ink outline-none placeholder:text-ink-muted"
          />
          <kbd className="rounded-xs border border-edge bg-canvas px-1.5 font-mono text-[length:var(--text-2xs)] text-ink-muted">
            Esc
          </kbd>
          {results.status === 'loading' ? (
            <span
              data-testid={ids.loading}
              className="absolute inset-x-0 bottom-[-1px] h-[2px] bg-signal"
            />
          ) : null}
        </div>
        <div
          // The list never takes focus from the input.
          onMouseDown={(event) => event.preventDefault()}
          className={`min-h-0 flex-1 overflow-auto p-1.5 ${results.status === 'loading' ? 'opacity-60' : ''}`}
        >
          {results.status === 'error' ? (
            <div
              data-testid={ids.error}
              role="alert"
              className="mb-1 flex items-center gap-3 rounded-control border border-edge bg-sunken px-3 py-2 text-[length:var(--text-sm)]"
            >
              <span className="flex-1">
                <span className="block text-ink">Search didn't respond.</span>
                <span className="block text-[length:var(--text-xs)] text-ink-muted">
                  Your query is kept. Actions below still work.
                </span>
              </span>
              {results.retry ? (
                <button
                  type="button"
                  data-testid={ids.retry}
                  className="btn-ghost"
                  onClick={results.retry}
                >
                  Retry
                </button>
              ) : null}
            </div>
          ) : null}
          {nothingMatches ? (
            <div data-testid={ids.none} className="px-3 py-4 text-center">
              <p className="m-0 text-ink">Nothing matches “{query.trim()}”</p>
              <p className="m-0 mt-1 text-[length:var(--text-xs)] text-ink-muted">
                Searched titles, descriptions, comments and attachments in your projects.
              </p>
            </div>
          ) : null}
          <ul
            id={listId}
            data-testid={ids.list}
            role="listbox"
            aria-label="Results"
            className="m-0 list-none p-0"
          >
            {shown.map(({ group, start }) => (
              <li key={group.id} role="presentation" className="contents">
                <div
                  role="presentation"
                  data-testid={ids.group}
                  data-group={group.id}
                  className="px-2 pt-2 pb-1 text-[length:var(--text-2xs)] font-semibold tracking-[0.05em] text-ink-muted uppercase"
                >
                  {group.title}
                </div>
                {group.rows.map((row, rowAt) => {
                  const position = start + rowAt;
                  const highlighted = position === index;
                  return (
                    <div
                      key={row.id}
                      id={optionId(position)}
                      role="option"
                      aria-selected={highlighted}
                      data-testid={ids.option}
                      data-kind={row.kind}
                      data-highlighted={highlighted}
                      {...rowDataAttributes(row)}
                      className={`grid min-h-[34px] cursor-pointer grid-cols-[16px_1fr_auto] items-center gap-x-2.5 rounded-control px-2 py-1 ${
                        highlighted ? 'bg-signal-soft' : ''
                      }`}
                      onMouseEnter={() => move(position)}
                      onClick={() => pick(row)}
                    >
                      {row.kind === 'item' ? (
                        <ItemRow item={row.item} match={row.match} query={query} />
                      ) : null}
                      {row.kind === 'sprint' ? (
                        <SprintRow
                          name={row.name}
                          goal={row.goal}
                          datesLabel={row.datesLabel}
                          current={row.current}
                          projectKey={row.projectKey}
                          query={query}
                        />
                      ) : null}
                      {row.kind === 'project' ? (
                        <ProjectRow
                          projectKey={row.key}
                          name={row.name}
                          detail={row.detail}
                          current={row.current}
                          query={query}
                        />
                      ) : null}
                      {row.kind === 'action' ? (
                        <ActionRow glyph={row.glyph} label={row.label} detail={row.detail} />
                      ) : null}
                    </div>
                  );
                })}
              </li>
            ))}
          </ul>
        </div>
        <QuickSearchFooter note={results.note} />
      </div>
    </div>,
    document.body,
  );
}
