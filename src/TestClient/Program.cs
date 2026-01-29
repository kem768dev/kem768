using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Net.Http.Json;
using LicenseCore;
using LicenseCore.Shared.DTOs;

const string DEFAULT_SERVER = "http://localhost:5108";
const string KEY = "TEST-LICENSE-PQ-FINAL";

Console.WriteLine("=== FINAL SYSTEM CHECK: ECDSA + ML-KEM-768 ===\n");
var server = Environment.GetEnvironmentVariable("LICENSE_SERVER_URL") ?? DEFAULT_SERVER;
using var http = new HttpClient { BaseAddress = new Uri(server) };

// 1. Keys generieren
var keys = new HybridKeyProvider();
var ecdsaPk = Convert.ToBase64String(keys.GetEcdsaPublicKey());
var mlkemPk = Convert.ToBase64String(keys.GetMLKemPublicKey());
Console.WriteLine($"[1] Keys generated. ML-KEM Size: {keys.GetMLKemPublicKey().Length} bytes");

// 2. Register
var reg = await http.PostAsJsonAsync("/register", new SharedModels.RegisterRequest(
    KEY, ecdsaPk, mlkemPk
));
Console.WriteLine($"[2] Register: {reg.StatusCode}");
if (!reg.IsSuccessStatusCode)
{
    Console.WriteLine(await reg.Content.ReadAsStringAsync());
    return;
}
var regRes = await reg.Content.ReadFromJsonAsync<SharedModels.RegisterResponse>();
if (regRes is null)
{
    Console.WriteLine("[2] Register: no response payload");
    return;
}

// 3. Challenge
var chal = await (await http.PostAsJsonAsync("/challenge", new SharedModels.ChallengeRequest(KEY)))
    .Content.ReadFromJsonAsync<SharedModels.ChallengeResponse>();
Console.WriteLine($"[3] Challenge Nonce: {chal.NonceBase64[..10]}...");

// 4. Validate
// Use server's ML-KEM public key returned by /register
var serverMlKemPk = Convert.FromBase64String(regRes.ServerMlKemPublicKeyBase64);
var (cipher, secret) = keys.EncapsulateWithMLKem(serverMlKemPk);
var sig = keys.SignWithEcdsa(Encoding.UTF8.GetBytes($"{KEY}.{chal.NonceBase64}"));
var pop = HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes($"Challenge{chal.NonceBase64}{KEY}"));

var val = await http.PostAsJsonAsync("/validate", new SharedModels.ValidateRequest(
    KEY,
    chal.NonceBase64,
    Convert.ToBase64String(sig),
    Convert.ToBase64String(cipher),
    Convert.ToBase64String(pop)
));

if (!val.IsSuccessStatusCode)
{
    Console.WriteLine($"[4] Validate failed: {val.StatusCode}");
    Console.WriteLine(await val.Content.ReadAsStringAsync());
    return;
}
var res = await val.Content.ReadFromJsonAsync<SharedModels.ValidateResponse>();
Console.WriteLine($"\n>>> RESULT: Valid = {res?.Valid} <<<\n");
Console.ReadKey();
