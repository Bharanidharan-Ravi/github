export const getOpportunityFields = (masters, values, context) => {
  // 🔹 Stage lookup (cross-master join)
  //   const stageMap = Object.fromEntries(
  //     (masters?.stagesList || []).map(s => [s.stepId, s])
  //   );

  return [
    /* =====================================================
       CUSTOMER (SEARCH BY CODE OR NAME)
       ===================================================== */
    {
      name: "customerCode",
      label: "Search by Customer Code or Name", // 🔥 UPDATED LABEL
      type: "select",
      ui: "mui",
      required: true,
      clearable: true,
      selectMode: "single",      // "single" | "multi"
      allowTyping: false,     // free text search (not limited to options)
      /*
        One dropdown
        - searchable by code
        - searchable by name
      */
      options: (masters?.customerDetails || []).map(c => {
        // const stage = stageMap[c.stageId];
        // console.log("masters :", masters);

        return {
          label: `${c.customerCode} - ${c.customerName}`, // 🔍 searchable
          value: c.customerCode,

          meta: {
            salesEmployee: { label: c.slpName, value: c.slpCode },
            contactPerson: { label: c.contactPerson, value: c.contactPersonCode },
            contactPersonName: { label: c.contactPerson, value: c.contactPersonCode },
            followUpStage: { label: c.stages, value: c.stageId }
            // customerStageName: stage?.descript
          }
        };
      }),

      /*
        Effects still work
        (customerName removed completely)
      */
      effects: {
        salesEmployee: "meta.salesEmployee",
        contactPerson: "meta.contactPerson",
        customerStageId: "meta.customerStageId",
        followUpStage: "meta.followUpStage"
      },

      clearContext: false
    },

    /* =====================================================
       SALES PERSON (DERIVED / OVERRIDABLE)
       ===================================================== */
    {
      name: "salesEmployee",
      label: "Sales Person",
      type: "select",
      ui: "mui",
      required: true,
 selectMode: "single",      // "single" | "multi"
      allowTyping: false,  
      options: (masters?.salesPersonDetails || []).map(s => ({
        label: `${s.slpCode} - ${s.slpName}`,
        value: s.slpCode,
        meta: {
          salesPersonName: s.slpName
        }
      })),

      effects: {
        salesEmployeeName: "meta.salesPersonName"
      }
    },

    /* =====================================================
       CONTACT PERSON (FILTERED BY CUSTOMER CONTEXT)
       ===================================================== */
    {
      name: "contactPerson",
      label: "Contact Person",
      type: "select",
      ui: "mui",
 selectMode: "single",      // "single" | "multi"
      allowTyping: false,  
      optionsResolver: ({ masters, context }) => {
        if (!context.customerCode) return [];

        return (masters?.contactPersonDetails || [])
          .filter(c => c.customerCode === context.customerCode)
          .map(c => ({
            label: c.contact_Person,
            value: c.contact_Person_Code,
            meta: {
              contactPersonName: c.contact_Person
            }
          }));
      },

      effects: {
        contactPersonName: "meta.contactPersonName"
      }
    },

    /* =====================================================
       POTENTIAL AMOUNT
       ===================================================== */
    {
      name: "PotentialAmount",
      label: "Potential Amount",
      type: "text",
      ui: "mui",
      required: true,

      validation: {
        type: "number",
        format: "decimal(10,2)"
      }
    },

    /* =====================================================
       FOLLOW UP STAGE (>= CUSTOMER STAGE)
       ===================================================== */
    {
      name: "followUpStage",
      label: "Follow Up Stage",
      type: "select",
      ui: "mui",
      required: true,
      clearable: false,
 selectMode: "single",      // "single" | "multi"
      allowTyping: false,  
      optionsResolver: ({ masters, context }) => {
        if (!context.customerStageId) return [];

        return (masters?.stagesList || [])
          .filter(s => s.stepId >= context.customerStageId)
          .map(s => ({
            label: s.descript,
            value: s.stepId
          }));
      }
    },

    /* =====================================================
       PREDICTED CLOSING DATE
       ===================================================== */
    {
      name: "PredicatedClosingDate",
      label: "Predicted Closing Date",
      type: "date",
      ui: "mui",
      required: true,

      validation: {
        type: "date",
        smartInput: true,
        allowPastDate: false
      }
    }
  ];
};
