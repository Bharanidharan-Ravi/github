using System.Security.Cryptography;
using APIGateWay.Business_Layer.Interface;
using APIGateWay.BusinessLayer.SignalRHub;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.ChatsModal.DTOs;
using APIGateWay.ModalLayer.ChatsModal.Master;
using APIGateWay.ModalLayer.MasterData;
using APIGateWay.ModelLayer.ErrorException;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace APIGateWay.BusinessLayer.Repository
{
    /// <summary>
    /// 1:1 end-to-end encrypted chat. The server stores and relays ciphertext only;
    /// it validates membership and that wrapped keys target real devices of the
    /// conversation members, but it can never read message content.
    /// </summary>
    public class ChatRepo : IChatRepo
    {
        public const string RealtimeEvent = "ChatMessage";

        private const int MaxPageSize = 100;
        private const int MaxCiphertextLength = 64_000;
        private const int MaxKeysPerMessage = 50;
        private const int GcmIvLength = 12;
        private const int WrappedKeyLength = 40; // AES-KW of a 32-byte key

        private readonly IDomainService _domainService;
        private readonly ILoginContextService _loginContext;
        private readonly IRequestStepContext _stepContext;
        private readonly IHubContext<RealtimeHub> _hub;
        private readonly ILogger<ChatRepo> _logger;

        public ChatRepo(
            IDomainService domainService,
            ILoginContextService loginContext,
            IRequestStepContext stepContext,
            IHubContext<RealtimeHub> hub,
            ILogger<ChatRepo> logger)
        {
            _domainService = domainService;
            _loginContext = loginContext;
            _stepContext = stepContext;
            _hub = hub;
            _logger = logger;
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
                    var now = IndiaNow();
                    var conversation = new ChatConversation
                    {
                        ConversationId = Guid.NewGuid(),
                        Type = ChatConversationType.Direct,
                        DirectKey = directKey,
                        CreatedBy = me,
                        CreatedAt = now,
                    };
                    await _domainService.SaveEntityAsync(conversation);
                    await _domainService.SaveEntitiesAsync(new List<ChatConversationMember>
                    {
                        new() { MemberId = Guid.NewGuid(), ConversationId = conversation.ConversationId, UserId = me, JoinedAt = now },
                        new() { MemberId = Guid.NewGuid(), ConversationId = conversation.ConversationId, UserId = otherUserId, JoinedAt = now },
                    });

                    _stepContext.Success("ChatConversations", "INSERT", conversation.ConversationId.ToString(), timer);
                    return ToDto(conversation, new List<Guid> { me, otherUserId });
                });
            }
            catch (Exception)
            {
                // Both users opened the chat at the same moment — the unique DirectKey index rejected ours
                return await FindDirectAsync(directKey) ?? throw new Exception("Could not open the conversation.");
            }
        }

        public async Task<List<ConversationDto>> GetMyConversationsAsync()
        {
            var me = _loginContext.userId;

            var conversations = await (
                from m in _domainService.Query<ChatConversationMember>().AsNoTracking()
                join c in _domainService.Query<ChatConversation>().AsNoTracking()
                    on m.ConversationId equals c.ConversationId
                where m.UserId == me
                select c
            ).ToListAsync();

            if (conversations.Count == 0) return new List<ConversationDto>();

            var ids = conversations.Select(c => c.ConversationId).ToList();
            var members = await _domainService.Query<ChatConversationMember>().AsNoTracking()
                .Where(m => ids.Contains(m.ConversationId))
                .Select(m => new { m.ConversationId, m.UserId })
                .ToListAsync();

            return conversations
                .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
                .Select(c => ToDto(c, members.Where(m => m.ConversationId == c.ConversationId).Select(m => m.UserId).ToList()))
                .ToList();
        }

        public async Task<List<ChatMessageDto>> GetMessagesAsync(Guid conversationId, Guid deviceId, DateTime? before, int take)
        {
            if (deviceId == Guid.Empty)
                throw new Exceptionlist.InvalidDataException("DeviceId is required.");

            await RequireMemberAsync(conversationId);
            take = Math.Clamp(take <= 0 ? 50 : take, 1, MaxPageSize);

            var query = _domainService.Query<ChatEncryptedMessage>().AsNoTracking()
                .Where(x => x.ConversationId == conversationId);
            if (before.HasValue)
                query = query.Where(x => x.CreatedAt < before.Value);

            var messages = await query
                .OrderByDescending(x => x.CreatedAt)
                .Take(take)
                .ToListAsync();

            var messageIds = messages.Select(x => x.MessageId).ToList();
            var me = _loginContext.userId;
            var keys = await _domainService.Query<ChatMessageKey>().AsNoTracking()
                .Where(k => messageIds.Contains(k.MessageId) && k.RecipientDeviceId == deviceId && k.RecipientUserId == me)
                .ToListAsync();

            // Oldest first, which is the order a chat window renders in
            return messages
                .OrderBy(x => x.CreatedAt)
                .Select(m => ToDto(m, keys.Where(k => k.MessageId == m.MessageId)))
                .ToList();
        }

        public async Task<ChatMessageDto> SendMessageAsync(Guid conversationId, SendMessageDto dto)
        {
            var me = _loginContext.userId;
            ValidateEnvelope(dto);

            var memberIds = await RequireMemberAsync(conversationId);

            // Retry of a message that was already stored — return it without re-broadcasting
            var duplicate = await _domainService.Query<ChatEncryptedMessage>().AsNoTracking()
                .FirstOrDefaultAsync(x => x.SenderUserId == me && x.ClientMessageId == dto.ClientMessageId);
            if (duplicate != null)
            {
                if (duplicate.ConversationId != conversationId)
                    throw new Exceptionlist.InvalidDataException("ClientMessageId was already used for another conversation.");
                return await LoadWithSenderKeysAsync(duplicate, dto.SenderDeviceId);
            }

            var senderDeviceActive = await _domainService.Query<ChatIdentityKey>().AsNoTracking()
                .AnyAsync(x => x.UserId == me && x.DeviceId == dto.SenderDeviceId && x.Status == ChatKeyStatus.Active);
            if (!senderDeviceActive)
                throw new Exceptionlist.InvalidDataException("This device has no active chat key. Reload the page and try again.");

            // Every wrapped key must target an active device of a member, using one of that device's pre-keys.
            // The recipient user is taken from the server's key directory, never from the client.
            var deviceIds = dto.Keys.Select(k => k.RecipientDeviceId).ToList();
            var devicePreKeys = await (
                from identity in _domainService.Query<ChatIdentityKey>().AsNoTracking()
                join preKey in _domainService.Query<ChatSignedPreKey>().AsNoTracking()
                    on identity.IdentityKeyId equals preKey.IdentityKeyId
                where deviceIds.Contains(identity.DeviceId)
                      && identity.Status == ChatKeyStatus.Active
                      && memberIds.Contains(identity.UserId)
                select new { identity.UserId, identity.DeviceId, preKey.KeyId }
            ).ToListAsync();

            var messageKeys = new List<ChatMessageKey>();
            var messageId = Guid.NewGuid();
            foreach (var key in dto.Keys)
            {
                var target = devicePreKeys.FirstOrDefault(d => d.DeviceId == key.RecipientDeviceId && d.KeyId == key.PreKeyId)
                    ?? throw new Exceptionlist.InvalidDataException(
                        "A recipient device key is no longer valid. Reload the conversation and send again.");

                messageKeys.Add(new ChatMessageKey
                {
                    MessageKeyId = Guid.NewGuid(),
                    MessageId = messageId,
                    RecipientUserId = target.UserId,
                    RecipientDeviceId = key.RecipientDeviceId,
                    PreKeyId = key.PreKeyId,
                    WrappedKey = key.WrappedKey,
                });
            }

            var recipients = memberIds.Where(id => id != me).ToList();
            if (recipients.Any(r => messageKeys.All(k => k.RecipientUserId != r)))
                throw new Exceptionlist.InvalidDataException("The message is not encrypted for the other participant.");

            var message = await _domainService.ExecuteInTransactionAsync(async () =>
            {
                var timer = _stepContext.StartStep();
                try
                {
                    var now = IndiaNow();
                    var entity = new ChatEncryptedMessage
                    {
                        MessageId = messageId,
                        ConversationId = conversationId,
                        SenderUserId = me,
                        SenderDeviceId = dto.SenderDeviceId,
                        ClientMessageId = dto.ClientMessageId,
                        Ciphertext = dto.Ciphertext,
                        Iv = dto.Iv,
                        EphemeralPublicKey = dto.EphemeralPublicKey,
                        CreatedAt = now,
                    };
                    await _domainService.SaveEntityAsync(entity);
                    await _domainService.SaveEntitiesAsync(messageKeys);
                    await _domainService.UpdateTrackedEntityAsync<ChatConversation>(
                        c => c.ConversationId == conversationId,
                        c => c.LastMessageAt = now);

                    _stepContext.Success("ChatEncryptedMessages", "INSERT", messageId.ToString(), timer);
                    return entity;
                }
                catch (Exception ex)
                {
                    _stepContext.Failure("ChatEncryptedMessages", "INSERT", ex.Message, ex.InnerException?.Message, timer);
                    throw;
                }
            });

            await BroadcastAsync(message, messageKeys, memberIds);

            return ToDto(message, messageKeys.Where(k => k.RecipientDeviceId == dto.SenderDeviceId));
        }

        #region Helpers

        /// <summary>Each member gets the message with only their own devices' wrapped keys.</summary>
        private async Task BroadcastAsync(ChatEncryptedMessage message, List<ChatMessageKey> keys, List<Guid> memberIds)
        {
            try
            {
                await Task.WhenAll(memberIds.Select(userId =>
                    _hub.Clients.Group($"user-{userId}").SendAsync(
                        RealtimeEvent,
                        ToDto(message, keys.Where(k => k.RecipientUserId == userId)))));
            }
            catch (Exception ex)
            {
                // The message is stored; clients will pick it up on their next fetch
                _logger.LogWarning(ex, "[Chat] Realtime delivery failed for message {MessageId}", message.MessageId);
            }
        }

        private async Task<List<Guid>> RequireMemberAsync(Guid conversationId)
        {
            var memberIds = await _domainService.Query<ChatConversationMember>().AsNoTracking()
                .Where(m => m.ConversationId == conversationId)
                .Select(m => m.UserId)
                .ToListAsync();

            // Same response whether the conversation doesn't exist or the caller isn't in it
            if (!memberIds.Contains(_loginContext.userId))
                throw new Exceptionlist.DataNotFoundException("Conversation not found.");

            return memberIds;
        }

        private async Task<ConversationDto?> FindDirectAsync(string directKey)
        {
            var conversation = await _domainService.Query<ChatConversation>().AsNoTracking()
                .FirstOrDefaultAsync(c => c.DirectKey == directKey);
            if (conversation == null) return null;

            var memberIds = await _domainService.Query<ChatConversationMember>().AsNoTracking()
                .Where(m => m.ConversationId == conversation.ConversationId)
                .Select(m => m.UserId)
                .ToListAsync();
            return ToDto(conversation, memberIds);
        }

        private async Task<ChatMessageDto> LoadWithSenderKeysAsync(ChatEncryptedMessage message, Guid deviceId)
        {
            var keys = await _domainService.Query<ChatMessageKey>().AsNoTracking()
                .Where(k => k.MessageId == message.MessageId && k.RecipientDeviceId == deviceId)
                .ToListAsync();
            return ToDto(message, keys);
        }

        private static void ValidateEnvelope(SendMessageDto dto)
        {
            if (dto.ClientMessageId == Guid.Empty)
                throw new Exceptionlist.InvalidDataException("ClientMessageId is required.");
            if (dto.SenderDeviceId == Guid.Empty)
                throw new Exceptionlist.InvalidDataException("SenderDeviceId is required.");

            if (string.IsNullOrEmpty(dto.Ciphertext) || dto.Ciphertext.Length > MaxCiphertextLength)
                throw new Exceptionlist.InvalidDataException("Message is empty or too long.");
            DecodeBase64(dto.Ciphertext, "Ciphertext");

            if (DecodeBase64(dto.Iv, "Iv").Length != GcmIvLength)
                throw new Exceptionlist.InvalidDataException("Iv must be 12 bytes.");

            try
            {
                using var ecdh = ECDiffieHellman.Create();
                ecdh.ImportSubjectPublicKeyInfo(DecodeBase64(dto.EphemeralPublicKey, "EphemeralPublicKey"), out _);
                if (ecdh.KeySize != 256) throw new CryptographicException();
            }
            catch (CryptographicException)
            {
                throw new Exceptionlist.InvalidDataException("EphemeralPublicKey must be a P-256 public key.");
            }

            if (dto.Keys == null || dto.Keys.Count == 0 || dto.Keys.Count > MaxKeysPerMessage)
                throw new Exceptionlist.InvalidDataException($"Between 1 and {MaxKeysPerMessage} recipient keys are required.");
            if (dto.Keys.Select(k => k.RecipientDeviceId).Distinct().Count() != dto.Keys.Count)
                throw new Exceptionlist.InvalidDataException("Each recipient device may appear only once.");
            if (dto.Keys.All(k => k.RecipientDeviceId != dto.SenderDeviceId))
                throw new Exceptionlist.InvalidDataException("The message must also be encrypted for the sending device.");

            foreach (var key in dto.Keys)
            {
                if (key.RecipientDeviceId == Guid.Empty || key.PreKeyId <= 0)
                    throw new Exceptionlist.InvalidDataException("Recipient key is missing its device or pre-key.");
                if (DecodeBase64(key.WrappedKey, "WrappedKey").Length != WrappedKeyLength)
                    throw new Exceptionlist.InvalidDataException("WrappedKey has an unexpected length.");
            }
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

        private static string DirectKeyFor(Guid a, Guid b)
        {
            var (first, second) = a.CompareTo(b) <= 0 ? (a, b) : (b, a);
            return $"{first:D}|{second:D}";
        }

        private static ConversationDto ToDto(ChatConversation c, List<Guid> memberIds) => new()
        {
            ConversationId = c.ConversationId,
            Type = c.Type,
            MemberUserIds = memberIds,
            CreatedAt = c.CreatedAt,
            LastMessageAt = c.LastMessageAt,
        };

        private static ChatMessageDto ToDto(ChatEncryptedMessage m, IEnumerable<ChatMessageKey> keys) => new()
        {
            MessageId = m.MessageId,
            ConversationId = m.ConversationId,
            SenderUserId = m.SenderUserId,
            SenderDeviceId = m.SenderDeviceId,
            ClientMessageId = m.ClientMessageId,
            Ciphertext = m.Ciphertext,
            Iv = m.Iv,
            EphemeralPublicKey = m.EphemeralPublicKey,
            CreatedAt = m.CreatedAt,
            Keys = keys.Select(k => new MessageKeyDto
            {
                RecipientDeviceId = k.RecipientDeviceId,
                PreKeyId = k.PreKeyId,
                WrappedKey = k.WrappedKey,
            }).ToList(),
        };

        private static DateTime IndiaNow()
        {
            var ist = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ist);
        }

        #endregion
    }
}
