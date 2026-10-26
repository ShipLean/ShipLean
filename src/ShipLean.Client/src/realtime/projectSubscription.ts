import type { RouteId } from '../routes';
import { EVENT_METHOD, RETRY_DELAYS_MS, type RealtimeConnection } from './connection';
import type { ConnectionState } from './connectionState';

export interface SubscriptionDeps {
  connect: () => RealtimeConnection;
  retryDelaysMs?: readonly number[];
  /** One message from the hub's "projectEvent", for the open project only, otherwise untouched. */
  onEvent: (event: unknown) => void;
  onState: (state: ConnectionState) => void;
  /** Everything the server knew about this connection is gone (a drop, or another project): clear what it said. */
  onReset: () => void;
  /** A connection that had dropped is up again and the project and screen are joined again. */
  onReconnected: (projectId: string | undefined) => void;
  /**
   * The server refused the connection or a call as if the session were over (401, 403, session_ended,
   * password_change_required). Asks the API whether it is: resolves false when the API refuses too (its client then sends
   * the user to sign in, and reconnecting stops), true when the API is fine (a proxy or Origin problem: keep retrying).
   */
  confirmSession: () => Promise<boolean>;
}

export interface ProjectSubscription {
  /** The project to be joined (undefined: none). Connects on the first project; the screen is left with the project. */
  setProject(projectId: string | undefined): void;
  /** The screen to be on in that project (undefined: none). Joined only after the project's join has resolved. */
  setScreen(screenId: RouteId | undefined): void;
  /** Skips the back-off wait and tries to connect now. */
  retryNow(): void;
  dispose(): void;
}

// A session that ends while the socket is open only shows as the socket closing; the reconnect's negotiate then answers
// 401 (session_ended or no session) or 403 (password_change_required). SignalR rethrows that as a
// FailedToNegotiateWithServerError with no statusCode: only the message carries it ("... Status code '401'").
function isSessionRefusal(error: unknown): boolean {
  const status = (error as { statusCode?: unknown } | null)?.statusCode;
  if (status === 401 || status === 403) return true;
  const message = (error as { message?: unknown } | null)?.message;
  return typeof message === 'string' && /Status code '(401|403)'/.test(message);
}

// A hub call that failed on the server arrives as an Error whose message holds "HubException: <code>: <text>".
function hubErrorCode(error: unknown): string | undefined {
  const message = (error as { message?: unknown } | null)?.message;
  return typeof message === 'string' ? /HubException: ([a-z_]+): /.exec(message)?.[1] : undefined;
}

function isProjectEvent(value: unknown): value is { type: string; projectId: string } {
  return (
    typeof value === 'object' &&
    value !== null &&
    typeof (value as { type?: unknown }).type === 'string' &&
    typeof (value as { projectId?: unknown }).projectId === 'string'
  );
}

