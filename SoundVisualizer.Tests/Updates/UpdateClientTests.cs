// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Net;
using System.Security.Cryptography;
using System.Text;
using SoundVisualizer.Updates;

namespace SoundVisualizer.Tests.Updates;

public sealed class UpdateClientTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "sv-dl-" + Guid.NewGuid().ToString("N"));

    public UpdateClientTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    /// <summary>요청을 적어 두고 정해 둔 답을 돌려주는 가짜 서버.</summary>
    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static UpdateClient Client(FakeHandler handler, string current = "2.2.0") => new(new HttpClient(handler), Version.Parse(current));

    private static Task<UpdateCheckResult> Check(FakeHandler handler, string current = "2.2.0") =>
        Client(handler, current).CheckAsync(CancellationToken.None);

    [Fact]
    public async Task 더_높은_버전이_있으면_새_버전이_있다고_답한다()
    {
        var json = ReleaseParserTests.Sample();
        json["tag_name"] = "SoundVisualizer_v2.3";

        var result = await Check(new FakeHandler(_ => Json(json.ToJsonString())));

        Assert.Equal(UpdateCheckOutcome.Available, result.Outcome);
        Assert.Equal(new Version(2, 3, 0), result.Release!.Version);
    }

    [Theory]
    [InlineData(UpdatePackageKind.Portable, "SoundVisualizer_v2.2_portable.zip")]
    [InlineData(UpdatePackageKind.Standard, "SoundVisualizer_v2.2.zip")]
    public async Task 받을_zip_의_종류에_맞는_자산을_고른다(UpdatePackageKind kind, string expected)
    {
        var json = ReleaseParserTests.TwoZips();
        json["tag_name"] = "SoundVisualizer_v2.3";
        var client = new UpdateClient(new HttpClient(new FakeHandler(_ => Json(json.ToJsonString()))), Version.Parse("2.2.0"), kind: kind);

        var result = await client.CheckAsync(CancellationToken.None);

        Assert.Equal(UpdateCheckOutcome.Available, result.Outcome);
        Assert.Equal(expected, result.Release!.Asset!.Name);
    }

    [Theory]
    [InlineData("2.2.0")]
    [InlineData("2.3.0")]
    public async Task 같거나_낮은_버전이면_최신이다(string current)
    {
        var result = await Check(new FakeHandler(_ => Json(ReleaseParserTests.Sample().ToJsonString())), current);
        Assert.Equal(UpdateCheckOutcome.Latest, result.Outcome);
    }

    [Fact]
    public async Task 묻는_곳과_보내는_정보는_최신_릴리스_주소와_앱_이름_버전뿐이다()
    {
        var handler = new FakeHandler(_ => Json(ReleaseParserTests.Sample().ToJsonString()));
        await Check(handler);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(UpdateClient.LatestReleaseApiUrl, request.RequestUri!.ToString());
        Assert.Equal("SoundVisualizer-Windows/2.2", request.Headers.UserAgent.ToString());
        Assert.Null(request.Content);
        Assert.Null(request.Headers.Authorization);
        Assert.DoesNotContain(request.Headers, h => h.Key.Equals("Cookie", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task 오류_응답은_확인_실패다(HttpStatusCode status)
    {
        var result = await Check(new FakeHandler(_ => new HttpResponseMessage(status)));
        Assert.Equal(UpdateCheckOutcome.Failed, result.Outcome);
    }

    [Fact]
    public async Task 연결이_안_되면_예외_없이_확인_실패다()
    {
        var result = await Check(new FakeHandler(_ => throw new HttpRequestException("offline")));
        Assert.Equal(UpdateCheckOutcome.Failed, result.Outcome);
    }

    [Fact]
    public async Task 읽을_수_없는_답은_확인_실패다()
    {
        var result = await Check(new FakeHandler(_ => Json("<html>rate limited</html>")));
        Assert.Equal(UpdateCheckOutcome.Failed, result.Outcome);
    }

    private static ReleaseAsset AssetFor(byte[] content) => new(
        "SoundVisualizer_v2.3.zip",
        new Uri("https://github.com/SOVIS-Team/SoundVisualizer-Windows/releases/download/SoundVisualizer_v2.3/SoundVisualizer_v2.3.zip"),
        content.Length,
        Convert.ToHexStringLower(SHA256.HashData(content)));

    private static FakeHandler Serving(byte[] body) => new(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) });

    [Fact]
    public async Task 내려받은_파일의_크기와_SHA256_이_맞으면_남긴다()
    {
        byte[] content = Encoding.UTF8.GetBytes(new string('z', 300_000));
        string target = Path.Combine(_dir, "dl", "a.zip");
        var reports = new List<double>();

        await Client(Serving(content)).DownloadAsync(AssetFor(content), target, new SyncProgress(reports.Add), CancellationToken.None);

        Assert.Equal(content, File.ReadAllBytes(target));
        Assert.Equal(1.0, reports[^1], 6);
        Assert.Equal(reports.Order().ToList(), reports);
    }

    [Fact]
    public async Task SHA256_이_다르면_파일을_지우고_멈춘다()
    {
        byte[] content = Encoding.UTF8.GetBytes("real");
        byte[] tampered = Encoding.UTF8.GetBytes("fake");
        string target = Path.Combine(_dir, "a.zip");

        await Assert.ThrowsAsync<UpdateVerificationException>(() =>
            Client(Serving(tampered)).DownloadAsync(AssetFor(content), target, null, CancellationToken.None));
        Assert.False(File.Exists(target));
    }

    [Theory]
    [InlineData("short")]
    [InlineData("much longer than declared")]
    public async Task 크기가_다르면_파일을_지우고_멈춘다(string body)
    {
        byte[] declared = Encoding.UTF8.GetBytes("twelve bytes");
        string target = Path.Combine(_dir, "a.zip");

        await Assert.ThrowsAsync<UpdateVerificationException>(() =>
            Client(Serving(Encoding.UTF8.GetBytes(body))).DownloadAsync(AssetFor(declared), target, null, CancellationToken.None));
        Assert.False(File.Exists(target));
    }

    [Fact]
    public async Task 받기_오류는_그대로_알리고_파일을_남기지_않는다()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        string target = Path.Combine(_dir, "a.zip");

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            Client(handler).DownloadAsync(AssetFor(new byte[] { 1 }), target, null, CancellationToken.None));
        Assert.False(File.Exists(target));
    }

    [Fact]
    public async Task 한동안_아무것도_오지_않으면_끊긴_것으로_보고_파일을_남기지_않는다()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new StallingStream()) });
        string target = Path.Combine(_dir, "a.zip");

        await Assert.ThrowsAsync<TimeoutException>(() =>
            Client(handler).DownloadAsync(AssetFor(new byte[100]), target, null, CancellationToken.None, TimeSpan.FromMilliseconds(200)));
        Assert.False(File.Exists(target));
    }

    [Fact]
    public void 기본_끊김_시간은_60초다()
    {
        // 전체 시간에는 한도가 없다. 느린 연결도 조금씩 오면 끝까지 받는다.
        Assert.Equal(TimeSpan.FromSeconds(60), UpdateClient.DefaultStallTimeout);
    }

    /// <summary>처음 몇 바이트를 보낸 뒤 더는 아무것도 보내지 않는 연결.</summary>
    private sealed class StallingStream : Stream
    {
        private bool _sent;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!_sent)
            {
                _sent = true;
                buffer.Span[..10].Clear();
                return 10;
            }
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }
    }

    /// <summary>Progress&lt;T&gt; 는 알림을 다른 스레드로 미뤄 순서를 보기 어렵다. 받은 자리에서 바로 적는다.</summary>
    private sealed class SyncProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
