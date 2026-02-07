import { FiSearch } from "react-icons/fi";
import "./searchDiv.css";

const SearchDiv = ({searchValue, openModal,parentData, placeholder, onChange,searcFields, onSearchResult, masterData}) => {
  const handleSearch = (value) => {
    onChange(value);

    if(!value || value.trim() === "") {
      onSearchResult(parentData);
      return;
    }

    const lower = value.toLowerCase();

    const filtered = parentData.filter(item => 
      searcFields.some(field =>
        String(item[field])?.toLowerCase().includes(lower)
      )
    );

    onSearchResult(filtered);
  }
    return (
        <div className="search-div">
            <h2>{placeholder}</h2>
            <div className="search-btn-div">
                 <div className="searchbar-wrapper">
                      <div className="searchbar-container">
                        <span className="searchbar-icon">
                          <FiSearch size={20} className="icon" />
                        </span>
                        <input
                          type="text"
                          className="searchbar-input"
                          placeholder={`Search ${placeholder}....`}
                          value={searchValue}
                          onChange={(e) => handleSearch(e.target.value)}
                        //   autoFocus={autoFocus}
                        />
                      </div>
                    </div>
                <button onClick={openModal} className="new-ticket-btn" >Create {placeholder}</button>
            </div>
        </div>
    );
};

export default SearchDiv;