// One connection for the tab. It connects when a project is first wanted (the sign-in page never connects), joins the
// wanted project, then the wanted screen (the hub allows one screen per connection per project, so a new screen leaves
// the old), and starts over from the wanted state after every drop. What the server holds is tracked as "joined"; every
// change of the wanted state, and every connect, brings joined in line, one call at a time.
export function createProjectSubscription(deps: SubscriptionDeps): ProjectSubscription {
  const delays = deps.retryDelaysMs ?? RETRY_DELAYS_MS;
  let connection: RealtimeConnection | undefined;
  let connected = false;
  let everConnected = false;
  // A connect attempt failed before the first success: the connect that finally works is a reconnect too.
  let attemptFailed = false;
  let starting = false;
  let disposed = false;
  let retry: ReturnType<typeof setTimeout> | undefined;
  // Counts connects and closes, so a call that failed because the connection went away is not taken as a refusal.
  let epoch = 0;

  let wantedProject: string | undefined;
  let wantedScreen: RouteId | undefined;
  let joinedProject: string | undefined;
  let joinedScreen: RouteId | undefined;
  // What the server refused (a failed call while the connection stayed up, e.g. its HubException): not asked again until the wanted state or the connection changes.
  let refused = new Set<string>();

  let syncing = false;
  let syncAgain = false;

  const setState = (state: ConnectionState) => deps.onState(state);

  // Asks whether the session is really over; if so the API client has redirected and nothing more is done here.
  async function endIfSessionOver(): Promise<boolean> {
    if (await deps.confirmSession()) return false;
    disposed = true;
    clearTimeout(retry);
    setState('offline');
    void connection?.stop();
    return true;
  }

  async function call(method: string, ...args: unknown[]): Promise<boolean> {
    const live = connection;
    if (live === undefined) return false;
    const epochBefore = epoch;
    try {
      await live.invoke(method, ...args);
      return true;
    } catch (error) {
      if (epoch !== epochBefore) return false;
      const code = hubErrorCode(error);
      if (code === 'session_ended' || code === 'password_change_required') {
        await endIfSessionOver();
      } else if (code === 'not_found' && method === 'JoinProject') {
        // Not joined, so the dot must not claim Live for it.
        setState('offline');
      }
      return false;
    }
  }

  async function syncOnce(): Promise<boolean> {
    if (
      joinedScreen !== undefined &&
      (joinedProject !== wantedProject || joinedScreen !== wantedScreen)
    ) {
      const [project, screen] = [joinedProject, joinedScreen];
      joinedScreen = undefined;
      await call('LeaveScreen', project, screen);
      return true;
    }
    if (joinedProject !== undefined && joinedProject !== wantedProject) {
      const project = joinedProject;
      joinedProject = undefined;
      await call('LeaveProject', project);
      return true;
    }
    if (
      wantedProject !== undefined &&
      joinedProject === undefined &&
      !refused.has(`p:${wantedProject}`)
    ) {
      const project = wantedProject;
      const epochBefore = epoch;
      if (await call('JoinProject', project)) joinedProject = project;
      else if (epoch === epochBefore) refused.add(`p:${project}`);
      return true;
    }
    if (
      wantedScreen !== undefined &&
      joinedProject !== undefined &&
      joinedProject === wantedProject &&
      joinedScreen === undefined &&
      !refused.has(`s:${joinedProject}:${wantedScreen}`)
    ) {
      const [project, screen] = [joinedProject, wantedScreen];
      const epochBefore = epoch;
      if (await call('JoinScreen', project, screen)) joinedScreen = screen;
      else if (epoch === epochBefore) refused.add(`s:${project}:${screen}`);
      return true;
    }
    return false;
  }

  async function sync(): Promise<void> {
    if (syncing) {
      syncAgain = true;
      return;
    }
    syncing = true;
    try {
      do {
        syncAgain = false;
        while (connected && !disposed && (await syncOnce())) {
          // each pass makes one call; stop when the joined state matches the wanted one
        }
      } while (syncAgain && connected && !disposed);
    } finally {
      syncing = false;
    }
  }

  function lost() {
    if (!connected || disposed) return;
    connected = false;
    epoch += 1;
    joinedProject = undefined;
    joinedScreen = undefined;
    deps.onReset();
    setState('reconnecting');
    schedule(0);
  }

  function schedule(attempt: number) {
    clearTimeout(retry);
    if (disposed) return;
    retry = setTimeout(() => void open(attempt), delays[Math.min(attempt, delays.length - 1)]);
  }

  async function open(attempt: number): Promise<void> {
    const live = connection;
    if (live === undefined || disposed || connected || starting) return;
    starting = true;
    try {
      await live.start();
    } catch (error) {
      starting = false;
      if (disposed) return;
      attemptFailed = true;
      if (isSessionRefusal(error)) {
        // The session is over (the API client redirects) or the refusal is something else (Origin, proxy): then keep
        // trying on the longest back-off, offline.
        setState('offline');
        if (await endIfSessionOver()) return;
        if (!disposed) schedule(delays.length - 1);
        return;
      }
      setState(everConnected ? 'reconnecting' : 'offline');
      schedule(attempt + 1);
      return;
    }
    starting = false;
    if (disposed) {
      void live.stop();
      return;
    }
    connected = true;
    epoch += 1;
    refused = new Set();
    const reconnect = everConnected || attemptFailed;
    everConnected = true;
    attemptFailed = false;
    setState('live');
    await sync();
    if (reconnect && connected) deps.onReconnected(joinedProject);
  }

  function ensureConnection() {
    if (connection !== undefined || disposed) return;
    const created = deps.connect();
    connection = created;
    created.on(EVENT_METHOD, (event: unknown) => {
      if (
        !disposed &&
        wantedProject !== undefined &&
        isProjectEvent(event) &&
        event.projectId.toLowerCase() === wantedProject.toLowerCase()
      )
        deps.onEvent(event);
    });
    created.onclose(() => lost());
    setState('connecting');
    void open(0);
  }

  return {
    setProject(projectId) {
      if (projectId === wantedProject) return;
      wantedProject = projectId;
      refused = new Set();
      deps.onReset();
      if (connected) setState('live');
      if (projectId !== undefined) ensureConnection();
      void sync();
    },
    setScreen(screenId) {
      if (screenId === wantedScreen) return;
      wantedScreen = screenId;
      void sync();
    },
    retryNow() {
      if (connection === undefined || connected) return;
      clearTimeout(retry);
      void open(0);
    },
    dispose() {
      disposed = true;
      clearTimeout(retry);
      if (connection !== undefined) void connection.stop();
    },
  };
}
