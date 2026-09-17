using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APIGateWay.ModalLayer.ChatsModal.Master
{
    /// <summary>
    /// Long-term public identity key (ECDSA P-256) of one user on one device.
    /// The matching private key lives only in the device's browser storage.
    /// </summary>
    [Table("ChatIdentityKeys")]
    public class ChatIdentityKey
    {
        [Key]
        public Guid IdentityKeyId { get; set; }
        public Guid UserId { get; set; }
        public Guid DeviceId { get; set; }

        /// <summary>Base64 SubjectPublicKeyInfo.</summary>
        public string PublicKey { get; set; } = string.Empty;

        /// <summary>SHA-256 hex of the SPKI bytes.</summary>
        public string Fingerprint { get; set; } = string.Empty;
        public string? DeviceInfo { get; set; }

        /// <summary>Active | Revoked</summary>
        public string Status { get; set; } = ChatKeyStatus.Active;
        public DateTime CreatedAt { get; set; }
        public DateTime? RevokedAt { get; set; }
    }

    public static class ChatKeyStatus
    {
        public const string Active = "Active";
        public const string Revoked = "Revoked";
    }
}
