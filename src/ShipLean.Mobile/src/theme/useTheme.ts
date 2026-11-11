import { useColorScheme } from 'react-native';
import { themes, type Theme } from './themes';

// Follows the device's colour scheme; light when the device doesn't say.
export function useTheme(): Theme {
  return themes[useColorScheme() === 'dark' ? 'dark' : 'light'];
}
