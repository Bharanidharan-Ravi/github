using System.Security.Cryptography;
using System.Text.RegularExpressions;
using APIGateWay.Business_Layer.Interface;
using APIGateWay.BusinessLayer.Helpers;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer;
using APIGateWay.ModalLayer.ChatsModal.DTOs;
using APIGateWay.ModalLayer.ChatsModal.Master;
using APIGateWay.ModelLayer.ErrorException;
using Microsoft.EntityFrameworkCore;

namespace APIGateWay.BusinessLayer.Repository
{
    /// <summary>
    /// User-based key directory for end-to-end encrypted chat.
    /// Stores each user's public key and their private key wrapped (in the browser) by a
    /// password-derived key and a recovery-code-derived key. The server cannot unwrap
    /// either blob, so it can never decrypt messages.
    /// </summary>
    public class ChatKeyRepo : IChatKeyRepo
    {
        private const int P256KeySize = 256;
        private const int MaxPublicKeyBase64Length = 256;
        private const int MaxParticipantsPerRequest = 200;

        // Wrapped blob = [12-byte IV | AES-GCM(pkcs8)] — at least IV + 16-byte tag + 1 byte
        private const int MinWrappedLength = 12 + 16 + 1;
        private const int MaxWrappedLength = 1024;
        private const int MinSaltLength = 16;
        private const int MaxSaltLength = 64;

        // "XXXX-XXXX-XXXX-XXXX-XXXX-XXXX" — Crockford Base32 (digits + A-Z minus I/L/O/U)
        private static readonly Regex RecoveryCodePattern =
            new(@"^([0-9A-HJKMNPQRSTVWXYZ]{4}-){5}[0-9A-HJKMNPQRSTVWXYZ]{4}$", RegexOptions.Compiled);

        private readonly IDomainService _domainService;
        private readonly ILoginContextService _loginContext;
        private readonly IRequestStepContext _stepContext;
        private readonly IChatRecoveryEscrowCipher _escrowCipher;

        public ChatKeyRepo(
            IDomainService domainService,
            ILoginContextService loginContext,
            IRequestStepContext stepContext,
            IChatRecoveryEscrowCipher escrowCipher)
        {
            _domainService = domainService;
            _loginContext = loginContext;
            _stepContext = stepContext;
            _escrowCipher = escrowCipher;
        }

        public async Task<ChatUserKeyBundleDto?> GetMyKeyBundleAsync()
        {
            var key = await _domainService.Query<ChatUserKey>().AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == _loginContext.userId);
            return key == null ? null : ToBundle(key);
        }

        public async Task<ChatUserKeyBundleDto> RegisterMyKeyAsync(RegisterChatUserKeyDto dto)
        {
            ValidatePublicKey(dto.PublicKey);
            var wrappedByPassword = DecodeWrapped(dto.WrappedByPassword, "WrappedByPassword");
            var passwordSalt = DecodeSalt(dto.PasswordSalt, "PasswordSalt");
            var wrappedByRecovery = DecodeWrapped(dto.WrappedByRecovery, "WrappedByRecovery");
            var recoverySalt = DecodeSalt(dto.RecoverySalt, "RecoverySalt");
            ValidateRecoveryCode(dto.RecoveryCode);

            var userId = _loginContext.userId;

            var existing = await _domainService.Query<ChatUserKey>().AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId);
            if (existing != null)
                return SameRegistrationOrConflict(existing, dto.PublicKey);

