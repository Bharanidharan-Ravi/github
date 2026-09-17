using System.Security.Cryptography;
using APIGateWay.BusinessLayer.Repository;
using APIGateWay.ModalLayer.ChatsModal.DTOs;
using APIGateWay.ModelLayer.ErrorException;
using APIGateWay.Tests.Fakes;
using Xunit;

namespace APIGateWay.Tests
{
    public class ChatKeyRepoTests
    {
        // ── Test data helpers ───────────────────────────────────────────────

        /// <summary>A real P-256 SubjectPublicKeyInfo, base64-encoded — passes server-side ECDH import.</summary>
        private static string ValidPublicKeyBase64()
        {
            using var ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
            return Convert.ToBase64String(ecdh.ExportSubjectPublicKeyInfo());
        }

        /// <summary>Right-shaped "[12-byte IV | ciphertext]" blob — content doesn't matter, only length.</summary>
        private static string WrappedBlob(int length = 60) => Convert.ToBase64String(RandomBytes(length));

        private static string Salt(int length = 16) => Convert.ToBase64String(RandomBytes(length));

        private static byte[] RandomBytes(int length)
        {
            var bytes = new byte[length];
            RandomNumberGenerator.Fill(bytes);
            return bytes;
        }

        private static RegisterChatUserKeyDto ValidRegisterDto() => new()
        {
            PublicKey = ValidPublicKeyBase64(),
            WrappedByPassword = WrappedBlob(),
            PasswordSalt = Salt(),
            WrappedByRecovery = WrappedBlob(),
            RecoverySalt = Salt(),
        };

        private static (ChatKeyRepo repo, FakeDomainService db, FakeLoginContextService login) MakeRepo()
        {
            var db = new FakeDomainService();
            var login = new FakeLoginContextService();
            var repo = new ChatKeyRepo(db, login, new FakeRequestStepContext());
            return (repo, db, login);
        }

        // ── GetMyKeyBundleAsync ─────────────────────────────────────────────

        [Fact]
        public async Task GetMyKeyBundle_ReturnsNull_WhenNoKeyRegistered()
        {
            var (repo, _, _) = MakeRepo();

            var result = await repo.GetMyKeyBundleAsync();

            Assert.Null(result);
        }

        [Fact]
        public async Task GetMyKeyBundle_ReturnsBundle_AfterRegistration()
        {
            var (repo, _, login) = MakeRepo();
            var dto = ValidRegisterDto();
            await repo.RegisterMyKeyAsync(dto);

            var result = await repo.GetMyKeyBundleAsync();

            Assert.NotNull(result);
            Assert.Equal(login.userId, result!.UserId);
            Assert.Equal(dto.PublicKey, result.PublicKey);
            Assert.Equal(1, result.KeyVersion);
        }

        [Fact]
        public async Task GetMyKeyBundle_DoesNotLeakAnotherUsersKey()
        {
            var (repo, db, login) = MakeRepo();
            await repo.RegisterMyKeyAsync(ValidRegisterDto());

            login.userId = Guid.NewGuid(); // switch to a different logged-in user

            var result = await repo.GetMyKeyBundleAsync();

            Assert.Null(result);
        }

        // ── RegisterMyKeyAsync ──────────────────────────────────────────────

        [Fact]
        public async Task RegisterMyKey_Succeeds_WithValidInput()
        {
            var (repo, _, login) = MakeRepo();
            var dto = ValidRegisterDto();

            var result = await repo.RegisterMyKeyAsync(dto);

            Assert.Equal(login.userId, result.UserId);
            Assert.Equal(dto.PublicKey, result.PublicKey);
            Assert.Equal(dto.WrappedByPassword, result.WrappedByPassword);
            Assert.Equal(dto.PasswordSalt, result.PasswordSalt);
            Assert.Equal(dto.WrappedByRecovery, result.WrappedByRecovery);
            Assert.Equal(dto.RecoverySalt, result.RecoverySalt);
            Assert.Equal(1, result.KeyVersion);
        }

        [Fact]
        public async Task RegisterMyKey_IsNoOp_WhenSamePublicKeyResent()
        {
            var (repo, db, _) = MakeRepo();
            var dto = ValidRegisterDto();
            await repo.RegisterMyKeyAsync(dto);

            var result = await repo.RegisterMyKeyAsync(dto);

            Assert.Equal(1, result.KeyVersion);
            Assert.Single(db.Db.ChatUserKeys); // no duplicate row was inserted
        }

        [Fact]
        public async Task RegisterMyKey_Throws_WhenDifferentKeyRegisteredForSameUser()
        {
            var (repo, _, _) = MakeRepo();
            await repo.RegisterMyKeyAsync(ValidRegisterDto());

            var differentKeyDto = ValidRegisterDto(); // fresh keypair => different PublicKey

            await Assert.ThrowsAsync<Exceptionlist.UserAlreadyExistsException>(
                () => repo.RegisterMyKeyAsync(differentKeyDto));
        }

