using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LicenseCore.Server.Data;
using LicenseCore.Shared.DTOs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Kems;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<LicenseDbContext>(o => o.UseSqlite("Data Source=license.db"));
builder.Services.AddSingleton<HmacSecretProvider>();
builder.Services.AddHostedService<NonceCleanupService>();
var app = builder.Build();

using (var scope = app.Services.CreateScope()) scope.ServiceProvider.GetRequiredService<LicenseDbContext>().Database.EnsureCreated();

// REGISTER
app.MapPost("/register", async (SharedModels.RegisterRequest req, LicenseDbContext db, HmacSecretProvider hmac, CancellationToken ct) => {
    byte[] ecdsaPk, mlkemPk;
    try {
        ecdsaPk = Convert.FromBase64String(req.EcdsaPublicKeyBase64);
        mlkemPk = Convert.FromBase64String(req.MlKemPublicKeyBase64);
        var _ = MLKemPublicKeyParameters.FromEncoding(MLKemParameters.ml_kem_768, mlkemPk);
    } catch { return Results.BadRequest("Invalid Keys"); }

    var existing = await db.Licenses.FirstOrDefaultAsync(x => x.Key == req.LicenseKey, ct);
    var kpg = new MLKemKeyPairGenerator();
    kpg.Init(new MLKemKeyGenerationParameters(new SecureRandom(), MLKemParameters.ml_kem_768));
    var kp = kpg.GenerateKeyPair();
    var serverPublic = ((MLKemPublicKeyParameters)kp.Public).GetEncoded();
    var serverPrivate = ((MLKemPrivateKeyParameters)kp.Private).GetEncoded();

    if (existing is null) {
        db.Licenses.Add(new License {
            Key = req.LicenseKey,
            EcdsaPublicKey = ecdsaPk,
            MlKemPublicKey = mlkemPk,
            MlKemPrivateKey = serverPrivate,
            Hash = HMACSHA256.HashData(hmac.Secret, Encoding.UTF8.GetBytes(req.LicenseKey))
        });
    } else {
        existing.EcdsaPublicKey = ecdsaPk;
        existing.MlKemPublicKey = mlkemPk;
        existing.MlKemPrivateKey = serverPrivate;
        existing.Hash = HMACSHA256.HashData(hmac.Secret, Encoding.UTF8.GetBytes(req.LicenseKey));
    }
    await db.SaveChangesAsync(ct);
    return Results.Ok(new SharedModels.RegisterResponse(true, Convert.ToBase64String(serverPublic)));
});

// CHALLENGE
app.MapPost("/challenge", async (SharedModels.ChallengeRequest req, LicenseDbContext db, CancellationToken ct) => {
    var lic = await db.Licenses.FirstOrDefaultAsync(x => x.Key == req.LicenseKey, ct);
    if (lic == null) return Results.NotFound();
    lic.Nonce = RandomNumberGenerator.GetBytes(32);
    lic.NonceExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(30);
    await db.SaveChangesAsync(ct);
    return Results.Ok(new SharedModels.ChallengeResponse(Convert.ToBase64String(lic.Nonce), lic.NonceExpiresAtUtc.Value));
});

// VALIDATE
app.MapPost("/validate", async (SharedModels.ValidateRequest req, LicenseDbContext db, CancellationToken ct) => {
    var lic = await db.Licenses.FirstOrDefaultAsync(x => x.Key == req.LicenseKey, ct);
    if (lic?.Nonce == null) return Results.Unauthorized();

    bool ecdsaOk = false, pqOk = false;
    try {
        // ECDSA Verify
        using var ecdsa = ECDsa.Create(); ecdsa.ImportSubjectPublicKeyInfo(lic.EcdsaPublicKey, out _);
        ecdsaOk = ecdsa.VerifyData(
            Encoding.UTF8.GetBytes($"{req.LicenseKey}.{req.NonceBase64}"),
            Convert.FromBase64String(req.SignatureBase64),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence);

        // ML-KEM Decapsulate
        var decap = new MLKemDecapsulator(MLKemParameters.ml_kem_768);
        decap.Init(MLKemPrivateKeyParameters.FromEncoding(MLKemParameters.ml_kem_768, lic.MlKemPrivateKey));
        var ctBytes = Convert.FromBase64String(req.MlKemCiphertextBase64);
        var sharedSecret = new byte[decap.SecretLength];
        decap.Decapsulate(ctBytes, 0, ctBytes.Length, sharedSecret, 0, sharedSecret.Length);
        
        var expectedPop = HMACSHA256.HashData(sharedSecret, Encoding.UTF8.GetBytes($"Challenge{req.NonceBase64}{req.LicenseKey}"));
        pqOk = CryptographicOperations.FixedTimeEquals(expectedPop, Convert.FromBase64String(req.MlKemProofOfPossessionBase64));
    } catch { }

    lic.Nonce = null;
    await db.SaveChangesAsync(ct);
    return (ecdsaOk && pqOk)
        ? Results.Ok(new SharedModels.ValidateResponse(true))
        : Results.Unauthorized();
});

app.Run();

sealed class HmacSecretProvider { public byte[] Secret { get; } = RandomNumberGenerator.GetBytes(32); }
sealed class NonceCleanupService : BackgroundService { protected override Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask; }
