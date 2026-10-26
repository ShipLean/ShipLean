import { ApiRequestError } from '../../../../api/client';

/** GET/PUT api/instance/integrations. A token is never in it, only its last four characters (C#: IntegrationsSettingsDto). */
export interface IntegrationsSettingsDto {
  gitHubTokenLastFour: string | null;
  gitHubTokenFromEnv: boolean;
  gitLabUrl: string | null;
  gitLabTokenLastFour: string | null;
  gitLabFromEnv: boolean;
  previewAllowHosts: string[];
}

/** The PUT body (C#: IntegrationsSaveDto). An absent token or URL leaves the saved one alone. */
export interface IntegrationsSaveDto {
  gitHubToken?: string;
  clearGitHubToken: boolean;
  gitLabUrl?: string;
  gitLabToken?: string;
  clearGitLabToken: boolean;
  previewAllowHosts: string[];
}

/** What the admin did to one token: typed a new one (`replacing`), or marked the saved one for removal (`clear`). */
export interface TokenEdit {
  replacing: boolean;
  clear: boolean;
  value: string;
}

export interface IntegrationsEdit {
  gitHub: TokenEdit;
  gitLab: TokenEdit;
  /** Set once the admin touches the field; absent means the saved URL. */
  gitLabUrl?: string;
  /** The allow-list as typed, one host a line. */
  allowText?: string;
}

export const noTokenEdit: TokenEdit = { replacing: false, clear: false, value: '' };
export const noEdit: IntegrationsEdit = { gitHub: noTokenEdit, gitLab: noTokenEdit };

/** One host a line; blank lines are dropped. The server validates the names. */
export const parseHosts = (text: string) =>
  text
    .split('\n')
    .map((line) => line.trim())
    .filter((line) => line.length > 0);

/** The GitLab address in the field: what was typed, otherwise the saved one. */
export const gitLabUrlOf = (saved: IntegrationsSettingsDto, edit: IntegrationsEdit) =>
  edit.gitLabUrl ?? saved.gitLabUrl ?? '';

/** A changed address leaves the saved token behind: the server drops it, so it is no longer shown. */
export const gitLabUrlChanged = (saved: IntegrationsSettingsDto, edit: IntegrationsEdit) =>
  edit.gitLabUrl !== undefined && edit.gitLabUrl.trim() !== (saved.gitLabUrl ?? '');

export const allowTextOf = (saved: IntegrationsSettingsDto, edit: IntegrationsEdit) =>
  edit.allowText ?? saved.previewAllowHosts.join('\n');

const tokenDirty = (edit: TokenEdit) => edit.replacing || edit.clear || edit.value.length > 0;

export function isDirty(saved: IntegrationsSettingsDto, edit: IntegrationsEdit): boolean {
  const hosts = parseHosts(allowTextOf(saved, edit));
  return (
    tokenDirty(edit.gitHub) ||
    tokenDirty(edit.gitLab) ||
    gitLabUrlChanged(saved, edit) ||
    hosts.join('\n') !== saved.previewAllowHosts.join('\n')
  );
}

export function toSaveDto(
  saved: IntegrationsSettingsDto,
  edit: IntegrationsEdit,
): IntegrationsSaveDto {
  return {
    gitHubToken: edit.gitHub.value.length > 0 ? edit.gitHub.value : undefined,
    clearGitHubToken: edit.gitHub.clear,
    // A URL that only the environment has is stored with the token the admin saves for it.
    gitLabUrl:
      gitLabUrlChanged(saved, edit) || (saved.gitLabFromEnv && edit.gitLab.value.length > 0)
        ? gitLabUrlOf(saved, edit).trim()
        : undefined,
    gitLabToken: edit.gitLab.value.length > 0 ? edit.gitLab.value : undefined,
    clearGitLabToken: edit.gitLab.clear,
    previewAllowHosts: parseHosts(allowTextOf(saved, edit)),
  };
}

/** The request field the server blames, when it names one. */
export function errorField(error: unknown): string | undefined {
  if (!(error instanceof ApiRequestError)) return undefined;
  const body = error.body as { field?: unknown } | undefined;
  return typeof body?.field === 'string' ? body.field : undefined;
}
