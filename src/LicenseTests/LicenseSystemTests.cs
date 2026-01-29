using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using Moq;
using Moq.Protected;
using Xunit;
using LicenseSystem;

// WICHTIG: Das hier verhindert den Absturz bei mehreren Tests!
// Es zwingt Visual Studio, die Tests nacheinander (nicht gleichzeitig) auszuführen.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace LicenseSystem.Tests
{
    public class LicenseSystemTests
    {
        // Hilfsfunktion: Baut Fake-Hardware
        private Mock<ISystemInfoProvider> CreateMockSys()
        {
            var m = new Mock<ISystemInfoProvider>();
            m.Setup(x => x.GetComputerModel()).Returns("MyPC");
            m.Setup(x => x.GetCpuId()).Returns("CPU-SECRET");
            m.Setup(x => x.GetDiskSerialNumber()).Returns("DISK-SECRET");
            m.Setup(x => x.GetBaseboardSerialNumber()).Returns("BOARD-SECRET");
            return m;
        }

        [Fact]
        public void IsLikelyVirtualMachine_DetectsVMware()
        {
            var sys = CreateMockSys();
            var hwid = new HwidProvider(sys.Object, new byte[32]);
            
            // Wir sagen explizit: Das Modell ist "VMware"
            bool isVm = hwid.IsLikelyVirtualMachine("VMware Virtual Platform");
            Assert.True(isVm);
        }

        [Fact]
        public void GetPseudonymizedHwid_IsStable()
        {
            var sys = CreateMockSys();
            var key = Encoding.UTF8.GetBytes("12345678901234567890123456789012");
            var hwid = new HwidProvider(sys.Object, key);

            var h1 = hwid.GetPseudonymizedHwid();
            var h2 = hwid.GetPseudonymizedHwid();

            Assert.Equal(h1, h2);
            Assert.False(string.IsNullOrEmpty(h1));
        }

        [Fact]
        public async Task RegisterAsync_NoPrivacyLeaks()
        {
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(System.Net.HttpStatusCode.OK))
                .Callback<HttpRequestMessage, CancellationToken>((r, c) => {
                    var body = r.Content.ReadAsStringAsync().Result;
                    Assert.DoesNotContain("CPU-SECRET", body);
                });

            // Vorbereitung: Key sicherstellen
            var idCheck = new DeviceIdentityManager();
            idCheck.GetOrCreatePublicKey();

            var client = new LicenseClient(new HttpClient(handlerMock.Object), new HwidProvider(CreateMockSys().Object, new byte[32]));
            await client.RegisterAsync("http://test.com");
        }

        [Fact]
        public void Signing_Works()
        {
            var client = new LicenseClient();
            
            // WICHTIG: Erst Key erstellen
            client.GetPublicKey(); 
            
            var data = Encoding.UTF8.GetBytes("test");
            var sig = client.SignChallenge(data);
            
            Assert.NotNull(sig);
            Assert.NotEmpty(sig);
        }
    }
}
