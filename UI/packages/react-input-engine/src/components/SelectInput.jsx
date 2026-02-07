import { useRef } from "react";
import { useEffect } from "react";
import { useState } from "react";
import { useMemo } from "react";

const SelectInput = ({ field, value, error, onChange }) => {
    const [open, setOpen] = useState(false);
    const [search, setSearch] = useState("");
    const ref = useRef(null);

    //   const options = field.options || [];

    // 🔒 Close on outside click
    useEffect(() => {
        const handleClickOutside = (e) => {
            if (ref.current && !ref.current.contains(e.target)) {
                setOpen(false);
                setSearch("");
            }
        };

        document.addEventListener("mousedown", handleClickOutside);
        return () =>
            document.removeEventListener("mousedown", handleClickOutside);
    }, []);


    const options = field.options || [];

    const filtered = useMemo(() => {
        if (!search) return options;
        return options.filter((o) =>
            o.label.toLowerCase().includes(search.toLowerCase())
        );
    }, [search, options]);

    const selectedLabel =
        options.find((o) => o.value === value)?.label || "";

    return (
        <div className="fe-field fe-search-select">
            <input
                value={open ? search : selectedLabel}
                placeholder={field.label}
                onFocus={() => {
                    setOpen(true);
                    setSearch("");
                }}
                onChange={(e) => {
                    setSearch(e.target.value);
                    setOpen(true);
                }}
            />

            {open && (
                <div className="fe-dropdown">
                    {filtered.length === 0 && (
                        <div className="fe-option muted">No results</div>
                    )}

                    {filtered.map((opt) => (
                        <div
                            key={opt.value}
                            className="fe-option"
                            onMouseDown={() => {
                                onChange(field.key, opt.value);
                                setOpen(false);
                            }}
                        >
                            {opt.label}
                        </div>
                    ))}
                </div>
            )}

            {error && <small className="fe-error">{error}</small>}
        </div>
    );
};

export default SelectInput;
