using ShipLean.Application.Fakes.DeviceSessions;
using ShipLean.Contracts.DeviceSessions;
using ShipLean.TestSupport.Contracts.DeviceSessions;

namespace ShipLean.UnitTest.Contracts.DeviceSessions;

public sealed class InMemoryDeviceSessionsContractTests : DeviceSessionsContract
{
    private InMemoryDeviceSessions _sessions = null!;

    [SetUp]
    public void CreateStore() => _sessions = new InMemoryDeviceSessions();

    protected override IDeviceSessions Sessions => _sessions;
}