            try
            {
                return await _domainService.ExecuteInTransactionAsync(async () =>
                {
                    var timer = _stepContext.StartStep();
                    try
                    {
                        var entity = new ChatUserKey
                        {
                            UserId = userId,
                            PublicKey = dto.PublicKey,
                            WrappedByPassword = wrappedByPassword,
                            PasswordSalt = passwordSalt,
                            WrappedByRecovery = wrappedByRecovery,
                            RecoverySalt = recoverySalt,
                            KeyVersion = 1,
                            CreatedAt = IndiaNow(),
                        };
                        await _domainService.SaveEntityAsync(entity);
                        await _domainService.SaveEntityAsync(new ChatUserKeyRecoveryEscrow
                        {
                            UserId = userId,
                            EncryptedRecoveryCode = _escrowCipher.Encrypt(dto.RecoveryCode),
                            CreatedAt = entity.CreatedAt,
                        });

                        _stepContext.Success("ChatUserKeys", "INSERT", userId.ToString(), timer);
                        return ToBundle(entity);
                    }
                    catch (Exception ex)
                    {
                        _stepContext.Failure("ChatUserKeys", "INSERT", ex.Message, ex.InnerException?.Message, timer);
                        throw;
                    }
                });
            }
            catch (DbUpdateException)
            {
                // Two tabs registered at the same moment — the primary key rejected ours
                var winner = await _domainService.Query<ChatUserKey>().AsNoTracking()
                    .FirstOrDefaultAsync(x => x.UserId == userId);
                if (winner == null) throw;
                return SameRegistrationOrConflict(winner, dto.PublicKey);
            }
        }

        public async Task<ChatUserKeyBundleDto> RewrapMyKeyAsync(RewrapChatUserKeyDto dto)
        {
            var wrappedByPassword = DecodeWrapped(dto.WrappedByPassword, "WrappedByPassword");
            var passwordSalt = DecodeSalt(dto.PasswordSalt, "PasswordSalt");
            var userId = _loginContext.userId;

            return await _domainService.ExecuteInTransactionAsync(async () =>
            {
                var timer = _stepContext.StartStep();
                try
                {
                    var key = await _domainService.Query<ChatUserKey>()
                        .FirstOrDefaultAsync(x => x.UserId == userId)
                        ?? throw new Exceptionlist.DataNotFoundException("No chat key is registered for this user.");

                    // The client unwrapped an older bundle (e.g. another tab already re-wrapped) — make it reload
                    if (key.KeyVersion != dto.KeyVersion)
                        throw new Exceptionlist.InvalidDataException("Chat key has changed. Reload and try again.");

                    key.WrappedByPassword = wrappedByPassword;
                    key.PasswordSalt = passwordSalt;
                    key.KeyVersion += 1;
                    key.RotatedAt = IndiaNow();
                    await _domainService.UpdateAsync(key);

                    _stepContext.Success("ChatUserKeys", "UPDATE", userId.ToString(), timer);
                    return ToBundle(key);
                }
                catch (Exception ex)
                {
                    _stepContext.Failure("ChatUserKeys", "UPDATE", ex.Message, ex.InnerException?.Message, timer);
                    throw;
                }
            });
        }

        public async Task<List<ParticipantPublicKeyDto>> GetParticipantKeysAsync(IReadOnlyCollection<Guid> userIds)
        {
            if (userIds == null || userIds.Count == 0)
                throw new Exceptionlist.InvalidDataException("At least one userId is required.");
            if (userIds.Count > MaxParticipantsPerRequest)
                throw new Exceptionlist.InvalidDataException($"Too many userIds in a single request (max {MaxParticipantsPerRequest}).");

            var distinctIds = userIds.Where(id => id != Guid.Empty).Distinct().ToList();

            return await _domainService.Query<ChatUserKey>().AsNoTracking()
                .Where(x => distinctIds.Contains(x.UserId))
                .Select(x => new ParticipantPublicKeyDto
                {
                    UserId = x.UserId,
                    PublicKey = x.PublicKey,
                    KeyVersion = x.KeyVersion,
                })
                .ToListAsync();
        }

        public async Task<ChatRecoveryEscrowDto> GetRecoveryEscrowAsync(Guid userId)
        {
            if (_loginContext.role != AppRoles.Admin)
                throw new Exceptionlist.UnauthorizedException("Only an administrator can view an escrowed recovery code.");

            var escrow = await _domainService.Query<ChatUserKeyRecoveryEscrow>().AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId)
                ?? throw new Exceptionlist.DataNotFoundException("No recovery code is escrowed for this user.");

            return new ChatRecoveryEscrowDto
            {
                UserId = escrow.UserId,
                RecoveryCode = _escrowCipher.Decrypt(escrow.EncryptedRecoveryCode),
                CreatedAt = escrow.CreatedAt,
            };
        }

        #region Helpers

        /// <summary>A retry with the same public key is a no-op; a different key must not overwrite the existing one.</summary>
        private static ChatUserKeyBundleDto SameRegistrationOrConflict(ChatUserKey existing, string publicKey)
        {
            if (existing.PublicKey == publicKey)
                return ToBundle(existing);

            throw new Exceptionlist.UserAlreadyExistsException("A chat key is already registered for this user.");
        }

        private static ChatUserKeyBundleDto ToBundle(ChatUserKey key) => new()
        {
            UserId = key.UserId,
            PublicKey = key.PublicKey,
            WrappedByPassword = Convert.ToBase64String(key.WrappedByPassword),
            PasswordSalt = Convert.ToBase64String(key.PasswordSalt),
            WrappedByRecovery = Convert.ToBase64String(key.WrappedByRecovery),
            RecoverySalt = Convert.ToBase64String(key.RecoverySalt),
            KeyVersion = key.KeyVersion,
            CreatedAt = key.CreatedAt,
            RotatedAt = key.RotatedAt,
        };

        private static void ValidatePublicKey(string? publicKey)
        {
            if (string.IsNullOrWhiteSpace(publicKey) || publicKey.Length > MaxPublicKeyBase64Length)
                throw new Exceptionlist.InvalidDataException("PublicKey is required.");

            try
            {
                using var ecdh = ECDiffieHellman.Create();
                ecdh.ImportSubjectPublicKeyInfo(DecodeBase64(publicKey, "PublicKey"), out _);
                if (ecdh.KeySize != P256KeySize)
                    throw new Exceptionlist.InvalidDataException("PublicKey must be a P-256 key.");
            }
            catch (CryptographicException)
            {
                throw new Exceptionlist.InvalidDataException("PublicKey is not a valid public key.");
            }
        }

        private static byte[] DecodeWrapped(string? value, string field)
        {
            var bytes = DecodeBase64(value, field);
            if (bytes.Length < MinWrappedLength || bytes.Length > MaxWrappedLength)
                throw new Exceptionlist.InvalidDataException($"{field} has an unexpected length.");
            return bytes;
        }

        private static void ValidateRecoveryCode(string? recoveryCode)
        {
            if (string.IsNullOrWhiteSpace(recoveryCode) || !RecoveryCodePattern.IsMatch(recoveryCode))
                throw new Exceptionlist.InvalidDataException("RecoveryCode must look like XXXX-XXXX-XXXX-XXXX-XXXX-XXXX.");
        }

        private static byte[] DecodeSalt(string? value, string field)
        {
            var bytes = DecodeBase64(value, field);
            if (bytes.Length < MinSaltLength || bytes.Length > MaxSaltLength)
                throw new Exceptionlist.InvalidDataException($"{field} must be {MinSaltLength}-{MaxSaltLength} bytes.");
            return bytes;
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

        // Same clock the rest of the gateway writes to the database
        private static DateTime IndiaNow()
        {
            var ist = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ist);
        }

        #endregion
    }
}
