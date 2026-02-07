export function mergeByIndex(data, index, updatedRow) {
  const copy = [...data];
  copy[index] = { ...copy[index], ...updatedRow };
  return copy;
}
