// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer.Tests.Updates;

/// <summary>업데이트 확인 설정(#36)의 저장·불러오기·초기화. AppSettings 는 정적이라 다른 설정 시험과 한 줄로 돈다.</summary>
[Collection(AppSettingsCollection.Name)]
public sealed class UpdateSettingsTests : IDisposable
{
    private readonly string _originalPath = AppSettings.SettingsFilePath;
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"sv-settings-{Guid.NewGuid():N}.json");

    public UpdateSettingsTests()
    {
        AppSettings.SettingsFilePath = _path;
        AppSettings.ResetToDefaults();
        AppSettings.LastUpdateCheck = null;
    }

    public void Dispose()
    {
        AppSettings.Flush();
        AppSettings.ResetToDefaults();
        AppSettings.LastUpdateCheck = null;
        AppSettings.SettingsFilePath = _originalPath;
        if (File.Exists(_path)) File.Delete(_path);
    }

    [Fact]
    public void 기본은_켜짐이다()
    {
        Assert.True(AppSettings.DefaultCheckUpdatesOnStart);
        Assert.True(AppSettings.CheckUpdatesOnStart);
    }

    [Fact]
    public void 끈_값과_마지막_확인_때를_저장하고_다시_읽는다()
    {
        var checkedAt = new DateTimeOffset(2026, 11, 26, 13, 5, 0, TimeSpan.FromHours(9));
        AppSettings.CheckUpdatesOnStart = false;
        AppSettings.LastUpdateCheck = checkedAt;
        AppSettings.SaveNow();

        AppSettings.CheckUpdatesOnStart = true;
        AppSettings.LastUpdateCheck = null;
        AppSettings.Load();

        Assert.False(AppSettings.CheckUpdatesOnStart);
        Assert.Equal(checkedAt, AppSettings.LastUpdateCheck);
    }

    [Fact]
    public void 이_기능_전의_설정_파일은_켜짐과_확인한_적_없음으로_읽는다()
    {
        File.WriteAllText(_path, "{\"VisualMode\":1,\"AutoMinimizeOnLaunch\":true}");
        AppSettings.CheckUpdatesOnStart = false;
        AppSettings.LastUpdateCheck = DateTimeOffset.Now;

        AppSettings.Load();

        Assert.True(AppSettings.CheckUpdatesOnStart);
        Assert.Null(AppSettings.LastUpdateCheck);
    }

    [Fact]
    public void 전체_초기화는_켜짐으로_되돌리고_마지막_확인_때는_남긴다()
    {
        var checkedAt = DateTimeOffset.Now;
        AppSettings.CheckUpdatesOnStart = false;
        AppSettings.LastUpdateCheck = checkedAt;

        AppSettings.ResetToDefaults();

        Assert.True(AppSettings.CheckUpdatesOnStart);
        Assert.Equal(checkedAt, AppSettings.LastUpdateCheck);
    }
}
