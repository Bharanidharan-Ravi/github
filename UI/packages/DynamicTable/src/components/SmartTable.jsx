import { useEditState } from '../hooks/useEditState';
import { useSelection } from '../hooks/useSelection';
import { useResponsive } from '../hooks/useResponsive';
import { warnDuplicateColumnKeys } from '../utils/devWarnings';
import { TableRow } from './TableRow';
import { TableHeader } from './TableHeader';
import { applyFilters, applySort, paginate } from '../utils/dataEngine';
import { Pagination } from './Pagination';
import { useEffect } from 'react';

export function SmartTable({
  columns,
  data,
  rowKey,
  editableMode = 'none',
  permissions,
  validation,
  sorting,
  filters,
  pagination,
  exportConfig,
  selectable,
  selectedRows,
  defaultSelectedRows,
  onSelectChange,
  onChange,
  onEditSuccess,
  onDelete,
  state = 'ready',
  actions,
  engine,
  onServerChange
}) {
  warnDuplicateColumnKeys(columns);

  const { isEditing, startEdit } = useEditState();
  const { selected, toggle, toggleAll } = useSelection({
    selectedRows,
    defaultSelectedRows,
    onChange: onSelectChange
  });
  let processed = data;

  if (engine === "client") {
    processed = applyFilters(processed, filters?.values, columns);
    processed = applySort(processed, sorting?.state, columns);
  }

  const totalRows = processed.length;

  if (engine === "client" && pagination?.enabled) {
    processed = paginate(
      processed,
      pagination.page,
      pagination.pageSize
    );
  }


  // SERVER MODE
  useEffect(() => {
    if (engine === "server") {
      onServerChange?.({
        page: pagination.page,
        pageSize: pagination.pageSize,
        sort: sorting?.state,
        filters: filters?.values
      });
    }
  }, [
    engine,
    pagination?.page,
    pagination?.pageSize,
    sorting?.state,
    filters?.values
  ]);

  console.log("pagination :", pagination, pagination?.enabled, processed);

  const isMobile = useResponsive();

  const getId = (row) =>
    typeof rowKey === 'function' ? rowKey(row) : row[rowKey];

  if (state === 'loading') return <div>Loading…</div>;
  if (state === 'error') return <div>Error loading data</div>;
  if (!data.length) return <div>No data</div>;

  return (
    <div>
      <table>
        <TableHeader
          columns={columns}
          sorting={sorting}
          filters={filters}
          selectable={selectable}
          // allSelected={selected.length === data.length}
          allSelected={
            processed.length > 0 &&
            processed.every(row => selected.includes(getId(row)))
          }
          onSelectAll={() =>
            toggleAll(data.map(getId))
          }
        />

        <tbody>
          {processed.map((row, index) => {
            const id = getId(row);
            const canEdit =
              permissions?.canEdit?.(row) ?? true;

            return (
              <TableRow
                key={id}
                row={row}
                rowId={id}
                columns={columns}
                isEditing={editableMode !== 'none' && isEditing(id)}
                canEdit={canEdit}
                selectable={selectable}
                selected={selected.includes(id)}
                onSelect={() => toggle(id)}
                onEdit={() => startEdit(id)}
                onDelete={() => onDelete?.(row)}
                onChange={(key, value) =>
                  onChange(row, key, value, index)
                }
                errors={validation?.errors}
                actions={actions}
              />
            );
          })}
        </tbody>
      </table>

      {/* {pagination?.enabled && ( */}
      <Pagination
        {...pagination}
        total={totalRows}
      />
      {/* )}   */}
    </div>
  );
}
