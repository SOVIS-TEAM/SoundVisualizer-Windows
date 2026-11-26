// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Updates;

namespace SoundVisualizer.Tests.Updates;

/// <summary>업데이트 상태에서 어떤 버튼과 안내를 보일지(#36).</summary>
public class UpdateViewTests
{
    private static readonly Version Current = new(2, 3, 0);

    private static ReleaseInfo Release(string version = "2.4", bool asset = true) => new(
        UpdateVersion.Normalize(Version.Parse(version)),
        "SoundVisualizer_v" + version,
        new Uri("https://github.com/SOVIS-Team/SoundVisualizer-Windows/releases/tag/SoundVisualizer_v" + version),
        asset ? new ReleaseAsset("SoundVisualizer_v" + version + ".zip", new Uri("https://github.com/x/y.zip"), 10, new string('a', 64)) : null);

    private static UpdateSnapshot S(UpdatePhase phase, UpdateProblem problem = UpdateProblem.None, ReleaseInfo? release = null,
        UpdateChannel channel = UpdateChannel.Portable, bool busy = false, bool dismissed = false, int percent = 0) =>
        new(channel, phase, problem, release, Current, busy, dismissed, percent);

    [Fact]
    public void 받을_zip_이_있는_새_버전이면_지금_업데이트를_보인다()
    {
        var s = S(UpdatePhase.Available, release: Release());
        Assert.True(UpdateView.CanInstall(s));
        Assert.False(UpdateView.ShowsDownloadPage(s));
        Assert.True(UpdateView.ShowsOnHome(s));
        Assert.Equal(new[] { "update.available" }, UpdateView.Status(s).Select(t => t.Key));
        Assert.Equal("2.4", UpdateView.Status(s)[0].Args[0]);
    }

    [Fact]
    public void 받을_zip_이_없는_새_버전이면_받기_페이지로_보낸다()
    {
        var s = S(UpdatePhase.Available, release: Release(asset: false));
        Assert.False(UpdateView.CanInstall(s));
        Assert.True(UpdateView.ShowsDownloadPage(s));
        Assert.True(UpdateView.ShowsOnHome(s));
        Assert.Equal(new[] { "update.available", "update.manual" }, UpdateView.Status(s).Select(t => t.Key));
    }

    [Fact]
    public void 검증에_실패하면_다시_해_보거나_받기_페이지에서_받을_수_있다()
    {
        // 릴리스의 실행 파일 버전이 태그와 다르면 다시 받아도 같다. 그래도 막다른 화면이 되지 않게 페이지를 함께 보인다.
        var s = S(UpdatePhase.Failed, UpdateProblem.Verify, Release());
        Assert.True(UpdateView.CanInstall(s));
        Assert.True(UpdateView.ShowsDownloadPage(s));
    }

    [Fact]
    public void 이미_찾은_새_버전은_수동_확인이_실패해도_받을_수_있다()
    {
        var s = S(UpdatePhase.Failed, UpdateProblem.Check, Release());
        Assert.True(UpdateView.CanInstall(s));
        Assert.True(UpdateView.ShowsDownloadPage(s));
        Assert.False(UpdateView.ShowsOnHome(s)); // 확인 실패는 도움말에만
    }

    [Fact]
    public void 새_버전을_모를_때의_확인_실패는_다시_받기를_보이지_않는다()
    {
        var s = S(UpdatePhase.Failed, UpdateProblem.Check, release: null);
        Assert.False(UpdateView.CanInstall(s));
        Assert.True(UpdateView.ShowsDownloadPage(s));
    }

    [Theory]
    [InlineData(UpdateProblem.Apply)]
    [InlineData(UpdateProblem.ReadOnly)]
    public void 바꾸지_못했거나_쓸_수_없는_폴더면_받기_페이지만_보인다(UpdateProblem problem)
    {
        var s = S(UpdatePhase.Failed, problem, Release());
        Assert.False(UpdateView.CanInstall(s));
        Assert.True(UpdateView.ShowsDownloadPage(s));
        Assert.True(UpdateView.ShowsOnHome(s));
    }

