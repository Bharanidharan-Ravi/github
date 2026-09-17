using APIGateWay.Business_Layer.Interface;
using APIGateWay.BusinessLayer.SignalRHub;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.ChatsModal.DTOs;
using APIGateWay.ModalLayer.ChatsModal.Master;
using APIGateWay.ModalLayer.MasterData;
using APIGateWay.ModelLayer.ErrorException;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace APIGateWay.BusinessLayer.Repository
{
    /// <summary>
    /// End-to-end encrypted conversations (user-based keys). The server stores and relays
    /// ciphertext only: it checks membership and that the content key is wrapped exactly
    /// once for every member, but it can never unwrap a key or read a message.
    /// </summary>
    public class ChatRepo : IChatRepo
    {
        public const string MessageEvent = "ChatMessage";
        public const string ReadEvent = "ChatRead";
        public const string ReactionEvent = "MessageReactionChanged";
        public const string MentionEvent = "ChatMention";

        private const int DefaultPageSize = 50;
        private const int MaxPageSize = 100;

        // EncryptedPayload = [12-byte IV | ciphertext + 16-byte tag]
        private const int MinPayloadLength = 12 + 16 + 1;
        private const int MaxPayloadLength = 64_000;

        // AES-KW of a 32-byte AES-256 content key
        private const int WrappedKeyLength = 40;

        private const int MaxGroupTitleLength = 100;
        private const int MaxGroupMembers = 200;
        private const int MaxEmojiLength = 16;

        private const int MaxTagsPerMessage = 30;
        private const int MaxTagEntityIdLength = 100;
        private const int MaxTagDisplayTextLength = 100;

        // Encrypted media/voice-note attachments (Phase 8)
        private const long MaxMediaBytes = 50 * 1024 * 1024; // 50 MB — images/files/voice notes, not full video
        // [12-byte wrap IV | AES-GCM-wrapped 32-byte file key | 16-byte tag]
        private const int EncryptedFileKeyLength = 12 + 32 + 16;
        private const int FileIvLength = 12;
        private const int MaxOriginalFileNameLength = 255;
        private const int MaxMimeTypeLength = 100;

        private readonly IDomainService _domainService;
        private readonly ILoginContextService _loginContext;
        private readonly IRequestStepContext _stepContext;
        private readonly IHubContext<RealtimeHub> _hub;
        private readonly ILogger<ChatRepo> _logger;
        private readonly IConfiguration _configuration;

        public ChatRepo(
            IDomainService domainService,
            ILoginContextService loginContext,
            IRequestStepContext stepContext,
            IHubContext<RealtimeHub> hub,
            ILogger<ChatRepo> logger,
            IConfiguration configuration)
        {
            _domainService = domainService;
            _loginContext = loginContext;
            _stepContext = stepContext;
            _hub = hub;
            _logger = logger;
            _configuration = configuration;
        }

        private string MediaStorageRoot => _configuration["ChatMedia:StorageFolder"]
            ?? throw new InvalidOperationException("Configuration \"ChatMedia:StorageFolder\" is required.");

        public async Task<List<ConversationDto>> GetMyConversationsAsync()
        {
            var me = _loginContext.userId;

            var myMemberships = await _domainService.Query<ChatConversationMember>().AsNoTracking()
                .Where(m => m.UserId == me)
                .ToListAsync();
            if (myMemberships.Count == 0) return new List<ConversationDto>();

            var ids = myMemberships.Select(m => m.ConversationId).ToList();

            var conversations = await _domainService.Query<ChatConversation>().AsNoTracking()
                .Where(c => ids.Contains(c.Id))
                .ToListAsync();

            var members = await _domainService.Query<ChatConversationMember>().AsNoTracking()
                .Where(m => ids.Contains(m.ConversationId))
                .Select(m => new { m.ConversationId, m.UserId })
                .ToListAsync();

            var messages = _domainService.Query<ChatEncryptedMessage>().AsNoTracking();
            var latest = await messages
                .Where(m => ids.Contains(m.ConversationId)
                            && m.CreatedAt == messages.Where(x => x.ConversationId == m.ConversationId).Max(x => x.CreatedAt))
                .ToListAsync();
            // Two messages with the same timestamp — keep one per conversation
            var latestByConversation = latest
                .GroupBy(m => m.ConversationId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(m => m.Id).First());

            var latestIds = latestByConversation.Values.Select(m => m.Id).ToList();
            var latestKeys = await _domainService.Query<ChatMessageKey>().AsNoTracking()
                .Where(k => latestIds.Contains(k.MessageId) && k.RecipientUserId == me)
                .ToDictionaryAsync(k => k.MessageId, k => k.WrappedMessageKey);

            var unread = await (
                from member in _domainService.Query<ChatConversationMember>().AsNoTracking()
                where member.UserId == me
                join message in _domainService.Query<ChatEncryptedMessage>().AsNoTracking()
                    on member.ConversationId equals message.ConversationId
                where message.SenderUserId != me
                      && (member.LastReadAt == null || message.CreatedAt > member.LastReadAt)
                group message by message.ConversationId into g
                select new { ConversationId = g.Key, Count = g.Count() }
            ).ToDictionaryAsync(x => x.ConversationId, x => x.Count);

            return conversations
                // An opened-but-empty chat only shows up for the person who opened it
                .Where(c => latestByConversation.ContainsKey(c.Id) || c.CreatedByUserId == me)
                .Select(c =>
                {
                    latestByConversation.TryGetValue(c.Id, out var last);
                    return new ConversationDto
                    {
                        ConversationId = c.Id,
                        Type = (int)c.Type,
                        Title = c.Title,
                        MemberUserIds = members.Where(m => m.ConversationId == c.Id).Select(m => m.UserId).ToList(),
                        CreatedAt = c.CreatedAt,
                        LastMessageAt = last?.CreatedAt,
                        LastReadAt = myMemberships.First(m => m.ConversationId == c.Id).LastReadAt,
                        UnreadCount = unread.TryGetValue(c.Id, out var count) ? count : 0,
                        LastMessage = last == null
                            ? null
                            : ToDto(last, latestKeys.TryGetValue(last.Id, out var key) ? key : null),
                    };
                })
                .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
                .ToList();
        }

        public async Task<ConversationDto> OpenDirectConversationAsync(Guid otherUserId)
        {
            var me = _loginContext.userId;
            if (otherUserId == Guid.Empty || otherUserId == me)
                throw new Exceptionlist.InvalidDataException("Choose another user to chat with.");

            var userExists = await _domainService.Query<LOGIN_MASTER>().AsNoTracking()
                .AnyAsync(u => u.UserID == otherUserId);
            if (!userExists)
                throw new Exceptionlist.DataNotFoundException("User not found.");

            var directKey = DirectKeyFor(me, otherUserId);
            var existing = await FindDirectAsync(directKey);
            if (existing != null) return existing;

            try
            {
                return await _domainService.ExecuteInTransactionAsync(async () =>
                {
                    var timer = _stepContext.StartStep();
                    try
                    {
                        var now = IndiaNow();
                        var conversation = new ChatConversation
                        {
                            Id = Guid.NewGuid(),
                            Type = ChatConversationType.Direct,
                            DirectKey = directKey,
                            CreatedByUserId = me,
                            CreatedAt = now,
                        };
                        await _domainService.SaveEntityAsync(conversation);
                        await _domainService.SaveEntitiesAsync(new List<ChatConversationMember>
                        {
                            new() { ConversationId = conversation.Id, UserId = me, Role = ChatMemberRole.Member, JoinedAt = now },
                            new() { ConversationId = conversation.Id, UserId = otherUserId, Role = ChatMemberRole.Member, JoinedAt = now },
                        });

                        _stepContext.Success("ChatConversations", "INSERT", conversation.Id.ToString(), timer);
                        return new ConversationDto
                        {
                            ConversationId = conversation.Id,
                            Type = (int)conversation.Type,
                            MemberUserIds = new List<Guid> { me, otherUserId },
                            CreatedAt = now,
                        };
                    }
                    catch (Exception ex)
                    {
                        _stepContext.Failure("ChatConversations", "INSERT", ex.Message, ex.InnerException?.Message, timer);
                        throw;
                    }
                });
            }
            catch (DbUpdateException)
            {
                // Both users opened the chat at the same moment — the unique DirectKey index rejected ours
                return await FindDirectAsync(directKey) ?? throw new Exceptionlist.InvalidDataException("Could not open the conversation.");
            }
        }

        public async Task<ConversationDto> OpenGroupConversationAsync(CreateGroupConversationDto dto)
        {
            var me = _loginContext.userId;
            if (dto == null)
                throw new Exceptionlist.InvalidDataException("Request body is required.");

            var title = (dto.Title ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(title))
                throw new Exceptionlist.InvalidDataException("Title is required.");
            if (title.Length > MaxGroupTitleLength)
                throw new Exceptionlist.InvalidDataException($"Title must be {MaxGroupTitleLength} characters or fewer.");

            var otherIds = (dto.MemberUserIds ?? new List<Guid>())
                .Where(id => id != Guid.Empty && id != me)
                .Distinct()
                .ToList();
            if (otherIds.Count == 0)
                throw new Exceptionlist.InvalidDataException("Choose at least one other member.");
            if (otherIds.Count + 1 > MaxGroupMembers)
                throw new Exceptionlist.InvalidDataException($"A group can have at most {MaxGroupMembers} members.");

            var existingUserCount = await _domainService.Query<LOGIN_MASTER>().AsNoTracking()
                .CountAsync(u => otherIds.Contains(u.UserID));
            if (existingUserCount != otherIds.Count)
                throw new Exceptionlist.DataNotFoundException("One or more users could not be found.");

            return await _domainService.ExecuteInTransactionAsync(async () =>
            {
                var timer = _stepContext.StartStep();
                try
                {
                    var now = IndiaNow();
                    var conversation = new ChatConversation
                    {
                        Id = Guid.NewGuid(),
                        Type = ChatConversationType.Group,
                        Title = title,
                        DirectKey = null,
                        CreatedByUserId = me,
                        CreatedAt = now,
                    };
                    await _domainService.SaveEntityAsync(conversation);

                    var members = new List<ChatConversationMember>
                    {
                        new() { ConversationId = conversation.Id, UserId = me, Role = ChatMemberRole.Admin, JoinedAt = now },
                    };
                    members.AddRange(otherIds.Select(id => new ChatConversationMember
                    {
                        ConversationId = conversation.Id,
                        UserId = id,
                        Role = ChatMemberRole.Member,
                        JoinedAt = now,
                    }));
                    await _domainService.SaveEntitiesAsync(members);

                    _stepContext.Success("ChatConversations", "INSERT", conversation.Id.ToString(), timer);
                    return new ConversationDto
                    {
                        ConversationId = conversation.Id,
                        Type = (int)conversation.Type,
                        Title = conversation.Title,
                        MemberUserIds = members.Select(m => m.UserId).ToList(),
                        CreatedAt = now,
                    };
                }
                catch (Exception ex)
                {
                    _stepContext.Failure("ChatConversations", "INSERT", ex.Message, ex.InnerException?.Message, timer);
                    throw;
                }
            });
        }

        public async Task<ConversationDto> UpdateGroupMembersAsync(Guid conversationId, ManageGroupMembersDto dto)
        {
            var me = _loginContext.userId;
            if (dto == null)
                throw new Exceptionlist.InvalidDataException("Request body is required.");

            var conversation = await _domainService.Query<ChatConversation>().AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == conversationId)
                ?? throw new Exceptionlist.DataNotFoundException("Conversation not found.");
            if (conversation.Type != ChatConversationType.Group)
                throw new Exceptionlist.InvalidDataException("Only group conversations have members that can be managed.");

            var members = await _domainService.Query<ChatConversationMember>().AsNoTracking()
                .Where(m => m.ConversationId == conversationId)
                .ToListAsync();
            var myMembership = members.FirstOrDefault(m => m.UserId == me)
                ?? throw new Exceptionlist.DataNotFoundException("Conversation not found.");
            if (myMembership.Role != ChatMemberRole.Admin)
                throw new Exceptionlist.UnauthorizedException("Only a group admin can add or remove members.");

            var removeIds = (dto.RemoveUserIds ?? new List<Guid>())
                .Where(id => id != Guid.Empty && members.Any(m => m.UserId == id))
                .Distinct()
                .ToList();
            var addIds = (dto.AddUserIds ?? new List<Guid>())
                .Where(id => id != Guid.Empty && !members.Any(m => m.UserId == id))
                .Distinct()
                .ToList();

            if (addIds.Count > 0)
            {
                var existingUserCount = await _domainService.Query<LOGIN_MASTER>().AsNoTracking()
                    .CountAsync(u => addIds.Contains(u.UserID));
                if (existingUserCount != addIds.Count)
                    throw new Exceptionlist.DataNotFoundException("One or more users could not be found.");
            }

            var remainingMembers = members.Where(m => !removeIds.Contains(m.UserId)).ToList();
            if (remainingMembers.Count == 0 && addIds.Count == 0)
                throw new Exceptionlist.InvalidDataException("A group must have at least one member.");
            var remainingAdmins = remainingMembers.Count(m => m.Role == ChatMemberRole.Admin);
            if (members.Any(m => m.Role == ChatMemberRole.Admin) && remainingAdmins == 0)
                throw new Exceptionlist.InvalidDataException("A group must keep at least one admin.");
            if (remainingMembers.Count + addIds.Count > MaxGroupMembers)
                throw new Exceptionlist.InvalidDataException($"A group can have at most {MaxGroupMembers} members.");

            return await _domainService.ExecuteInTransactionAsync(async () =>
            {
                var timer = _stepContext.StartStep();
                try
                {
                    var now = IndiaNow();
                    foreach (var userId in removeIds)
                    {
                        await _domainService.DeleteEntityAsync(new ChatConversationMember
                        {
                            ConversationId = conversationId,
                            UserId = userId,
                        });
                    }
                    if (addIds.Count > 0)
                    {
                        await _domainService.SaveEntitiesAsync(addIds.Select(id => new ChatConversationMember
                        {
                            ConversationId = conversationId,
                            UserId = id,
                            Role = ChatMemberRole.Member,
                            JoinedAt = now,
                        }).ToList());
                    }

                    _stepContext.Success("ChatConversationMembers", "UPDATE", conversationId.ToString(), timer);
                    return new ConversationDto
                    {
                        ConversationId = conversation.Id,
                        Type = (int)conversation.Type,
                        Title = conversation.Title,
                        MemberUserIds = remainingMembers.Select(m => m.UserId).Concat(addIds).Distinct().ToList(),
                        CreatedAt = conversation.CreatedAt,
                    };
                }
                catch (Exception ex)
                {
                    _stepContext.Failure("ChatConversationMembers", "UPDATE", ex.Message, ex.InnerException?.Message, timer);
                    throw;
                }
            });
        }

        public async Task<List<ChatMessageDto>> GetMessagesAsync(Guid conversationId, DateTime? before, int take)
        {
            await RequireMembersAsync(conversationId);
            take = Math.Clamp(take <= 0 ? DefaultPageSize : take, 1, MaxPageSize);
            var me = _loginContext.userId;

            var query = _domainService.Query<ChatEncryptedMessage>().AsNoTracking()
                .Where(m => m.ConversationId == conversationId);
            if (before.HasValue)
                query = query.Where(m => m.CreatedAt < before.Value);

            var page = await (
                from m in query
                join k in _domainService.Query<ChatMessageKey>().AsNoTracking().Where(k => k.RecipientUserId == me)
                    on m.Id equals k.MessageId into keys
                from k in keys.DefaultIfEmpty()
                orderby m.CreatedAt descending, m.Id descending
                select new { Message = m, Key = k == null ? null : k.WrappedMessageKey }
            ).Take(take).ToListAsync();

            var pageIds = page.Select(x => x.Message.Id).ToList();
            var reactions = await _domainService.Query<ChatMessageReaction>().AsNoTracking()
                .Where(r => pageIds.Contains(r.MessageId))
                .ToListAsync();
            var reactionsByMessage = reactions.GroupBy(r => r.MessageId)
                .ToDictionary(g => g.Key, g => g.Select(ToReactionDto).ToList());

            var tags = await _domainService.Query<ChatMessageTag>().AsNoTracking()
                .Where(t => pageIds.Contains(t.MessageId))
                .ToListAsync();
            var tagsByMessage = tags.GroupBy(t => t.MessageId)
                .ToDictionary(g => g.Key, g => g.Select(ToTagDto).ToList());

            var mediaByMessage = await _domainService.Query<ChatMediaAttachment>().AsNoTracking()
                .Where(a => pageIds.Contains(a.MessageId))
                .ToDictionaryAsync(a => a.MessageId, ToMediaDto);

            // Oldest first — the order a chat window renders in
            return page
                .OrderBy(x => x.Message.CreatedAt)
                .ThenBy(x => x.Message.Id)
                .Select(x => ToDto(
                    x.Message,
                    x.Key,
                    reactionsByMessage.TryGetValue(x.Message.Id, out var r) ? r : null,
                    tagsByMessage.TryGetValue(x.Message.Id, out var t) ? t : null,
                    mediaByMessage.TryGetValue(x.Message.Id, out var med) ? med : null))
                .ToList();
        }

        public async Task<ChatMessageDto> SendMessageAsync(Guid conversationId, SendMessageDto dto)
        {
            var me = _loginContext.userId;
            if (dto == null)
                throw new Exceptionlist.InvalidDataException("Request body is required.");
            var payload = ValidateEnvelope(dto.ClientMessageId, dto.EncryptedPayload);
            var memberIds = await RequireMembersAsync(conversationId);

            var existing = await FindOwnMessageAsync(dto.ClientMessageId);
            if (existing != null) return existing.ConversationId == conversationId
                ? existing
                : throw new Exceptionlist.InvalidDataException("ClientMessageId was already used for another conversation.");

            var messageKeys = ValidateRecipientKeys(dto.ClientMessageId, dto.Keys, memberIds);
            var tags = ValidateTags(dto.Tags, dto.ClientMessageId);

            if (dto.ReplyToMessageId.HasValue)
            {
                var replyTargetExists = await _domainService.Query<ChatEncryptedMessage>().AsNoTracking()
                    .AnyAsync(m => m.Id == dto.ReplyToMessageId.Value && m.ConversationId == conversationId);
                if (!replyTargetExists)
                    throw new Exceptionlist.InvalidDataException("The message being replied to could not be found in this conversation.");
            }

            var membersWithKeys = await _domainService.Query<ChatUserKey>().AsNoTracking()
                .CountAsync(k => memberIds.Contains(k.UserId));
            if (membersWithKeys != memberIds.Count)
                throw new Exceptionlist.InvalidDataException("Someone in this conversation hasn't set up secure chat yet.");

            ChatEncryptedMessage message;
            try
            {
                message = await _domainService.ExecuteInTransactionAsync(async () =>
                {
                    var timer = _stepContext.StartStep();
                    try
                    {
                        var entity = new ChatEncryptedMessage
                        {
                            // The client's id is the HKDF salt of every wrapped key, so it must be the message id
                            Id = dto.ClientMessageId,
                            ConversationId = conversationId,
                            SenderUserId = me,
                            ClientMessageId = dto.ClientMessageId.ToString("D"),
                            EncryptedPayload = payload,
                            ReplyToMessageId = dto.ReplyToMessageId,
                            CreatedAt = IndiaNow(),
                        };
                        await _domainService.SaveEntityAsync(entity);
                        await _domainService.SaveEntitiesAsync(messageKeys);
                        if (tags.Count > 0)
                            await _domainService.SaveEntitiesAsync(tags.Select(t => t.Entity).ToList());

                        _stepContext.Success("ChatEncryptedMessages", "INSERT", entity.Id.ToString(), timer);
                        return entity;
                    }
                    catch (Exception ex)
                    {
                        _stepContext.Failure("ChatEncryptedMessages", "INSERT", ex.Message, ex.InnerException?.Message, timer);
                        throw;
                    }
                });
            }
            catch (DbUpdateException)
            {
                // A retry raced this request, or the id is already taken by someone else's message
                existing = await FindOwnMessageAsync(dto.ClientMessageId);
                if (existing?.ConversationId == conversationId) return existing;
                throw new Exceptionlist.InvalidDataException("This message id is already in use. Send the message again.");
            }

            await BroadcastMessageAsync(message, messageKeys);
            await BroadcastMentionsAsync(message, tags, memberIds);

            var senderKey = messageKeys.First(k => k.RecipientUserId == me).WrappedMessageKey;
            var tagDtos = tags.Select(t => ToTagDto(t.Entity)).ToList();
            return ToDto(message, senderKey, null, tagDtos);
        }

        /// <summary>
        /// Same envelope/membership/tag validation as SendMessageAsync, plus the encrypted file itself.
        /// The file is written to disk before the DB transaction opens (so a failed transaction has a
        /// known path to delete); ChatMediaAttachments.Id is always set to ClientMessageId — one
        /// attachment per message in this design, so no separate media id needs to travel in the
        /// encrypted payload for the client to know what to fetch later.
        /// </summary>
        public async Task<ChatMessageDto> SendMediaMessageAsync(Guid conversationId, SendMediaMessageDto dto)
        {
            var me = _loginContext.userId;
            if (dto == null)
                throw new Exceptionlist.InvalidDataException("Request body is required.");

            var payload = ValidateEnvelope(dto.ClientMessageId, dto.EncryptedPayload);
            var memberIds = await RequireMembersAsync(conversationId);

            var existing = await FindOwnMessageAsync(dto.ClientMessageId);
            if (existing != null) return existing.ConversationId == conversationId
                ? existing
                : throw new Exceptionlist.InvalidDataException("ClientMessageId was already used for another conversation.");

            var messageKeys = ValidateRecipientKeys(dto.ClientMessageId, dto.Keys, memberIds);
            var tags = ValidateTags(dto.Tags, dto.ClientMessageId);

            if (dto.ReplyToMessageId.HasValue)
            {
                var replyTargetExists = await _domainService.Query<ChatEncryptedMessage>().AsNoTracking()
                    .AnyAsync(m => m.Id == dto.ReplyToMessageId.Value && m.ConversationId == conversationId);
                if (!replyTargetExists)
                    throw new Exceptionlist.InvalidDataException("The message being replied to could not be found in this conversation.");
            }

            var membersWithKeys = await _domainService.Query<ChatUserKey>().AsNoTracking()
                .CountAsync(k => memberIds.Contains(k.UserId));
            if (membersWithKeys != memberIds.Count)
                throw new Exceptionlist.InvalidDataException("Someone in this conversation hasn't set up secure chat yet.");

            var encryptedFileKey = DecodeBase64(dto.EncryptedFileKey, "EncryptedFileKey");
            if (encryptedFileKey.Length != EncryptedFileKeyLength)
                throw new Exceptionlist.InvalidDataException("EncryptedFileKey has an unexpected length.");
            var fileIv = DecodeBase64(dto.Iv, "Iv");
            if (fileIv.Length != FileIvLength)
                throw new Exceptionlist.InvalidDataException("Iv must be 12 bytes.");

            var originalFileName = (dto.OriginalFileName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(originalFileName) || originalFileName.Length > MaxOriginalFileNameLength)
                throw new Exceptionlist.InvalidDataException("OriginalFileName is missing or too long.");
            var mimeType = (dto.MimeType ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(mimeType) || mimeType.Length > MaxMimeTypeLength)
                throw new Exceptionlist.InvalidDataException("MimeType is missing or too long.");

            if (dto.File == null || dto.File.Length == 0)
                throw new Exceptionlist.InvalidDataException("A file is required.");
            if (dto.File.Length > MaxMediaBytes)
                throw new Exceptionlist.InvalidDataException($"Files must be {MaxMediaBytes / (1024 * 1024)} MB or smaller.");

            var relativePath = Path.Combine(conversationId.ToString("D"), dto.ClientMessageId.ToString("D") + ".bin");
            var absolutePath = Path.Combine(MediaStorageRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

            try
            {
                using var fileStream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write);
                await dto.File.CopyToAsync(fileStream);
            }
            catch (Exception)
            {
                TryDeleteFile(absolutePath);
                throw new Exceptionlist.InvalidDataException("Could not save the file.");
            }

            ChatEncryptedMessage message;
            MediaAttachmentDto mediaDto;
            try
            {
                (message, mediaDto) = await _domainService.ExecuteInTransactionAsync(async () =>
                {
                    var timer = _stepContext.StartStep();
                    try
                    {
                        var now = IndiaNow();
                        var entity = new ChatEncryptedMessage
                        {
                            Id = dto.ClientMessageId,
                            ConversationId = conversationId,
                            SenderUserId = me,
                            ClientMessageId = dto.ClientMessageId.ToString("D"),
                            EncryptedPayload = payload,
                            ReplyToMessageId = dto.ReplyToMessageId,
                            CreatedAt = now,
                        };
                        await _domainService.SaveEntityAsync(entity);
                        await _domainService.SaveEntitiesAsync(messageKeys);
                        if (tags.Count > 0)
                            await _domainService.SaveEntitiesAsync(tags.Select(t => t.Entity).ToList());

                        var attachment = new ChatMediaAttachment
                        {
                            Id = dto.ClientMessageId,
                            MessageId = entity.Id,
                            StoragePath = relativePath,
                            OriginalFileName = originalFileName,
                            MimeType = mimeType,
                            FileSize = dto.File.Length,
                            EncryptedFileKey = encryptedFileKey,
                            Iv = fileIv,
                            CreatedAt = now,
                        };
                        await _domainService.SaveEntityAsync(attachment);

                        _stepContext.Success("ChatEncryptedMessages", "INSERT", entity.Id.ToString(), timer);
                        return (entity, ToMediaDto(attachment));
                    }
                    catch (Exception ex)
                    {
                        _stepContext.Failure("ChatEncryptedMessages", "INSERT", ex.Message, ex.InnerException?.Message, timer);
                        throw;
                    }
                });
            }
            catch (DbUpdateException)
            {
                TryDeleteFile(absolutePath);
                // A retry raced this request, or the id is already taken by someone else's message
                existing = await FindOwnMessageAsync(dto.ClientMessageId);
                if (existing?.ConversationId == conversationId) return existing;
                throw new Exceptionlist.InvalidDataException("This message id is already in use. Send the message again.");
            }
            catch
            {
                TryDeleteFile(absolutePath);
                throw;
            }

            await BroadcastMessageAsync(message, messageKeys, mediaDto);
            await BroadcastMentionsAsync(message, tags, memberIds);

            var senderKey = messageKeys.First(k => k.RecipientUserId == me).WrappedMessageKey;
            var tagDtos = tags.Select(t => ToTagDto(t.Entity)).ToList();
            return ToDto(message, senderKey, null, tagDtos, mediaDto);
        }

        /// <summary>Base64 of the encrypted file's bytes, after checking the caller belongs to the
        /// conversation the media's message is in. 404 (not 403) when missing or not a member. See the
        /// interface doc comment for why this returns base64/JSON instead of a raw byte stream.</summary>
        public async Task<string> GetMediaBase64Async(Guid mediaId)
        {
            var media = await _domainService.Query<ChatMediaAttachment>().AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == mediaId)
                ?? throw new Exceptionlist.DataNotFoundException("File not found.");

            var message = await _domainService.Query<ChatEncryptedMessage>().AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == media.MessageId)
                ?? throw new Exceptionlist.DataNotFoundException("File not found.");

            await RequireMembersAsync(message.ConversationId);

            var absolutePath = Path.Combine(MediaStorageRoot, media.StoragePath);
            if (!File.Exists(absolutePath))
                throw new Exceptionlist.DataNotFoundException("File not found.");

            var bytes = await File.ReadAllBytesAsync(absolutePath);
            return Convert.ToBase64String(bytes);
        }

        public async Task<ConversationReadDto> MarkReadAsync(Guid conversationId, MarkConversationReadDto dto)
        {
            await RequireMembersAsync(conversationId);
            var me = _loginContext.userId;

            var now = IndiaNow();
            var readUpTo = dto.ReadUpTo.HasValue && dto.ReadUpTo.Value < now ? dto.ReadUpTo.Value : now;

            var result = await _domainService.ExecuteInTransactionAsync(async () =>
            {
                var timer = _stepContext.StartStep();
                try
                {
                    DateTime? lastReadAt = null;
                    await _domainService.UpdateTrackedEntityAsync<ChatConversationMember>(
                        m => m.ConversationId == conversationId && m.UserId == me,
                        m =>
                        {
                            // Never move the marker backwards (an older tab reporting late)
                            if (m.LastReadAt == null || readUpTo > m.LastReadAt) m.LastReadAt = readUpTo;
                            lastReadAt = m.LastReadAt;
                        });

                    _stepContext.Success("ChatConversationMembers", "UPDATE", conversationId.ToString(), timer);
                    return new ConversationReadDto { ConversationId = conversationId, LastReadAt = lastReadAt };
                }
                catch (Exception ex)
                {
                    _stepContext.Failure("ChatConversationMembers", "UPDATE", ex.Message, ex.InnerException?.Message, timer);
                    throw;
                }
            });

            // Other tabs / devices of this user clear their unread badge
            try
            {
                await _hub.Clients.Group($"user-{me}").SendAsync(ReadEvent, result);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Chat] Read-state broadcast failed for conversation {ConversationId}", conversationId);
            }

            return result;
        }

        public async Task<MessageReactionEventDto> ToggleReactionAsync(Guid messageId, ToggleReactionDto dto)
        {
            var me = _loginContext.userId;
            if (dto == null)
                throw new Exceptionlist.InvalidDataException("Request body is required.");

            var emoji = (dto.Emoji ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(emoji))
                throw new Exceptionlist.InvalidDataException("Emoji is required.");
            if (emoji.Length > MaxEmojiLength)
                throw new Exceptionlist.InvalidDataException($"Emoji must be {MaxEmojiLength} characters or fewer.");

            var message = await _domainService.Query<ChatEncryptedMessage>().AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == messageId)
                ?? throw new Exceptionlist.DataNotFoundException("Message not found.");

            await RequireMembersAsync(message.ConversationId);

            var existing = await _domainService.Query<ChatMessageReaction>().AsNoTracking()
                .FirstOrDefaultAsync(r => r.MessageId == messageId && r.UserId == me && r.Emoji == emoji);

            var added = existing == null;
            await _domainService.ExecuteInTransactionAsync(async () =>
            {
                var timer = _stepContext.StartStep();
                try
                {
                    if (existing != null)
                    {
                        await _domainService.DeleteEntityAsync(existing);
                    }
                    else
                    {
                        await _domainService.SaveEntityAsync(new ChatMessageReaction
                        {
                            Id = Guid.NewGuid(),
                            MessageId = messageId,
                            UserId = me,
                            Emoji = emoji,
                            CreatedAt = IndiaNow(),
                        });
                    }

                    _stepContext.Success("ChatMessageReactions", added ? "INSERT" : "DELETE", messageId.ToString(), timer);
                    return true;
                }
                catch (Exception ex)
                {
                    _stepContext.Failure("ChatMessageReactions", added ? "INSERT" : "DELETE", ex.Message, ex.InnerException?.Message, timer);
                    throw;
                }
            });

            var result = new MessageReactionEventDto
            {
                MessageId = messageId,
                ConversationId = message.ConversationId,
                UserId = me,
                Emoji = emoji,
                Added = added,
            };

            await BroadcastReactionAsync(result);
            return result;
        }

        #region Helpers

        /// <summary>Each member receives the message with only their own wrapped key.</summary>
        private async Task BroadcastMessageAsync(ChatEncryptedMessage message, List<ChatMessageKey> keys, MediaAttachmentDto? media = null)
        {
            try
            {
                await Task.WhenAll(keys.Select(k =>
                    _hub.Clients.Group($"user-{k.RecipientUserId}")
                        .SendAsync(MessageEvent, ToDto(message, k.WrappedMessageKey, media: media))));
            }
            catch (Exception ex)
            {
                // The message is stored; clients pick it up on their next fetch
                _logger.LogWarning(ex, "[Chat] Realtime delivery failed for message {MessageId}", message.Id);
            }
        }

        /// <summary>Alerts whoever a tag names or points at (a #ticket's assignee, an @mentioned user, ...),
        /// even if they aren't a member of this conversation. Best-effort, never blocks the send.</summary>
        private async Task BroadcastMentionsAsync(ChatEncryptedMessage message, List<(ChatMessageTag Entity, Guid? NotifyUserId)> tags, List<Guid> memberIds)
        {
            if (tags.Count == 0) return;
            try
            {
                var targets = new HashSet<Guid>();
                foreach (var (entity, notifyUserId) in tags)
                {
                    Guid? target = entity.EntityType == ChatTagEntityType.User
                        ? (Guid.TryParse(entity.EntityId, out var uid) ? uid : null)
                        : notifyUserId;
                    if (target.HasValue && target.Value != message.SenderUserId)
                        targets.Add(target.Value);
                }
                if (targets.Count == 0) return;

                var evt = new ChatMentionEventDto
                {
                    ConversationId = message.ConversationId,
                    MessageId = message.Id,
                    TaggedByUserId = message.SenderUserId,
                };
                await Task.WhenAll(targets.Select(id =>
                {
                    var forThisTarget = tags.First(t =>
                        (t.Entity.EntityType == ChatTagEntityType.User && Guid.TryParse(t.Entity.EntityId, out var uid) && uid == id)
                        || t.NotifyUserId == id);
                    return _hub.Clients.Group($"user-{id}").SendAsync(MentionEvent, new ChatMentionEventDto
                    {
                        ConversationId = evt.ConversationId,
                        MessageId = evt.MessageId,
                        TaggedByUserId = evt.TaggedByUserId,
                        EntityType = forThisTarget.Entity.EntityType,
                        EntityId = forThisTarget.Entity.EntityId,
                        DisplayText = forThisTarget.Entity.DisplayText,
                    });
                }));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Chat] Mention broadcast failed for message {MessageId}", message.Id);
            }
        }

        /// <summary>Every conversation member (including the actor) gets the delta instantly.</summary>
        private async Task BroadcastReactionAsync(MessageReactionEventDto evt)
        {
            try
            {
                var memberIds = await _domainService.Query<ChatConversationMember>().AsNoTracking()
                    .Where(m => m.ConversationId == evt.ConversationId)
                    .Select(m => m.UserId)
                    .ToListAsync();

                await Task.WhenAll(memberIds.Select(id =>
                    _hub.Clients.Group($"user-{id}").SendAsync(ReactionEvent, evt)));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Chat] Reaction broadcast failed for message {MessageId}", evt.MessageId);
            }
        }

        /// <summary>Member ids of the conversation; 404 (not 403) when the caller isn't one of them.</summary>
        private async Task<List<Guid>> RequireMembersAsync(Guid conversationId)
        {
            var memberIds = await _domainService.Query<ChatConversationMember>().AsNoTracking()
                .Where(m => m.ConversationId == conversationId)
                .Select(m => m.UserId)
                .ToListAsync();

            if (!memberIds.Contains(_loginContext.userId))
                throw new Exceptionlist.DataNotFoundException("Conversation not found.");

            return memberIds;
        }

        private async Task<ChatMessageDto?> FindOwnMessageAsync(Guid clientMessageId)
        {
            var me = _loginContext.userId;
            var clientId = clientMessageId.ToString("D");

            var found = await (
                from m in _domainService.Query<ChatEncryptedMessage>().AsNoTracking()
                where m.SenderUserId == me && m.ClientMessageId == clientId
                join k in _domainService.Query<ChatMessageKey>().AsNoTracking().Where(k => k.RecipientUserId == me)
                    on m.Id equals k.MessageId into keys
                from k in keys.DefaultIfEmpty()
                select new { Message = m, Key = k == null ? null : k.WrappedMessageKey }
            ).FirstOrDefaultAsync();
            if (found == null) return null;

            var media = await _domainService.Query<ChatMediaAttachment>().AsNoTracking()
                .FirstOrDefaultAsync(a => a.MessageId == found.Message.Id);
            return ToDto(found.Message, found.Key, media: media == null ? null : ToMediaDto(media));
        }

        private async Task<ConversationDto?> FindDirectAsync(string directKey)
        {
            var conversation = await _domainService.Query<ChatConversation>().AsNoTracking()
                .FirstOrDefaultAsync(c => c.DirectKey == directKey);
            if (conversation == null) return null;

            var memberIds = await _domainService.Query<ChatConversationMember>().AsNoTracking()
                .Where(m => m.ConversationId == conversation.Id)
                .Select(m => m.UserId)
                .ToListAsync();

            return new ConversationDto
            {
                ConversationId = conversation.Id,
                Type = (int)conversation.Type,
                Title = conversation.Title,
                MemberUserIds = memberIds,
                CreatedAt = conversation.CreatedAt,
            };
        }

        private static byte[] ValidateEnvelope(Guid clientMessageId, string? encryptedPayload)
        {
            if (clientMessageId == Guid.Empty)
                throw new Exceptionlist.InvalidDataException("ClientMessageId is required.");

            var payload = DecodeBase64(encryptedPayload, "EncryptedPayload");
            if (payload.Length < MinPayloadLength || payload.Length > MaxPayloadLength)
                throw new Exceptionlist.InvalidDataException("Message is empty or too long.");

            return payload;
        }

        /// <summary>Exactly one wrapped key per member — so nobody in the conversation is silently left unable to read it.</summary>
        private List<ChatMessageKey> ValidateRecipientKeys(Guid clientMessageId, List<MessageRecipientKeyDto>? dtoKeys, List<Guid> memberIds)
        {
            if (dtoKeys == null || dtoKeys.Count != memberIds.Count)
                throw new Exceptionlist.InvalidDataException("The message must be encrypted for every member of the conversation.");

            var keys = new List<ChatMessageKey>();
            foreach (var key in dtoKeys)
            {
                if (!memberIds.Contains(key.RecipientUserId))
                    throw new Exceptionlist.InvalidDataException("A message key targets someone outside this conversation.");
                if (keys.Any(k => k.RecipientUserId == key.RecipientUserId))
                    throw new Exceptionlist.InvalidDataException("Each member may appear only once.");

                var wrapped = DecodeBase64(key.WrappedMessageKey, "WrappedMessageKey");
                if (wrapped.Length != WrappedKeyLength)
                    throw new Exceptionlist.InvalidDataException("WrappedMessageKey has an unexpected length.");

                keys.Add(new ChatMessageKey
                {
                    MessageId = clientMessageId,
                    RecipientUserId = key.RecipientUserId,
                    WrappedMessageKey = wrapped,
                });
            }

            return keys;
        }

        private List<(ChatMessageTag Entity, Guid? NotifyUserId)> ValidateTags(List<MessageTagDto>? dtoTags, Guid messageId)
        {
            if (dtoTags == null || dtoTags.Count == 0) return new List<(ChatMessageTag, Guid?)>();
            if (dtoTags.Count > MaxTagsPerMessage)
                throw new Exceptionlist.InvalidDataException($"A message can reference at most {MaxTagsPerMessage} items.");

            var result = new List<(ChatMessageTag, Guid?)>();
            foreach (var tag in dtoTags)
            {
                var entityType = (tag.EntityType ?? string.Empty).Trim();
                if (!ChatTagEntityType.All.Contains(entityType))
                    throw new Exceptionlist.InvalidDataException($"Unsupported tag type: {entityType}.");

                var entityId = (tag.EntityId ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(entityId) || entityId.Length > MaxTagEntityIdLength)
                    throw new Exceptionlist.InvalidDataException("Tag EntityId is missing or too long.");
                if (entityType == ChatTagEntityType.User && !Guid.TryParse(entityId, out _))
                    throw new Exceptionlist.InvalidDataException("A User tag's EntityId must be a user id.");

                var displayText = (tag.DisplayText ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(displayText) || displayText.Length > MaxTagDisplayTextLength)
                    throw new Exceptionlist.InvalidDataException("Tag DisplayText is missing or too long.");

                result.Add((new ChatMessageTag
                {
                    Id = Guid.NewGuid(),
                    MessageId = messageId,
                    EntityType = entityType,
                    EntityId = entityId,
                    DisplayText = displayText,
                }, tag.NotifyUserId));
            }
            return result;
        }

        private static byte[] DecodeBase64(string? value, string field)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new Exceptionlist.InvalidDataException($"{field} is required.");
            try
            {
                return Convert.FromBase64String(value);
            }
            catch (FormatException)
            {
                throw new Exceptionlist.InvalidDataException($"{field} is not valid base64.");
            }
        }

        private void TryDeleteFile(string absolutePath)
        {
            try
            {
                if (File.Exists(absolutePath)) File.Delete(absolutePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Chat] Failed to remove orphaned media file {Path}", absolutePath);
            }
        }

        private static string DirectKeyFor(Guid a, Guid b)
        {
            var (first, second) = a.CompareTo(b) <= 0 ? (a, b) : (b, a);
            return $"{first:D}|{second:D}";
        }

        private static ChatMessageDto ToDto(ChatEncryptedMessage m, byte[]? wrappedKey, List<MessageReactionDto>? reactions = null, List<MessageTagDto>? tags = null, MediaAttachmentDto? media = null) => new()
        {
            MessageId = m.Id,
            ConversationId = m.ConversationId,
            SenderUserId = m.SenderUserId,
            ClientMessageId = m.ClientMessageId,
            EncryptedPayload = Convert.ToBase64String(m.EncryptedPayload),
            ReplyToMessageId = m.ReplyToMessageId,
            CreatedAt = m.CreatedAt,
            WrappedMessageKey = wrappedKey == null ? null : Convert.ToBase64String(wrappedKey),
            Reactions = reactions ?? new List<MessageReactionDto>(),
            Tags = tags ?? new List<MessageTagDto>(),
            Media = media,
        };

        private static MessageReactionDto ToReactionDto(ChatMessageReaction r) => new()
        {
            UserId = r.UserId,
            Emoji = r.Emoji,
        };

        private static MessageTagDto ToTagDto(ChatMessageTag t) => new()
        {
            EntityType = t.EntityType,
            EntityId = t.EntityId,
            DisplayText = t.DisplayText,
        };

        private static MediaAttachmentDto ToMediaDto(ChatMediaAttachment a) => new()
        {
            MediaId = a.Id,
            OriginalFileName = a.OriginalFileName,
            MimeType = a.MimeType,
            FileSize = a.FileSize,
            EncryptedFileKey = Convert.ToBase64String(a.EncryptedFileKey),
            Iv = Convert.ToBase64String(a.Iv),
        };

        // Same clock the rest of the gateway writes to the database
        private static DateTime IndiaNow()
        {
            var ist = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ist);
        }

        #endregion
    }
}
