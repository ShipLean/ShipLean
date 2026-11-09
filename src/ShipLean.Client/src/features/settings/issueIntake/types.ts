import type { ApiErrorCode } from '@shiplean/core/api/client';
import type { UserRef } from '@shiplean/core/api/refs';

/** C#: ShipLean.Contracts.IssueIntake.IssueIntakeReadiness. Each is true when that part is in place. */
export interface IssueIntakeReadiness {
  hasGitHubRepository: boolean;
  hasAgentConnection: boolean;
  hasGitHubToken: boolean;
  hasWebhookSecret: boolean;
  hasRunAs: boolean;
}

/**
 * C#: ShipLean.Contracts.IssueIntake.IssueIntakeSettingsDto. `repository` is "owner/name", null when the project's
 * repo URL names no github.com repository; an empty `proposalInstructions` means `defaultProposalInstructions`.
 */
export interface IssueIntakeSettingsDto {
  enabled: boolean;
  repository: string | null;
  runAs: UserRef | null;
  proposalInstructions: string;
  defaultProposalInstructions: string;
  maxRounds: number;
  readiness: IssueIntakeReadiness;
}

/** C#: ShipLean.Contracts.IssueIntake.IssueIntakeSaveDto. Admins only. */
export interface IssueIntakeSaveDto {
  enabled: boolean;
  runAsUserId: string | null;
  proposalInstructions: string;
  maxRounds: number;
}

/** The refusals a save can answer with; the error body's field (ApiRequestError.body) names the field at fault. */
export const issueIntakeErrorCodes = [
  'repository_taken',
  'no_repo_url',
  'not_member',
  'intake_on',
] as const satisfies readonly ApiErrorCode[];

export type IssueIntakeErrorCode = (typeof issueIntakeErrorCodes)[number];
