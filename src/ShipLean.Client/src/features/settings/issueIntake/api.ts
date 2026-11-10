import { api } from '@shiplean/core/api/client';
import type { IssueIntakeSaveDto, IssueIntakeSettingsDto } from './types';

/** Keyed under the project like the rest of its settings. */
export const issueIntakeKey = (projectId: string) =>
  ['projects', projectId, 'settings', 'issue-intake'] as const;

const path = (projectId: string) =>
  `/api/projects/${encodeURIComponent(projectId)}/settings/issue-intake`;

/** Members can read it. */
export const fetchIssueIntake = (projectId: string) =>
  api.get<IssueIntakeSettingsDto>(path(projectId));

/** Admins only; a refusal is thrown as an ApiRequestError with one of issueIntakeErrorCodes or forbidden. */
export const saveIssueIntake = (projectId: string, save: IssueIntakeSaveDto) =>
  api.put<IssueIntakeSettingsDto>(path(projectId), save);
