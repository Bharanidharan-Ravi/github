export function Pagination({
  variant,
  page,
  pageSize,
  total,
  pageSizeOptions,
  onChange
}) {
  const totalPages = Math.ceil(total / pageSize);

  if (totalPages <= 1) return null;
  console.log("variant :", variant);
  

  const prev = () => page > 1 && onChange(page - 1, pageSize);
  const next = () => page < totalPages && onChange(page + 1, pageSize);

  if (variant === "simple") {
    return (
      <div className="st-pagination">
        <button onClick={prev} disabled={page === 1}>Prev</button>
        <span>Page {page}</span>
        <button onClick={next} disabled={page === totalPages}>Next</button>
      </div>
    );
  }

  // ADVANCED
  return (
    <div className="st-pagination">
      <button onClick={prev} disabled={page === 1}>Prev</button>

      {Array.from({ length: totalPages }).map((_, i) => {
        const p = i + 1;
        return (
          <button
            key={p}
            className={p === page ? "active" : ""}
            onClick={() => onChange(p, pageSize)}
          >
            {p}
          </button>
        );
      })}

      <button onClick={next} disabled={page === totalPages}>Next</button>

      <select
        value={pageSize}
        onChange={e => onChange(1, Number(e.target.value))}
      >
        {pageSizeOptions.map(size => (
          <option key={size} value={size}>
            {size} / page
          </option>
        ))}
      </select>
    </div>
  );
}
