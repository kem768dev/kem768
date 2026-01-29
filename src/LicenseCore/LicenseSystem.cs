using System;
using System.Linq;
using System.Management; 
using System.Security.Cryptography;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;

namespace LicenseSystem
{
    // Interface für Hardware-Infos (damit der Test lügen kann)
    public interface ISystemInfoProvider
    {
        string GetComputerModel();
        string GetCpuId();
        string GetDiskSerialNumber();
        string GetBaseboardSerialNumber();
        string GetMachineGuid();
    }

    // Die echte Windows-Implementierung
    public class WindowsSystemInfoProvider : ISystemInfoProvider
    {
        public string GetComputerModel() => GetWmi("Win32_ComputerSystem", "Model");
        public string GetCpuId() => GetWmi("Win32_Processor", "ProcessorId");
        public string GetDiskSerialNumber() => GetWmi("Win32_DiskDrive", "SerialNumber");
        public string GetBaseboardSerialNumber() => GetWmi("Win32_BaseBoard", "SerialNumber");
        
        public string GetMachineGuid() {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) return "DUMMY";
            try {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                return key?.GetValue("MachineGuid")?.ToString() ?? "UNKNOWN";
            } catch { return "UNKNOWN"; }
        }

        private string GetWmi(string cls, string prop) {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) return "DUMMY";
            try {
                using var s = new ManagementObjectSearcher($"SELECT {prop} FROM {cls}");
                foreach (ManagementObject o in s.Get()) return o[prop]?.ToString()?.Trim() ?? "";
            } catch {}
            return "UNKNOWN";
        }
    }

    // Die Haupt-Logik
    public class HwidProvider
    {
        private readonly ISystemInfoProvider _sys;
        private readonly byte[] _key;

        // Konstruktor für Tests (Fake Hardware)
        public HwidProvider(ISystemInfoProvider sys, byte[] tenantKey) { _sys = sys; _key = tenantKey; }
        // Konstruktor für App (Echtes Windows)
        public HwidProvider(byte[] tenantKey) : this(new WindowsSystemInfoProvider(), tenantKey) { }

        // Testet auf VMs (akzeptiert optionalen Namen für Tests)
        public bool IsLikelyVirtualMachine(string modelOverride = null)
        {
            string m = (modelOverride ?? _sys.GetComputerModel()).ToLowerInvariant();
            return new[] { "vmware", "virtual", "box", "qemu", "xen", "hyper-v" }.Any(v => m.Contains(v));
        }

        public string GetPseudonymizedHwid()
        {
            var raw = $"{_sys.GetCpuId()}||{_sys.GetDiskSerialNumber()}||{_sys.GetBaseboardSerialNumber()}";
            using var hmac = new HMACSHA256(_key);
            return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(raw)));
        }
    }

    public class DeviceIdentityManager
    {
        private const string KName = "LicKeyV1";
        public byte[] GetOrCreatePublicKey() {
            using var k = CngKey.Create(CngAlgorithm.ECDsaP256, KName, new CngKeyCreationParameters {
                Provider = CngProvider.MicrosoftSoftwareKeyStorageProvider,
                KeyCreationOptions = CngKeyCreationOptions.OverwriteExistingKey
            });
            return k.Export(CngKeyBlobFormat.EccPublicBlob);
        }
        public byte[] SignChallenge(byte[] data) {
            using var k = CngKey.Open(KName, CngProvider.MicrosoftSoftwareKeyStorageProvider);
            using var d = new ECDsaCng(k);
            return d.SignData(data, HashAlgorithmName.SHA256);
        }
    }

    public class LicenseClient
    {
        private readonly HttpClient _http;
        private readonly HwidProvider _hwid;
        private readonly DeviceIdentityManager _id = new DeviceIdentityManager();

        public LicenseClient(HttpClient http, HwidProvider hwid) { _http = http; _hwid = hwid; }
        public LicenseClient() : this(new HttpClient(), new HwidProvider(new byte[32])) { }

        public byte[] SignChallenge(byte[] c) => _id.SignChallenge(c);
        public byte[] GetPublicKey() => _id.GetOrCreatePublicKey();

        public async Task RegisterAsync(string url)
        {
            // JSON ohne Seriennummern
            var json = $"{{\"hwid\":\"{_hwid.GetPseudonymizedHwid()}\", \"isVm\":{_hwid.IsLikelyVirtualMachine().ToString().ToLower()}}}";
            await _http.PostAsync(url, new StringContent(json));
        }
    }
}
