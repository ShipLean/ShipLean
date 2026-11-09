// Hands the browser to the app: the one place this page leaves for a shiplean:// address. The URL always comes from
// the core's appRedirect.
export function openApp(url: string): void {
  window.location.assign(url);
}
