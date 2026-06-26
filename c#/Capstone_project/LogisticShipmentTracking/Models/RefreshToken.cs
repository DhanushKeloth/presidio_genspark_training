using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace LogisticShipmentTracking.Models;

/// <summary>
/// Hashed refresh tokens per user session. Supports multi-device and explicit logout.
/// </summary>
[Table("refresh_tokens")]
[Index("UserId", Name = "idx_refresh_tokens_user_id")]
[Index("TokenHash", Name = "uq_refresh_tokens_hash", IsUnique = true)]
public partial class RefreshToken
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    /// <summary>
    /// SHA-256 of raw token. Raw token sent to client; this hash stored in DB.
    /// </summary>
    [Column("token_hash")]
    [StringLength(512)]
    public string TokenHash { get; set; } = null!;

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Set TRUE on logout. Checked before issuing a new access token.
    /// </summary>
    [Column("is_revoked")]
    public bool IsRevoked { get; set; }

    /// <summary>
    /// Optional browser/device label for session management UI.
    /// </summary>
    [Column("device_hint")]
    [StringLength(100)]
    public string? DeviceHint { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("RefreshTokens")]
    public virtual User User { get; set; } = null!;
}
