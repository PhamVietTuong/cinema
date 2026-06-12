import { EnvironmentProviders, Provider } from '@angular/core';
import { provideSvgIcons, provideSvgIconsConfig, SvgIconComponent } from '@ngneat/svg-icon';
import * as generated from './index';

export { SvgIconComponent };

/**
 * All icons generated from `projects/CinemaLib/src/svg` (see `index.ts`).
 * Add a new icon by dropping an .svg into that folder and running
 * `npm run generate-icons` — it is picked up here automatically.
 */
export const cinemaSvgIcons = Object.values(generated) as { name: string; data: string }[];

/** Standard size scale used across the Cinema apps. */
export const cinemaSvgSizes = {
  xs: '12px',
  sm: '16px',
  md: '20px',
  lg: '24px',
  xl: '32px',
  xxl: '48px',
};

/**
 * Registers the shared Cinema icon set and size config. Spread into an app's
 * providers: `providers: [...provideCinemaSvgIcons()]`. `<svg-icon>` itself is
 * available via `SharedModule`.
 */
export function provideCinemaSvgIcons(): (Provider | EnvironmentProviders)[] {
  return [
    provideSvgIcons(cinemaSvgIcons),
    provideSvgIconsConfig({ sizes: cinemaSvgSizes, defaultSize: 'md' }),
  ];
}
