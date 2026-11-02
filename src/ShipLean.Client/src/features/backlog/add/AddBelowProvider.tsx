import { useState, type ReactNode } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import type { BacklogViewDto } from '../../../api/views';
import { backlogRootKey } from '../../../queries/backlogKeys';
import { useBacklogAdd } from '../bulk/useBacklogAdd';
import { useSelection } from '../selection/useSelection';
import { AddStoreContext, createAddStore } from './useAddChain';

function Store({
  projectId,
  projectKey,
  children,
}: {
  projectId: string;
  projectKey: string;
  children: ReactNode;
}) {
  const [store] = useState(createAddStore);
  const add = useBacklogAdd(projectId, projectKey);
  const queryClient = useQueryClient();
  const selection = useSelection();
  // The store reads the newest of these when a queued write starts, so they are bound on every render.
  store.bind({
    add: add.mutateAsync,
    focus: selection.setFocused,
    // The backlog view read last, to place a row by the order and sprints it shows; opening the row asks for nothing.
    view: () =>
      queryClient
        .getQueryCache()
        .findAll({ queryKey: backlogRootKey(projectId) })
        .filter((query) => query.state.data !== undefined)
        .sort((a, b) => b.state.dataUpdatedAt - a.state.dataUpdatedAt)[0]?.state.data as
        BacklogViewDto | undefined,
  });
  return <AddStoreContext.Provider value={store}>{children}</AddStoreContext.Provider>;
}

/** The add rows' state for one backlog screen. A new project starts a new store, so nothing carries over. */
export function AddBelowProvider(props: {
  projectId: string;
  projectKey: string;
  children: ReactNode;
}) {
  return <Store key={props.projectId} {...props} />;
}
