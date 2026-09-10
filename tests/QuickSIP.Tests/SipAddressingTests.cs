using QuickSIP.Core.Models;
using QuickSIP.Core.Sip;
using Xunit;

namespace QuickSIP.Tests;

public sealed class SipAddressingTests
{
    [Fact]
    public void BuildRegistrarHost_UdpDefault()
    {
        var settings = new AppSettings { SipServer = "pbx.example.com", SipPort = 5060, Transport = "udp" };
        Assert.Equal("sip:pbx.example.com:5060", SipAddressing.BuildRegistrarHost(settings));
    }

    [Fact]
    public void BuildRegistrarHost_TcpAddsTransport()
    {
        var settings = new AppSettings { SipServer = "pbx.example.com", SipPort = 5070, Transport = "tcp" };
        Assert.Equal("sip:pbx.example.com:5070;transport=tcp", SipAddressing.BuildRegistrarHost(settings));
    }

    [Fact]
    public void BuildInviteUri_PlainNumber()
    {
        var settings = new AppSettings { SipServer = "sip.example.com", Domain = "example.com" };
        Assert.Equal("sip:18005551212@example.com", SipAddressing.BuildInviteUri("18005551212", settings));
    }

    [Fact]
    public void BuildInviteUri_PassesThroughSipUri()
    {
        var settings = new AppSettings { SipServer = "sip.example.com" };
        Assert.Equal("sip:music@iptel.org", SipAddressing.BuildInviteUri("sip:music@iptel.org", settings));
    }

    [Fact]
    public void BuildFromHeader_IncludesDisplayName()
    {
        var settings = new AppSettings
        {
            Username = "1001",
            Domain = "example.com",
            DisplayName = "Front Desk"
        };
        Assert.Equal("\"Front Desk\" <sip:1001@example.com>", SipAddressing.BuildFromHeader(settings));
    }

    [Fact]
    public void DescribeIncoming_UsesDisplayAndUri()
    {
        var described = SipAddressing.DescribeIncoming("\"Alice\" <sip:1002@example.com>", null);
        Assert.Contains("Alice", described);
        Assert.Contains("sip:1002@example.com", described);
    }

    [Fact]
    public void BuildRegistrarHost_RequiresServer()
    {
        Assert.Throws<InvalidOperationException>(() =>
            SipAddressing.BuildRegistrarHost(new AppSettings()));
    }
}
