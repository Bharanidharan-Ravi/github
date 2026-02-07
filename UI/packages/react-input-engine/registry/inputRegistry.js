// registry/inputRegistry.js

import HtmlDateInput from "../adapters/html/HtmlDateInput";
import HtmlSelectInput from "../adapters/html/HtmlSelectInput";
import HtmlTextInput from "../adapters/html/HtmlTextInput";
import HtmlTimeInput from "../adapters/html/HtmlTimeInput";
import MuiDateInput from "../adapters/mui/MuiDateInput";
import MuiSelectInput from "../adapters/mui/MuiSelectInput";
import MuiTextInput from "../adapters/mui/MuiTextInput";
import MuiTimeInput from "../adapters/mui/MuiTimeInput";

export const inputRegistry = {
  mui: {
    text: MuiTextInput,
    select: MuiSelectInput,
    date: MuiDateInput,
    time: MuiTimeInput
  },
  html: {
    text: HtmlTextInput,
    select: HtmlSelectInput,
    date: HtmlDateInput,
    time: HtmlTimeInput
  }
};