using System;
using System.Collections.Generic;

namespace APIGateWay.ModalLayer.PostData
{
   
    public class BulkIssueLogItemDto
    {
        // Rich-text issue description (same content the single-issue
        // CommentText/IssueLog_Description field used to carry).
        public string? Description { get; set; }
        public string? Status { get; set; }
        public string? Priority { get; set; }
        public string? Remarks { get; set; }

        // Time spent on this issue, "HH:MM" (stored as decimal hours in ISSUEACTIVITY).
        public string? Hours { get; set; }

        // Attachments pasted/uploaded into this specific issue's editor
        // (each staged issue has its own temp file batch).
        public TempReturn? temp { get; set; }
    }

    
}
