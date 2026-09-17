using System;

namespace APIGateWay.ModalLayer.ChatsModal.DTOs
{
    public class RegisterIdentityKeyDto
    {
        public Guid DeviceId { get; set; }
        public string PublicKey { get; set; } = string.Empty;
        public string? DeviceInfo { get; set; }
    }

    public class RegisterSignedPreKeyDto
    {
        public Guid DeviceId { get; set; }
        public int KeyId { get; set; }
        public string PublicKey { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
    }

    /// <summary>What the server currently holds for this user + device.</summary>
    public class ChatKeyStatusDto
    {
        public Guid DeviceId { get; set; }
        public string? IdentityFingerprint { get; set; }
        public int? PreKeyId { get; set; }
        public DateTime? PreKeyExpiresAt { get; set; }

        /// <summary>True when there is no active pre-key or it has expired.</summary>
        public bool RotationDue { get; set; }
    }

    /// <summary>One device of a participant, with everything needed to encrypt to it.
    /// Only returned when the device has an active identity key AND a still-valid pre-key —
    /// revoked identities and expired/rotated pre-keys never leave the server.</summary>
    public class DeviceKeysDto
    {
        public Guid DeviceId { get; set; }
        public string IdentityPublicKey { get; set; } = string.Empty;
        public string IdentityFingerprint { get; set; } = string.Empty;
        public int PreKeyId { get; set; }
        public string PreKeyPublicKey { get; set; } = string.Empty;
        public string PreKeySignature { get; set; } = string.Empty;
        public DateTime PreKeyExpiresAt { get; set; }
    }

    /// <summary>A participant's usable devices. Empty <see cref="Devices"/> means that user
    /// has no device with a currently valid key pair (e.g. never logged in, or offline past
    /// its pre-key's expiry) — the client should treat them as "cannot encrypt to yet".</summary>
    public class ParticipantKeysDto
    {
        public Guid UserId { get; set; }
        public List<DeviceKeysDto> Devices { get; set; } = new();
    }
}
