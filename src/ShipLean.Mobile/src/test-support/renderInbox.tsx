// Its own file: vitest runs in Node and can't load React Native, so inbox.ts stays pure.
import type { AnsweredDto, InboxDto } from '@shiplean/core/api/inbox';
import type { ReactElement } from 'react';
import type { ActionRunner } from '../actions/types';
import {
  InboxActionsContext,
  InboxActionsProvider,
  type InboxActions,
} from '../inbox/InboxActions';
import { InboxSheetsContext, InboxSheetsProvider, type InboxSheets } from '../inbox/InboxSheets';
import { InboxScreen } from '../screens/InboxScreen';
import type { ControlledApi } from './controlledApi';
import { inboxApi } from './inbox';
import { renderWithRunner } from './renderWithRunner';

export interface RenderInboxOptions {
  projectKey?: string;
  inbox: InboxDto;
  answered?: AnsweredDto;
  // Defaults to inboxApi over the inbox and answered given.
  api?: ControlledApi;
  // Fakes in place of the real providers (fakeInboxActions(), fakeInboxSheets()).
  actions?: InboxActions;
  sheets?: InboxSheets;
  // Defaults to a fakeRunner().
  runner?: ActionRunner;
}

// InboxScreen in the Inbox slot's providers, or the fakes given, over a fake server and runner.
export async function renderInbox({
  projectKey = 'SHIP',
  inbox,
  answered,
  api,
  actions,
  sheets,
  runner,
}: RenderInboxOptions) {
  const used = api ?? inboxApi({}, { inbox, answered });
  used.install();
  const screen = <InboxScreen projectKey={projectKey} />;
  const withSheets: ReactElement =
    sheets === undefined ? (
      <InboxSheetsProvider>{screen}</InboxSheetsProvider>
    ) : (
      <InboxSheetsContext.Provider value={sheets}>{screen}</InboxSheetsContext.Provider>
    );
  const tree =
    actions === undefined ? (
      <InboxActionsProvider>{withSheets}</InboxActionsProvider>
    ) : (
      <InboxActionsContext.Provider value={actions}>{withSheets}</InboxActionsContext.Provider>
    );
  const result = await renderWithRunner(tree, { runner });
  return Object.assign(result, { api: used });
}
