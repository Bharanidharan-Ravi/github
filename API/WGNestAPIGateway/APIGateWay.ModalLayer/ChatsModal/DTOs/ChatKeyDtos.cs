using System;
using System.Collections.Generic;

namespace APIGateWay.ModalLayer.ChatsModal.DTOs
{
    // All binary fields travel as base64 strings.

    /// <summary>The logged-in user's key bundle — everything the browser needs to unwrap its private key.</summary>
    public class ChatUserKeyBundleDto
    {
        public Guid UserId { get; set; }
        public string PublicKey { get; set; } = string.Empty;
        public string WrappedByPassword { get; set; } = string.Empty;
        public string PasswordSalt { get; set; } = string.Empty;
        public string WrappedByRecovery { get; set; } = string.Empty;
        public string RecoverySalt { get; set; } = string.Empty;
        public int KeyVersion { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? RotatedAt { get; set; }
    }

    /// <summary>POST /api/ChatKeys/me — first-time registration.</summary>
    public class RegisterChatUserKeyDto
    {
        public string PublicKey { get; set; } = string.Empty;
        public string WrappedByPassword { get; set; } = string.Empty;
        public string PasswordSalt { get; set; } = string.Empty;
        public string WrappedByRecovery { get; set; } = string.Empty;
        public string RecoverySalt { get; set; } = string.Empty;
    }

    /// <summary>POST /api/ChatKeys/rewrap — re-wrap with the new password after a recovery-code unlock.</summary>
    public class RewrapChatUserKeyDto
    {
        public string WrappedByPassword { get; set; } = string.Empty;
        public string PasswordSalt { get; set; } = string.Empty;

        /// <summary>The KeyVersion the client unwrapped; rejected if the server has moved on.</summary>
        public int KeyVersion { get; set; }
    }

    /// <summary>A participant's public key. Users who never registered a key are omitted.</summary>
    public class ParticipantPublicKeyDto
    {
        public Guid UserId { get; set; }
        public string PublicKey { get; set; } = string.Empty;
        public int KeyVersion { get; set; }
    }
}
