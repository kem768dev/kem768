using System.Security.Cryptography;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Kems;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

namespace LicenseCore
{
    public class HybridKeyProvider
    {
        private readonly ECDsa _ecdsa;
        private readonly MLKemParameters _mlkemParams = MLKemParameters.ml_kem_768;
        private readonly MLKemPublicKeyParameters _mlkemPublic;
        private readonly MLKemPrivateKeyParameters _mlkemPrivate;

        public HybridKeyProvider()
        {
            _ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var random = new SecureRandom();
            var kpg = new MLKemKeyPairGenerator();
            kpg.Init(new MLKemKeyGenerationParameters(random, _mlkemParams));
            var kp = kpg.GenerateKeyPair();
            _mlkemPublic = (MLKemPublicKeyParameters)kp.Public;
            _mlkemPrivate = (MLKemPrivateKeyParameters)kp.Private;
        }

        public byte[] GetEcdsaPublicKey() => _ecdsa.ExportSubjectPublicKeyInfo();
        public byte[] GetMLKemPublicKey() => _mlkemPublic.GetEncoded();

        public byte[] SignWithEcdsa(byte[] data) => 
            _ecdsa.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

        public (byte[] ciphertext, byte[] sharedSecret) EncapsulateWithMLKem(byte[] serverPublicKey)
        {
            var serverPkParams = MLKemPublicKeyParameters.FromEncoding(_mlkemParams, serverPublicKey);
            var random = new SecureRandom();
            var encapsulator = new MLKemEncapsulator(_mlkemParams);
            encapsulator.Init(new ParametersWithRandom(serverPkParams, random));
            
            var ciphertext = new byte[encapsulator.EncapsulationLength];
            var sharedSecret = new byte[encapsulator.SecretLength];
            encapsulator.Encapsulate(ciphertext, 0, ciphertext.Length, sharedSecret, 0, sharedSecret.Length);
            
            return (ciphertext, sharedSecret);
        }
    }
}
