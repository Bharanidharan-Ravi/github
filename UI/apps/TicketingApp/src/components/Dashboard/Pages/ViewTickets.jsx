import { useEffect, useRef, useState } from "react";
import { useDashboardContext } from "../Context/DashboardContext.jsx";
import { useAppStore } from "../../../store/appStore.js";
// import { SmartTable } from '@marvick/smart-table';
import { SmartTable } from "shared-table";

const columns = [
  {
    key: "id",
    label: "ID"
  },
  {
    key: "title",
    label: "Title",
    editable: true,
    type: "input"
  },
  {
    key: "status",
    label: "Status",
    editable: true,
    type: "select",
    options: [
      { label: "Open", value: "Open" },
      { label: "In Progress", value: "In Progress" },
      { label: "Closed", value: "Closed" }
    ],
    filterable: true,
    sortable: true
  },
  {
    key: "priority",
    label: "Priority",
    editable: true,
    type: "select",
    options: [
      { label: "Low", value: "Low" },
      { label: "Medium", value: "Medium" },
      { label: "High", value: "High" }
    ],
    filterable: true,
    sortable: true,
    //  sortAccessor: row => row.status,
    // filterAccessor: row => row.status
  },
  {
    key: "createdAt",
    label: "Created At",
    sortable: true,
    render: (row) =>
      new Date(row.createdAt).toLocaleDateString()
  }
];
const statuses = ["Open", "In Progress", "Closed"];
const priorities = ["Low", "Medium", "High"];
const assignees = ["Ravi", "Kumar", "Anita", "John", "Meera"];

const initialData = Array.from({ length: 100 }, (_, i) => ({
  id: i + 1,
  title: `Issue #${i + 1}`,
  description: `This is a detailed description for issue ${i + 1}`,
  status: statuses[i % statuses.length],
  priority: priorities[i % priorities.length],
  assignee: assignees[i % assignees.length],
  estimateHours: (i % 8) + 1,
  createdAt: new Date(
    2025,
    0,
    (i % 28) + 1,
    10,
    30
  ).toISOString()
}));


const Dashboard = () => {
  const [data, setData] = useState(initialData);
  const [selectedRows, setSelectedRows] = useState([]);
  const [errors, setErrors] = useState({});
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(2);
  const [sort, setSort] = useState(null);
  const [filters, setFilters] = useState({});

  // const handleSortChange = (key, order) => {
  //   const sortedData = [...data].sort((a, b) => {
  //     if (a[key] < b[key]) return order === "asc" ? -1 : 1;
  //     if (a[key] > b[key]) return order === "asc" ? 1 : -1;
  //     return 0;
  //   });
  //   setData(sortedData);
  // }

  return (
    <SmartTable
      columns={columns}
      data={data}
      rowKey="id"
      engine="client"
      /* Editing */
      editableMode="row"
      onChange={(row, key, value, index) => {
        setData(prev => {
          const copy = [...prev];
          copy[index] = { ...copy[index], [key]: value };
          return copy;
        });
      }}

      /* Validation */
      validation={{
        errors
      }}

      /* Selection */
      selectable
      selectedRows={selectedRows}
      onSelectChange={setSelectedRows}

      /* Sorting */
         sorting={{
        enabled: true,
        state: sort,
        onChange: setSort
      }}

      filters={{
        enabled: true,
        values: filters,
        onChange: setFilters
      }}

      pagination={{
        enabled: true,
        variant: "advanced",   // or "simple"
        page,
        pageSize,
        pageSizeOptions: [10, 20, 50],
        total: data.length,
        onChange: (p, ps) => {
          setPage(p);
          setPageSize(ps);
        }
      }}

      /* Permissions */
      permissions={{
        canEdit: (row) => row.status !== "Closed",
        canDelete: (row) => row.status !== "Closed"
      }}

      /* Actions */
      actions={{
        edit: true,
        delete: true,
        custom: (row) => (
          <button onClick={() => alert(row.title)}>
            View
          </button>
        )
      }}

      /* States */
      state={data.length ? "ready" : "empty"}
    />
  );
};

export default Dashboard;