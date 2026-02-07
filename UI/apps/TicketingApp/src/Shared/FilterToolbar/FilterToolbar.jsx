import { useState, useRef, useEffect } from "react";
import {
  ChevronDown,
  Check,
  X,
  Tag,
  Folder,
  Filter,
  RefreshCcw,
  ArrowUpDown
} from "lucide-react";
import "./FilterToolbar.css";

export const filterEngine = (multiSelect, masterData, updateFilter, filterData) => {
  if (!masterData) return [];

  let filtered = [...masterData];

  const fixDate = (d) => {
    if (!d) return new Date(0);
    return new Date(d.replace(/(\.\d{3})\d+$/, "$1"));
  }
  Object.entries(updateFilter).forEach(([key, value]) => {
    if (key === "sort") return;
    const isMulti = multiSelect[key] === true;

    if (isMulti) {
      if (value.length > 0) {
        filtered = filtered.filter(item => value.includes(item[key]));
      }
      return;
    }

    if (!isMulti && value !== "all") {
      filtered = filtered.filter(item => item[key] === value);
    }

  });
  if (updateFilter?.sort) {
    const sortKey = updateFilter.sort;            // ex: "Newest"
    const config = filterData.sort[sortKey];      // ex: { field: "created_On", order: "desc" }

    if (config) {
      const { field, order } = config;

      filtered.sort((a, b) => {
        const valA = a[field];
        const valB = b[field];

        // If sorting by date
        const isDate = field.toLowerCase().includes("date") || field.toLowerCase().includes("created");

        const parsedA = isDate ? new Date(valA) : String(valA);
        const parsedB = isDate ? new Date(valB) : String(valB);

        if (order === "asc") return parsedA > parsedB ? 1 : -1;
        if (order === "desc") return parsedA < parsedB ? 1 : -1;

        return 0;
      });
    }
  }
  return filtered;
};


export function FilterToolbar({
  filterList = [],
  filterData = {},
  filters = {},
  onChange,
  masterData,
  multiSelect,
  setShowValue,
  parentdata
}) {
  const [openMenu, setOpenMenu] = useState(null);
  const toolbarRef = useRef();

  const toggleMenu = (key) => setOpenMenu((prev) => (prev === key ? null : key));


  const updateSingle = (key, value) => {
    const updated = { ...filters, [key]: value };
    onChange(updated);
    const result = filterEngine(multiSelect, masterData, updated, filterData);

    setShowValue(result);
    // onChange((prev) => ({ ...prev, [key]: value }));
    setOpenMenu(null);
  };

  const updateMulti = (key, value) => {
    const exists = Array.isArray(filters[key]) && filters[key].includes(value);
    const newValues = exists
      ? filters[key].filter((x) => x !== value)
      : [...(filters[key] || []), value];
    const updated = { ...filters, [key]: newValues };
    onChange(updated);
    // onChange((prev) => ({ ...prev, [key]: newValues }));
    const result = filterEngine(multiSelect, masterData, updated, filterData);
    setShowValue(result);
  }

  const resetFilters = () => {
    const reset = {};
    Object.keys(filters).forEach((k) => {
      reset[k] = Array.isArray(filters[k]) ? [] : "all";
    });
    // onChange(() => ({ ...reset }));
    onChange({ ...reset });
    setShowValue(parentdata);
    setOpenMenu(null);
  };

  useEffect(() => {
    const handleOutside = (event) => {
      if (toolbarRef.current && !toolbarRef.current.contains(event.target)) {
        setOpenMenu(null);
      }
    };
    document.addEventListener("mousedown", handleOutside);
    return () => document.removeEventListener("mousedown", handleOutside);
  }, []);

  const iconMap = {
    status: <Filter size={14} />,
    labels: <Tag size={14} />,
    project: <Folder size={14} />,
    sort: <ArrowUpDown size={14} />
  };

  const labelify = (key) => key.charAt(0).toUpperCase() + key.slice(1);

  const renderDropdown = (key) => {
    // const values = filterData[key] || [];
    const values = Array.isArray(filterData[key])
      ? filterData[key]                          // normal filter (status)
      : Object.keys(filterData[key]);            // sort filter (object)

    const isMulti = multiSelect[key] === true;
    const selectedValue = filters[key];

    const displayValue = isMulti
      ? selectedValue?.length === 0
        ? "None"
        : selectedValue?.join(",")
      : selectedValue;

    return (
      <div className="ft-dropdown" key={key}>
        <button
          className={`ft-btn ${openMenu === key ? "active" : ""}`}
          onClick={() => toggleMenu(key)}
          aria-expanded={openMenu === key}
          aria-haspopup="listbox"
        >
          <span className="ft-btn-label">
            {iconMap[key]}
            {/* <span className="ft-btn-label">{labelify(key)}:</span> */}
            {key.charAt(0).toUpperCase() + key.slice(1)}
            {/* <span className="ft-btn-value">{displayValue}</span> */}
          </span>
          <ChevronDown className={`ft-btn-arrow ${openMenu === key ? "open" : ""}`} />
        </button>

        <div className={`ft-menu ${openMenu === key ? "ft-menu-open" : "ft-menu-closed"}`}
          role="listbox">
          {values.length === 0 && (
            <div className="ft-menu-item no-items">No options</div>
          )}
          {values.map((opt) => {
            const selected = isMulti ? selectedValue?.includes(opt) : selectedValue === opt;
            return (
              <div
                key={opt}
                role="option"
                // aria-selected={selected}
                className={`ft-menu-item ${selected ? "selected" : ""}`}
                onClick={() => (isMulti ? updateMulti(key, opt) : updateSingle(key, opt))}
              >
                {/* {selected && <Check size={14} className="ft-check" />} */}
                {isMulti && (
                  <input type="checkbox"
                    checked={selected}
                    readOnly
                    className="ft-checkbox" />
                )}
                {!isMulti && selected && <Check size={14} className="ft-check" />}
                <span className="ft-item-text">{opt}</span>
              </div>
            );
          })}
        </div>
      </div>
    );
  };

  const hasActiveFilters = Object.entries(filters).some(([k, v]) =>
    Array.isArray(v) ? v.length > 0 : v !== "all");

  // console.log("filters :", filters, filterData, filterList);

  return (
    <div className="ft-toolbar-wrap">
      <div className="ft-toolbar" ref={toolbarRef}>
        <div className="ft-left">
          {filterList.map((key) => renderDropdown(key))}
        </div>

        <div className="ft-actions">
          <button
            className="ft-reset-btn"
            title="Reset filters"
            onClick={resetFilters}
            aria-label="Reset filters"
          >
            <RefreshCcw size={14} /> Reset
          </button>
        </div>
      </div>
      <div className={`ft-chips-warp ${hasActiveFilters ? "visible" : "hidden"}`}>
        <div className="ft-chips">
          {Object.entries(filters).map(([key, value]) => {
            if (Array.isArray(value)) {
              return value.map((v) => (
                <div className="ft-chip" key={key + v}>
                  <span className="ft-chip-text">
                    {labelify(key)}: {v}
                  </span>
                  <X size={14}
                    className="ft-chip-x"
                    onClick={() => updateMulti(key, v)} />
                </div>
              ));
            }
            if (value !== "all") {
              return (
                <div className="ft-chip" key={key + value}>
                  <span className="ft-chip-text">
                    {labelify(key)}: {value}
                  </span>
                  <X
                    size={14}
                    className="ft-chip-x"
                    onClick={() => updateSingle(key, "all")}
                  />
                </div>
              );
            }
            return null;
          })}
        </div>
      </div>
    </div>
  )
}