    [Fact]
    public void 다_되돌리지_못했으면_다시_열라고만_한다()
    {
        var s = S(UpdatePhase.Failed, UpdateProblem.Restart, Release());
        Assert.False(UpdateView.CanInstall(s));
        Assert.False(UpdateView.ShowsDownloadPage(s));
        Assert.Equal(new[] { "update.failed_restart" }, UpdateView.Status(s).Select(t => t.Key));
    }

    [Theory]
    [InlineData(UpdatePhase.Downloading)]
    [InlineData(UpdatePhase.Installing)]
    [InlineData(UpdatePhase.Checking)]
    public void 일하는_동안에는_버튼을_막는다(UpdatePhase phase)
    {
        var s = S(phase, release: Release(), busy: true);
        Assert.False(UpdateView.CanInstall(s));
        Assert.False(UpdateView.ShowsDownloadPage(s));
    }

    [Fact]
    public void 바꾸는_동안에만_끝내기를_막는다()
    {
        Assert.True(UpdateView.BlocksExit(S(UpdatePhase.Installing, busy: true)));
        foreach (var phase in Enum.GetValues<UpdatePhase>().Where(p => p != UpdatePhase.Installing))
        {
            Assert.False(UpdateView.BlocksExit(S(phase)));
        }
    }

    [Fact]
    public void 같거나_낮은_버전은_받지_않는다()
    {
        Assert.False(UpdateView.CanInstall(S(UpdatePhase.Available, release: Release("2.3"))));
        Assert.False(UpdateView.CanInstall(S(UpdatePhase.Failed, UpdateProblem.Download, Release("2.2"))));
    }

    [Theory]
    [InlineData(UpdateChannel.Store)]
    [InlineData(UpdateChannel.Chocolatey)]
    public void 스토어_판과_Chocolatey_판은_아무것도_보이지_않는다(UpdateChannel channel)
    {
        var s = S(UpdatePhase.Available, release: Release(), channel: channel);
        Assert.False(UpdateView.CanInstall(s));
        Assert.False(UpdateView.ShowsDownloadPage(s));
        Assert.False(UpdateView.ShowsOnHome(s));
    }

    [Fact]
    public void 닫은_홈_안내는_다시_보이지_않는다()
    {
        Assert.False(UpdateView.ShowsOnHome(S(UpdatePhase.Available, release: Release(), dismissed: true)));
    }

    [Theory]
    [InlineData(UpdatePhase.Idle)]
    [InlineData(UpdatePhase.Latest)]
    [InlineData(UpdatePhase.Checking)]
    public void 최신이거나_확인_중이면_홈에_보이지_않는다(UpdatePhase phase)
    {
        Assert.False(UpdateView.ShowsOnHome(S(phase)));
    }

    [Fact]
    public void 진행률과_바뀐_버전을_글에_넣는다()
    {
        Assert.Equal(42, UpdateView.Status(S(UpdatePhase.Downloading, busy: true, percent: 42))[0].Args[0]);
        var done = UpdateView.Status(S(UpdatePhase.Updated));
        Assert.Equal("update.done", done[0].Key);
        Assert.Equal("2.3", done[0].Args[0]);
    }

    [Fact]
    public void 모든_상태의_글_키는_문자열_표에_있다()
    {
        var keys = SoundVisualizer.Localization.StringTables.Get("en")!.Entries.Keys.ToHashSet();
        foreach (var phase in Enum.GetValues<UpdatePhase>())
        foreach (var problem in Enum.GetValues<UpdateProblem>())
        foreach (var release in new[] { null, Release(), Release(asset: false) })
        foreach (var text in UpdateView.Status(S(phase, problem, release)))
        {
            Assert.Contains(text.Key, keys);
        }
    }
}
