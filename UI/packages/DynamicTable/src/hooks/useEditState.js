import React,{ useState } from "react";

export function useEditState() {
  const [editingRowId, setEditingRowId] = useState(null);

  return {
    isEditing: (id) => id === editingRowId,
    startEdit: setEditingRowId,
    stopEdit: () => setEditingRowId(null)
  };
}
