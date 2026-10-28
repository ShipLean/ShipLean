/** GET api/instance/agent-connections (C#: AgentConnectionDto). A secret or token is never in it, only its last four characters. */
export interface AgentConnectionDto {
  id: string;
  type: string;
  name: string;
  receiverUrl: string;
  signingSecretLast4: string;
  callbackTokenLast4: string;
  createdAt: string;
  updatedAt: string;
  archivedAt: string | null;
}

/** The answer to creating a connection or regenerating a key (C#: AgentConnectionIssuedDto): the plaintext, once. */
export interface AgentConnectionIssuedDto {
  connection: AgentConnectionDto;
  signingSecret: string | null;
  callbackToken: string | null;
}

/** The body of creating or editing a connection (C#: AgentConnectionPut). */
export interface AgentConnectionPut {
  name: string;
  receiverUrl: string;
}

/** What the show-once dialog holds: the connection's name and whichever of the two were just made. */
export interface ShownOnce {
  name: string;
  signingSecret: string | null;
  callbackToken: string | null;
}

export const shownOf = (issued: AgentConnectionIssuedDto): ShownOnce => ({
  name: issued.connection.name,
  signingSecret: issued.signingSecret,
  callbackToken: issued.callbackToken,
});

/** The form is ready to send: both fields have text. The server validates the rest. */
export const isComplete = (put: AgentConnectionPut) =>
  put.name.trim().length > 0 && put.receiverUrl.trim().length > 0;
