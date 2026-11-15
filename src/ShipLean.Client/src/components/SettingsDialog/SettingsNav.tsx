import { useId } from 'react';
import { settingsDialogTestIds as ids } from '../../features/settingsDialog/testIds';
import { useTouchShell } from '../../touch/useTouchShell';
import { isDisabledPane, type PaneDef, type PaneGroup } from './types';

export function SettingsNav<TDoc, TContext>({
  groups,
  activeId,
  dirtyIds,
  warnings,
  onSelect,
}: {
  groups: PaneGroup<TDoc, TContext>[];
  activeId: string | undefined;
  dirtyIds: ReadonlySet<string>;
  warnings: ReadonlyMap<string, string>;
  onSelect(id: string): void;
}) {
  const { touch } = useTouchShell();
  if (touch) {
    return (
      <TouchChips
        panes={groups.flatMap((group) => group.panes)}
        activeId={activeId}
        dirtyIds={dirtyIds}
        warnings={warnings}
        onSelect={onSelect}
      />
    );
  }
  return (
    <nav data-testid={ids.nav} aria-label="Settings sections" className="grid content-start gap-1">
      {groups.map((group) => (
        <NavGroup
          key={group.label}
          group={group}
          activeId={activeId}
          dirtyIds={dirtyIds}
          warnings={warnings}
          onSelect={onSelect}
        />
      ))}
    </nav>
  );
}

function NavGroup<TDoc, TContext>({
  group,
  activeId,
  dirtyIds,
  warnings,
  onSelect,
}: {
  group: PaneGroup<TDoc, TContext>;
  activeId: string | undefined;
  dirtyIds: ReadonlySet<string>;
  warnings: ReadonlyMap<string, string>;
  onSelect(id: string): void;
}) {
  const labelId = useId();
  return (
    <div className="grid gap-px pb-2">
      <div
        id={labelId}
        className="px-2 py-1 text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted uppercase"
      >
        {group.label}
      </div>
      <ul aria-labelledby={labelId} className="m-0 grid list-none gap-px p-0">
        {group.panes.map((pane) => (
          <NavItem
            key={pane.id}
            pane={pane}
            active={pane.id === activeId}
            dirty={dirtyIds.has(pane.id)}
            warning={warnings.get(pane.id)}
            onSelect={onSelect}
          />
        ))}
      </ul>
    </div>
  );
}

function NavItem<TDoc, TContext>({
  pane,
  active,
  dirty,
  warning,
  onSelect,
}: {
  pane: PaneDef<TDoc, TContext>;
  active: boolean;
  dirty: boolean;
  warning: string | undefined;
  onSelect(id: string): void;
}) {
  const disabled = isDisabledPane(pane);
  const hintId = useId();
  return (
    <li>
      <button
        type="button"
        data-testid={`${ids.navItem}-${pane.id}`}
        data-dirty={dirty}
        aria-current={active ? 'page' : undefined}
        disabled={disabled}
        aria-describedby={disabled ? hintId : undefined}
        onClick={() => onSelect(pane.id)}
        className={`flex h-8 w-full items-center gap-2 rounded-control px-2 text-left text-[length:var(--text-base)] disabled:text-ink-muted ${
          active ? 'bg-signal-soft font-semibold text-ink' : 'text-ink hover:bg-sunken'
        }`}
      >
        <span className="flex-1">{pane.label}</span>
        {warning === undefined ? null : (
          <span
            data-testid={`${ids.navWarning}-${pane.id}`}
            title={warning}
            className="text-caution"
          >
            <span aria-hidden="true">!</span>
            <span className="sr-only">has warnings</span>
          </span>
        )}
        {dirty ? (
          <span data-testid={`${ids.navDot}-${pane.id}`} title="Unsaved changes">
            <span aria-hidden="true" className="block size-[7px] rounded-full bg-signal" />
            <span className="sr-only">unsaved changes</span>
          </span>
        ) : null}
      </button>
      {disabled ? (
        <div
          id={hintId}
          data-testid={`${ids.navDisabledHint}-${pane.id}`}
          className="px-2 pb-1 text-[length:var(--text-xs)] text-ink-muted"
        >
          {pane.disabled}
        </div>
      ) : null}
    </li>
  );
}

/**
 * At 900px and below the sections are one row of chips that scrolls sideways in its own box (SHIP-805 rule 7). A pane that
 * cannot be opened is left out: its reason is the desktop nav's hint, and a disabled chip would be a dead target.
 */
function TouchChips<TDoc, TContext>({
  panes,
  activeId,
  dirtyIds,
  warnings,
  onSelect,
}: {
  panes: PaneDef<TDoc, TContext>[];
  activeId: string | undefined;
  dirtyIds: ReadonlySet<string>;
  warnings: ReadonlyMap<string, string>;
  onSelect(id: string): void;
}) {
  return (
    <nav
      data-testid={ids.nav}
      aria-label="Settings sections"
      className="flex min-w-0 gap-1.5 overflow-x-auto"
    >
      {panes
        .filter((pane) => !isDisabledPane(pane))
        .map((pane) => {
          const active = pane.id === activeId;
          return (
            <button
              key={pane.id}
              type="button"
              data-testid={`${ids.navItem}-${pane.id}`}
              data-dirty={dirtyIds.has(pane.id)}
              aria-current={active ? 'page' : undefined}
              onClick={() => onSelect(pane.id)}
              className={`inline-flex h-[var(--touch-target)] shrink-0 items-center gap-1.5 rounded-full border px-3 text-[length:var(--text-sm)] ${
                active
                  ? 'border-signal bg-signal-soft font-semibold text-ink'
                  : 'border-edge bg-panel font-medium text-ink-muted'
              }`}
            >
              {pane.label}
              {warnings.has(pane.id) ? (
                <span data-testid={`${ids.navWarning}-${pane.id}`} className="text-caution">
                  <span aria-hidden="true">!</span>
                  <span className="sr-only">has warnings</span>
                </span>
              ) : null}
              {dirtyIds.has(pane.id) ? (
                <span data-testid={`${ids.navDot}-${pane.id}`}>
                  <span aria-hidden="true" className="block size-[7px] rounded-full bg-signal" />
                  <span className="sr-only">unsaved changes</span>
                </span>
              ) : null}
            </button>
          );
        })}
    </nav>
  );
}
