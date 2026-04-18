using APIGateWay.Business_Layer.Helper;
using APIGateWay.Business_Layer.Interface;
using APIGateWay.BusinessLayer.Interface;
using APIGateWay.BusinessLayer.SignalRHub;
using APIGateWay.DomainLayer.CommonSevice;
using APIGateWay.DomainLayer.DBContext;
using APIGateWay.DomainLayer.Helpers;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.DomainLayer.Service;
using APIGateWay.DomainLayer.Utilities;
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
        private readonly ITicketHistoryRepository _historyRepository;
        private readonly IRequestStepContext _stepContext;              // ← ADDED

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
            APIGatewayDBContext dBContext,
            ITicketHistoryRepository historyRepository,
            IRequestStepContext stepContext)                            // ← ADDED
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
            _historyRepository = historyRepository;
            _stepContext = stepContext;                                 // ← ADDED
        }

        // ─────────────────────────────────────────────────────────────────────
        // CREATE
        // POST /api/ticket/CreateTicket
        //
        // Step log order:
        //   1. AttachmentMaster  — file copy + DB rows
        //   2. TicketMaster      — main ticket row
        //   3. TicketHistory     — created event
        //   4. IssueLabels       — label rows  (skipped if no labels)
        //   5. WorkStream        — one row per assignee  (skipped if no resourceIds)
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
                    ticketMaster.ReopenCount = 0;

                    if (!ticketDto.RepoId.HasValue)
                        throw new Exception("Repo_Id is required to create a Ticket.");

                    projectKey = await _helperGet.GetProjectByIdAsync(ticketDto.Project_Id.Value);
                    var seq = await _commonService.GetNextSequenceAsync(projectKey.RepoKey, "Tickets", "IssueMaster");
                    ticketMaster.SiNo = seq.CurrentValue;
                    ticketMaster.Issue_Code = $"T{seq.ColumnValue}";
                    ticketMaster.RepoKey = projectKey.RepoKey;
                    ticketMaster.ProjKey = projectKey.ProjectKey;
                    string finalHtmlDescription = ticketDto.Description;

                    // ── Step 1: AttachmentMaster ──────────────────────────────
                    if (ticketDto.temp?.temps != null && ticketDto.temp.temps.Any())
                    {
                        var timer = _stepContext.StartStep();
                        try
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

                            var attachmentIds = string.Join(",",
                                attachmentResult.Attachments.Select(a => a.AttachmentId));
                            _stepContext.Success("AttachmentMaster", "INSERT", attachmentIds, timer);
                        }
                        catch (Exception ex)
                        {
                            _stepContext.Failure("AttachmentMaster", "INSERT",
                                ex.Message, ex.InnerException?.Message, timer);
                            throw;
                        }
                    }

                    ticketMaster.HtmlDesc = finalHtmlDescription;
                    ticketMaster.Description = HtmlUtilities.ConvertToPlainText(finalHtmlDescription);

                    // ── Step 2: TicketMaster ──────────────────────────────────
                    {
                        var timer = _stepContext.StartStep();
                        try
                        {
                            await _domainService.SaveEntityWithAttachmentsAsync(
                                ticketMaster, attachmentResult?.Attachments);

                            _stepContext.Success("TicketMaster", "INSERT",
                                ticketMaster.Issue_Id.ToString(), timer);
                        }
                        catch (Exception ex)
                        {
                            _stepContext.Failure("TicketMaster", "INSERT",
                                ex.Message, ex.InnerException?.Message, timer);
                            throw;
                        }
                    }

                    // ── Step 3: TicketHistory (created event) ─────────────────
                    {
                        var timer = _stepContext.StartStep();
                        try
                        {
                            await _historyRepository.LogAsync(TicketHistoryHelper.TicketCreated(
                                issueId: ticketMaster.Issue_Id,
                                issueCode: ticketMaster.Issue_Code,
                                actorId: _loginContext.userId,
                                actorName: _loginContext.userName));

                            _stepContext.Success("TicketHistory", "INSERT",
                                ticketMaster.Issue_Id.ToString(), timer);
                        }
                        catch (Exception ex)
                        {
                            _stepContext.Failure("TicketHistory", "INSERT",
                                ex.Message, ex.InnerException?.Message, timer);
                            throw;
                        }
                    }

                    // ── Step 4: IssueLabels ───────────────────────────────────
                    if (ticketDto.labelId != null && ticketDto.labelId.Any())
                    {
                        var timer = _stepContext.StartStep();
                        try
                        {
                            var issueLabels = ticketDto.labelId.Select(l => new IssueLabel
                            {
                                Issue_Id = ticketMaster.Issue_Id,
                                Label_Id = l.Id
                            }).ToList();

                            await _domainService.SaveLabelAsync(issueLabels);

                            foreach (var label in issueLabels)
                            {
                                await _historyRepository.LogAsync(
                                    TicketHistoryHelper.LabelAdded(
                                        issueId: ticketMaster.Issue_Id,
                                        labelId: label.Label_Id ?? 0,
                                        actorId: _loginContext.userId,
                                        actorName: _loginContext.userName));
                            }

                            var labelIds = string.Join(",",
                                ticketDto.labelId.Select(l => l.Id));
                            _stepContext.Success("IssueLabels", "INSERT", labelIds, timer);
                        }
                        catch (Exception ex)
                        {
                            _stepContext.Failure("IssueLabels", "INSERT",
                                ex.Message, ex.InnerException?.Message, timer);
                            throw;
                        }
                    }

                    // ── Step 5: WorkStream (one row per assignee) ─────────────
                    var createResourceIds = ticketDto.resourceIds?
                        .Where(r => r.Id.HasValue)
                        .Select(r => r.Id!.Value)
                        .ToList();

                    if (createResourceIds != null && createResourceIds.Any())
                    {
                        var timer = _stepContext.StartStep();
                        try
                        {
                            foreach (var resourceId in createResourceIds)
                            {
                                await _workStreamService.UpsertWorkStreamsAsync(
                                    new WorkStreamContext
                                    {
                                        IssueId = ticketMaster.Issue_Id,
                                        ResourceId = resourceId,
                                        StreamStatus = null,
                                        CompletionPct = 0,
                                        TargetDate = ticketDto.TargetDate
                                    });

                                var assigneeName = await _db.eMPLOYEEMASTERs
                                    .Where(e => e.EmployeeID == resourceId)
                                    .Select(e => new { Name = e.EmployeeName ?? "Unknown" })
                                    .FirstOrDefaultAsync();

                                var newStream = await _db.WorkStreams
                                    .Where(ws =>
                                        ws.IssueId == ticketMaster.Issue_Id &&
                                        ws.ResourceId == resourceId)
                                    .OrderByDescending(ws => ws.CreatedAt)
                                    .Select(ws => new { ws.StreamId })
                                    .FirstOrDefaultAsync();

                                if (newStream != null)
                                {
                                    await _historyRepository.LogAsync(TicketHistoryHelper.WorkStreamCreated(
                                        issueId: ticketMaster.Issue_Id,
                                        assigneeName: assigneeName?.Name ?? "Unknown",
                                        streamName: "General",
                                        statusName: "New",
                                        workStreamId: newStream.StreamId,
                                        actorId: _loginContext.userId,
                                        actorName: _loginContext.userName));
                                }
                            }

                            var streamIds = string.Join(",", createResourceIds);
                            _stepContext.Success("WorkStream", "INSERT", streamIds, timer);
                        }
                        catch (Exception ex)
                        {
                            _stepContext.Failure("WorkStream", "INSERT",
                                ex.Message, ex.InnerException?.Message, timer);
                            throw;
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
                lastSync: null);

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
        // Step log order:
        //   1. AttachmentMaster  — new files only  (skipped if no uploads)
        //   2. TicketMaster      — core field update
        //   3. IssueLabels       — full replace     (skipped if labelId null)
        //   4. WorkStream        — upsert/deactivate per assignee (skipped if resourceIds null)
        // ─────────────────────────────────────────────────────────────────────
       
        public async Task<GetTickets> UpdateTicketAsync(Guid ticketId, UpdateTicketDto dto)
        {
            ProcessedAttachmentResult attachmentResult = null;
            GetTickets finalTicketData = null;

            try
            {
                finalTicketData = await _domainService.ExecuteInTransactionAsync(async () =>
                {
                    // ── Load existing record first ────────────────────────────
                    var existingTicket = await _db.ISSUEMASTER.FindAsync(ticketId)
                        ?? throw new Exception("Ticket not found");

                    var oldLabels = await _db.ISSUE_LABELS
                        .Where(il => il.Issue_Id == ticketId)
                        .ToListAsync();

                    // ── Step 1: AttachmentMaster ──────────────────────────────
                    string finalHtmlDescription = dto.Description ?? string.Empty;

                    if (dto.temp?.temps != null && dto.temp.temps.Any())
                    {
                        var timer = _stepContext.StartStep();
                        try
                        {
                            var permUserId = $"{_loginContext.userId}-{_loginContext.userName}";
                            var relativePath = $"{permUserId}/{ticketId}";

                            attachmentResult = await _attachmentService.ProcessAndCopyAttachmentsAsync(
                                dto.Description, dto.temp.temps,
                                relativePath, ticketId.ToString(), "TicketMaster");

                            finalHtmlDescription = attachmentResult.UpdatedHtml;

                            var attachmentIds = string.Join(",",
                                attachmentResult.Attachments.Select(a => a.AttachmentId));

                            _stepContext.Success("AttachmentMaster", "INSERT", attachmentIds, timer);
                        }
                        catch (Exception ex)
                        {
                            _stepContext.Failure("AttachmentMaster", "INSERT",
                                ex.Message, ex.InnerException?.Message, timer);
                            throw;
                        }
                    }

                    // ── Step 2: TicketMaster — patch only changed fields ──────
                    var patcher = new EntityPatcher<TicketMaster>(existingTicket)
                        .Set("Title",
                            existingTicket.Title, dto.Title,
                            (e, v) => e.Title = v)

                        .Set("Description",
                            existingTicket.HtmlDesc?.Trim(), finalHtmlDescription.Trim(),
                            (e, v) =>
                            {
                                e.HtmlDesc = v;
                                e.Description = HtmlUtilities.ConvertToPlainText(v ?? string.Empty);
                            },
                            // Display formatter: plain-text truncated for history
                            v => Truncate(HtmlUtilities.ConvertToPlainText(v ?? string.Empty), 100))

                        .Set("Priority",
                            existingTicket.Priority?.ToString(), dto.Priority,
                            (e, v) => e.Priority = v == null ? null : v)

                        .Set("Hours",
                            existingTicket.Hours, dto.Hours,
                            (e, v) => e.Hours = v)

                        .Set("Assignee",
                            existingTicket.Assignee_Id, dto.Assignee_Id,
                            (e, v) => { if (v.HasValue) e.Assignee_Id = v.Value; })

                        .Set("Due Date",
                            existingTicket.Due_Date, dto.Due_Date,
                            (e, v) => { if (v.HasValue) e.Due_Date = v.Value; },
                            v => v?.ToString("yyyy-MM-dd"))

                        .Set("Status",
                            existingTicket.Status, dto.Status,
                            (e, v) => { if (v.HasValue) e.Status = v.Value; });

                    TicketMaster updatedTicket;

                    if (patcher.HasChanges)
                    {
                        var timer = _stepContext.StartStep();
                        try
                        {
                            patcher.Apply();   // ← mutates existingTicket with only the changed fields

                            updatedTicket = await _domainService.UpdateEntityWithAttachmentsAsync<TicketMaster>(
                                ticketId,
                                _ => { },      // entity already patched above — no-op lambda
                                attachmentResult?.Attachments);

                            _stepContext.Success("TicketMaster", "UPDATE", ticketId.ToString(), timer);
                        }
                        catch (Exception ex)
                        {
                            _stepContext.Failure("TicketMaster", "UPDATE",
                                ex.Message, ex.InnerException?.Message, timer);
                            throw;
                        }

                        // ── Audit history — only for changed fields ───────────
                        foreach (var change in patcher.Changes)
                        {
                            await _historyRepository.LogAsync(TicketHistoryHelper.TicketUpdated(
                                issueId: ticketId,
                                fieldName: change.FieldName,
                                oldValue: change.OldValue,
                                newValue: change.NewValue,
                                actorId: _loginContext.userId,
                                actorName: _loginContext.userName));
                        }
                    }
                    else
                    {
                        // Nothing changed — skip DB write entirely
                        updatedTicket = existingTicket;
                    }

                    // ── Step 3: IssueLabels ───────────────────────────────────
                    if (dto.labelId != null)
                    {
                        var incomingLabelIds = dto.labelId
                            .Where(l => l.Id.HasValue)
                            .Select(l => l.Id!.Value)
                            .OrderBy(x => x)
                            .ToList();

                        var existingLabelIds = oldLabels
                            .Where(ol => ol.Label_Id.HasValue)
                            .Select(ol => ol.Label_Id!.Value)
                            .OrderBy(x => x)
                            .ToList();

                        // Only touch labels if the set actually changed
                        if (!incomingLabelIds.SequenceEqual(existingLabelIds))
                        {
                            var timer = _stepContext.StartStep();
                            try
                            {
                                var labelNames = await _db.labelMaster
                                    .Where(lm => existingLabelIds.Contains(lm.Id)
                                              || incomingLabelIds.Contains(lm.Id))
                                    .ToDictionaryAsync(lm => lm.Id, lm => lm.Title);

                                var oldLabelNames = string.Join(", ",
                                    existingLabelIds.Select(id => labelNames.GetValueOrDefault(id, "Unknown")));

                                var newLabelNames = string.Join(", ",
                                    incomingLabelIds.Select(id => labelNames.GetValueOrDefault(id, "Unknown")));

                                await _historyRepository.LogAsync(TicketHistoryHelper.TicketUpdated(
                                    issueId: ticketId, fieldName: "Label",
                                    oldValue: string.IsNullOrEmpty(oldLabelNames) ? "None" : oldLabelNames,
                                    newValue: string.IsNullOrEmpty(newLabelNames) ? "None" : newLabelNames,
                                    actorId: _loginContext.userId,
                                    actorName: _loginContext.userName));

                                var newLabels = incomingLabelIds.Select(id => new IssueLabel
                                {
                                    Issue_Id = ticketId,
                                    Label_Id = id
                                }).ToList();

                                await _domainService.UpdateLabelAsync(ticketId, newLabels);

                                _stepContext.Success("IssueLabels", "UPDATE",
                                    string.Join(",", incomingLabelIds), timer);
                            }
                            catch (Exception ex)
                            {
                                _stepContext.Failure("IssueLabels", "UPDATE",
                                    ex.Message, ex.InnerException?.Message, timer);
                                throw;
                            }
                        }
                    }

                    // ── Step 4: WorkStream — full assignee sync ───────────────
                    //
                    //   DB has:      [A, B, C]
                    //   DTO sends:   [A, B]
                    //   Result:      A → keep, B → keep, C → Inactive
                    //
                    if (dto.resourceIds != null)
                    {
                        var incomingIds = dto.resourceIds
                            .Where(r => r.Id.HasValue)
                            .Select(r => r.Id!.Value)
                            .ToList();

                        var activeInDb = await _db.WorkStreams
                            .Where(ws => ws.IssueId == ticketId
                                      && ws.StreamStatus != StatusId.Inactive
                                      && ws.StreamStatus != StatusId.Cancelled)
                            .Select(ws => ws.ResourceId!.Value)
                            .ToListAsync();

                        // Who to deactivate: in DB but NOT in incoming list
                        var toDeactivate = activeInDb.Except(incomingIds).ToList();

                        // Who to upsert: in incoming list (new or existing)
                        var toUpsert = incomingIds.ToList();

                        bool assigneesChanged = toDeactivate.Any()
                            || incomingIds.Except(activeInDb).Any();

                        if (assigneesChanged)
                        {
                            var timer = _stepContext.StartStep();
                            try
                            {
                                // ── Deactivate removed assignees ──────────────
                                foreach (var removedId in toDeactivate)
                                {
                                    await _workStreamService.UpsertWorkStreamsAsync(
                                        new WorkStreamContext
                                        {
                                            IssueId = ticketId,
                                            ResourceId = removedId,
                                            StreamStatus = StatusId.Inactive,
                                            CompletionPct = null,
                                            TargetDate = null
                                        });
                                }

                                // ── Upsert kept / new assignees ───────────────
                                foreach (var resourceId in toUpsert)
                                {
                                    await _workStreamService.UpsertWorkStreamsAsync(
                                        new WorkStreamContext
                                        {
                                            IssueId = ticketId,
                                            ResourceId = resourceId,
                                            StreamStatus = null,
                                            CompletionPct = 0,
                                            TargetDate = dto.TargetDate
                                        });
                                }

                                _stepContext.Success("WorkStream", "UPDATE",
                                    string.Join(",", toUpsert), timer);
                            }
                            catch (Exception ex)
                            {
                                _stepContext.Failure("WorkStream", "UPDATE",
                                    ex.Message, ex.InnerException?.Message, timer);
                                throw;
                            }
                        }
                    }

                    if (dto.temp?.temps != null && dto.temp.temps.Any())
                        await _attachmentService.CleanupTempFiles(dto.temp);

                    return _mapper.Map<GetTickets>(updatedTicket);
                });
            }
            catch (Exception ex)
            {
                if (attachmentResult?.PermanentFilePathsCreated?.Any() == true)
                    _attachmentService.RollbackPhysicalFiles(attachmentResult.PermanentFilePathsCreated);

                throw new Exception($"Ticket update failed. Everything was rolled back safely. {ex}", ex);
            }

            // ── Realtime broadcast (unchanged) ───────────────────────────────────
            var richTicketData = await _syncExecutionService.FetchRichDataAsync<GetTickets>(
                configKey: "TicketsList",
                syncParams: new Dictionary<string, string> { { "IssueId", finalTicketData.Issue_Id.ToString() } },
                matchPredicate: p => p.Issue_Id == finalTicketData.Issue_Id,
                fallbackData: finalTicketData,
                lastSync: null);

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

        // ── Private helper ────────────────────────────────────────────────────────
        private static string Truncate(string value, int max) =>
            value.Length > max ? value[..max] + "..." : value;

        // ─────────────────────────────────────────────────────────────────────
        // STATUS-ONLY UPDATE
        // PATCH /api/ticket/{id}/status
        //
        // Step log order:
        //   1. TicketMaster  — status column only
        // ─────────────────────────────────────────────────────────────────────
        public async Task<GetTickets> UpdateTicketStatusAsync(Guid ticketId, UpdateTicketStatusDto dto)
        {
            GetTickets finalTicketData = null;

            try
            {
                finalTicketData = await _domainService.ExecuteInTransactionAsync(async () =>
                {
                    // ── Step 1: TicketMaster (status column only) ─────────────
                    var timer = _stepContext.StartStep();
                    try
                    {
                        var updatedTicket = await _domainService.UpdateEntityWithAttachmentsAsync<TicketMaster>(
                            ticketId,
                            entity =>
                            {
                                entity.Status = dto.Status;  // Only Status — nothing else touched
                            }
                            // no newAttachments — defaults to null
                        );

                        _stepContext.Success("TicketMaster", "UPDATE", ticketId.ToString(), timer);
                        return _mapper.Map<GetTickets>(updatedTicket);
                    }
                    catch (Exception ex)
                    {
                        _stepContext.Failure("TicketMaster", "UPDATE",
                            ex.Message, ex.InnerException?.Message, timer);
                        throw;
                    }
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
                lastSync: null);

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

        //public async Task<GetTickets> UpdateTicketProgressAsync(Guid ticketId, UpdateProgressDto dto)
        //{
        //    GetTickets finalTicketData = null;

        //    try
        //    {
        //        finalTicketData = await _domainService.ExecuteInTransactionAsync(async () =>
        //        {
        //            var timer = _stepContext.StartStep();

        //            try
        //            {
        //                // 1. Find the current active log(s) and make them inactive
        //                var activeLogs = await _db.TicketProgressLogs
        //                    .Where(log => log.Issue_Id == ticketId && log.IsActive)
        //                    .ToListAsync();

        //                foreach (var log in activeLogs)
        //                {
        //                    log.IsActive = false;
        //                }

        //                // 2. Create the new active log from the user's input
        //                var newLog = new TicketProgressLog
        //                {
        //                    Issue_Id = ticketId,
        //                    Assignee_Id = _loginContext.userId,
        //                    Percentage = dto.Percentage,
        //                    StatusSummary = dto.StatusSummary,
        //                    IsActive = true,
        //                    CreatedAt = DateTime.UtcNow
        //                };

        //                await _db.TicketProgressLogs.AddAsync(newLog);

        //                // 3. Update the TicketMaster so your list views load fast
        //                var ticketMaster = await _db.ISSUEMASTER.FindAsync(ticketId)
        //                    ?? throw new Exception("Ticket not found");

        //                ticketMaster.OverallPercentage = dto.Percentage;

        //                // 4. Save changes and log to TicketHistory
        //                await _db.SaveChangesAsync();

        //                await _historyRepository.LogAsync(TicketHistoryHelper.TicketUpdated(
        //                    issueId: ticketId,
        //                    fieldName: "Overall Progress",
        //                    oldValue: activeLogs.FirstOrDefault()?.Percentage.ToString() ?? "0",
        //                    newValue: dto.Percentage.ToString(),
        //                    actorId: _loginContext.userId,
        //                    actorName: _loginContext.userName));

        //                _stepContext.Success("TicketProgress", "UPDATE", ticketId.ToString(), timer);

        //                return _mapper.Map<GetTickets>(ticketMaster);
        //            }
        //            catch (Exception ex)
        //            {
        //                _stepContext.Failure("TicketProgress", "UPDATE", ex.Message, ex.InnerException?.Message, timer);
        //                throw;
        //            }
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        throw new Exception($"Failed to update ticket progress. {ex.Message}", ex);
        //    }

        //    // Broadcast the update to realtime notifier just like your other methods
        //    //await BroadcastTicketUpdateAsync(finalTicketData);

        //    return finalTicketData;
        //}
    }
   
}


//public class TicketRepo : ITicketRepo
//{
//    private readonly IDomainService _domainService;
//    private readonly APIGateWayCommonService _commonService;
//    private readonly IMapper _mapper;
//    private readonly ILoginContextService _loginContext;
//    private readonly IAttachmentService _attachmentService;
//    private readonly IHelperGetData _helperGet;
//    private readonly IRealtimeNotifier _realtimeNotifier;
//    private readonly ISyncExecutionService _syncExecutionService;
//    private readonly IWorkStreamService _workStreamService;
//    private readonly APIGatewayDBContext _db;
//    private readonly ITicketHistoryRepository _historyRepository;

//    public TicketRepo(
//        IDomainService domainService,
//        APIGateWayCommonService service,
//        IMapper mapper,
//        ILoginContextService loginContext,
//        IAttachmentService attachmentService,
//        IHelperGetData helperGet,
//        IRealtimeNotifier realtimeNotifier,
//        ISyncExecutionService syncExecutionService,
//        IWorkStreamService workStreamService,
//        APIGatewayDBContext dBContext,
//        ITicketHistoryRepository historyRepository)
//    {
//        _domainService = domainService;
//        _commonService = service;
//        _mapper = mapper;
//        _loginContext = loginContext;
//        _attachmentService = attachmentService;
//        _helperGet = helperGet;
//        _realtimeNotifier = realtimeNotifier;
//        _syncExecutionService = syncExecutionService;  
//        _workStreamService = workStreamService;
//        _db = dBContext;
//        _historyRepository = historyRepository;
//    }

//    // ─────────────────────────────────────────────────────────────────────
//    // CREATE — your original code, unchanged
//    // ─────────────────────────────────────────────────────────────────────
//    public async Task<GetTickets> CreateTicketAsync(PostTicketDto ticketDto)
//    {
//        ProcessedAttachmentResult attachmentResult = null;
//        GetTickets finalTicketData = null;
//        ProjectKeysDto projectKey = null;

//        try
//        {
//            finalTicketData = await _domainService.ExecuteInTransactionAsync(async () =>
//            {
//                var ticketMaster = _mapper.Map<TicketMaster>(ticketDto);
//                ticketMaster.Issue_Id = Guid.NewGuid();
//                ticketMaster.Status = 1;

//                if (!ticketDto.RepoId.HasValue)
//                    throw new Exception("Repo_Id is required to create a Ticket.");

//                projectKey = await _helperGet.GetProjectByIdAsync(ticketDto.Project_Id.Value);
//                var seq = await _commonService.GetNextSequenceAsync(projectKey.RepoKey, "Tickets", "IssueMaster");
//                ticketMaster.SiNo = seq.CurrentValue;
//                ticketMaster.Issue_Code = $"T{seq.ColumnValue}";
//                ticketMaster.RepoKey = projectKey.RepoKey;
//                ticketMaster.ProjKey = projectKey.ProjectKey;
//                string finalHtmlDescription = ticketDto.Description;

//                if (ticketDto.temp?.temps != null && ticketDto.temp.temps.Any())
//                {
//                    var permUserId = $"{_loginContext.userId}-{_loginContext.userName}";
//                    var permFolder = $"{ticketMaster.Issue_Code}-{ticketDto.Title}";
//                    var relativePath = $"{permUserId}/{permFolder}";

//                    attachmentResult = await _attachmentService.ProcessAndCopyAttachmentsAsync(
//                        ticketDto.Description,
//                        ticketDto.temp.temps,
//                        relativePath,
//                        ticketMaster.Issue_Id.ToString(),
//                        "TicketMaster");

//                    finalHtmlDescription = attachmentResult.UpdatedHtml;
//                }

//                ticketMaster.HtmlDesc = finalHtmlDescription;
//                ticketMaster.Description = HtmlUtilities.ConvertToPlainText(finalHtmlDescription);

//                await _domainService.SaveEntityWithAttachmentsAsync(ticketMaster, attachmentResult?.Attachments);
//                await _historyRepository.LogAsync(TicketHistoryHelper.TicketCreated(issueId: ticketMaster?.Issue_Id,
//                                    issueCode: ticketMaster.Issue_Code,
//                                  actorId: _loginContext.userId,
//                                   actorName: _loginContext.userName
//                    )
//                );
//                if (ticketDto.labelId != null && ticketDto.labelId.Any())
//                {
//                    var issueLabels = ticketDto.labelId.Select(l => new IssueLabel
//                    {
//                        Issue_Id = ticketMaster.Issue_Id,
//                        Label_Id = l.Id
//                    }).ToList();
//                    await _domainService.SaveLabelAsync(issueLabels);

//                    foreach (var label in issueLabels)
//                    {
//                        await _historyRepository.LogAsync(
//                            TicketHistoryHelper.LabelAdded(
//                                issueId: ticketMaster?.Issue_Id,
//                                // look up from label cache or DB
//                                labelId: label.Label_Id ?? 0,
//                                actorId: _loginContext.userId,
//                                actorName: _loginContext.userName
//                            )
//                        );
//                    }
//                }

//                // ── Save WorkStreams — one row per assignee ────────────────
//                // Mirrors the labels pattern: loop + insert per resource
//                // ResourceIds null/empty → no WorkStream rows created yet
//                // (assignees can be added later via ticket update)
//                var createResourceIds = ticketDto.resourceIds?
//              .Where(r => r.Id.HasValue)
//              .Select(r => r.Id!.Value)
//              .ToList();

//                if (createResourceIds != null && createResourceIds.Any())
//                {
//                    foreach (var resourceId in createResourceIds)
//                    {
//                        await _workStreamService.UpsertWorkStreamsAsync(
//                            new WorkStreamContext
//                            {
//                                IssueId = ticketMaster.Issue_Id,

//                                // Pass the single ID from the loop
//                                ResourceId = resourceId,

//                                StreamStatus = null,
//                                CompletionPct = 0,
//                                TargetDate = ticketDto.TargetDate
//                            }
//                        );
//                        var assigneeName = await _db.eMPLOYEEMASTERs
//                              .Where(e => e.EmployeeID == resourceId)
//                              .Select(e => new { Name = e.EmployeeName ?? "Unknown" })
//                              .FirstOrDefaultAsync();

//                        var newStream = await _db.WorkStreams
//                        .Where(ws =>
//                        ws.IssueId == ticketMaster.Issue_Id &&
//                        ws.ResourceId == resourceId)
//                        .OrderByDescending(ws => ws.CreatedAt)
//                        .Select(ws => new { ws.StreamId })
//                        .FirstOrDefaultAsync();

//                        if (newStream != null)
//                        {
//                            await _historyRepository.LogAsync(TicketHistoryHelper.WorkStreamCreated(
//                                issueId: ticketMaster.Issue_Id,
//                                assigneeName: assigneeName?.Name ?? "Unknown",
//                                streamName: "General",
//                                statusName: "New",
//                                workStreamId: newStream.StreamId,
//                                actorId: _loginContext.userId,
//                                actorName: _loginContext.userName
//                                ));
//                        }
//                        //await _historyRepository.LogAsync(
//                        //        TicketHistoryHelper.WorkStreamCreated(
//                        //            issueId: ticketMaster.Issue_Id,
//                        //    workStreamId: ticketDto.resourceIds,
//                        //actorId: _loginContext.userId,
//                        //actorName: _loginContext.userName
//                        //)
//                        //);
//                    }
//                }

//                if (ticketDto.temp?.temps != null && ticketDto.temp.temps.Any())
//                    await _attachmentService.CleanupTempFiles(ticketDto.temp);

//                return _mapper.Map<GetTickets>(ticketMaster);
//            });
//        }
//        catch (Exception ex)
//        {
//            if (attachmentResult?.PermanentFilePathsCreated?.Any() == true)
//                _attachmentService.RollbackPhysicalFiles(attachmentResult.PermanentFilePathsCreated);

//            throw new Exception($"Ticket creation failed. Everything was rolled back safely.{ex}", ex);
//        }

//        var richTicketData = await _syncExecutionService.FetchRichDataAsync<GetTickets>(

//           configKey: "TicketsList",
//           syncParams: new Dictionary<string, string> { { "IssueId", finalTicketData.Issue_Id.ToString() } },
//           matchPredicate: p => p.Issue_Id == finalTicketData.Issue_Id,
//           fallbackData: finalTicketData,
//           lastSync: null // Optional: pass DateTimeOffset if your SP requires it
//       );

//        if (richTicketData != null)
//        {
//            try
//            {
//                await _realtimeNotifier.BroadcastAsync(new RealtimeMessage
//                {
//                    Entity = "Ticket",
//                    Action = "Create",
//                    Payload = richTicketData,
//                    KeyField = "Issue_Id",
//                    RepoKey = richTicketData.RepoKey,
//                    Timestamp = DateTime.UtcNow
//                });
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Failed to broadcast Ticket creation: {ex.Message}");
//            }
//        }

//        return richTicketData;
//    }
//    public async Task<GetTickets> UpdateTicketAsync(Guid ticketId, UpdateTicketDto dto)
//    {
//        ProcessedAttachmentResult attachmentResult = null;
//        GetTickets finalTicketData = null;

//        try
//        {
//            finalTicketData = await _domainService.ExecuteInTransactionAsync(async () =>
//            {
//                string finalHtmlDescription = dto.Description ?? string.Empty;

//                // Process new attachments if uploaded
//                if (dto.temp?.temps != null && dto.temp.temps.Any())
//                {
//                    var permUserId = $"{_loginContext.userId}-{_loginContext.userName}";
//                    var relativePath = $"{permUserId}/{ticketId}";

//                    attachmentResult = await _attachmentService.ProcessAndCopyAttachmentsAsync(
//                        dto.Description,
//                        dto.temp.temps,
//                        relativePath,
//                        ticketId.ToString(),
//                        "TicketMaster");

//                    finalHtmlDescription = attachmentResult.UpdatedHtml;
//                }

//                var capturedHtml = finalHtmlDescription;
//                var existingTicket = await _db.ISSUEMASTER.FindAsync(ticketId);
//                if (existingTicket == null)
//                    throw new Exception("Ticket not found");

//                var oldTitle = existingTicket.Title;
//                var oldDescription = existingTicket.HtmlDesc;
//                var oldPriority = existingTicket.Priority?.ToString();
//                var oldDueDate = existingTicket.Due_Date;
//                var oldStatus = existingTicket.Status?.ToString();

//                var oldLabels = await _db.ISSUE_LABELS
//                .Where(il => il.Issue_Id == ticketId)
//                .ToListAsync();

//                var currentlyActiveAssignees = await _db.WorkStreams
//                .Where(ws =>
//                ws.IssueId == ticketId &&
//                ws.StreamStatus != StatusId.Inactive &&
//                ws.StreamStatus != StatusId.Cancelled)
//                .Join(_db.eMPLOYEEMASTERs,
//                ws => ws.ResourceId,
//                e => e.EmployeeID,
//                (ws, e) => new { ws.ResourceId, e.EmployeeName })
//                .ToListAsync();

//                var updatedTicket = await _domainService.UpdateEntityWithAttachmentsAsync<TicketMaster>(
//                    ticketId,
//                    entity =>
//                    {
//                        entity.Title = dto.Title;
//                        entity.HtmlDesc = capturedHtml;
//                        entity.Description = HtmlUtilities.ConvertToPlainText(capturedHtml);
//                        entity.Priority = dto.Priority;
//                        entity.Hours = dto.Hours;

//                        if (dto.Assignee_Id.HasValue)
//                            entity.Assignee_Id = dto.Assignee_Id.Value;

//                        if (dto.Due_Date.HasValue)
//                            entity.Due_Date = dto.Due_Date.Value;

//                        if (dto.Status.HasValue)
//                            entity.Status = dto.Status.Value;
//                    },
//                    attachmentResult?.Attachments
//                );

//                if (oldTitle != dto.Title)
//                {
//                    await _historyRepository.LogAsync(TicketHistoryHelper.TicketUpdated(
//                        issueId: ticketId,
//                        fieldName: "Title",
//                        oldValue: oldTitle,
//                        newValue: dto.Title,
//                        actorId: _loginContext.userId,
//                        actorName: _loginContext.userName
//                        ));
//                }

//                if (oldDescription != dto.Description)
//                {
//                    await _historyRepository.LogAsync(TicketHistoryHelper.TicketUpdated(
//                        issueId: ticketId,
//                        fieldName: "Description",
//                        oldValue: oldDescription,
//                        newValue: dto.Description,
//                        actorId: _loginContext.userId,
//                        actorName: _loginContext.userName
//                        ));
//                }

//                if (oldPriority != dto.Priority)
//                {
//                    await _historyRepository.LogAsync(TicketHistoryHelper.TicketUpdated(
//                        issueId: ticketId,
//                        fieldName: "Priority",
//                        oldValue: oldPriority,
//                        newValue: dto.Priority,
//                        actorId: _loginContext.userId,
//                        actorName: _loginContext.userName
//                        ));
//                }

//                if (dto.Due_Date.HasValue && oldDueDate != dto.Due_Date)
//                {
//                    await _historyRepository.LogAsync(TicketHistoryHelper.TicketUpdated(
//                        issueId: ticketId,
//                        fieldName: "Due Date",
//                        oldValue: oldDueDate?.ToString("yyyy-MM-dd"),
//                        newValue: dto.Due_Date?.ToString("yyyy-MM-dd"),
//                        actorId: _loginContext.userId,
//                        actorName: _loginContext.userName
//                        ));
//                }


//                if (dto.labelId != null)
//                {

//                    var labelNames = await _db.labelMaster
//                       .Where(lm => oldLabels.Select(ol => ol.Label_Id).Contains(lm.Id) || dto.labelId.Select(d => d.Id).Contains(lm.Id))
//                       .ToDictionaryAsync(lm => lm.Id, lm => lm.Title);

//                    var oldLabelNames = string.Join(", ", oldLabels
//                        .Select(ol => labelNames.GetValueOrDefault(ol.Label_Id ?? 0, "Unknown"))
//                        .Where(name => name != "Unknown"));

//                    var newLabelNames = string.Join(", ", dto.labelId
//                        .Select(nl => labelNames.GetValueOrDefault(nl.Id ?? 0, "Unknown"))
//                        .Where(name => name != "Unknown"));

//                    if (oldLabelNames != newLabelNames)
//                    {
//                        await _historyRepository.LogAsync(TicketHistoryHelper.TicketUpdated(
//                            issueId: ticketId,
//                            fieldName: "Label",
//                            oldValue: string.IsNullOrEmpty(oldLabelNames) ? "None" : oldLabelNames,
//                            newValue: string.IsNullOrEmpty(newLabelNames) ? "None" : newLabelNames,
//                            actorId: _loginContext.userId,
//                            actorName: _loginContext.userName
//                            ));
//                    }
//                    // 🔥 ADD THIS: Actually save the new labels to the database!
//                    var newLabels = dto.labelId.Select(l => new IssueLabel
//                    {
//                        Issue_Id = ticketId,
//                        Label_Id = l.Id
//                    }).ToList();

//                    await _domainService.UpdateLabelAsync(ticketId, newLabels);
//                }

//                if (dto.resourceIds != null)
//                    {
//                        var updateResourceIds = dto.resourceIds
//                            .Where(r => r.Id.HasValue)
//                            .Select(r => r.Id!.Value)
//                            .ToList();

//                        // --- STEP 1: Find who was active before this update ---
//                        var currentlyActiveIds = await _db.WorkStreams
//                            .Where(ws =>
//                                ws.IssueId == ticketId &&
//                                ws.StreamStatus != StatusId.Inactive &&
//                                ws.StreamStatus != StatusId.Cancelled)
//                            .Select(ws => ws.ResourceId!.Value)
//                            .ToListAsync();

//                        // --- STEP 2: Find who was removed ---
//                        var removedIds = currentlyActiveIds
//                            .Where(id => !updateResourceIds.Contains(id))
//                            .ToList();

//                        // --- STEP 3: Mark removed people Inactive ---
//                        // We use UpsertWorkStreamsAsync and explicitly force the status to Inactive!
//                        foreach (var removedId in removedIds)
//                        {
//                            await _workStreamService.UpsertWorkStreamsAsync(
//                                new WorkStreamContext
//                                {
//                                    IssueId = ticketId,
//                                    ResourceId = removedId,
//                                    StreamStatus = StatusId.Inactive, // 🔥 Force the status to Inactive!
//                                    CompletionPct = null,
//                                    TargetDate = null
//                                }
//                            );
//                        }

//                        // --- STEP 4: Upsert the remaining/new people ---
//                        if (updateResourceIds.Any())
//                        {
//                            foreach (var resourceId in updateResourceIds)
//                            {
//                                await _workStreamService.UpsertWorkStreamsAsync(
//                                    new WorkStreamContext
//                                    {
//                                        IssueId = ticketId,
//                                        ResourceId = resourceId,
//                                        StreamStatus = null, // Auto-resolves from department inside Upsert
//                                        CompletionPct = 0,
//                                        TargetDate = dto.TargetDate
//                                    }
//                                );
//                            }
//                        }
//                    }

//                // 2. THIS MUST BE OUTSIDE THE RESOURCE IDS IF STATEMENT
//                if (dto.temp?.temps != null && dto.temp.temps.Any())
//                    await _attachmentService.CleanupTempFiles(dto.temp);

//                return _mapper.Map<GetTickets>(updatedTicket);

//            }); // <--- 3. PROPERLY CLOSE THE LAMBDA AND METHOD CALL HERE
//        }
//        catch (Exception ex)
//        {
//            if (attachmentResult?.PermanentFilePathsCreated?.Any() == true)
//                _attachmentService.RollbackPhysicalFiles(attachmentResult.PermanentFilePathsCreated);

//            throw new Exception($"Ticket update failed. Everything was rolled back safely.{ex}", ex);
//        }

//        var richTicketData = await _syncExecutionService.FetchRichDataAsync<GetTickets>(
//           configKey: "TicketsList",
//           syncParams: new Dictionary<string, string> { { "IssueId", finalTicketData.Issue_Id.ToString() } },
//           matchPredicate: p => p.Issue_Id == finalTicketData.Issue_Id,
//           fallbackData: finalTicketData,
//           lastSync: null
//       );

//        if (richTicketData != null)
//        {
//            try
//            {
//                await _realtimeNotifier.BroadcastAsync(new RealtimeMessage
//                {
//                    Entity = "Ticket",
//                    Action = "Update",
//                    Payload = richTicketData,
//                    KeyField = "Issue_Id",
//                    RepoKey = richTicketData.RepoKey,
//                    Timestamp = DateTime.UtcNow
//                });
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Failed to broadcast Ticket update: {ex.Message}");
//            }
//        }

//        return richTicketData;
//    }        
//    public async Task<GetTickets> UpdateTicketStatusAsync(Guid ticketId, UpdateTicketStatusDto dto)
//    {
//        GetTickets finalTicketData = null;

//        try
//        {
//            finalTicketData = await _domainService.ExecuteInTransactionAsync(async () =>
//            {
//                var updatedTicket = await _domainService.UpdateEntityWithAttachmentsAsync<TicketMaster>(
//                    ticketId,
//                    entity =>
//                    {
//                        // Only Status — nothing else in the row is touched
//                        entity.Status = dto.Status;
//                    }
//                    // no newAttachments — defaults to null
//                );

//                return _mapper.Map<GetTickets>(updatedTicket);
//            });
//        }
//        catch (Exception ex)
//        {
//            throw new Exception("Ticket status update failed. Everything was rolled back safely.", ex);
//        }

//        var richTicketData = await _syncExecutionService.FetchRichDataAsync<GetTickets>(

//           configKey: "TicketsList",
//           syncParams: new Dictionary<string, string> { { "IssueId", finalTicketData.Issue_Id.ToString() } },
//           matchPredicate: p => p.Issue_Id == finalTicketData.Issue_Id,
//           fallbackData: finalTicketData,
//           lastSync: null // Optional: pass DateTimeOffset if your SP requires it
//       );

//        if (richTicketData != null)
//        {
//            try
//            {
//                await _realtimeNotifier.BroadcastAsync(new RealtimeMessage
//                {
//                    Entity = "Ticket",
//                    Action = "StatusUpdate",
//                    Payload = richTicketData,
//                    KeyField = "Issue_Id",
//                    RepoKey = richTicketData.RepoKey,
//                    Timestamp = DateTime.UtcNow
//                });
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Failed to broadcast Ticket status update: {ex.Message}");
//            }
//        }

//        return richTicketData;
//    }
//}

#region update ticket
//public async Task<GetTickets> UpdateTicketAsync(Guid ticketId, UpdateTicketDto dto)
//{
//    ProcessedAttachmentResult attachmentResult = null;
//    GetTickets finalTicketData = null;

//    try
//    {
//        finalTicketData = await _domainService.ExecuteInTransactionAsync(async () =>
//        {
//            string finalHtmlDescription = dto.Description ?? string.Empty;

//            // ── Step 1: AttachmentMaster ──────────────────────────────
//            if (dto.temp?.temps != null && dto.temp.temps.Any())
//            {
//                var timer = _stepContext.StartStep();
//                try
//                {
//                    var permUserId = $"{_loginContext.userId}-{_loginContext.userName}";
//                    var relativePath = $"{permUserId}/{ticketId}";

//                    attachmentResult = await _attachmentService.ProcessAndCopyAttachmentsAsync(
//                        dto.Description,
//                        dto.temp.temps,
//                        relativePath,
//                        ticketId.ToString(),
//                        "TicketMaster");

//                    finalHtmlDescription = attachmentResult.UpdatedHtml;

//                    var attachmentIds = string.Join(",",
//                        attachmentResult.Attachments.Select(a => a.AttachmentId));
//                    _stepContext.Success("AttachmentMaster", "INSERT", attachmentIds, timer);
//                }
//                catch (Exception ex)
//                {
//                    _stepContext.Failure("AttachmentMaster", "INSERT",
//                        ex.Message, ex.InnerException?.Message, timer);
//                    throw;
//                }
//            }

//            var capturedHtml = finalHtmlDescription;
//            var existingTicket = await _db.ISSUEMASTER.FindAsync(ticketId);
//            if (existingTicket == null)
//                throw new Exception("Ticket not found");

//            var oldTitle = existingTicket.Title;
//            var oldDescription = existingTicket.HtmlDesc;
//            var oldPriority = existingTicket.Priority?.ToString();
//            var oldDueDate = existingTicket.Due_Date;
//            var oldStatus = existingTicket.Status?.ToString();

//            var oldLabels = await _db.ISSUE_LABELS
//                .Where(il => il.Issue_Id == ticketId)
//                .ToListAsync();

//            var currentlyActiveAssignees = await _db.WorkStreams
//                .Where(ws =>
//                    ws.IssueId == ticketId &&
//                    ws.StreamStatus != StatusId.Inactive &&
//                    ws.StreamStatus != StatusId.Cancelled)
//                .Join(_db.eMPLOYEEMASTERs,
//                    ws => ws.ResourceId,
//                    e => e.EmployeeID,
//                    (ws, e) => new { ws.ResourceId, e.EmployeeName })
//                .ToListAsync();

//            // ── Step 2: TicketMaster ──────────────────────────────────
//            TicketMaster updatedTicket;
//            {
//                var timer = _stepContext.StartStep();
//                try
//                {
//                    updatedTicket = await _domainService.UpdateEntityWithAttachmentsAsync<TicketMaster>(
//                        ticketId,
//                        entity =>
//                        {
//                            entity.Title = dto.Title;
//                            entity.HtmlDesc = capturedHtml;
//                            entity.Description = HtmlUtilities.ConvertToPlainText(capturedHtml);
//                            entity.Priority = dto.Priority;
//                            entity.Hours = dto.Hours;

//                            if (dto.Assignee_Id.HasValue)
//                                entity.Assignee_Id = dto.Assignee_Id.Value;

//                            if (dto.Due_Date.HasValue)
//                                entity.Due_Date = dto.Due_Date.Value;

//                            if (dto.Status.HasValue)
//                                entity.Status = dto.Status.Value;
//                        },
//                        attachmentResult?.Attachments);

//                    _stepContext.Success("TicketMaster", "UPDATE", ticketId.ToString(), timer);
//                }
//                catch (Exception ex)
//                {
//                    _stepContext.Failure("TicketMaster", "UPDATE",
//                        ex.Message, ex.InnerException?.Message, timer);
//                    throw;
//                }
//            }

//            // History logs for changed fields (no step log needed — these are audit rows)
//            if (oldTitle != dto.Title)
//                await _historyRepository.LogAsync(TicketHistoryHelper.TicketUpdated(
//                    issueId: ticketId, fieldName: "Title",
//                    oldValue: oldTitle, newValue: dto.Title,
//                    actorId: _loginContext.userId, actorName: _loginContext.userName));

//            string currentHtml = (oldDescription ?? string.Empty).Trim();
//            string incomingHtml = (dto.Description ?? string.Empty).Trim();

//            // 2. Compare the cleaned strings
//            if (!string.Equals(currentHtml, incomingHtml, StringComparison.OrdinalIgnoreCase))
//            {
//                // 3. Truncate the values to prevent the SQL crash we fixed earlier!
//                int maxLength = 100; // Adjust based on your DB column size

//                string safeOldDesc = HtmlUtilities.ConvertToPlainText(currentHtml);
//                safeOldDesc = safeOldDesc.Length > maxLength ? safeOldDesc.Substring(0, maxLength) + "..." : safeOldDesc;

//                string safeNewDesc = HtmlUtilities.ConvertToPlainText(incomingHtml);
//                safeNewDesc = safeNewDesc.Length > maxLength ? safeNewDesc.Substring(0, maxLength) + "..." : safeNewDesc;

//                await _historyRepository.LogAsync(TicketHistoryHelper.TicketUpdated(
//                    issueId: ticketId,
//                    fieldName: "Description",
//                    oldValue: safeOldDesc,
//                    newValue: safeNewDesc,
//                    actorId: _loginContext.userId,
//                    actorName: _loginContext.userName));
//            }

//            if (oldPriority != dto.Priority)
//                await _historyRepository.LogAsync(TicketHistoryHelper.TicketUpdated(
//                    issueId: ticketId, fieldName: "Priority",
//                    oldValue: oldPriority, newValue: dto.Priority,
//                    actorId: _loginContext.userId, actorName: _loginContext.userName));

//            if (dto.Due_Date.HasValue && oldDueDate != dto.Due_Date)
//                await _historyRepository.LogAsync(TicketHistoryHelper.TicketUpdated(
//                    issueId: ticketId, fieldName: "Due Date",
//                    oldValue: oldDueDate?.ToString("yyyy-MM-dd"),
//                    newValue: dto.Due_Date?.ToString("yyyy-MM-dd"),
//                    actorId: _loginContext.userId, actorName: _loginContext.userName));

//            // ── Step 3: IssueLabels ───────────────────────────────────
//            if (dto.labelId != null)
//            {
//                var timer = _stepContext.StartStep();
//                try
//                {
//                    var labelNames = await _db.labelMaster
//                        .Where(lm =>
//                            oldLabels.Select(ol => ol.Label_Id).Contains(lm.Id) ||
//                            dto.labelId.Select(d => d.Id).Contains(lm.Id))
//                        .ToDictionaryAsync(lm => lm.Id, lm => lm.Title);

//                    var oldLabelNames = string.Join(", ", oldLabels
//                        .Select(ol => labelNames.GetValueOrDefault(ol.Label_Id ?? 0, "Unknown"))
//                        .Where(name => name != "Unknown"));

//                    var newLabelNames = string.Join(", ", dto.labelId
//                        .Select(nl => labelNames.GetValueOrDefault(nl.Id ?? 0, "Unknown"))
//                        .Where(name => name != "Unknown"));

//                    if (oldLabelNames != newLabelNames)
//                        await _historyRepository.LogAsync(TicketHistoryHelper.TicketUpdated(
//                            issueId: ticketId, fieldName: "Label",
//                            oldValue: string.IsNullOrEmpty(oldLabelNames) ? "None" : oldLabelNames,
//                            newValue: string.IsNullOrEmpty(newLabelNames) ? "None" : newLabelNames,
//                            actorId: _loginContext.userId, actorName: _loginContext.userName));

//                    var newLabels = dto.labelId.Select(l => new IssueLabel
//                    {
//                        Issue_Id = ticketId,
//                        Label_Id = l.Id
//                    }).ToList();

//                    await _domainService.UpdateLabelAsync(ticketId, newLabels);

//                    var labelIds = string.Join(",", dto.labelId.Select(l => l.Id));
//                    _stepContext.Success("IssueLabels", "UPDATE", labelIds, timer);
//                }
//                catch (Exception ex)
//                {
//                    _stepContext.Failure("IssueLabels", "UPDATE",
//                        ex.Message, ex.InnerException?.Message, timer);
//                    throw;
//                }
//            }

//            // ── Step 4: WorkStream ────────────────────────────────────
//            if (dto.resourceIds != null)
//            {
//                var timer = _stepContext.StartStep();
//                try
//                {
//                    var updateResourceIds = dto.resourceIds
//                        .Where(r => r.Id.HasValue)
//                        .Select(r => r.Id!.Value)
//                        .ToList();

//                    var currentlyActiveIds = await _db.WorkStreams
//                        .Where(ws =>
//                            ws.IssueId == ticketId &&
//                            ws.StreamStatus != StatusId.Inactive &&
//                            ws.StreamStatus != StatusId.Cancelled)
//                        .Select(ws => ws.ResourceId!.Value)
//                        .ToListAsync();

//                    // Deactivate removed assignees
//                    var removedIds = currentlyActiveIds
//                        .Where(id => !updateResourceIds.Contains(id))
//                        .ToList();

//                    foreach (var removedId in removedIds)
//                    {
//                        await _workStreamService.UpsertWorkStreamsAsync(
//                            new WorkStreamContext
//                            {
//                                IssueId = ticketId,
//                                ResourceId = removedId,
//                                StreamStatus = StatusId.Inactive,
//                                CompletionPct = null,
//                                TargetDate = null
//                            });
//                    }

//                    // Upsert remaining / new assignees
//                    if (updateResourceIds.Any())
//                    {
//                        foreach (var resourceId in updateResourceIds)
//                        {
//                            await _workStreamService.UpsertWorkStreamsAsync(
//                                new WorkStreamContext
//                                {
//                                    IssueId = ticketId,
//                                    ResourceId = resourceId,
//                                    StreamStatus = null,
//                                    CompletionPct = 0,
//                                    TargetDate = dto.TargetDate
//                                });
//                        }
//                    }

//                    var streamIds = string.Join(",", updateResourceIds);
//                    _stepContext.Success("WorkStream", "UPDATE", streamIds, timer);
//                }
//                catch (Exception ex)
//                {
//                    _stepContext.Failure("WorkStream", "UPDATE",
//                        ex.Message, ex.InnerException?.Message, timer);
//                    throw;
//                }
//            }

//            if (dto.temp?.temps != null && dto.temp.temps.Any())
//                await _attachmentService.CleanupTempFiles(dto.temp);

//            return _mapper.Map<GetTickets>(updatedTicket);
//        });
//    }
//    catch (Exception ex)
//    {
//        if (attachmentResult?.PermanentFilePathsCreated?.Any() == true)
//            _attachmentService.RollbackPhysicalFiles(attachmentResult.PermanentFilePathsCreated);

//        throw new Exception($"Ticket update failed. Everything was rolled back safely.{ex}", ex);
//    }

//    var richTicketData = await _syncExecutionService.FetchRichDataAsync<GetTickets>(
//        configKey: "TicketsList",
//        syncParams: new Dictionary<string, string> { { "IssueId", finalTicketData.Issue_Id.ToString() } },
//        matchPredicate: p => p.Issue_Id == finalTicketData.Issue_Id,
//        fallbackData: finalTicketData,
//        lastSync: null);

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
#endregion