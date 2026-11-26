// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Updates;

namespace SoundVisualizer.Tests.Updates;

public class UpdateVersionTests
{
    [Theory]
    [InlineData("SoundVisualizer_v2.2", "2.2.0")]
    [InlineData("SoundVisualizer_v2.1.1", "2.1.1")]
    [InlineData("v3.0", "3.0.0")]
    [InlineData("SoundVisualizer_v10.12.3", "10.12.3")]
    [InlineData(" SoundVisualizer_v2.3 ", "2.3.0")]
    public void 릴리스_태그에서_버전을_읽는다(string tag, string expected)
    {
        Assert.Equal(Version.Parse(expected), UpdateVersion.FromTag(tag));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("v1")]
    [InlineData("SoundVisualizer_v2.2-beta")]
    [InlineData("release-2.2")]
    [InlineData("SoundVisualizerv2.2")]
    public void 버전으로_끝나지_않는_태그는_읽지_않는다(string? tag)
    {
        Assert.Null(UpdateVersion.FromTag(tag));
    }

    [Theory]
    [InlineData("2.2.0+21d87a3c4f", "2.2.0")]
    [InlineData("2.2.0", "2.2.0")]
    [InlineData("2.2", "2.2.0")]
    [InlineData("2.3.0-preview", "2.3.0")]
    public void 실행_파일_버전에서_빌드_정보를_뗀다(string informational, string expected)
    {
        Assert.Equal(Version.Parse(expected), UpdateVersion.FromInformational(informational));
    }

    [Fact]
    public void 빈_실행_파일_버전은_읽지_않는다()
    {
        Assert.Null(UpdateVersion.FromInformational(null));
        Assert.Null(UpdateVersion.FromInformational("abc"));
    }

    [Theory]
    [InlineData("2.3.0", "2.2.0", true)]
    [InlineData("2.2.1", "2.2.0", true)]
    [InlineData("2.10.0", "2.9.0", true)]
    [InlineData("2.2.0", "2.2.0", false)]
    [InlineData("2.2", "2.2.0", false)]
    [InlineData("2.1.1", "2.2.0", false)]
    [InlineData("2.2.0.5", "2.2.0", false)]
    public void 새_버전인지_세_자리로_비교한다(string candidate, string current, bool newer)
    {
        Assert.Equal(newer, UpdateVersion.IsNewer(Version.Parse(candidate), Version.Parse(current)));
    }

    [Theory]
    [InlineData("2.2.0", "2.2")]
    [InlineData("2.2", "2.2")]
    [InlineData("2.1.1", "2.1.1")]
    [InlineData("3.0.0.7", "3.0")]
    public void 화면에는_릴리스_이름처럼_적는다(string version, string expected)
    {
        Assert.Equal(expected, UpdateVersion.Display(Version.Parse(version)));
    }
}
