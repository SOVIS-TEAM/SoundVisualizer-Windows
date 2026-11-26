// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Text.Json.Nodes;
using SoundVisualizer.Updates;

namespace SoundVisualizer.Tests.Updates;

/// <summary>GitHub "최신 릴리스" 응답 읽기. 예시는 v2.2 의 실제 응답에서 쓰는 칸만 남긴 것이다.</summary>
public class ReleaseParserTests
{
    internal const string Digest = "30e4ab9379bfcc82952eea86e147fd130fe38eb99fe7986e3b14766880bf3a77";

    internal static JsonObject Sample() => new()
    {
        ["tag_name"] = "SoundVisualizer_v2.2",
        ["name"] = "SoundVisualizer_v2.2",
        ["html_url"] = "https://github.com/SOVIS-TEAM/SoundVisualizer-Windows/releases/tag/SoundVisualizer_v2.2",
        ["draft"] = false,
        ["prerelease"] = false,
        ["assets"] = new JsonArray
        {
            new JsonObject
            {
                ["name"] = "SoundVisualizer_v2.2.zip",
                ["size"] = 103995026,
                ["digest"] = "sha256:" + Digest,
                ["content_type"] = "application/zip",
                ["browser_download_url"] = "https://github.com/SOVIS-TEAM/SoundVisualizer-Windows/releases/download/SoundVisualizer_v2.2/SoundVisualizer_v2.2.zip",
            },
        },
    };

    private static JsonObject Asset(JsonObject release) => (JsonObject)release["assets"]![0]!;

    [Fact]
    public void 버전_페이지_zip_크기_SHA256_을_읽는다()
    {
        var release = ReleaseParser.Parse(Sample().ToJsonString());

        Assert.NotNull(release);
        Assert.Equal(new Version(2, 2, 0), release.Version);
        Assert.Equal("SoundVisualizer_v2.2", release.Tag);
        Assert.Equal("github.com", release.PageUrl.Host);
        Assert.NotNull(release.Asset);
        Assert.Equal("SoundVisualizer_v2.2.zip", release.Asset.Name);
        Assert.Equal(103995026, release.Asset.Size);
        Assert.Equal(Digest, release.Asset.Sha256Hex);
        Assert.Equal("https", release.Asset.DownloadUrl.Scheme);
    }

    [Theory]
    [InlineData("draft")]
    [InlineData("prerelease")]
    public void 초안과_시험판은_새_버전으로_치지_않는다(string flag)
    {
        var json = Sample();
        json[flag] = true;
        Assert.Null(ReleaseParser.Parse(json.ToJsonString()));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"tag_name\":\"latest\",\"html_url\":\"https://github.com/x\"}")]
    public void 형식이_다른_응답은_읽지_않는다(string json)
    {
        Assert.Null(ReleaseParser.Parse(json));
    }

    [Theory]
    [InlineData("http://github.com/SOVIS-Team/SoundVisualizer-Windows/releases/tag/v2.2")]
    [InlineData("https://example.com/releases/tag/v2.2")]
    public void 릴리스_페이지는_https_github_com_이어야_한다(string url)
    {
        var json = Sample();
        json["html_url"] = url;
        Assert.Null(ReleaseParser.Parse(json.ToJsonString()));
    }

    [Fact]
    public void SHA256_이_없으면_zip_을_받지_않고_페이지만_안내한다()
    {
        var json = Sample();
        Asset(json).Remove("digest");
        var release = ReleaseParser.Parse(json.ToJsonString());
        Assert.NotNull(release);
        Assert.Null(release.Asset);
    }

    [Theory]
    [InlineData("digest", "sha1:0123")]
    [InlineData("digest", "sha256:xyz")]
    [InlineData("browser_download_url", "https://evil.example/SoundVisualizer_v2.2.zip")]
    [InlineData("browser_download_url", "http://github.com/SOVIS-Team/SoundVisualizer-Windows/releases/download/SoundVisualizer_v2.2/SoundVisualizer_v2.2.zip")]
    [InlineData("name", "SoundVisualizer_v2.2.exe")]
    [InlineData("name", "other.zip")]
    public void 믿을_수_없는_자산은_쓰지_않는다(string field, string value)
    {
        var json = Sample();
        Asset(json)[field] = value;
        var release = ReleaseParser.Parse(json.ToJsonString());
        Assert.NotNull(release);
        Assert.Null(release.Asset);
    }

    [Fact]
    public void 크기가_없거나_0_이면_쓰지_않는다()
    {
        var json = Sample();
        Asset(json)["size"] = 0;
        Assert.Null(ReleaseParser.Parse(json.ToJsonString())!.Asset);
    }

    [Fact]
    public void 다른_파일이_먼저_있어도_zip_을_찾는다()
    {
        var json = Sample();
        var assets = (JsonArray)json["assets"]!;
        assets.Insert(0, new JsonObject { ["name"] = "notes.txt", ["size"] = 10, ["browser_download_url"] = "https://github.com/a/b/notes.txt" });
        Assert.Equal("SoundVisualizer_v2.2.zip", ReleaseParser.Parse(json.ToJsonString())!.Asset!.Name);
    }
}
