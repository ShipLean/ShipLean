// Agent consent and the connected agents. C#: ShipLean.Contracts.Agents. Nothing is omitted: absent values are null;
// timestamps are ISO strings.
import { api, type RequestOptions } from './client';

/** The pending authorize request the consent page shows. */
export interface ConsentRequestDto {
  requestId: string;
  clientName: string;
  /** The verified host of a metadata-document client; null when the client is unverified. */
  clientHost: string | null;
  verified: boolean;
  /** Where the person is sent back to. */
  redirectHost: string;
  requestedScopes: string[];
  /** Every registered redirect of the client is a loopback address: the page warns. */
  localhostOnly: boolean;
}

export interface ConsentDecision {
  allow: boolean;
  /** The scopes left ticked; `shiplean:read` is always granted. Ignored on deny. */
  scopes: string[];
}

export interface ConsentResult {
  /** The client's redirect URI with `code`, `state`, `iss`, or `error=access_denied`. */
  redirectUrl: string;
}

/** One row of "Connected agents". */
export interface ConnectedAgentDto {
  id: string;
  clientName: string;
  clientHost: string | null;
  verified: boolean;
  scopes: string[];
  connectedAt: string;
  lastUsedAt: string | null;
  /** Server-made, e.g. "2h ago" or "Yesterday". */
  connectedLabel: string;
  /** Null when the agent was never used. */
  lastUsedLabel: string | null;
}

/** The address to paste into an agent. */
export interface McpUrlDto {
  mcpUrl: string;
}

export const getConsentRequest = (requestId: string, options?: RequestOptions) =>
  api.get<ConsentRequestDto>(`/api/agents/consent/${encodeURIComponent(requestId)}`, options);

export const decideConsent = (requestId: string, decision: ConsentDecision) =>
  api.post<ConsentResult>(`/api/agents/consent/${encodeURIComponent(requestId)}`, decision);

export const getConnectedAgents = (options?: RequestOptions) =>
  api.get<ConnectedAgentDto[]>('/api/me/agents', options);

export const revokeConnectedAgent = (id: string) =>
  api.delete<undefined>(`/api/me/agents/${encodeURIComponent(id)}`);

export const getMcpUrl = (options?: RequestOptions) =>
  api.get<McpUrlDto>('/api/agents/mcp-url', options);