        [Theory]
        [InlineData("")]
        [InlineData("not-base64-!!!")]
        [InlineData("aGVsbG8=")] // valid base64, but not a P-256 SPKI
        public async Task RegisterMyKey_Throws_OnInvalidPublicKey(string badPublicKey)
        {
            var (repo, _, _) = MakeRepo();
            var dto = ValidRegisterDto();
            dto.PublicKey = badPublicKey;

            await Assert.ThrowsAsync<Exceptionlist.InvalidDataException>(() => repo.RegisterMyKeyAsync(dto));
        }

        [Fact]
        public async Task RegisterMyKey_Throws_WhenWrappedBlobTooShort()
        {
            var (repo, _, _) = MakeRepo();
            var dto = ValidRegisterDto();
            dto.WrappedByPassword = WrappedBlob(length: 10); // below the 12+16+1 minimum

            await Assert.ThrowsAsync<Exceptionlist.InvalidDataException>(() => repo.RegisterMyKeyAsync(dto));
        }

        [Theory]
        [InlineData(8)]   // below 16-byte minimum
        [InlineData(128)] // above 64-byte maximum
        public async Task RegisterMyKey_Throws_OnBadSaltLength(int saltLength)
        {
            var (repo, _, _) = MakeRepo();
            var dto = ValidRegisterDto();
            dto.PasswordSalt = Salt(saltLength);

            await Assert.ThrowsAsync<Exceptionlist.InvalidDataException>(() => repo.RegisterMyKeyAsync(dto));
        }

        // ── RewrapMyKeyAsync ────────────────────────────────────────────────

        [Fact]
        public async Task Rewrap_Throws_WhenNoKeyExists()
        {
            var (repo, _, _) = MakeRepo();

            await Assert.ThrowsAsync<Exceptionlist.DataNotFoundException>(() => repo.RewrapMyKeyAsync(
                new RewrapChatUserKeyDto { WrappedByPassword = WrappedBlob(), PasswordSalt = Salt(), KeyVersion = 1 }));
        }

        [Fact]
        public async Task Rewrap_UpdatesWrappingAndBumpsVersion()
        {
            var (repo, _, _) = MakeRepo();
            var original = await repo.RegisterMyKeyAsync(ValidRegisterDto());
            var newWrapped = WrappedBlob();
            var newSalt = Salt();

            var result = await repo.RewrapMyKeyAsync(new RewrapChatUserKeyDto
            {
                WrappedByPassword = newWrapped,
                PasswordSalt = newSalt,
                KeyVersion = original.KeyVersion,
            });

            Assert.Equal(2, result.KeyVersion);
            Assert.Equal(newWrapped, result.WrappedByPassword);
            Assert.Equal(newSalt, result.PasswordSalt);
            Assert.NotNull(result.RotatedAt);
            // Recovery wrapping is untouched by a password-only rewrap
            Assert.Equal(original.WrappedByRecovery, result.WrappedByRecovery);
        }

        [Fact]
        public async Task Rewrap_Throws_WhenKeyVersionIsStale()
        {
            var (repo, _, _) = MakeRepo();
            await repo.RegisterMyKeyAsync(ValidRegisterDto());

            await Assert.ThrowsAsync<Exceptionlist.InvalidDataException>(() => repo.RewrapMyKeyAsync(
                new RewrapChatUserKeyDto { WrappedByPassword = WrappedBlob(), PasswordSalt = Salt(), KeyVersion = 99 }));
        }

        // ── GetParticipantKeysAsync ─────────────────────────────────────────

        [Fact]
        public async Task GetParticipantKeys_Throws_WhenEmpty()
        {
            var (repo, _, _) = MakeRepo();

            await Assert.ThrowsAsync<Exceptionlist.InvalidDataException>(
                () => repo.GetParticipantKeysAsync(Array.Empty<Guid>()));
        }

        [Fact]
        public async Task GetParticipantKeys_Throws_WhenOverTheCap()
        {
            var (repo, _, _) = MakeRepo();
            var tooMany = Enumerable.Range(0, 201).Select(_ => Guid.NewGuid()).ToList();

            await Assert.ThrowsAsync<Exceptionlist.InvalidDataException>(() => repo.GetParticipantKeysAsync(tooMany));
        }

        [Fact]
        public async Task GetParticipantKeys_OmitsUsersWithNoRegisteredKey()
        {
            var (repo, _, login) = MakeRepo();
            var dto = ValidRegisterDto();
            await repo.RegisterMyKeyAsync(dto);
            var userWithNoKey = Guid.NewGuid();

            var result = await repo.GetParticipantKeysAsync(new[] { login.userId, userWithNoKey });

            var ids = result.Select(r => r.UserId).ToList();
            Assert.Contains(login.userId, ids);
            Assert.DoesNotContain(userWithNoKey, ids);
            Assert.Single(result);
            Assert.Equal(dto.PublicKey, result[0].PublicKey);
        }

        [Fact]
        public async Task GetParticipantKeys_DedupesRequestedIds()
        {
            var (repo, _, login) = MakeRepo();
            await repo.RegisterMyKeyAsync(ValidRegisterDto());

            var result = await repo.GetParticipantKeysAsync(new[] { login.userId, login.userId, login.userId });

            Assert.Single(result);
        }
    }
}
