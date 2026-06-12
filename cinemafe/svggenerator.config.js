/**
 * @ngneat/svg-generator config. Regenerate the CinemaAdmin icon set with:
 *   npm run generate-icons
 * Source SVGs live in projects/CinemaLib/src/svg; generated TS lands in
 * projects/CinemaLib/src/lib/svg (committed, exported via provideCinemaSvgIcons).
 */
module.exports = {
  outputPath: './projects/CinemaLib/src/lib/svg',
  srcPath: './projects/CinemaLib/src/svg',
  prefix: '',
  rootBarrelFile: true,
  svgoConfig: {
    plugins: [{ name: 'preset-default' }],
  },
};
