// The only place a route path is written. Screens call router.push(links.x()) and never build a path by hand.
// Each builder returns expo-router's object form, so params stay typed when a route gains some.
export interface Link<P extends string> {
  pathname: P;
  params: Record<string, string>;
}

export const links = {
  signIn: (): Link<'/sign-in'> => ({ pathname: '/sign-in', params: {} }),
  projects: (): Link<'/'> => ({ pathname: '/', params: {} }),
  account: (): Link<'/account'> => ({ pathname: '/account', params: {} }),
};
