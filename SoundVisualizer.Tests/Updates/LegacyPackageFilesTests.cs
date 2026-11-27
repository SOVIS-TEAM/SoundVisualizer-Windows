// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Ai;
using SoundVisualizer.Updates;

namespace SoundVisualizer.Tests.Updates;

/// <summary>옛 zip 판(2.3.2 이하)에서 포터블 판으로 업데이트한 뒤 남은 파일 지우기(#58).</summary>
public class LegacyPackageFilesTests
{
    // 2.3.2 zip(SoundVisualizer_v2.3.2.zip)에서 실행 파일과 라이선스 글을 뺀 나머지. 포터블 판은 이것을 모두 실행 파일 안에 넣었다.
    private static readonly string[] Shipped232SideFiles =
    {
        @"AIModel\silero_vad.onnx", @"AIModel\yamnet.data", @"AIModel\yamnet.onnx", @"AIModel\yamnet_class_map.csv",
        "D3DCompiler_47_cor3.dll", "PenImc_cor3.dll", "PresentationNative_cor3.dll", "onnxruntime.dll",
        "onnxruntime_providers_shared.dll", "vcruntime140_cor3.dll", "wpfgfx_cor3.dll",
    };

    [Fact]
    public void 지울_이름은_2_3_2_zip_이_실행_파일_옆에_두던_파일과_같다()
    {
        Assert.Equal(Shipped232SideFiles.OrderBy(n => n, StringComparer.Ordinal), LegacyPackageFiles.Names.OrderBy(n => n, StringComparer.Ordinal));
        // 모델 폴더의 이름은 포터블 판이 실행 파일에 넣은 모델과 같다.
        var models = LegacyPackageFiles.Names.Where(n => n.StartsWith(@"AIModel\", StringComparison.Ordinal)).Select(n => n[@"AIModel\".Length..]);
        Assert.Equal(
            BundledModels.ModelFiles.Select(f => f.Name).Append(BundledModels.ClassMapName).OrderBy(n => n, StringComparer.Ordinal),
            models.OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void 정해진_이름만_지우고_설정과_다른_파일은_그대로_둔다()
    {
        using var dir = new TempDirectory();
        foreach (string name in LegacyPackageFiles.Names) Write(dir.Path, name);
        string[] keep = { "settings.json", "SoundVisualizer.exe", "LICENSE", "other.dll", "onnxruntime.dll.bak", @"AIModel\my-note.txt", @"SoundVisualizer-AI\record.csv" };
        foreach (string name in keep) Write(dir.Path, name);

        int failed = LegacyPackageFiles.Remove(dir.Path);

        Assert.Equal(0, failed);
        Assert.Equal(0, LegacyPackageFiles.Remaining(dir.Path));
        foreach (string name in LegacyPackageFiles.Names) Assert.False(File.Exists(Path.Combine(dir.Path, name)), name);
        foreach (string name in keep) Assert.True(File.Exists(Path.Combine(dir.Path, name)), name);
        Assert.True(Directory.Exists(Path.Combine(dir.Path, "AIModel"))); // 다른 파일이 있어 폴더는 남긴다
    }

    [Fact]
    public void 모델_폴더는_비면_지운다()
    {
        using var dir = new TempDirectory();
        foreach (string name in LegacyPackageFiles.Names) Write(dir.Path, name);

        Assert.Equal(0, LegacyPackageFiles.Remove(dir.Path));

        Assert.False(Directory.Exists(Path.Combine(dir.Path, "AIModel")));
    }

    [Fact]
    public void 지울_것이_없으면_아무것도_하지_않는다()
    {
        using var dir = new TempDirectory();
        Write(dir.Path, "settings.json");

        Assert.Equal(0, LegacyPackageFiles.Remaining(dir.Path));
        Assert.Equal(0, LegacyPackageFiles.Remove(dir.Path));
        Assert.True(File.Exists(Path.Combine(dir.Path, "settings.json")));
    }

    [Fact]
    public void 잡혀_있는_파일은_남기고_센_뒤_다음에_지운다()
    {
        using var dir = new TempDirectory();
        foreach (string name in LegacyPackageFiles.Names) Write(dir.Path, name);
        string locked = Path.Combine(dir.Path, "wpfgfx_cor3.dll");

        using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.Equal(1, LegacyPackageFiles.Remove(dir.Path));
            Assert.True(File.Exists(locked));
            Assert.Equal(1, LegacyPackageFiles.Remaining(dir.Path));
        }

        Assert.Equal(0, LegacyPackageFiles.Remove(dir.Path));
        Assert.False(File.Exists(locked));
    }

    private static void Write(string root, string relative)
    {
        string path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, relative);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sv-legacy-" + Guid.NewGuid().ToString("N"));

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
