export function applyFilters(data, filters, columns) {
  if (!filters) return data;

  return data.filter(row =>
    Object.entries(filters).every(([key, value]) => {
      if (!value) return true;

      const col = columns.find(c => c.key === key);
      const accessor = col?.filterAccessor || (r => r[key]);

      return String(accessor(row))
        .toLowerCase()
        .includes(String(value).toLowerCase());
    })
  );
}

export function applySort(data, sort, columns) {
  console.log("applySort", data, sort, columns);
  
  if (!sort?.key) return data;

  const col = columns.find(c => c.key === sort.key);
  const accessor = col?.sortAccessor || (r => r[sort.key]);

  return [...data].sort((a, b) => {
    const aVal = accessor(a);
    const bVal = accessor(b);

    if (aVal > bVal) return sort.order === "asc" ? 1 : -1;
    if (aVal < bVal) return sort.order === "asc" ? -1 : 1;
    return 0;
  });
}

export function paginate(data, page, pageSize) {
  const start = (page - 1) * pageSize;
  return data.slice(start, start + pageSize);
}
