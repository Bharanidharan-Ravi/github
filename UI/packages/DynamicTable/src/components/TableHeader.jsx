export function TableHeader({
  columns,
  sorting,
  filters,
  selectable,
  allSelected,
  onSelectAll
}) {
  // const sorting = () => {
  //   processed = applySort(processed, sorting?.state, columns);
  // };
  return (
    <thead>
      <tr>
        {selectable && (
          <th>
            <input
              type="checkbox"
              checked={allSelected}
              onChange={onSelectAll}
            />
          </th>
        )}

        {columns.map(col => (
          <th key={col.key}>
            {col.label}

            {sorting?.enabled && col.sortable && (
              <button
                onClick={() => {
                  const current = sorting.state;
                  const nextOrder =
                    current?.key === col.key && current.order === "asc"
                      ? "desc"
                      : "asc";

                  sorting.onChange({
                    key: col.key,
                    order: nextOrder
                  });
                }}
              >
                ⇅
              </button>
            )}

            {filters?.enabled && col.filterable && (
              <input
                placeholder="Filter"
                onChange={e =>
                  filters.onChange({
                    ...filters.values,
                    [col.key]: e.target.value
                  })
                }
              />
            )}
          </th>
        ))}

        <th>Actions</th>
      </tr>
    </thead>
  );
}
