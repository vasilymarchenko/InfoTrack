import type { Solicitor } from './types';

/** Replicates the API's branch-identity key used for deduplication. */
export function firmKey(s: Solicitor): string {
  return (normalise(s.firmName) + '|' + (s.postcode ?? s.phone ?? '')).toLowerCase();
}

function normalise(name: string): string {
  return name
    .toLowerCase()
    .replace(/[^a-z0-9 ]/g, '')
    .replace(/\band\b/g, 'and')
    .trim();
}
