import React,{ useState } from 'react';

export function useSelection({
  selectedRows,
  defaultSelectedRows = [],
  onChange
}) {
  const [internal, setInternal] = useState(defaultSelectedRows);
  const selected = selectedRows ?? internal;

  const update = (next) => {
    if (onChange) onChange(next);
    else setInternal(next);
  };

  const toggle = (id) =>
    update(
      selected.includes(id)
        ? selected.filter(x => x !== id)
        : [...selected, id]
    );

  const toggleAll = (ids) =>
    update(selected.length === ids.length ? [] : ids);

  return { selected, toggle, toggleAll };
}
