using APIGateWay.Business_Layer.Helper;
using APIGateWay.BusinessLayer.Interface;
using APIGateWay.BusinessLayer.SignalRHub;
using APIGateWay.DomainLayer.CommonSevice;
using APIGateWay.DomainLayer.DBContext;
using APIGateWay.DomainLayer.Helpers;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.DomainLayer.Service;
using APIGateWay.ModalLayer.GETData;
using APIGateWay.ModalLayer.Hub;
using APIGateWay.ModalLayer.MasterData;
using APIGateWay.ModalLayer.PostData;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using ReverseMarkdown.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.BusinessLayer.Repository
{
    public class TicketRepo : ITicketRepo
    {
        private readonly IDomainService _domainService;
        private readonly APIGateWayCommonService _commonService;
        private readonly IMapper _mapper;
        private readonly ILoginContextService _loginContext;
        private readonly IAttachmentService _attachmentService;
        private readonly IHelperGetData _helperGet;
        private readonly IRealtimeNotifier _realtimeNotifier;
        private readonly ISyncExecutionService _syncExecutionService;
        private readonly IWorkStreamService _workStreamService;
        private readonly APIGatewayDBContext _db;

        public TicketRepo(
            IDomainService domainService,
            APIGateWayCommonService service,
            IMapper mapper,
            ILoginContextService loginContext,
            IAttachmentService attachmentService,
            IHelperGetData helperGet,
            IRealtimeNotifier realtimeNotifier,
            ISyncExecutionService syncExecutionService,
            IWorkStreamService workStreamService,
            APIGatewayDBContext dBContext)
        {
            _domainService = domainService;
            _commonService = service;
            _mapper = mapper;
            _loginContext = loginContext;
            _attachmentService = attachmentService;
            _helperGet = helperGet;
            _realtimeNotifier = realtimeNotifier;
            _syncExecutionService = syncExecutionService;  
            _workStreamService = workStreamService;
            _db = dBContext;
        }

        // ─────────────────────────────────────────────────────────────────────
        // CREATE — your original code, unchanged
        // ─────────────────────────────────────────────────────────────────────
        public async Task<GetTickets> CreateTicketAsync(PostTicketDto ticketDto)
        {
            ProcessedAttachmentResult attachmentResult = null;
            GetTickets finalTicketData = null;
            ProjectKeysDto projectKey = null;

            try
            {
                finalTicketData = await _domainService.ExecuteInTransactionAsync(async () =>
                {
                    var ticketMaster = _mapper.Map<TicketMaster>(ticketDto);
                    ticketMaster.Issue_Id = Guid.NewGuid();
                    ticketMaster.Status = 1;

                    if (!ticketDto.RepoId.HasValue)
                        throw new Exception("Repo_Id is required to create a Ticket.");

                    projectKey = await _helperGet.GetProjectByIdAsync(ticketDto.Project_Id.Value);
                    var seq = await _commonService.GetNextSequenceAsync(projectKey.RepoKey, "Tickets", "IssueMaster");
                    ticketMaster.SiNo = seq.CurrentValue;
                    ticketMaster.Issue_Code = $"T{seq.ColumnValue}";
                    ticketMaster.RepoKey = projectKey.RepoKey;
                    ticketMaster.ProjKey = projectKey.ProjectKey;
                    string finalHtmlDescription = ticketDto.Description;

                    if (ticketDto.temp?.temps != null && ticketDto.temp.temps.Any())
                    {
                        var permUserId = $"{_loginContext.userId}-{_loginContext.userName}";
                        var permFolder = $"{ticketMaster.Issue_Code}-{ticketDto.Title}";
                        var relativePath = $"{permUserId}/{permFolder}";

                        attachmentResult = await _attachmentService.ProcessAndCopyAttachmentsAsync(
                            ticketDto.Description,
                            ticketDto.temp.temps,
                            relativePath,
                            ticketMaster.Issue_Id.ToString(),
                            "TicketMaster");

                        finalHtmlDescription = attachmentResult.UpdatedHtml;
                    }

                    ticketMaster.HtmlDesc = finalHtmlDescription;
                    ticketMaster.Description = HtmlUtilities.ConvertToPlainText(finalHtmlDescription);

                    await _domainService.SaveEntityWithAttachmentsAsync(ticketMaster, attachmentResult?.Attachments);

                    if (ticketDto.labelId != null && ticketDto.labelId.Any())
                    {
                        var issueLabels = ticketDto.labelId.Select(l => new IssueLabel
                        {
                            Issue_Id = ticketMaster.Issue_Id,
                            Label_Id = l.Id
                        }).ToList();
                        await _domainService.SaveLabelAsync(issueLabels);
                    }


                    // ── Save WorkStreams — one row per assignee ────────────────
                    // Mirrors the labels pattern: loop + insert per resource
                    // ResourceIds null/empty → no WorkStream rows created yet
                    // (assignees can be added later via ticket update)
                    var createResourceIds = ticketDto.resourceIds?
                  .Where(r => r.Id.HasValue)
                  .Select(r => r.Id!.Value)
                  .ToList();

                    if (createResourceIds != null && createResourceIds.Any())
                    {
                        foreach (var resourceId in createResourceIds)
                        {
                            await _workStreamService.UpsertWorkStreamsAsync(
                                new WorkStreamContext
                                {
                                    IssueId = ticketMaster.Issue_Id,

                                    // Pass the single ID from the loop
                                    ResourceId = resourceId,

                                    StreamStatus = null,
                                    CompletionPct = 0,
                                    TargetDate = ticketDto.TargetDate
                                }
                            );
                        }
                    }

                    if (ticketDto.temp?.temps != null && ticketDto.temp.temps.Any())
                        await _attachmentService.CleanupTempFiles(ticketDto.temp);

                    return _mapper.Map<GetTickets>(ticketMaster);
                });
            }
            catch (Exception ex)
            {
                if (attachmentResult?.PermanentFilePathsCreated?.Any() == true)
                    _attachmentService.RollbackPhysicalFiles(attachmentResult.PermanentFilePathsCreated);

                throw new Exception($"Ticket creation failed. Everything was rolled back safely.{ex}", ex);
            }

            var richTicketData = await _syncExecutionService.FetchRichDataAsync<GetTickets>(

               configKey: "TicketsList",
               syncParams: new Dictionary<string, string> { { "IssueId", finalTicketData.Issue_Id.ToString() } },
               matchPredicate: p => p.Issue_Id == finalTicketData.Issue_Id,
               fallbackData: finalTicketData,
               lastSync: null // Optional: pass DateTimeOffset if your SP requires it
           );

            if (richTicketData != null)
            {
                try
                {
                    await _realtimeNotifier.BroadcastAsync(new RealtimeMessage
                    {
                        Entity = "Ticket",
                        Action = "Create",
                        Payload = richTicketData,
                        KeyField = "Issue_Id",
                        RepoKey = richTicketData.RepoKey,
                        Timestamp = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to broadcast Ticket creation: {ex.Message}");
                }
            }

            return richTicketData;
        }

        // ─────────────────────────────────────────────────────────────────────
        // FULL UPDATE
        // PUT /api/ticket/{id}
        //
        // What changes:    Title, HtmlDesc, Description, Priority, AssigneeId,
        //                  DueDate, Status (optional), Labels (full replace),
        //                  new attachments added
        // What NEVER changes: Issue_Id, Issue_Code, SiNo, RepoId, CreatedAt, CreatedBy
        // Auto-updated:    UpdatedAt, UpdatedBy — DBContext audit handles this
        //
        // Labels are FULLY REPLACED — old IssueLabel rows deleted, new ones inserted.
        // This is the same transaction so rollback covers everything.
        // ─────────────────────────────────────────────────────────────────────

        public async Task<GetTickets> UpdateTicketAsync(Guid ticketId, UpdateTicketDto dto)
        {
            ProcessedAttachmentResult attachmentResult = null;
            GetTickets finalTicketData = null;

            try
            {
                finalTicketData = await _domainService.ExecuteInTransactionAsync(async () =>
                {
                    string finalHtmlDescription = dto.Description ?? string.Empty;

                    // Process new attachments if uploaded
                    if (dto.temp?.temps != null && dto.temp.temps.Any())
                    {
                        var permUserId = $"{_loginContext.userId}-{_loginContext.userName}";
                        var relativePath = $"{permUserId}/{ticketId}";

                        attachmentResult = await _attachmentService.ProcessAndCopyAttachmentsAsync(
                            dto.Description,
                            dto.temp.temps,
                            relativePath,
                            ticketId.ToString(),
                            "TicketMaster");

                        finalHtmlDescription = attachmentResult.UpdatedHtml;
                    }

                    var capturedHtml = finalHtmlDescription;

                    var updatedTicket = await _domainService.UpdateEntityWithAttachmentsAsync<TicketMaster>(
                        ticketId,
                        entity =>
                        {
                            entity.Title = dto.Title;
                            entity.HtmlDesc = capturedHtml;
                            entity.Description = HtmlUtilities.ConvertToPlainText(capturedHtml);
                            entity.Priority = dto.Priority;

                            if (dto.Assignee_Id.HasValue)
                                entity.Assignee_Id = dto.Assignee_Id.Value;

                            if (dto.Due_Date.HasValue)
                                entity.Due_Date = dto.Due_Date.Value;

                            if (dto.Status.HasValue)
                                entity.Status = dto.Status.Value;
                        },
                        attachmentResult?.Attachments
                    );

                    if (dto.labelId != null)
                    {
                        var newLabels = dto.labelId.Select(l => new IssueLabel
                        {
                            Issue_Id = ticketId,
                            Label_Id = l.Id
                        }).ToList();

                        await _domainService.UpdateLabelAsync(ticketId, newLabels);
                    }
                        if (dto.resourceIds != null)
                        {
                            var updateResourceIds = dto.resourceIds
                                .Where(r => r.Id.HasValue)
                                .Select(r => r.Id!.Value)
                                .ToList();

                            // --- STEP 1: Find who was active before this update ---
                            var currentlyActiveIds = await _db.WorkStreams
                                .Where(ws =>
                                    ws.IssueId == ticketId &&
                                    ws.StreamStatus != StatusId.Inactive &&
                                    ws.StreamStatus != StatusId.Cancelled)
                                .Select(ws => ws.ResourceId!.Value)
                                .ToListAsync();

                            // --- STEP 2: Find who was removed ---
                            var removedIds = currentlyActiveIds
                                .Where(id => !updateResourceIds.Contains(id))
                                .ToList();

                            // --- STEP 3: Mark removed people Inactive ---
                            // We use UpsertWorkStreamsAsync and explicitly force the status to Inactive!
                            foreach (var removedId in removedIds)
                            {
                                await _workStreamService.UpsertWorkStreamsAsync(
                                    new WorkStreamContext
                                    {
                                        IssueId = ticketId,
                                        ResourceId = removedId,
                                        StreamStatus = StatusId.Inactive, // 🔥 Force the status to Inactive!
                                        CompletionPct = null,
                                        TargetDate = null
                                    }
                                );
                            }

                            // --- STEP 4: Upsert the remaining/new people ---
                            if (updateResourceIds.Any())
                            {
                                foreach (var resourceId in updateResourceIds)
                                {
                                    await _workStreamService.UpsertWorkStreamsAsync(
                                        new WorkStreamContext
                                        {
                                            IssueId = ticketId,
                                            ResourceId = resourceId,
                                            StreamStatus = null, // Auto-resolves from department inside Upsert
                                            CompletionPct = 0,
                                            TargetDate = dto.TargetDate
                                        }
                                    );
                                }
                            }
                        }

                    // 2. THIS MUST BE OUTSIDE THE RESOURCE IDS IF STATEMENT
                    if (dto.temp?.temps != null && dto.temp.temps.Any())
                        await _attachmentService.CleanupTempFiles(dto.temp);

                    return _mapper.Map<GetTickets>(updatedTicket);

                }); // <--- 3. PROPERLY CLOSE THE LAMBDA AND METHOD CALL HERE
            }
            catch (Exception ex)
            {
                if (attachmentResult?.PermanentFilePathsCreated?.Any() == true)
                    _attachmentService.RollbackPhysicalFiles(attachmentResult.PermanentFilePathsCreated);

                throw new Exception($"Ticket update failed. Everything was rolled back safely.{ex}", ex);
            }

            var richTicketData = await _syncExecutionService.FetchRichDataAsync<GetTickets>(
               configKey: "TicketsList",
               syncParams: new Dictionary<string, string> { { "IssueId", finalTicketData.Issue_Id.ToString() } },
               matchPredicate: p => p.Issue_Id == finalTicketData.Issue_Id,
               fallbackData: finalTicketData,
               lastSync: null
           );

            if (richTicketData != null)
            {
                try
                {
                    await _realtimeNotifier.BroadcastAsync(new RealtimeMessage
                    {
                        Entity = "Ticket",
                        Action = "Update",
                        Payload = richTicketData,
                        KeyField = "Issue_Id",
                        RepoKey = richTicketData.RepoKey,
                        Timestamp = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to broadcast Ticket update: {ex.Message}");
                }
            }

            return richTicketData;
        }
        //public async Task<GetTickets> UpdateTicketAsync(Guid ticketId, UpdateTicketDto dto)
        //{
        //    ProcessedAttachmentResult attachmentResult = null;
        //    GetTickets finalTicketData = null;

        //    try
        //    {
        //        finalTicketData = await _domainService.ExecuteInTransactionAsync(async () =>
        //        {
        //            string finalHtmlDescription = dto.Description ?? string.Empty;

        //            // Process new attachments if uploaded
        //            if (dto.temp?.temps != null && dto.temp.temps.Any())
        //            {
        //                // Keep same folder pattern as create — userId + ticketId
        //                var permUserId = $"{_loginContext.userId}-{_loginContext.userName}";
        //                var relativePath = $"{permUserId}/{ticketId}";

        //                attachmentResult = await _attachmentService.ProcessAndCopyAttachmentsAsync(
        //                    dto.Description,
        //                    dto.temp.temps,
        //                    relativePath,
        //                    ticketId.ToString(),
        //                    "TicketMaster");

        //                finalHtmlDescription = attachmentResult.UpdatedHtml;
        //            }

        //            var capturedHtml = finalHtmlDescription;

        //            // UpdateEntityWithAttachmentsAsync:
        //            //   → FindAsync(ticketId) uses Issue_Id as EF PK — works correctly
        //            //   → calls mutator — ONLY listed fields are marked Modified by EF
        //            //   → DBContext audit sets UpdatedAt/UpdatedBy on SaveChangesAsync
        //            //   → Issue_Id, Issue_Code, SiNo, RepoId, CreatedAt, CreatedBy never touched
        //            var updatedTicket = await _domainService.UpdateEntityWithAttachmentsAsync<TicketMaster>(
        //                ticketId,
        //                entity =>
        //                {
        //                    entity.Title = dto.Title;
        //                    entity.HtmlDesc = capturedHtml;
        //                    entity.Description = HtmlUtilities.ConvertToPlainText(capturedHtml);
        //                    entity.Priority = dto.Priority;

        //                    if (dto.Assignee_Id.HasValue)
        //                        entity.Assignee_Id = dto.Assignee_Id.Value;

        //                    if (dto.Due_Date.HasValue)
        //                        entity.Due_Date = dto.Due_Date.Value;

        //                    if (dto.Status.HasValue)
        //                        entity.Status = dto.Status.Value;

        //                    // Issue_Id, Issue_Code, SiNo, RepoId, CreatedAt, CreatedBy
        //                    // not listed here = EF never marks them Modified = never sent to DB
        //                },
        //                attachmentResult?.Attachments
        //            );

        //            // Replace labels inside the same transaction
        //            // labelId == null → don't touch labels at all
        //            // labelId == []   → clear all labels
        //            // labelId filled  → delete old, insert new
        //            if (dto.labelId != null)
        //            {
        //                var newLabels = dto.labelId.Select(l => new IssueLabel
        //                {
        //                    Issue_Id = ticketId,
        //                    Label_Id = l.Id
        //                }).ToList();

        //                await _domainService.UpdateLabelAsync(ticketId, newLabels);
        //            }

        //            // ── WorkStreams — same null/[]/[ids] pattern as labels ─────
        //            // null     → skip entirely, no change to WorkStreams
        //            // []       → soft-close all rows (all assignees marked completed)
        //            // [ids]    → upsert each: existing person = update, new = insert
        //            if (dto.resourceIds != null)
        //            {
        //                var updateResourceIds = dto.resourceIds
        //                    .Where(r => r.Id.HasValue)
        //                    .Select(r => r.Id!.Value)
        //                    .ToList();

        //                if (updateResourceIds != null && updateResourceIds.Any())
        //                {
        //                    foreach (var resourceId in updateResourceIds)
        //                    {
        //                        await _workStreamService.UpsertWorkStreamsAsync(
        //                            new WorkStreamContext
        //                            {
        //                                IssueId = ticketId,

        //                                // Pass the single ID from the loop
        //                                ResourceId = resourceId,

        //                                StreamStatus = null,
        //                                CompletionPct = 0,
        //                                TargetDate = dto.TargetDate
        //                            }
        //                        );
        //                    }
        //                }
        //                //if (!updateResourceIds.Any())
        //                //{
        //                //    // Empty list → mark ALL active WorkStreams as Inactive
        //                //    await _workStreamService.ClearWorkStreamsAsync(ticketId);
        //                //}
        //                //else
        //                //{
        //                //    // Step 1: find who was active before this update
        //                //    // Compare with new list to detect who was removed
        //                //    var currentlyActiveIds = await _db.WorkStreams
        //                //        .Where(ws =>
        //                //            ws.IssueId == ticketId &&
        //                //            ws.StreamStatus != StatusId.Inactive
        //                //            //&& ws.StreamStatus != StatusId.
        //                //            )
        //                //        .Select(ws => ws.ResourceId!.Value)
        //                //        .ToListAsync();

        //                //    var removedIds = currentlyActiveIds
        //                //        .Where(id => !updateResourceIds.Contains(id))
        //                //        .ToList();

        //                //    // Step 2: mark removed people Inactive
        //                //    if (removedIds.Any())
        //                //        await _workStreamService.MarkInactiveAsync(ticketId, removedIds);

        //                //    // Step 3: upsert each person in the new list
        //                //    // existing active person → UPDATE status + %
        //                //    // new person (or previously inactive) → INSERT fresh row
        //                //    await _workStreamService.UpsertWorkStreamsAsync(
        //                //        issueId: ticketId,
        //                //        resourceIds: updateResourceIds,
        //                //        streamStatus: dto.StreamStatus,
        //                //        completionPct: dto.CompletionPct,
        //                //        targetDate: dto.TargetDate
        //                //    );
        //                //    }
        //                //}

        //                if (dto.temp?.temps != null && dto.temp.temps.Any())
        //                    await _attachmentService.CleanupTempFiles(dto.temp);

        //                return _mapper.Map<GetTickets>(updatedTicket);
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        if (attachmentResult?.PermanentFilePathsCreated?.Any() == true)
        //            _attachmentService.RollbackPhysicalFiles(attachmentResult.PermanentFilePathsCreated);

        //        throw new Exception($"Ticket update failed. Everything was rolled back safely.{ex}", ex);
        //    }

        //    var richTicketData = await _syncExecutionService.FetchRichDataAsync<GetTickets>(

        //       configKey: "TicketsList",
        //       syncParams: new Dictionary<string, string> { { "IssueId", finalTicketData.Issue_Id.ToString() } },
        //       matchPredicate: p => p.Issue_Id == finalTicketData.Issue_Id,
        //       fallbackData: finalTicketData,
        //       lastSync: null // Optional: pass DateTimeOffset if your SP requires it
        //   );

        //    if (richTicketData != null)
        //    {
        //        try
        //        {
        //            await _realtimeNotifier.BroadcastAsync(new RealtimeMessage
        //            {
        //                Entity = "Ticket",
        //                Action = "Update",
        //                Payload = richTicketData,
        //                KeyField = "Issue_Id",
        //                RepoKey = richTicketData.RepoKey,
        //                Timestamp = DateTime.UtcNow
        //            });
        //        }
        //        catch (Exception ex)
        //        {
        //            Console.WriteLine($"Failed to broadcast Ticket update: {ex.Message}");
        //        }
        //    }

        //    return richTicketData;
        //}

        // ─────────────────────────────────────────────────────────────────────
        // STATUS-ONLY UPDATE
        // PATCH /api/ticket/{id}/status
        // Body: { "Status": 2 }
        //
        // Only Status column changes in DB.
        // EF sends: UPDATE IssueMasters SET Status=@s, UpdatedAt=@t, UpdatedBy=@u
        //           WHERE Issue_Id=@id
        // Everything else in the row stays exactly as-is.
        // Labels are not touched.
        //
        // No RepoId in body — RepoScopeHandler looked up ticket's Repo_Id
        // from DB by {id} route param and validated it before reaching here.
        // ─────────────────────────────────────────────────────────────────────
        public async Task<GetTickets> UpdateTicketStatusAsync(Guid ticketId, UpdateTicketStatusDto dto)
        {
            GetTickets finalTicketData = null;

            try
            {
                finalTicketData = await _domainService.ExecuteInTransactionAsync(async () =>
                {
                    var updatedTicket = await _domainService.UpdateEntityWithAttachmentsAsync<TicketMaster>(
                        ticketId,
                        entity =>
                        {
                            // Only Status — nothing else in the row is touched
                            entity.Status = dto.Status;
                        }
                        // no newAttachments — defaults to null
                    );

                    return _mapper.Map<GetTickets>(updatedTicket);
                });
            }
            catch (Exception ex)
            {
                throw new Exception("Ticket status update failed. Everything was rolled back safely.", ex);
            }

            var richTicketData = await _syncExecutionService.FetchRichDataAsync<GetTickets>(

               configKey: "TicketsList",
               syncParams: new Dictionary<string, string> { { "IssueId", finalTicketData.Issue_Id.ToString() } },
               matchPredicate: p => p.Issue_Id == finalTicketData.Issue_Id,
               fallbackData: finalTicketData,
               lastSync: null // Optional: pass DateTimeOffset if your SP requires it
           );

            if (richTicketData != null)
            {
                try
                {
                    await _realtimeNotifier.BroadcastAsync(new RealtimeMessage
                    {
                        Entity = "Ticket",
                        Action = "StatusUpdate",
                        Payload = richTicketData,
                        KeyField = "Issue_Id",
                        RepoKey = richTicketData.RepoKey,
                        Timestamp = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to broadcast Ticket status update: {ex.Message}");
                }
            }

            return richTicketData;
        }
    }
}
