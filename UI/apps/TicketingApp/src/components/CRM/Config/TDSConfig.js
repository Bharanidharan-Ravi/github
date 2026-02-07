export const getTdsFields = (vertical) => {
  const base = [
    { key: "partNo", label: "Part Number", type: "text" },
    { key: "partDesc", label: "Part Description", type: "text" },
    { key: "quantity", label: "Quantity", type: "text" }
  ];

  if (vertical === "Cylinder") {
    return [
      ...base,
      { key: "boreSize", label: "Bore Size", type: "text" },
      { key: "rodSize", label: "Rod Size", type: "text" },
      { key: "stroke", label: "Stroke", type: "text" }
    ];
  }

  return base;
};