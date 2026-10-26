using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace ShipLean.TestSupport.Hosting;

public static class LocalAddresses
{
    // A non-loopback IPv4 address of this machine: a request to it reaches a host started with listenOnAllInterfaces
    // from a peer that isn't loopback, for untrusted-peer tests. (ForwardedHeadersTests keeps its own copy.)
    public static IPAddress OtherThanLoopback() => NetworkInterface.GetAllNetworkInterfaces()
        .Where(nic => nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
        .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
        .Select(address => address.Address)
        .First(address => address.AddressFamily == AddressFamily.InterNetwork);
}
