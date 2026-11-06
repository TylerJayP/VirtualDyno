const whole = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });
const one = new Intl.NumberFormat('en-US', { minimumFractionDigits: 1, maximumFractionDigits: 1 });

export const fmt = {
  whole: (n: number) => whole.format(n),
  one: (n: number) => one.format(n),
  rpm: (n: number) => whole.format(Math.round(n / 10) * 10),
  ordinal: (n: number) => (n === 1 ? '1st' : n === 2 ? '2nd' : n === 3 ? '3rd' : `${n}th`),
  date: (iso: string) => new Date(iso).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' }),
  correction: (c: string) => (c === 'Sae' ? 'SAE' : c === 'Std' ? 'STD' : 'Uncorrected'),
};

/** Where a number came from, in words a user understands. */
export function sourceLabel(source: string): string {
  switch (source) {
    case 'User':
      return 'you entered';
    case 'Log':
      return 'from the log';
    case 'Catalog':
      return 'factory spec';
    case 'Measured':
      return 'measured from the log';
    default:
      return 'assumed';
  }
}
