// A work item's history read. The shape is in workItems.ts.
import { api } from './client';
import type { HistoryDto } from './workItems';

export const historyKeys = {
  item: (itemId: string) => ['history', itemId] as const,
};

export const getHistory = (itemId: string) =>
  api.get<HistoryDto>(`/api/work-items/${encodeURIComponent(itemId)}/history`);
