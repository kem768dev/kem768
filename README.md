@"
# SecureLicenseCore - Hybrid License System (ECDSA + ML-KEM)

## ⚡ Performance

**Average: 5ms** | **Min: 1.65ms** | **Max: 276ms** on Intel Core i5-2500K (2011)

![Benchmark Results](./assets/benchmark-results.png)

<details>
<summary>📊 View Full Benchmark Data (100 requests)</summary>

![Full Benchmark](./assets/benchmark-full.png)

</details>

## Overview
- Purpose: Hybrid license challenge-response using ECDSA (classic) + ML-KEM (post-quantum KEM)
- Client: Generates ECDSA P-256 + ML-KEM-768, registers both public keys, then validates with ECDSA signature + ML-KEM ciphertext + PoP tag
- Server: Stores client keys, generates its own ML-KEM-768 keypair per license, decapsulates ciphertext, verifies PoP, and verifies ECDSA

## What was created/changed (current state)
1) DTOs (shared contract)
   File: src/LicenseCore.Shared/DTOs/SharedModels.cs
   - RegisterRequest now carries 2 public keys:
     - EcdsaPublicKeyBase64 (SPKI DER)
     - MlKemPublicKeyBase64 (raw ML-KEM public key bytes)
   - RegisterResponse now returns:
     - ServerMlKemPublicKeyBase64 (raw ML-KEM public key bytes)
   - ValidateRequest carries:
     - SignatureBase64 (ECDSA DER signature)
     - MlKemCiphertextBase64 (KEM ciphertext)
     - MlKemProofOfPossessionBase64 (HMAC/tag for key confirmation)

2) Server behavior
   File: src/LicenseCore.Server/Program.cs
   - /register:
     - Validates client keys
     - Generates a fresh ML-KEM-768 keypair for the server side
     - Stores server ML-KEM private key per license
     - Returns server ML-KEM public key in RegisterResponse
   - /challenge:
     - Generates 32-byte nonce with expiry
   - /validate:
     - Verifies ECDSA signature with explicit DER format (Rfc3279DerSequence)
     - Decapsulates ML-KEM ciphertext with stored server private key
     - Verifies PoP tag using FixedTimeEquals
     - Authorizes only if ECDSA + PQ checks both succeed

3) Client behavior
   File: src/TestClient/Program.cs
   - Uses server URL from LICENSE_SERVER_URL (default: http://localhost:5108)
   - Registers keys, reads server ML-KEM public key from response
   - Encapsulates to server ML-KEM public key
   - Builds PoP tag as HMAC(sharedSecret, "Challenge{NonceBase64}{LicenseKey}")
   - Validates with ECDSA signature + ciphertext + PoP

4) Hybrid key provider
   File: src/LicenseCore/HybridKeyProvider.cs
   - Generates ECDSA P-256 and ML-KEM-768
   - Encapsulates with randomness (SecureRandom) for ML-KEM
   - ECDSA signatures are DER (Rfc3279DerSequence)

## Crypto formats (current)
- EcdsaPublicKeyBase64: Base64(SPKI DER)
- MlKemPublicKeyBase64: Base64(raw ML-KEM public key bytes)
- ServerMlKemPublicKeyBase64: Base64(raw ML-KEM public key bytes)
- MlKemCiphertextBase64: Base64(raw ciphertext bytes)
- MlKemProofOfPossessionBase64: Base64(HMAC/tag)
- ECDSA signature: DER sequence (Rfc3279DerSequence)

## Run & test (PowerShell)
1) Start server
   ```powershell
   dotnet run --project .\src\LicenseCore.Server
