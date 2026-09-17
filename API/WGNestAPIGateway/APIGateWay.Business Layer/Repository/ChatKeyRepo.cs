using System.Security.Cryptography;
using APIGateWay.Business_Layer.Interface;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.ChatsModal.DTOs;
using APIGateWay.ModalLayer.ChatsModal.Master;
using APIGateWay.ModelLayer.ErrorException;
using Microsoft.EntityFrameworkCore;

namespace APIGateWay.BusinessLayer.Repository
{
    /// <summary>
    /// Public key directory for end-to-end encrypted chat.
    /// The server only stores and validates PUBLIC keys; it never sees private keys,
    /// so it cannot decrypt messages.
    /// </summary>
    public class ChatKeyRepo : IChatKeyRepo
    {
        public static readonly TimeSpan PreKeyLifetime = TimeSpan.FromDays(7);

        private const int P256KeySize = 256;
        private const int P256SignatureLength = 64; // IEEE P1363 r||s
        private const int MaxBase64Length = 512;

        private readonly IDomainService _domainService;
        private readonly ILoginContextService _loginContext;
        private readonly IRequestStepContext _stepContext;

        public ChatKeyRepo(
            IDomainService domainService,
            ILoginContextService loginContext,
            IRequestStepContext stepContext)
        {
            _domainService = domainService;
            _loginContext = loginContext;
            _stepContext = stepContext;
        }

        public async Task<ChatKeyStatusDto> GetStatusAsync(Guid deviceId)
        {
            RequireDeviceId(deviceId);
            var userId = _loginContext.userId;

            var identity = await FindActiveIdentityAsync(userId, deviceId, tracking: false);
            var preKey = identity == null ? null : await FindActivePreKeyAsync(identity.IdentityKeyId, tracking: false);

            return BuildStatus(deviceId, identity, preKey);
        }

        public async Task<ChatKeyStatusDto> RegisterIdentityKeyAsync(RegisterIdentityKeyDto dto)
        {
            RequireDeviceId(dto.DeviceId);
            var spki = DecodeBase64(dto.PublicKey, "PublicKey");
            using (var ecdsa = ImportP256<ECDsa>(spki, ECDsa.Create, "PublicKey")) { }

            var fingerprint = Convert.ToHexString(SHA256.HashData(spki));
            var userId = _loginContext.userId;

            return await _domainService.ExecuteInTransactionAsync(async () =>
            {
                var timer = _stepContext.StartStep();
                try
                {
                    var existing = await FindActiveIdentityAsync(userId, dto.DeviceId, tracking: true);

                    // Same key re-sent (e.g. every login) — nothing to change
                    if (existing != null && existing.Fingerprint == fingerprint)
                    {
                        var currentPreKey = await FindActivePreKeyAsync(existing.IdentityKeyId, tracking: false);
                        _stepContext.Success("ChatIdentityKeys", "NOOP", existing.IdentityKeyId.ToString(), timer);
                        return BuildStatus(dto.DeviceId, existing, currentPreKey);
                    }

                    var now = IndiaNow();

                    // Device came back with a different identity (browser data cleared, reinstall):
                    // revoke the old identity and every pre-key signed by it.
                    if (existing != null)
                    {
                        existing.Status = ChatKeyStatus.Revoked;
                        existing.RevokedAt = now;

                        var oldPreKeys = await _domainService.Query<ChatSignedPreKey>()
                            .Where(x => x.IdentityKeyId == existing.IdentityKeyId && x.IsActive)
                            .ToListAsync();
                        foreach (var p in oldPreKeys)
                        {
                            p.IsActive = false;
                            p.RotatedAt = now;
                        }

                        // Saved before the insert so the filtered unique index sees the revoke first
                        await _domainService.UpdateEntitiesAsync(oldPreKeys);
                        await _domainService.UpdateAsync(existing);
                    }

                    var entity = new ChatIdentityKey
                    {
                        IdentityKeyId = Guid.NewGuid(),
                        UserId = userId,
                        DeviceId = dto.DeviceId,
                        PublicKey = dto.PublicKey,
                        Fingerprint = fingerprint,
                        DeviceInfo = dto.DeviceInfo,
                        Status = ChatKeyStatus.Active,
                        CreatedAt = now,
                    };
                    await _domainService.SaveEntityAsync(entity);

                    _stepContext.Success("ChatIdentityKeys", "INSERT", entity.IdentityKeyId.ToString(), timer);
                    return BuildStatus(dto.DeviceId, entity, null);
                }
                catch (Exception ex)
                {
                    _stepContext.Failure("ChatIdentityKeys", "INSERT", ex.Message, ex.InnerException?.Message, timer);
                    throw;
                }
            });
        }

