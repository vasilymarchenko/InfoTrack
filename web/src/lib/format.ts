export function formatDate(iso: string): string {
  return new Date(iso).toLocaleString('en-GB', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  });
}

export function formatPercent(n: number): string {
  return `${n.toFixed(1)}%`;
}

export function formatCount(n: number): string {
  return Math.round(n).toLocaleString('en-GB');
}

/** Returns "—" for null/undefined/NaN values. */
export function dash(v: string | number | null | undefined): string {
  if (v === null || v === undefined) return '—';
  if (typeof v === 'number' && isNaN(v)) return '—';
  return String(v);
}
