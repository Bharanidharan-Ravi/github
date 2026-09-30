using System;
using System.Collections.Generic;

namespace APIGateWay.ModalLayer.PostData
{
   

    // One issue edited inline in the Issue Logger (PostWorkStreamDto.IssueLogUpdates).
    // The IssueLog row is never changed: the edit becomes a new ISSUEACTIVITY
    // row for this issue. Status and Priority are the issue's values after the
    // edit (blank keeps the current one); Remarks is the note for this update.
    public class BulkIssueLogUpdateItemDto
    {
        // IssueLog.IssueLogId: the issue's number within the ticket.
        public int IssueLogId { get; set; }
        public string? Status { get; set; }
        public string? Priority { get; set; }
        public string? Remarks { get; set; }

        // Files added with this edit: <img> / file-attachment links pointing at
        // the temp uploads in temp. They are copied to permanent storage and the
        // links rewritten before being saved on the ISSUEACTIVITY row.
        public string? AttachmentsHtml { get; set; }
        public TempReturn? temp { get; set; }
    }

   
}
