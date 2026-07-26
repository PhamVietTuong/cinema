import { defineConfig } from 'vitest/config';

// Runner config shared by all three projects' `test` targets (wired via `runnerConfig` in
// angular.json). The Angular unit-test builder supplies the environment, transforms and virtual
// entry points; this only overrides pool behaviour.
export default defineConfig({
  test: {
    // The default `forks` pool fails to hand off to its worker on Windows ("Timeout waiting for
    // worker to respond"). Threads start reliably on both Windows and the Linux CI runner.
    pool: 'threads',
  },
});
