import { TableCell } from './TableCell';

export function TableRow({
  row,
  rowId,
  columns,
  isEditing,
  canEdit,
  onEdit,
  onDelete,
  onChange,
  selectable,
  selected,
  onSelect,
  errors,
  actions
}) {
  return (
    <tr>
      {selectable && (
        <td>
          <input
            type="checkbox"
            checked={selected}
            onChange={onSelect}
          />
        </td>
      )}

      {columns.map(col => (
        <TableCell
          key={col.key}
          column={col}
          row={row}
          isEditing={isEditing && canEdit}
          error={errors?.[`${rowId}.${col.key}`]}
          onChange={onChange}
        />
      ))}

      {actions && (
        <td>
          {actions.edit && canEdit && (
            <button onClick={onEdit}>Edit</button>
          )}
          {actions.delete && (
            <button onClick={onDelete}>Delete</button>
          )}
          {actions.custom?.(row)}
        </td>
      )}
    </tr>
  );
}
