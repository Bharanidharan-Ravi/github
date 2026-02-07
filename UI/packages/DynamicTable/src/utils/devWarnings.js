export function warnDuplicateColumnKeys(columns) {
  if (process.env.NODE_ENV === 'production') return;

  const keys = columns.map(c => c.key);
  const duplicates = keys.filter((k, i) => keys.indexOf(k) !== i);

  if (duplicates.length) {
    console.warn(
      '[SmartTable] Duplicate column keys detected:',
      duplicates
    );
  }
}
