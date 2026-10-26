import type { ComponentType } from 'react';

export interface PaneProps<TDoc, TContext = undefined> {
  draft: TDoc;
  onChange(next: TDoc): void;
  readOnly: boolean;
  context: TContext;
}

interface PaneBase<TDoc, TContext> {
  id: string;
  label: string;
  /** The top-level properties of the document this pane edits: the pane is dirty when any differs from the saved one. */
  sections: (keyof TDoc)[];
  /** A message to mark the nav item with and show above the pane, or null. */
  warning?: (draft: TDoc, context: TContext) => string | null;
}

export interface ActivePaneDef<TDoc, TContext = undefined> extends PaneBase<TDoc, TContext> {
  component: ComponentType<PaneProps<TDoc, TContext>>;
}

/** Shown in the nav but not selectable; `disabled` says where the setting lives instead. */
export interface DisabledPaneDef<TDoc, TContext = undefined> extends PaneBase<TDoc, TContext> {
  disabled: string;
}

export type PaneDef<TDoc, TContext = undefined> =
  ActivePaneDef<TDoc, TContext> | DisabledPaneDef<TDoc, TContext>;

export interface PaneGroup<TDoc, TContext = undefined> {
  label: string;
  panes: PaneDef<TDoc, TContext>[];
}

export type SaveOutcome<TDoc> =
  { ok: true; document: TDoc; mergedSections: string[] } | { ok: false; message: string };

export function isDisabledPane<TDoc, TContext>(
  pane: PaneDef<TDoc, TContext>,
): pane is DisabledPaneDef<TDoc, TContext> {
  return 'disabled' in pane;
}
