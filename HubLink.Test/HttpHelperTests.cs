namespace HubLink.Test;

public class HttpHelperTests
{
    [Fact]
    public void TestIsTlsPacket_ValidTlsHandshake()
    {
        var tlsData = new byte[] { 0x16, 0x03, 0x01, 0x00, 0x01 };
        Assert.True(HttpHelper.IsTlsPacket(tlsData));
    }

    [Fact]
    public void TestIsTlsPacket_ValidTlsChangeCipherSpec()
    {
        var tlsData = new byte[] { 0x14, 0x03, 0x01, 0x00, 0x01 };
        Assert.True(HttpHelper.IsTlsPacket(tlsData));
    }

    [Fact]
    public void TestIsTlsPacket_ValidTlsAlert()
    {
        var tlsData = new byte[] { 0x15, 0x03, 0x01, 0x00, 0x01 };
        Assert.True(HttpHelper.IsTlsPacket(tlsData));
    }

    [Fact]
    public void TestIsTlsPacket_ValidTlsApplicationData()
    {
        var tlsData = new byte[] { 0x17, 0x03, 0x01, 0x00, 0x01 };
        Assert.True(HttpHelper.IsTlsPacket(tlsData));
    }

    [Fact]
    public void TestIsTlsPacket_DifferentVersions()
    {
        var tlsVersions = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04 };
        foreach (var version in tlsVersions)
        {
            var tlsData = new byte[] { 0x16, 0x03, version, 0x00, 0x01 };
            Assert.True(HttpHelper.IsTlsPacket(tlsData));
        }
    }

    [Fact]
    public void TestIsTlsPacket_InvalidContentType()
    {
        var invalidData = new byte[] { 0x18, 0x03, 0x01, 0x00, 0x01 };
        Assert.False(HttpHelper.IsTlsPacket(invalidData));
    }

    [Fact]
    public void TestIsTlsPacket_InvalidVersion()
    {
        var invalidData = new byte[] { 0x16, 0x04, 0x01, 0x00, 0x01 };
        Assert.False(HttpHelper.IsTlsPacket(invalidData));
    }

    [Fact]
    public void TestIsTlsPacket_NullData()
    {
        Assert.False(HttpHelper.IsTlsPacket(null));
    }

    [Fact]
    public void TestIsTlsPacket_ShortData()
    {
        var shortData = new byte[] { 0x16, 0x03, 0x01 };
        Assert.False(HttpHelper.IsTlsPacket(shortData));
    }

    [Fact]
    public void TestIsConnectRequest_String_ValidConnect()
    {
        var connectRequest = "CONNECT www.example.com:443 HTTP/1.1\r\nHost: www.example.com\r\n\r\n";
        Assert.True(HttpHelper.IsConnectRequest(connectRequest));
    }

    [Fact]
    public void TestIsConnectRequest_String_Lowercase()
    {
        var connectRequest = "connect www.example.com:443 HTTP/1.1\r\n";
        Assert.True(HttpHelper.IsConnectRequest(connectRequest));
    }

    [Fact]
    public void TestIsConnectRequest_String_MixedCase()
    {
        var connectRequest = "CoNnEcT www.example.com:443 HTTP/1.1\r\n";
        Assert.True(HttpHelper.IsConnectRequest(connectRequest));
    }

    [Fact]
    public void TestIsConnectRequest_String_GetRequest()
    {
        var getRequest = "GET / HTTP/1.1\r\nHost: www.example.com\r\n\r\n";
        Assert.False(HttpHelper.IsConnectRequest(getRequest));
    }

    [Fact]
    public void TestIsConnectRequest_String_Empty()
    {
        Assert.False(HttpHelper.IsConnectRequest(""));
    }

    [Fact]
    public void TestIsHttpRequest_String_GetRequest()
    {
        var getRequest = "GET / HTTP/1.1\r\nHost: www.example.com\r\n\r\n";
        Assert.True(HttpHelper.IsHttpRequest(getRequest));
    }

    [Fact]
    public void TestIsHttpRequest_String_PostRequest()
    {
        var postRequest = "POST /api HTTP/1.1\r\nHost: www.example.com\r\n\r\n";
        Assert.True(HttpHelper.IsHttpRequest(postRequest));
    }

    [Fact]
    public void TestIsHttpRequest_String_PutRequest()
    {
        var putRequest = "PUT /resource HTTP/1.1\r\nHost: www.example.com\r\n\r\n";
        Assert.True(HttpHelper.IsHttpRequest(putRequest));
    }

    [Fact]
    public void TestIsHttpRequest_String_DeleteRequest()
    {
        var deleteRequest = "DELETE /resource HTTP/1.1\r\nHost: www.example.com\r\n\r\n";
        Assert.True(HttpHelper.IsHttpRequest(deleteRequest));
    }

    [Fact]
    public void TestIsHttpRequest_String_HeadRequest()
    {
        var headRequest = "HEAD / HTTP/1.1\r\nHost: www.example.com\r\n\r\n";
        Assert.True(HttpHelper.IsHttpRequest(headRequest));
    }

    [Fact]
    public void TestIsHttpRequest_String_OptionsRequest()
    {
        var optionsRequest = "OPTIONS * HTTP/1.1\r\nHost: www.example.com\r\n\r\n";
        Assert.True(HttpHelper.IsHttpRequest(optionsRequest));
    }

    [Fact]
    public void TestIsHttpRequest_String_PatchRequest()
    {
        var patchRequest = "PATCH /resource HTTP/1.1\r\nHost: www.example.com\r\n\r\n";
        Assert.True(HttpHelper.IsHttpRequest(patchRequest));
    }

    [Fact]
    public void TestIsHttpRequest_String_ConnectRequest()
    {
        var connectRequest = "CONNECT www.example.com:443 HTTP/1.1\r\n";
        Assert.False(HttpHelper.IsHttpRequest(connectRequest));
    }

    [Fact]
    public void TestIsHttpRequest_String_Lowercase()
    {
        var getRequest = "get / HTTP/1.1\r\nHost: www.example.com\r\n\r\n";
        Assert.True(HttpHelper.IsHttpRequest(getRequest));
    }

    [Fact]
    public void TestParseHttpConnectRequest_ValidRequest()
    {
        var connectRequest = "CONNECT www.example.com:443 HTTP/1.1\r\nHost: www.example.com\r\n\r\n";
        var (host, port) = HttpHelper.ParseHttpConnectRequest(connectRequest);
        Assert.Equal("www.example.com", host);
        Assert.Equal(443, port);
    }

    [Fact]
    public void TestParseHttpConnectRequest_DifferentPort()
    {
        var connectRequest = "CONNECT www.example.com:8080 HTTP/1.1\r\n";
        var (host, port) = HttpHelper.ParseHttpConnectRequest(connectRequest);
        Assert.Equal("www.example.com", host);
        Assert.Equal(8080, port);
    }

    [Fact]
    public void TestParseHttpConnectRequest_NoPort()
    {
        var connectRequest = "CONNECT www.example.com HTTP/1.1\r\n";
        var (host, port) = HttpHelper.ParseHttpConnectRequest(connectRequest);
        Assert.Null(host);
        Assert.Equal(0, port);
    }

    [Fact]
    public void TestParseHttpConnectRequest_Empty()
    {
        var (host, port) = HttpHelper.ParseHttpConnectRequest("");
        Assert.Null(host);
        Assert.Equal(0, port);
    }

    [Fact]
    public void TestParseHttpConnectRequest_InvalidFormat()
    {
        var invalidRequest = "INVALID REQUEST\r\n";
        var (host, port) = HttpHelper.ParseHttpConnectRequest(invalidRequest);
        Assert.Null(host);
        Assert.Equal(0, port);
    }

    [Fact]
    public void TestParseHttpRequest_ValidRequest()
    {
        var getRequest = "GET / HTTP/1.1\r\nHost: www.example.com\r\n\r\n";
        var (host, port) = HttpHelper.ParseHttpRequest(getRequest);
        Assert.Equal("www.example.com", host);
        Assert.Equal(80, port);
    }

    [Fact]
    public void TestParseHttpRequest_WithPort()
    {
        var getRequest = "GET / HTTP/1.1\r\nHost: www.example.com:8080\r\n\r\n";
        var (host, port) = HttpHelper.ParseHttpRequest(getRequest);
        Assert.Equal("www.example.com", host);
        Assert.Equal(8080, port);
    }

    [Fact]
    public void TestParseHttpRequest_MultipleHeaders()
    {
        var getRequest = "GET / HTTP/1.1\r\nUser-Agent: Test\r\nHost: www.example.com\r\nAccept: */*\r\n\r\n";
        var (host, port) = HttpHelper.ParseHttpRequest(getRequest);
        Assert.Equal("www.example.com", host);
        Assert.Equal(80, port);
    }

    [Fact]
    public void TestParseHttpRequest_NoHostHeader()
    {
        var getRequest = "GET / HTTP/1.1\r\nUser-Agent: Test\r\n\r\n";
        var (host, port) = HttpHelper.ParseHttpRequest(getRequest);
        Assert.Null(host);
        Assert.Equal(0, port);
    }

    [Fact]
    public void TestParseHttpRequest_Empty()
    {
        var (host, port) = HttpHelper.ParseHttpRequest("");
        Assert.Null(host);
        Assert.Equal(0, port);
    }

    [Fact]
    public void TestParseHttpRequest_HostHeaderCaseInsensitive()
    {
        var getRequest = "GET / HTTP/1.1\r\nhost: www.example.com\r\n\r\n";
        var (host, port) = HttpHelper.ParseHttpRequest(getRequest);
        Assert.Equal("www.example.com", host);
        Assert.Equal(80, port);
    }

    [Fact]
    public void TestIsConnectRequest_Bytes_ValidConnect()
    {
        var connectRequest = Encoding.ASCII.GetBytes("CONNECT www.example.com:443 HTTP/1.1\r\nHost: www.example.com\r\n\r\n");
        Assert.True(HttpHelper.IsConnectRequest(connectRequest));
    }

    [Fact]
    public void TestIsConnectRequest_Bytes_GetRequest()
    {
        var getRequest = Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: www.example.com\r\n\r\n");
        Assert.False(HttpHelper.IsConnectRequest(getRequest));
    }

    [Fact]
    public void TestIsConnectRequest_Bytes_Null()
    {
        Assert.False(HttpHelper.IsConnectRequest((byte[])null));
    }

    [Fact]
    public void TestIsConnectRequest_Bytes_Short()
    {
        var shortData = new byte[] { 0x43, 0x4F, 0x4E };
        Assert.False(HttpHelper.IsConnectRequest(shortData));
    }

    [Fact]
    public void TestIsConnectRequest_Bytes_Lowercase()
    {
        var connectRequest = Encoding.ASCII.GetBytes("connect www.example.com:443 HTTP/1.1\r\n");
        Assert.True(HttpHelper.IsConnectRequest(connectRequest));
    }

    [Fact]
    public void TestParseHttpHost_ConnectRequest()
    {
        var connectRequest = Encoding.ASCII.GetBytes("CONNECT www.example.com:443 HTTP/1.1\r\nHost: www.example.com\r\n\r\n");
        var (host, port) = HttpHelper.ParseHttpHost(connectRequest);
        Assert.Equal("www.example.com", host);
        Assert.Equal(443, port);
    }

    [Fact]
    public void TestParseHttpHost_GetRequest()
    {
        var getRequest = Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: www.example.com\r\n\r\n");
        var (host, port) = HttpHelper.ParseHttpHost(getRequest);
        Assert.Equal("www.example.com", host);
        Assert.Equal(80, port);
    }

    [Fact]
    public void TestParseHttpHost_GetRequestWithPort()
    {
        var getRequest = Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: www.example.com:8080\r\n\r\n");
        var (host, port) = HttpHelper.ParseHttpHost(getRequest);
        Assert.Equal("www.example.com", host);
        Assert.Equal(8080, port);
    }

    [Fact]
    public void TestParseHttpHost_Empty()
    {
        var emptyData = Array.Empty<byte>();
        var (host, port) = HttpHelper.ParseHttpHost(emptyData);
        Assert.Equal("www.baidu.com", host);
        Assert.Equal(80, port);
    }

    [Fact]
    public void TestParseHttpHost_Null()
    {
        var (host, port) = HttpHelper.ParseHttpHost(null);
        Assert.Equal("www.baidu.com", host);
        Assert.Equal(80, port);
    }

    [Fact]
    public void TestParseHttpHost_InvalidRequest()
    {
        var invalidRequest = Encoding.ASCII.GetBytes("INVALID REQUEST DATA");
        var (host, port) = HttpHelper.ParseHttpHost(invalidRequest);
        Assert.Equal("www.baidu.com", host);
        Assert.Equal(80, port);
    }

    [Fact]
    public void TestParseHttpHost_ConnectRequestDifferentPorts()
    {
        var testCases = new[]
        {
            ("CONNECT www.example.com:80 HTTP/1.1\r\n", "www.example.com", 80),
            ("CONNECT www.example.com:443 HTTP/1.1\r\n", "www.example.com", 443),
            ("CONNECT www.example.com:8080 HTTP/1.1\r\n", "www.example.com", 8080),
            ("CONNECT www.example.com:9000 HTTP/1.1\r\n", "www.example.com", 9000)
        };

        foreach (var (request, expectedHost, expectedPort) in testCases)
        {
            var data = Encoding.ASCII.GetBytes(request);
            var (host, port) = HttpHelper.ParseHttpHost(data);
            Assert.Equal(expectedHost, host);
            Assert.Equal(expectedPort, port);
        }
    }

    [Fact]
    public void TestParseHttpHost_HttpRequestDifferentHosts()
    {
        var testCases = new[]
        {
            ("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n", "example.com", 80),
            ("GET / HTTP/1.1\r\nHost: www.example.com\r\n\r\n", "www.example.com", 80),
            ("GET / HTTP/1.1\r\nHost: api.example.com:443\r\n\r\n", "api.example.com", 443),
            ("GET / HTTP/1.1\r\nHost: sub.domain.co.uk\r\n\r\n", "sub.domain.co.uk", 80)
        };

        foreach (var (request, expectedHost, expectedPort) in testCases)
        {
            var data = Encoding.ASCII.GetBytes(request);
            var (host, port) = HttpHelper.ParseHttpHost(data);
            Assert.Equal(expectedHost, host);
            Assert.Equal(expectedPort, port);
        }
    }

    [Fact]
    public void TestParseHttpHost_LargeData()
    {
        var largeData = new byte[10000];
        Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: www.example.com\r\n\r\n").CopyTo(largeData, 0);
        var (host, port) = HttpHelper.ParseHttpHost(largeData);
        Assert.Equal("www.example.com", host);
        Assert.Equal(80, port);
    }
}