        public async Task<ChatKeyStatusDto> RegisterSignedPreKeyAsync(RegisterSignedPreKeyDto dto)
        {
            RequireDeviceId(dto.DeviceId);
            if (dto.KeyId <= 0)
                throw new Exceptionlist.InvalidDataException("KeyId must be a positive number.");

            var preKeySpki = DecodeBase64(dto.PublicKey, "PublicKey");
            using (var ecdh = ImportP256<ECDiffieHellman>(preKeySpki, ECDiffieHellman.Create, "PublicKey")) { }

            var signature = DecodeBase64(dto.Signature, "Signature");
            if (signature.Length != P256SignatureLength)
                throw new Exceptionlist.InvalidDataException("Signature must be a 64-byte P-256 signature.");

            var userId = _loginContext.userId;

            return await _domainService.ExecuteInTransactionAsync(async () =>
            {
                var timer = _stepContext.StartStep();
                try
                {
                    var identity = await FindActiveIdentityAsync(userId, dto.DeviceId, tracking: false)
                        ?? throw new Exceptionlist.InvalidDataException("Register the identity key for this device first.");

                    // Only the owner of the identity private key can publish a pre-key for it
                    using (var ecdsa = ImportP256<ECDsa>(Convert.FromBase64String(identity.PublicKey), ECDsa.Create, "IdentityKey"))
                    {
                        if (!ecdsa.VerifyData(preKeySpki, signature, HashAlgorithmName.SHA256))
                            throw new Exceptionlist.InvalidDataException("Pre-key signature is invalid.");
                    }

                    var current = await FindActivePreKeyAsync(identity.IdentityKeyId, tracking: true);

                    // Retry of the same upload — nothing to change
                    if (current != null && current.KeyId == dto.KeyId && current.PublicKey == dto.PublicKey)
                    {
                        _stepContext.Success("ChatSignedPreKeys", "NOOP", current.PreKeyId.ToString(), timer);
                        return BuildStatus(dto.DeviceId, identity, current);
                    }

                    var keyIdUsed = await _domainService.Query<ChatSignedPreKey>()
                        .AnyAsync(x => x.IdentityKeyId == identity.IdentityKeyId && x.KeyId == dto.KeyId);
                    if (keyIdUsed)
                        throw new Exceptionlist.InvalidDataException($"Pre-key {dto.KeyId} already exists for this device.");

                    var now = IndiaNow();

                    if (current != null)
                    {
                        current.IsActive = false;
                        current.RotatedAt = now;
                        await _domainService.UpdateAsync(current);
                    }

                    var entity = new ChatSignedPreKey
                    {
                        PreKeyId = Guid.NewGuid(),
                        IdentityKeyId = identity.IdentityKeyId,
                        UserId = userId,
                        DeviceId = dto.DeviceId,
                        KeyId = dto.KeyId,
                        PublicKey = dto.PublicKey,
                        Signature = dto.Signature,
                        IsActive = true,
                        CreatedAt = now,
                        ExpiresAt = now.Add(PreKeyLifetime),
                    };
                    await _domainService.SaveEntityAsync(entity);

                    _stepContext.Success("ChatSignedPreKeys", "INSERT", entity.PreKeyId.ToString(), timer);
                    return BuildStatus(dto.DeviceId, identity, entity);
                }
                catch (Exception ex)
                {
                    _stepContext.Failure("ChatSignedPreKeys", "INSERT", ex.Message, ex.InnerException?.Message, timer);
                    throw;
                }
            });
        }

