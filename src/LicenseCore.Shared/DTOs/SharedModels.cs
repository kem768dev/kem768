using System;

namespace LicenseCore.Shared.DTOs;

/// <summary>
/// API contracts (DTOs) - Version 2.0: Hybrid Crypto (ECDSA + ML-KEM)
/// 
/// BREAKING CHANGES from v1:
/// - RegisterRequest: Added MlKemPublicKeyBase64 field
/// - ValidateRequest: Added MlKemCiphertextBase64 + MlKemProofOfPossessionBase64
/// </summary>
public static class SharedModels
{
    // ----- REGISTER -----
    public sealed record RegisterRequest(
        string LicenseKey,
        string EcdsaPublicKeyBase64,   // Base64(SPKI DER)
        string MlKemPublicKeyBase64    // Base64(raw ML-KEM-512 public key)
    );

    public sealed record RegisterResponse(
        bool Registered,
        string ServerMlKemPublicKeyBase64 // Base64(raw ML-KEM public key bytes)
    );

    // ----- CHALLENGE -----
    public sealed record ChallengeRequest(string LicenseKey);

    public sealed record ChallengeResponse(
        string NonceBase64,
        DateTimeOffset ExpiresAtUtc
    );

    // ----- VALIDATE -----
    public sealed record ValidateRequest(
        string LicenseKey,
        string NonceBase64,
        string SignatureBase64,              // ECDSA signature
        string MlKemCiphertextBase64,        // ML-KEM ciphertext
        string MlKemProofOfPossessionBase64  // HMAC for Key Confirmation
    );

    public sealed record ValidateResponse(bool Valid);
}
