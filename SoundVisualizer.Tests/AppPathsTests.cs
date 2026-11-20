// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer.Tests;

public sealed class AppPathsTests
{
    private const string Base = @"C:\Tools\SoundVisualizer";
    private const string Local = @"C:\Users\someone\AppData\Local";

    [Fact]
    public void ZipBuildWritesNextToTheExecutable()
    {
        Assert.Equal(Base, AppPaths.ResolveDataDirectory(packaged: false, Base, Local));
    }

    [Fact]
    public void PackagedBuildWritesToTheUserFolder()
    {
        // MSIX 설치 폴더는 읽기 전용이라 설정을 거기에 쓰면 저장되지 않는다(#21).
        Assert.Equal(Path.Combine(Local, "SoundVisualizer"), AppPaths.ResolveDataDirectory(packaged: true, Base, Local));
    }

    [Fact]
    public void PackagedBuildFallsBackWhenTheUserFolderIsUnknown()
    {
        Assert.Equal(Base, AppPaths.ResolveDataDirectory(packaged: true, Base, ""));
    }

    [Fact]
    public void TestRunIsNotPackaged()
    {
        // 시험은 패키지 밖에서 돈다. 그래서 기본 데이터 폴더는 실행 파일 폴더다.
        Assert.False(AppPaths.IsPackaged);
        Assert.Equal(AppContext.BaseDirectory, AppPaths.DataDirectory);
    }

    [Fact]
    public void AiRecorderFallbackUsesTheSameFolderName()
    {
        Assert.Equal("SoundVisualizer", AppPaths.AppFolderName);
    }
}