        public async Task<List<ParticipantKeysDto>> GetParticipantKeysAsync(IReadOnlyCollection<Guid> userIds)
        {
            if (userIds == null || userIds.Count == 0)
                throw new Exceptionlist.InvalidDataException("At least one userId is required.");
            if (userIds.Count > 200)
                throw new Exceptionlist.InvalidDataException("Too many userIds in a single request (max 200).");

            var distinctIds = userIds.Distinct().ToList();
            var now = IndiaNow();

            // Only devices with BOTH an active identity AND a still-valid (not expired/rotated) pre-key —
            // that is the only pair another client can actually encrypt to right now.
            var rows = await (
                from identity in _domainService.Query<ChatIdentityKey>().AsNoTracking()
                join preKey in _domainService.Query<ChatSignedPreKey>().AsNoTracking()
                    on identity.IdentityKeyId equals preKey.IdentityKeyId
                where distinctIds.Contains(identity.UserId)
                      && identity.Status == ChatKeyStatus.Active
                      && preKey.IsActive
                      && preKey.ExpiresAt > now
                select new { identity, preKey }
            ).ToListAsync();

            return distinctIds
                .Select(userId => new ParticipantKeysDto
                {
                    UserId = userId,
                    Devices = rows
                        .Where(r => r.identity.UserId == userId)
                        .Select(r => new DeviceKeysDto
                        {
                            DeviceId = r.identity.DeviceId,
                            IdentityPublicKey = r.identity.PublicKey,
                            IdentityFingerprint = r.identity.Fingerprint,
                            PreKeyId = r.preKey.KeyId,
                            PreKeyPublicKey = r.preKey.PublicKey,
                            PreKeySignature = r.preKey.Signature,
                            PreKeyExpiresAt = r.preKey.ExpiresAt,
                        })
                        .ToList(),
                })
                .ToList();
        }

        #region Helpers

        private Task<ChatIdentityKey?> FindActiveIdentityAsync(Guid userId, Guid deviceId, bool tracking)
        {
            var query = _domainService.Query<ChatIdentityKey>();
            if (!tracking) query = query.AsNoTracking();
            return query.FirstOrDefaultAsync(x =>
                x.UserId == userId && x.DeviceId == deviceId && x.Status == ChatKeyStatus.Active);
        }

        private Task<ChatSignedPreKey?> FindActivePreKeyAsync(Guid identityKeyId, bool tracking)
        {
            var query = _domainService.Query<ChatSignedPreKey>();
            if (!tracking) query = query.AsNoTracking();
            return query.FirstOrDefaultAsync(x => x.IdentityKeyId == identityKeyId && x.IsActive);
        }

        private static ChatKeyStatusDto BuildStatus(Guid deviceId, ChatIdentityKey? identity, ChatSignedPreKey? preKey)
        {
            return new ChatKeyStatusDto
            {
                DeviceId = deviceId,
                IdentityFingerprint = identity?.Fingerprint,
                PreKeyId = preKey?.KeyId,
                PreKeyExpiresAt = preKey?.ExpiresAt,
                RotationDue = preKey == null || preKey.ExpiresAt <= IndiaNow(),
            };
        }

        // Same clock the rest of the gateway writes to the database
        private static DateTime IndiaNow()
        {
            var ist = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ist);
        }

        private static void RequireDeviceId(Guid deviceId)
        {
            if (deviceId == Guid.Empty)
                throw new Exceptionlist.InvalidDataException("DeviceId is required.");
        }

        private static byte[] DecodeBase64(string? value, string field)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > MaxBase64Length)
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

        private static T ImportP256<T>(byte[] spki, Func<T> create, string field) where T : AsymmetricAlgorithm
        {
            var key = create();
            try
            {
                switch (key)
                {
                    case ECDsa ecdsa: ecdsa.ImportSubjectPublicKeyInfo(spki, out _); break;
                    case ECDiffieHellman ecdh: ecdh.ImportSubjectPublicKeyInfo(spki, out _); break;
                }
            }
            catch (CryptographicException)
            {
                key.Dispose();
                throw new Exceptionlist.InvalidDataException($"{field} is not a valid public key.");
            }

            if (key.KeySize != P256KeySize)
            {
                key.Dispose();
                throw new Exceptionlist.InvalidDataException($"{field} must be a P-256 key.");
            }
            return key;
        }

        #endregion
    }
}
