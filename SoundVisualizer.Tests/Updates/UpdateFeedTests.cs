// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Updates;

namespace SoundVisualizer.Tests.Updates;

public class UpdateFeedTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("https://example.com/latest.json")]
    [InlineData("http://192.168.0.10:8000/latest.json")]
    [InlineData("file:///C:/feed/latest.json")]
    [InlineData("ftp://127.0.0.1/latest.json")]
    public void 이_PC_밖이거나_웹_주소가_아니면_GitHub_에_묻는다(string? value)
    {
        var feed = UpdateFeed.Resolve(value);
        Assert.Same(UpdateFeed.GitHub, feed);
        Assert.Equal(UpdateClient.LatestReleaseApiUrl, feed.ApiUrl);
    }

    [Theory]
    [InlineData("http://127.0.0.1:8765/latest.json")]
    [InlineData("http://localhost:8765/latest.json")]
    public void 이_PC_의_주소면_거기에_묻고_같은_곳만_믿는다(string value)
    {
        var feed = UpdateFeed.Resolve(value);
        var origin = new Uri(value);

        Assert.Equal(origin, new Uri(feed.ApiUrl));
        Assert.True(feed.IsTrusted(new Uri(origin, "/SoundVisualizer_v9.9.zip")));
        Assert.False(feed.IsTrusted(new Uri("http://" + origin.Host + ":9999/x.zip")));
        Assert.False(feed.IsTrusted(new Uri("https://github.com/SOVIS-Team/SoundVisualizer-Windows/releases/latest")));
    }

    [Theory]
    [InlineData("https://github.com/SOVIS-Team/SoundVisualizer-Windows/releases/latest", true)]
    [InlineData("https://GitHub.com/x", true)]
    [InlineData("http://github.com/x", false)]
    [InlineData("https://github.com.evil.example/x", false)]
    [InlineData("https://objects.githubusercontent.com/x", false)]
    public void 기본은_https_github_com_만_믿는다(string url, bool trusted)
    {
        Assert.Equal(trusted, UpdateFeed.GitHub.IsTrusted(new Uri(url)));
    }

    [Fact]
    public async Task 믿지_않는_주소의_자산은_내려받지_않는다()
    {
        var client = new UpdateClient(new HttpClient(new ThrowingHandler()), new Version(2, 2, 0));
        var asset = new ReleaseAsset("SoundVisualizer_v2.3.zip", new Uri("https://evil.example/SoundVisualizer_v2.3.zip"), 10, new string('0', 64));

        await Assert.ThrowsAsync<UpdateVerificationException>(() =>
            client.DownloadAsync(asset, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".zip"), null, CancellationToken.None));
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("요청을 보내면 안 됩니다: " + request.RequestUri);
    }
}
