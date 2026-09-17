using System.Security.Cryptography;
using APIGateWay.BusinessLayer.Repository;
using APIGateWay.ModalLayer.ChatsModal.DTOs;
using APIGateWay.ModalLayer.ChatsModal.Master;
using APIGateWay.ModalLayer.MasterData;
using APIGateWay.ModelLayer.ErrorException;
using APIGateWay.Tests.Fakes;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace APIGateWay.Tests
{
    public class ChatRepoTests
    {
        // ── Test data helpers ───────────────────────────────────────────────

        private static byte[] RandomBytes(int length)
        {
            var bytes = new byte[length];
            RandomNumberGenerator.Fill(bytes);
            return bytes;
        }

        /// <summary>Right-shaped 40-byte AES-KW wrap — content doesn't matter to ChatRepo, only length.</summary>
        private static string WrappedKey() => Convert.ToBase64String(RandomBytes(40));

        /// <summary>Right-shaped "[12-byte IV | ciphertext + tag]" payload.</summary>
        private static string Payload(int length = 48) => Convert.ToBase64String(RandomBytes(length));

        private static IConfiguration TestConfiguration() =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ChatMedia:StorageFolder"] = Path.Combine(Path.GetTempPath(), "wg_chat_media_tests"),
                })
                .Build();

        private static (ChatRepo repo, FakeDomainService db, FakeLoginContextService login, FakeHubContext hub) MakeRepo()
        {
            var db = new FakeDomainService();
            var login = new FakeLoginContextService();
            var hub = new FakeHubContext();
            var repo = new ChatRepo(db, login, new FakeRequestStepContext(), hub, new FakeLogger<ChatRepo>(), TestConfiguration());
            return (repo, db, login, hub);
        }

        private static async Task<LOGIN_MASTER> AddUserAsync(FakeDomainService db, string name = "user")
        {
            var user = new LOGIN_MASTER { UserID = Guid.NewGuid(), UserName = name, DBName = "WGNEST" };
            db.Db.LoginMasters.Add(user);
            await db.Db.SaveChangesAsync();
            return user;
        }

        /// <summary>Marks a user as having a registered chat key — SendMessageAsync requires this for every member.</summary>
        private static async Task GiveChatKeyAsync(FakeDomainService db, Guid userId)
        {
            if (db.Db.ChatUserKeys.Any(k => k.UserId == userId)) return; // already given one in this test

            db.Db.ChatUserKeys.Add(new ChatUserKey
            {
                UserId = userId,
                PublicKey = "fake-public-key",
                WrappedByPassword = RandomBytes(60),
                PasswordSalt = RandomBytes(16),
                WrappedByRecovery = RandomBytes(60),
                RecoverySalt = RandomBytes(16),
                KeyVersion = 1,
                CreatedAt = DateTime.UtcNow,
            });
            await db.Db.SaveChangesAsync();
        }

        /// <summary>Opens a direct conversation between the two given users and gives both a chat key.</summary>
        private static async Task<(Guid conversationId, Guid me, Guid other)> SetupDirectConversationAsync(
            ChatRepo repo, FakeDomainService db, FakeLoginContextService login, string otherName = "other")
        {
            var other = await AddUserAsync(db, otherName);
            var me = login.userId;
            var conversation = await repo.OpenDirectConversationAsync(other.UserID);
            await GiveChatKeyAsync(db, me);
            await GiveChatKeyAsync(db, other.UserID);
            return (conversation.ConversationId, me, other.UserID);
        }

        private static SendMessageDto ValidSendDto(params Guid[] memberIds) => new()
        {
            ClientMessageId = Guid.NewGuid(),
            EncryptedPayload = Payload(),
            Keys = memberIds.Select(id => new MessageRecipientKeyDto { RecipientUserId = id, WrappedMessageKey = WrappedKey() }).ToList(),
        };

        // ── OpenDirectConversationAsync ──────────────────────────────────────

        [Fact]
        public async Task OpenDirect_CreatesConversation_WithBothMembers()
        {
            var (repo, db, login, _) = MakeRepo();
            var other = await AddUserAsync(db);

            var result = await repo.OpenDirectConversationAsync(other.UserID);

            Assert.Equal(1, result.Type); // Direct
            Assert.Equal(2, result.MemberUserIds.Count);
            Assert.Contains(login.userId, result.MemberUserIds);
            Assert.Contains(other.UserID, result.MemberUserIds);
        }

        [Fact]
        public async Task OpenDirect_ReturnsSameConversation_OnRepeatedCalls()
        {
            var (repo, db, _, _) = MakeRepo();
            var other = await AddUserAsync(db);

            var first = await repo.OpenDirectConversationAsync(other.UserID);
            var second = await repo.OpenDirectConversationAsync(other.UserID);

            Assert.Equal(first.ConversationId, second.ConversationId);
            Assert.Single(db.Db.ChatConversations); // no duplicate row
        }

        [Fact]
        public async Task OpenDirect_Throws_WhenChattingWithSelf()
        {
            var (repo, _, login, _) = MakeRepo();
            await Assert.ThrowsAsync<Exceptionlist.InvalidDataException>(() => repo.OpenDirectConversationAsync(login.userId));
        }

        [Fact]
        public async Task OpenDirect_Throws_WhenOtherUserDoesNotExist()
        {
            var (repo, _, _, _) = MakeRepo();
            await Assert.ThrowsAsync<Exceptionlist.DataNotFoundException>(() => repo.OpenDirectConversationAsync(Guid.NewGuid()));
        }

        // ── GetMyConversationsAsync ──────────────────────────────────────────

        [Fact]
        public async Task GetMyConversations_ReturnsEmpty_WhenNoneExist()
        {
            var (repo, _, _, _) = MakeRepo();
            var result = await repo.GetMyConversationsAsync();
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetMyConversations_ShowsEmptyConversation_OnlyToWhoeverOpenedIt()
        {
            var (repo, db, login, _) = MakeRepo();
            var other = await AddUserAsync(db);
            await repo.OpenDirectConversationAsync(other.UserID); // I opened it, no messages yet

            var mine = await repo.GetMyConversationsAsync();
            Assert.Single(mine);
            Assert.Null(mine[0].LastMessage);

            login.userId = other.UserID; // the other member hasn't looked at it
            var theirs = await repo.GetMyConversationsAsync();
            Assert.Empty(theirs);
        }

        [Fact]
        public async Task GetMyConversations_CountsOnlyOthersMessagesAfterLastReadAt_AsUnread()
        {
            var (repo, db, login, _) = MakeRepo();
            var (conversationId, me, other) = await SetupDirectConversationAsync(repo, db, login);

            await repo.SendMessageAsync(conversationId, ValidSendDto(me, other)); // from me — never unread for me

            login.userId = other;
            var otherView = await repo.GetMyConversationsAsync();
            Assert.Equal(1, otherView.Single().UnreadCount);

            await repo.MarkReadAsync(conversationId, new MarkConversationReadDto());
            otherView = await repo.GetMyConversationsAsync();
            Assert.Equal(0, otherView.Single().UnreadCount);
        }

        [Fact]
        public async Task GetMyConversations_OrdersByMostRecentActivityFirst()
        {
            var (repo, db, login, _) = MakeRepo();
            var (convA, meA, otherA) = await SetupDirectConversationAsync(repo, db, login, "userA");
            var (convB, _, otherB) = await SetupDirectConversationAsync(repo, db, login, "userB");

            await repo.SendMessageAsync(convA, ValidSendDto(meA, otherA));
            await repo.SendMessageAsync(convB, ValidSendDto(meA, otherB)); // sent after, should sort first

            var result = await repo.GetMyConversationsAsync();
            Assert.Equal(convB, result[0].ConversationId);
            Assert.Equal(convA, result[1].ConversationId);
        }

        // ── SendMessageAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task SendMessage_Throws_WhenCallerIsNotAMember()
        {
            var (repo, _, _, _) = MakeRepo();
            await Assert.ThrowsAsync<Exceptionlist.DataNotFoundException>(
                () => repo.SendMessageAsync(Guid.NewGuid(), ValidSendDto(Guid.NewGuid())));
        }

        [Fact]
        public async Task SendMessage_Throws_WhenKeyCountDoesNotMatchMemberCount()
        {
            var (repo, db, login, _) = MakeRepo();
            var (conversationId, me, _) = await SetupDirectConversationAsync(repo, db, login);

            var dto = ValidSendDto(me); // missing the other member's key
            await Assert.ThrowsAsync<Exceptionlist.InvalidDataException>(() => repo.SendMessageAsync(conversationId, dto));
        }

        [Fact]
        public async Task SendMessage_Throws_WhenAKeyTargetsSomeoneOutsideTheConversation()
        {
            var (repo, db, login, _) = MakeRepo();
            var (conversationId, me, other) = await SetupDirectConversationAsync(repo, db, login);

            var dto = ValidSendDto(me, Guid.NewGuid()); // stranger instead of `other`
            await Assert.ThrowsAsync<Exceptionlist.InvalidDataException>(() => repo.SendMessageAsync(conversationId, dto));
        }

        [Fact]
        public async Task SendMessage_Throws_WhenAMemberAppearsTwice()
        {
            var (repo, db, login, _) = MakeRepo();
            var (conversationId, me, _) = await SetupDirectConversationAsync(repo, db, login);

            var dto = ValidSendDto(me, me);
            await Assert.ThrowsAsync<Exceptionlist.InvalidDataException>(() => repo.SendMessageAsync(conversationId, dto));
        }

        [Fact]
        public async Task SendMessage_Throws_WhenWrappedKeyHasWrongLength()
        {
            var (repo, db, login, _) = MakeRepo();
            var (conversationId, me, other) = await SetupDirectConversationAsync(repo, db, login);

            var dto = ValidSendDto(me, other);
            dto.Keys[0].WrappedMessageKey = Convert.ToBase64String(RandomBytes(10)); // not 40 bytes
            await Assert.ThrowsAsync<Exceptionlist.InvalidDataException>(() => repo.SendMessageAsync(conversationId, dto));
        }

        [Fact]
        public async Task SendMessage_Throws_WhenPayloadIsTooShort()
        {
            var (repo, db, login, _) = MakeRepo();
            var (conversationId, me, other) = await SetupDirectConversationAsync(repo, db, login);

            var dto = ValidSendDto(me, other);
            dto.EncryptedPayload = Convert.ToBase64String(RandomBytes(5)); // shorter than IV+tag
            await Assert.ThrowsAsync<Exceptionlist.InvalidDataException>(() => repo.SendMessageAsync(conversationId, dto));
        }

        [Fact]
        public async Task SendMessage_Throws_WhenAMemberHasNoChatKeyRegistered()
        {
            var (repo, db, login, _) = MakeRepo();
            var other = await AddUserAsync(db);
            var conversation = await repo.OpenDirectConversationAsync(other.UserID);
            await GiveChatKeyAsync(db, login.userId); // only I have a key — `other` doesn't

            var dto = ValidSendDto(login.userId, other.UserID);
            await Assert.ThrowsAsync<Exceptionlist.InvalidDataException>(() => repo.SendMessageAsync(conversation.ConversationId, dto));
        }

        [Fact]
        public async Task SendMessage_Stores_OneRowPerMemberKey()
        {
            var (repo, db, login, _) = MakeRepo();
            var (conversationId, me, other) = await SetupDirectConversationAsync(repo, db, login);

            var result = await repo.SendMessageAsync(conversationId, ValidSendDto(me, other));

            Assert.Single(db.Db.ChatEncryptedMessages);
            Assert.Equal(2, db.Db.ChatMessageKeys.Count());
            Assert.NotNull(result.WrappedMessageKey); // sender gets their own key back in the response
        }

        [Fact]
        public async Task SendMessage_IsIdempotent_OnRetryWithSameClientMessageId()
        {
            var (repo, db, login, _) = MakeRepo();
            var (conversationId, me, other) = await SetupDirectConversationAsync(repo, db, login);
            var dto = ValidSendDto(me, other);

            var first = await repo.SendMessageAsync(conversationId, dto);
            var retry = await repo.SendMessageAsync(conversationId, dto); // same ClientMessageId

            Assert.Equal(first.MessageId, retry.MessageId);
            Assert.Single(db.Db.ChatEncryptedMessages); // not inserted twice
        }

        [Fact]
        public async Task SendMessage_Throws_WhenClientMessageIdReusedForAnotherConversation()
        {
            var (repo, db, login, _) = MakeRepo();
            var (convA, meA, otherA) = await SetupDirectConversationAsync(repo, db, login, "userA");
            var (convB, _, otherB) = await SetupDirectConversationAsync(repo, db, login, "userB");

            var dto = ValidSendDto(meA, otherA);
            await repo.SendMessageAsync(convA, dto);

            var reused = new SendMessageDto
            {
                ClientMessageId = dto.ClientMessageId,
                EncryptedPayload = Payload(),
                Keys = new() { new() { RecipientUserId = meA, WrappedMessageKey = WrappedKey() }, new() { RecipientUserId = otherB, WrappedMessageKey = WrappedKey() } },
            };
            await Assert.ThrowsAsync<Exceptionlist.InvalidDataException>(() => repo.SendMessageAsync(convB, reused));
        }

        [Fact]
        public async Task SendMessage_BroadcastsToEachMembersOwnGroup_WithOnlyTheirWrappedKey()
        {
            var (repo, db, login, hub) = MakeRepo();
            var (conversationId, me, other) = await SetupDirectConversationAsync(repo, db, login);

            await repo.SendMessageAsync(conversationId, ValidSendDto(me, other));

            Assert.Equal(2, hub.Sent.Count);
            Assert.Contains(hub.Sent, s => s.Group == $"user-{me}" && s.Method == ChatRepo.MessageEvent);
            Assert.Contains(hub.Sent, s => s.Group == $"user-{other}" && s.Method == ChatRepo.MessageEvent);

            var forOther = hub.Sent.Single(s => s.Group == $"user-{other}").Arg as ChatMessageDto;
            var forMe = hub.Sent.Single(s => s.Group == $"user-{me}").Arg as ChatMessageDto;
            Assert.NotEqual(forOther!.WrappedMessageKey, forMe!.WrappedMessageKey);
        }

        // ── GetMessagesAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task GetMessages_Throws_WhenCallerIsNotAMember()
        {
            var (repo, _, _, _) = MakeRepo();
            await Assert.ThrowsAsync<Exceptionlist.DataNotFoundException>(() => repo.GetMessagesAsync(Guid.NewGuid(), null, 50));
        }

        [Fact]
        public async Task GetMessages_ReturnsOldestFirst_WithOnlyCallersWrappedKey()
        {
            var (repo, db, login, _) = MakeRepo();
            var (conversationId, me, other) = await SetupDirectConversationAsync(repo, db, login);

            var first = await repo.SendMessageAsync(conversationId, ValidSendDto(me, other));
            var second = await repo.SendMessageAsync(conversationId, ValidSendDto(me, other));

            var page = await repo.GetMessagesAsync(conversationId, null, 50);

            Assert.Equal(new[] { first.MessageId, second.MessageId }, page.Select(m => m.MessageId));
            Assert.All(page, m => Assert.NotNull(m.WrappedMessageKey));
        }

        [Fact]
        public async Task GetMessages_BeforeCursor_ExcludesNewerMessages()
        {
            var (repo, db, login, _) = MakeRepo();
            var (conversationId, me, other) = await SetupDirectConversationAsync(repo, db, login);

            var first = await repo.SendMessageAsync(conversationId, ValidSendDto(me, other));
            await repo.SendMessageAsync(conversationId, ValidSendDto(me, other));

            var page = await repo.GetMessagesAsync(conversationId, first.CreatedAt, 50);
            Assert.Empty(page); // nothing older than the very first message
        }

        [Fact]
        public async Task GetMessages_ClampsOversizedTakeToMax()
        {
            var (repo, db, login, _) = MakeRepo();
            var (conversationId, me, other) = await SetupDirectConversationAsync(repo, db, login);
            await repo.SendMessageAsync(conversationId, ValidSendDto(me, other));

            var page = await repo.GetMessagesAsync(conversationId, null, 10_000);
            Assert.Single(page); // doesn't throw; just capped internally
        }

        // ── MarkReadAsync ────────────────────────────────────────────────────

        [Fact]
        public async Task MarkRead_Throws_WhenCallerIsNotAMember()
        {
            var (repo, _, _, _) = MakeRepo();
            await Assert.ThrowsAsync<Exceptionlist.DataNotFoundException>(
                () => repo.MarkReadAsync(Guid.NewGuid(), new MarkConversationReadDto()));
        }

        [Fact]
        public async Task MarkRead_SetsLastReadAt_ToNow_WhenNoneGiven()
        {
            var (repo, db, login, _) = MakeRepo();
            var (conversationId, _, _) = await SetupDirectConversationAsync(repo, db, login);

            var before = DateTime.UtcNow.AddSeconds(-5);
            var result = await repo.MarkReadAsync(conversationId, new MarkConversationReadDto());

            Assert.NotNull(result.LastReadAt);
            Assert.True(result.LastReadAt >= before);
        }

        [Fact]
        public async Task MarkRead_NeverMovesTheMarkerBackward()
        {
            var (repo, db, login, _) = MakeRepo();
            var (conversationId, _, _) = await SetupDirectConversationAsync(repo, db, login);

            var later = await repo.MarkReadAsync(conversationId, new MarkConversationReadDto { ReadUpTo = DateTime.UtcNow });
            var earlierAttempt = await repo.MarkReadAsync(
                conversationId, new MarkConversationReadDto { ReadUpTo = DateTime.UtcNow.AddDays(-1) });

            Assert.Equal(later.LastReadAt, earlierAttempt.LastReadAt);
        }

        [Fact]
        public async Task MarkRead_BroadcastsToMyOwnUserGroup_ForOtherTabsAndDevices()
        {
            var (repo, db, login, hub) = MakeRepo();
            var (conversationId, me, _) = await SetupDirectConversationAsync(repo, db, login);

            await repo.MarkReadAsync(conversationId, new MarkConversationReadDto());

            Assert.Contains(hub.Sent, s => s.Group == $"user-{me}" && s.Method == ChatRepo.ReadEvent);
        }

        // ── ToggleReactionAsync (Phase 6) ────────────────────────────────────

        [Fact]
        public async Task ToggleReaction_Throws_WhenMessageDoesNotExist()
        {
            var (repo, _, _, _) = MakeRepo();
            await Assert.ThrowsAsync<Exceptionlist.DataNotFoundException>(
                () => repo.ToggleReactionAsync(Guid.NewGuid(), new ToggleReactionDto { Emoji = "👍" }));
        }

        [Fact]
        public async Task ToggleReaction_Throws_WhenCallerIsNotAConversationMember()
        {
            var (repo, db, login, _) = MakeRepo();
            var (conversationId, me, other) = await SetupDirectConversationAsync(repo, db, login);
            var message = await repo.SendMessageAsync(conversationId, ValidSendDto(me, other));

            login.userId = Guid.NewGuid(); // a stranger
            await Assert.ThrowsAsync<Exceptionlist.DataNotFoundException>(
                () => repo.ToggleReactionAsync(message.MessageId, new ToggleReactionDto { Emoji = "👍" }));
        }

        [Fact]
        public async Task ToggleReaction_Throws_WhenEmojiIsMissing()
        {
            var (repo, db, login, _) = MakeRepo();
            var (conversationId, me, other) = await SetupDirectConversationAsync(repo, db, login);
            var message = await repo.SendMessageAsync(conversationId, ValidSendDto(me, other));

            await Assert.ThrowsAsync<Exceptionlist.InvalidDataException>(
                () => repo.ToggleReactionAsync(message.MessageId, new ToggleReactionDto { Emoji = "  " }));
        }

        [Fact]
        public async Task ToggleReaction_Throws_WhenEmojiIsTooLong()
        {
            var (repo, db, login, _) = MakeRepo();
            var (conversationId, me, other) = await SetupDirectConversationAsync(repo, db, login);
            var message = await repo.SendMessageAsync(conversationId, ValidSendDto(me, other));

            await Assert.ThrowsAsync<Exceptionlist.InvalidDataException>(
                () => repo.ToggleReactionAsync(message.MessageId, new ToggleReactionDto { Emoji = new string('a', 17) }));
        }

        [Fact]
        public async Task ToggleReaction_AddsRow_OnFirstCall()
        {
            var (repo, db, login, _) = MakeRepo();
            var (conversationId, me, other) = await SetupDirectConversationAsync(repo, db, login);
            var message = await repo.SendMessageAsync(conversationId, ValidSendDto(me, other));

            var result = await repo.ToggleReactionAsync(message.MessageId, new ToggleReactionDto { Emoji = "👍" });

            Assert.True(result.Added);
            Assert.Equal(message.MessageId, result.MessageId);
            Assert.Equal(conversationId, result.ConversationId);
            Assert.Equal(me, result.UserId);
            Assert.Equal("👍", result.Emoji);
            Assert.Single(db.Db.ChatMessageReactions);
        }

        [Fact]
        public async Task ToggleReaction_RemovesRow_OnSecondCallWithSameEmoji()
        {
            var (repo, db, login, _) = MakeRepo();
            var (conversationId, me, other) = await SetupDirectConversationAsync(repo, db, login);
            var message = await repo.SendMessageAsync(conversationId, ValidSendDto(me, other));

            await repo.ToggleReactionAsync(message.MessageId, new ToggleReactionDto { Emoji = "👍" });
            var second = await repo.ToggleReactionAsync(message.MessageId, new ToggleReactionDto { Emoji = "👍" });

            Assert.False(second.Added);
            Assert.Empty(db.Db.ChatMessageReactions);
        }

        [Fact]
        public async Task ToggleReaction_KeepsSeparateRows_ForDifferentEmojiFromSameUser()
        {
            var (repo, db, login, _) = MakeRepo();
            var (conversationId, me, other) = await SetupDirectConversationAsync(repo, db, login);
            var message = await repo.SendMessageAsync(conversationId, ValidSendDto(me, other));

            await repo.ToggleReactionAsync(message.MessageId, new ToggleReactionDto { Emoji = "👍" });
            await repo.ToggleReactionAsync(message.MessageId, new ToggleReactionDto { Emoji = "❤️" });

            Assert.Equal(2, db.Db.ChatMessageReactions.Count());
        }

        [Fact]
        public async Task ToggleReaction_KeepsSeparateRows_ForSameEmojiFromDifferentUsers()
        {
            var (repo, db, login, _) = MakeRepo();
            var (conversationId, me, other) = await SetupDirectConversationAsync(repo, db, login);
            var message = await repo.SendMessageAsync(conversationId, ValidSendDto(me, other));

            await repo.ToggleReactionAsync(message.MessageId, new ToggleReactionDto { Emoji = "👍" });
            login.userId = other;
            await repo.ToggleReactionAsync(message.MessageId, new ToggleReactionDto { Emoji = "👍" });

            Assert.Equal(2, db.Db.ChatMessageReactions.Count());
        }

        [Fact]
        public async Task ToggleReaction_BroadcastsToEveryMembersOwnGroup()
        {
            var (repo, db, login, hub) = MakeRepo();
            var (conversationId, me, other) = await SetupDirectConversationAsync(repo, db, login);
            var message = await repo.SendMessageAsync(conversationId, ValidSendDto(me, other));
            hub.Sent.Clear(); // drop the SendMessage broadcasts, only care about the reaction one

            await repo.ToggleReactionAsync(message.MessageId, new ToggleReactionDto { Emoji = "🎉" });

            Assert.Equal(2, hub.Sent.Count);
            Assert.Contains(hub.Sent, s => s.Group == $"user-{me}" && s.Method == ChatRepo.ReactionEvent);
            Assert.Contains(hub.Sent, s => s.Group == $"user-{other}" && s.Method == ChatRepo.ReactionEvent);

            var evt = hub.Sent.First(s => s.Group == $"user-{other}").Arg as MessageReactionEventDto;
            Assert.True(evt!.Added);
            Assert.Equal("🎉", evt.Emoji);
        }

        [Fact]
        public async Task GetMessages_IncludesReactions_ForEachMessage()
        {
            var (repo, db, login, _) = MakeRepo();
            var (conversationId, me, other) = await SetupDirectConversationAsync(repo, db, login);
            var message = await repo.SendMessageAsync(conversationId, ValidSendDto(me, other));
            await repo.ToggleReactionAsync(message.MessageId, new ToggleReactionDto { Emoji = "👍" });
            login.userId = other;
            await repo.ToggleReactionAsync(message.MessageId, new ToggleReactionDto { Emoji = "👍" });
            login.userId = me;

            var page = await repo.GetMessagesAsync(conversationId, null, 50);

            var reactions = page.Single().Reactions;
            Assert.Equal(2, reactions.Count);
            Assert.All(reactions, r => Assert.Equal("👍", r.Emoji));
            Assert.Contains(reactions, r => r.UserId == me);
            Assert.Contains(reactions, r => r.UserId == other);
        }

        [Fact]
        public async Task SendMessage_ReturnsEmptyReactions_ForABrandNewMessage()
        {
            var (repo, db, login, _) = MakeRepo();
            var (conversationId, me, other) = await SetupDirectConversationAsync(repo, db, login);

            var result = await repo.SendMessageAsync(conversationId, ValidSendDto(me, other));

            Assert.Empty(result.Reactions);
        }

        // ── Tags & @mentions (Phase 7) ───────────────────────────────────────

        private static MessageTagDto UserTag(Guid userId, string display = "@someone") => new()
        {
            EntityType = ChatTagEntityType.User,
            EntityId = userId.ToString(),
            DisplayText = display,
        };

        private static MessageTagDto TicketTag(string display = "#TCK-1", Guid? notifyUserId = null) => new()
        {
            EntityType = ChatTagEntityType.Ticket,
            EntityId = Guid.NewGuid().ToString(),
            DisplayText = display,
            NotifyUserId = notifyUserId,
        };

        [Fact]
        public async Task SendMessage_ReturnsEmptyTags_WhenNoneProvided()
        {
            var (repo, db, login, _) = MakeRepo();
            var (conversationId, me, other) = await SetupDirectConversationAsync(repo, db, login);

            var result = await repo.SendMessageAsync(conversationId, ValidSendDto(me, other));

            Assert.Empty(result.Tags);
            Assert.Empty(db.Db.ChatMessageTags);
        }

        [Fact]
        public async Task SendMessage_Throws_WhenTooManyTags()
        {
            var (repo, db, login, _) = MakeRepo();
            var (conversationId, me, other) = await SetupDirectConversationAsync(repo, db, login);

            var dto = ValidSendDto(me, other);
            dto.Tags = Enumerable.Range(0, 31).Select(i => TicketTag($"#TCK-{i}")).ToList();

            await Assert.ThrowsAsync<Exceptionlist.InvalidDataException>(() => repo.SendMessageAsync(conversationId, dto));
        }

        [Fact]
        public async Task SendMessage_Throws_WhenTagEntityTypeIsUnsupported()
        {
            var (repo, db, login, _) = MakeRepo();
            var (conversationId, me, other) = await SetupDirectConversationAsync(repo, db, login);

            var dto = ValidSendDto(me, other);
            dto.Tags = new() { new MessageTagDto { EntityType = "Bogus", EntityId = "x", DisplayText = "#x" } };

            await Assert.ThrowsAsync<Exceptionlist.InvalidDataException>(() => repo.SendMessageAsync(conversationId, dto));
        }

        [Fact]
        public async Task SendMessage_Throws_WhenUserTagEntityIdIsNotAGuid()
        {
            var (repo, db, login, _) = MakeRepo();
            var (conversationId, me, other) = await SetupDirectConversationAsync(repo, db, login);

            var dto = ValidSendDto(me, other);
            dto.Tags = new() { new MessageTagDto { EntityType = ChatTagEntityType.User, EntityId = "not-a-guid", DisplayText = "@x" } };

            await Assert.ThrowsAsync<Exceptionlist.InvalidDataException>(() => repo.SendMessageAsync(conversationId, dto));
        }

        [Fact]
        public async Task SendMessage_Throws_WhenTagDisplayTextIsMissing()
        {
            var (repo, db, login, _) = MakeRepo();
            var (conversationId, me, other) = await SetupDirectConversationAsync(repo, db, login);

            var dto = ValidSendDto(me, other);
            dto.Tags = new() { new MessageTagDto { EntityType = ChatTagEntityType.User, EntityId = other.ToString(), DisplayText = " " } };

            await Assert.ThrowsAsync<Exceptionlist.InvalidDataException>(() => repo.SendMessageAsync(conversationId, dto));
        }

        [Fact]
        public async Task SendMessage_Throws_WhenTagEntityIdIsMissing()
        {
            var (repo, db, login, _) = MakeRepo();
            var (conversationId, me, other) = await SetupDirectConversationAsync(repo, db, login);

            var dto = ValidSendDto(me, other);
            dto.Tags = new() { new MessageTagDto { EntityType = ChatTagEntityType.Ticket, EntityId = " ", DisplayText = "#TCK-1" } };

            await Assert.ThrowsAsync<Exceptionlist.InvalidDataException>(() => repo.SendMessageAsync(conversationId, dto));
        }

        [Fact]
        public async Task SendMessage_Stores_TagRows_AndReturnsThemOnTheMessage()
        {
            var (repo, db, login, _) = MakeRepo();
            var (conversationId, me, other) = await SetupDirectConversationAsync(repo, db, login);

            var dto = ValidSendDto(me, other);
            dto.Tags = new() { UserTag(other), TicketTag() };

            var result = await repo.SendMessageAsync(conversationId, dto);

            Assert.Equal(2, result.Tags.Count);
            Assert.Equal(2, db.Db.ChatMessageTags.Count());
            Assert.All(db.Db.ChatMessageTags, t => Assert.Equal(result.MessageId, t.MessageId));
            Assert.Contains(result.Tags, t => t.EntityType == ChatTagEntityType.User && t.EntityId == other.ToString());
            Assert.Contains(result.Tags, t => t.EntityType == ChatTagEntityType.Ticket);
        }

        [Fact]
        public async Task GetMessages_IncludesTags_ForEachMessage()
        {
            var (repo, db, login, _) = MakeRepo();
            var (conversationId, me, other) = await SetupDirectConversationAsync(repo, db, login);

            var dto = ValidSendDto(me, other);
            dto.Tags = new() { UserTag(other) };
            await repo.SendMessageAsync(conversationId, dto);

            var page = await repo.GetMessagesAsync(conversationId, null, 50);

            var tags = page.Single().Tags;
            Assert.Single(tags);
            Assert.Equal(ChatTagEntityType.User, tags[0].EntityType);
            Assert.Equal(other.ToString(), tags[0].EntityId);
        }

        [Fact]
        public async Task SendMessage_BroadcastsMention_ToTaggedUser()
        {
            var (repo, db, login, hub) = MakeRepo();
            var (conversationId, me, other) = await SetupDirectConversationAsync(repo, db, login);

            var dto = ValidSendDto(me, other);
            dto.Tags = new() { UserTag(other, "@other") };
            await repo.SendMessageAsync(conversationId, dto);

            var mention = hub.Sent.Single(s => s.Group == $"user-{other}" && s.Method == ChatRepo.MentionEvent);
            var evt = Assert.IsType<ChatMentionEventDto>(mention.Arg);
            Assert.Equal(me, evt.TaggedByUserId);
            Assert.Equal(ChatTagEntityType.User, evt.EntityType);
            Assert.Equal("@other", evt.DisplayText);
        }

        [Fact]
        public async Task SendMessage_BroadcastsMention_ToNotifyUserId_ForNonUserTag()
        {
            var (repo, db, login, hub) = MakeRepo();
            var (conversationId, me, other) = await SetupDirectConversationAsync(repo, db, login);
            var assignee = Guid.NewGuid(); // not even a conversation member

            var dto = ValidSendDto(me, other);
            dto.Tags = new() { TicketTag("#TCK-42", notifyUserId: assignee) };
            await repo.SendMessageAsync(conversationId, dto);

            var mention = hub.Sent.Single(s => s.Group == $"user-{assignee}" && s.Method == ChatRepo.MentionEvent);
            var evt = Assert.IsType<ChatMentionEventDto>(mention.Arg);
            Assert.Equal(ChatTagEntityType.Ticket, evt.EntityType);
            Assert.Equal("#TCK-42", evt.DisplayText);
        }

        [Fact]
        public async Task SendMessage_DoesNotBroadcastMention_WhenNoNotifyUserIdProvided_ForNonUserTag()
        {
            var (repo, db, login, hub) = MakeRepo();
            var (conversationId, me, other) = await SetupDirectConversationAsync(repo, db, login);

            var dto = ValidSendDto(me, other);
            dto.Tags = new() { TicketTag("#TCK-1") }; // no NotifyUserId (e.g. a #repo tag with no owner)
            await repo.SendMessageAsync(conversationId, dto);

            Assert.DoesNotContain(hub.Sent, s => s.Method == ChatRepo.MentionEvent);
        }

        [Fact]
        public async Task SendMessage_DoesNotBroadcastMention_ToTheSenderThemself()
        {
            var (repo, db, login, hub) = MakeRepo();
            var (conversationId, me, other) = await SetupDirectConversationAsync(repo, db, login);

            var dto = ValidSendDto(me, other);
            dto.Tags = new() { UserTag(me, "@me") }; // tagging yourself
            await repo.SendMessageAsync(conversationId, dto);

            Assert.DoesNotContain(hub.Sent, s => s.Method == ChatRepo.MentionEvent);
        }

        [Fact]
        public async Task SendMessage_DedupesMentionBroadcast_WhenSameUserTaggedMultipleTimes()
        {
            var (repo, db, login, hub) = MakeRepo();
            var (conversationId, me, other) = await SetupDirectConversationAsync(repo, db, login);

            var dto = ValidSendDto(me, other);
            dto.Tags = new() { UserTag(other, "@other"), TicketTag("#TCK-1", notifyUserId: other) };
            await repo.SendMessageAsync(conversationId, dto);

            Assert.Single(hub.Sent.Where(s => s.Method == ChatRepo.MentionEvent && s.Group == $"user-{other}"));
        }
    }
}
