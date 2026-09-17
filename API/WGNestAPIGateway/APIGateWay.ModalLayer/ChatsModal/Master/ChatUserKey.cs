using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APIGateWay.ModalLayer.ChatsModal.Master
{
    /// <summary>
    /// A user's chat key pair (ECDH P-256). The private key is wrapped in the browser —
    /// once with a PBKDF2 key from the login password, once with one from the recovery
    /// code — so the server only ever holds opaque blobs it cannot unwrap.
    /// Wrapped blobs are laid out as [12-byte IV | AES-GCM ciphertext].
    /// </summary>
    [Table("ChatUserKeys")]
    public class ChatUserKey
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public Guid UserId { get; set; }

        /// <summary>Base64 SubjectPublicKeyInfo.</summary>
        public string PublicKey { get; set; } = string.Empty;

        public byte[] WrappedByPassword { get; set; } = Array.Empty<byte>();
        public byte[] PasswordSalt { get; set; } = Array.Empty<byte>();
        public byte[] WrappedByRecovery { get; set; } = Array.Empty<byte>();
        public byte[] RecoverySalt { get; set; } = Array.Empty<byte>();

        /// <summary>Incremented whenever the stored wrapping changes.</summary>
        public int KeyVersion { get; set; } = 1;

        [Column(TypeName = "datetime2")]
        public DateTime CreatedAt { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime? RotatedAt { get; set; }
    }
}
