using System;
using System.Security.Cryptography;
using LicenseCore;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Kems;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace LicenseSystem.Tests
{
    public class MLKemTests
    {
        private readonly ITestOutputHelper _output;

        public MLKemTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static (byte[] PublicKey, byte[] PrivateKey) GenerateServerKeyPair()
        {
            var kpg = new MLKemKeyPairGenerator();
            kpg.Init(new MLKemKeyGenerationParameters(new SecureRandom(), MLKemParameters.ml_kem_768));
            var kp = kpg.GenerateKeyPair();
            var pub = ((MLKemPublicKeyParameters)kp.Public).GetEncoded();
            var priv = ((MLKemPrivateKeyParameters)kp.Private).GetEncoded();
            return (pub, priv);
        }

        private static byte[] Decapsulate(byte[] privateKey, byte[] ciphertext)
        {
            var decap = new MLKemDecapsulator(MLKemParameters.ml_kem_768);
            var privParams = MLKemPrivateKeyParameters.FromEncoding(MLKemParameters.ml_kem_768, privateKey);
            decap.Init(privParams);
            var sharedSecret = new byte[decap.SecretLength];
            decap.Decapsulate(ciphertext, 0, ciphertext.Length, sharedSecret, 0, sharedSecret.Length);
            return sharedSecret;
        }

        [Fact]
        public void MLKem_EncapDecap_RoundTrip_Works()
        {
            var (serverPub, serverPriv) = GenerateServerKeyPair();
            var clientKeys = new HybridKeyProvider();

            var (cipher, secret) = clientKeys.EncapsulateWithMLKem(serverPub);
            var decapped = Decapsulate(serverPriv, cipher);

            Assert.True(CryptographicOperations.FixedTimeEquals(secret, decapped));
        }

        [Fact]
        public void MLKem_CiphertextTamper_ChangesSecret()
        {
            var (serverPub, serverPriv) = GenerateServerKeyPair();
            var clientKeys = new HybridKeyProvider();

            var (cipher, secret) = clientKeys.EncapsulateWithMLKem(serverPub);
            cipher[0] ^= 0x01;
            var decapped = Decapsulate(serverPriv, cipher);

            Assert.False(CryptographicOperations.FixedTimeEquals(secret, decapped));
        }

        [Fact]
        public void MLKem_Encapsulation_IsRandomized()
        {
            var (serverPub, serverPriv) = GenerateServerKeyPair();
            var clientKeys = new HybridKeyProvider();

            var (cipher1, secret1) = clientKeys.EncapsulateWithMLKem(serverPub);
            var (cipher2, secret2) = clientKeys.EncapsulateWithMLKem(serverPub);

            Assert.False(CryptographicOperations.FixedTimeEquals(cipher1, cipher2));
            Assert.False(CryptographicOperations.FixedTimeEquals(secret1, secret2));

            // Sanity: both should still decapsulate correctly.
            var decapped1 = Decapsulate(serverPriv, cipher1);
            var decapped2 = Decapsulate(serverPriv, cipher2);
            Assert.True(CryptographicOperations.FixedTimeEquals(secret1, decapped1));
            Assert.True(CryptographicOperations.FixedTimeEquals(secret2, decapped2));
        }

        [Fact]
        public void MLKem_SpeedAndSizes_AreReasonable()
        {
            var sw = Stopwatch.StartNew();
            var (serverPub, serverPriv) = GenerateServerKeyPair();
            sw.Stop();
            var keyGenMs = sw.ElapsedMilliseconds;

            var clientKeys = new HybridKeyProvider();

            sw.Restart();
            var (cipher, secret) = clientKeys.EncapsulateWithMLKem(serverPub);
            sw.Stop();
            var encapMs = sw.ElapsedMilliseconds;

            sw.Restart();
            var decapped = Decapsulate(serverPriv, cipher);
            sw.Stop();
            var decapMs = sw.ElapsedMilliseconds;

            _output.WriteLine($"ML-KEM-768 KeyGen: {keyGenMs} ms");
            _output.WriteLine($"ML-KEM-768 Encap: {encapMs} ms, CT size: {cipher.Length} bytes, secret: {secret.Length} bytes");
            _output.WriteLine($"ML-KEM-768 Decap: {decapMs} ms");

            Assert.True(cipher.Length > 0);
            Assert.True(secret.Length > 0);
            Assert.True(CryptographicOperations.FixedTimeEquals(secret, decapped));
        }
    }
}
