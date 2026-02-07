export function TableCell({
  column,
  row,
  isEditing,
  error,
  onChange
}) {
  const value = row[column.key];

  if (column.render) {
    return <td>{column.render(row)}</td>;
  }

  if (isEditing && column.editable) {
    if (column.type === 'select') {
      return (
        <td>
          <select
            value={value}
            onChange={(e) =>
              onChange(column.key, e.target.value)
            }
          >
            {column.options?.map(opt => (
              <option key={opt.value} value={opt.value}>
                {opt.label}
              </option>
            ))}
          </select>
          {error && <div className="st-error">{error}</div>}
        </td>
      );
    }

    return (
      <td>
        <input
          value={value ?? ''}
          onChange={(e) =>
            onChange(column.key, e.target.value)
          }
        />
        {error && <div className="st-error">{error}</div>}
      </td>
    );
  }

  return <td>{value ?? '-'}</td>;
}
