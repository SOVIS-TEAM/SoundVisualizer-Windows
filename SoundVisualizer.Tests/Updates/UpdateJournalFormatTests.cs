// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Text.Json;
using SoundVisualizer.Updates;

namespace SoundVisualizer.Tests.Updates;

/// <summary>
/// 업데이트 기록(.svupdate-journal)은 옛 버전이 쓰고 새 버전이 읽는다(#36). 다음 버전들도 이 꼴을 그대로 읽어야 하므로 꼴을 고정한다.
/// </summary>
public sealed class UpdateJournalFormatTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "sv-journal-" + Guid.NewGuid().ToString("N"));

    public UpdateJournalFormatTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void 기록_파일_이름과_꼬리는_바뀌지_않는다()
    {
        Assert.Equal(".svupdate-journal", UpdateInstaller.JournalName);
        Assert.Equal(".svupdate-old", UpdateInstaller.OldSuffix);
        Assert.Equal(".svupdate-discard", UpdateInstaller.DiscardSuffix);
    }

    [Fact]
    public void Apply_가_쓰는_기록은_정해진_꼴이다()
    {
        string staging = Path.Combine(_dir, "staging");
        string install = Path.Combine(_dir, "app");
        Directory.CreateDirectory(Path.Combine(staging, "AIModel"));
        Directory.CreateDirectory(install);
        File.WriteAllText(Path.Combine(staging, "SoundVisualizer.exe"), "new");
        File.WriteAllText(Path.Combine(staging, "AIModel", "m.onnx"), "new");
        File.WriteAllText(Path.Combine(install, "SoundVisualizer.exe"), "old");

        Assert.True(UpdateInstaller.Apply(staging, install, new[] { "SoundVisualizer.exe", @"AIModel\m.onnx" }, out _));

        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(install, UpdateInstaller.JournalName)));
        var root = json.RootElement;
        Assert.Equal("applied", root.GetProperty("State").GetString());
        var entries = root.GetProperty("Entries").EnumerateArray().ToList();
        Assert.Equal("SoundVisualizer.exe", entries[0].GetProperty("Path").GetString());
        Assert.True(entries[0].GetProperty("Existed").GetBoolean());
        Assert.Equal(@"AIModel\m.onnx", entries[1].GetProperty("Path").GetString());
        Assert.False(entries[1].GetProperty("Existed").GetBoolean());
        Assert.Equal(JsonValueKind.Array, root.GetProperty("Leftovers").ValueKind);
    }

    [Fact]
    public void 손으로_쓴_바꾸는_중_기록을_읽어_되돌린다()
    {
        // 옛 버전이 바꾸다 꺼지면 새 버전(또는 옛 버전)이 이 기록을 읽어 되돌린다.
        File.WriteAllText(Path.Combine(_dir, "SoundVisualizer.exe"), "new");
        File.WriteAllText(Path.Combine(_dir, "SoundVisualizer.exe" + UpdateInstaller.OldSuffix), "old");
        File.WriteAllText(Path.Combine(_dir, "added.dll"), "new");
        File.WriteAllText(Path.Combine(_dir, "b.dll" + UpdateInstaller.NewSuffix), "half");
        File.WriteAllText(Path.Combine(_dir, "b.dll"), "old");
        File.WriteAllText(Path.Combine(_dir, UpdateInstaller.JournalName),
            "{\"State\":\"applying\",\"Entries\":[{\"Path\":\"SoundVisualizer.exe\",\"Existed\":true},{\"Path\":\"added.dll\",\"Existed\":false},{\"Path\":\"b.dll\",\"Existed\":true}],\"Leftovers\":[]}");

        UpdateInstaller.RecoverAtStartup(_dir, null);

        Assert.Equal("old", File.ReadAllText(Path.Combine(_dir, "SoundVisualizer.exe")));
        Assert.False(File.Exists(Path.Combine(_dir, "added.dll")));
        Assert.Equal("old", File.ReadAllText(Path.Combine(_dir, "b.dll")));
        Assert.Empty(Directory.EnumerateFiles(_dir, "*.svupdate-*"));
    }

    [Fact]
    public void 꼬리와_잠금_이름도_바뀌지_않는다()
    {
        Assert.Equal(".svupdate-new", UpdateInstaller.NewSuffix);
        Assert.Equal(".svupdate-lock", UpdateInstaller.LockName);
    }

    [Fact]
    public void 손으로_쓴_정리_기록을_읽어_지운다()
    {
        File.WriteAllText(Path.Combine(_dir, "a.dll" + UpdateInstaller.OldSuffix), "x");
        File.WriteAllText(Path.Combine(_dir, UpdateInstaller.JournalName),
            "{\"State\":\"cleanup\",\"Entries\":[],\"Leftovers\":[\"a.dll.svupdate-old\"]}");

        Assert.Equal(0, UpdateInstaller.CleanupNow(_dir));
        Assert.False(File.Exists(Path.Combine(_dir, "a.dll" + UpdateInstaller.OldSuffix)));
        Assert.False(File.Exists(Path.Combine(_dir, UpdateInstaller.JournalName)));
    }
}
