// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer.Tests.Ui;

/// <summary>
/// 앱 아이콘(#42). 256px 하나만 있으면 Windows 가 탐색기·작업 표시줄·알림 영역에서 크게 줄여 그려 가는 선이 사라진다.
/// 작은 크기를 따로 그려 넣었는지 아이콘 파일 머리를 읽어 본다.
/// </summary>
public class AppIconTests
{
    private static List<int> IconSizes(string path)
    {
        byte[] data = File.ReadAllBytes(path);
        Assert.Equal(1, BitConverter.ToUInt16(data, 2)); // 아이콘
        int count = BitConverter.ToUInt16(data, 4);
        var sizes = new List<int>();
        for (int i = 0; i < count; i++)
        {
            int width = data[6 + 16 * i];
            sizes.Add(width == 0 ? 256 : width);
        }
        return sizes;
    }

    [Fact]
    public void 아이콘_파일에는_작은_크기가_따로_들어_있다()
    {
        var sizes = IconSizes(Path.Combine(RepositoryPaths.Root, "SoundVisualizer", "SoundVisualizer.ico"));
        foreach (int size in new[] { 16, 20, 24, 32, 48, 256 })
        {
            Assert.Contains(size, sizes);
        }
    }

    [Fact]
    public void 알림_영역은_큰_그림을_줄이지_않고_그_크기_그림을_꺼낸다()
    {
        string tray = AppSource.Read("TrayIcon.cs");
        Assert.Contains("ExtractIcon(exe, 0, Forms.SystemInformation.SmallIconSize.Width)", tray);
        Assert.DoesNotContain("ExtractIcon(exe, 0, 256)", tray);
    }
}
