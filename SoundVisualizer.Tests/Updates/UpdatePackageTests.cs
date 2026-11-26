// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.IO.Compression;
using System.Text;
using SoundVisualizer.Updates;

namespace SoundVisualizer.Tests.Updates;

public sealed class UpdatePackageTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "sv-pkg-" + Guid.NewGuid().ToString("N"));

    public UpdatePackageTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    /// <summary>항목 이름과 내용으로 zip 을 만든다. 이름이 '/' 로 끝나면 폴더 항목이다.</summary>
    internal static string MakeZip(string path, params (string Name, string Content)[] entries)
    {
        using var stream = File.Create(path);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        foreach (var (name, content) in entries)
        {
            var entry = archive.CreateEntry(name);
            if (name.EndsWith('/')) continue;
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.Write(content);
        }
        return path;
    }

    /// <summary>실제 배포 zip 과 같은 꼴(맨 위 exe 와 DLL, AIModel·Fonts 폴더).</summary>
    internal static (string Name, string Content)[] ReleaseLayout(string marker) => new[]
    {
        ("AIModel/", ""),
        ("AIModel/yamnet.onnx", "model " + marker),
        ("Fonts/Pretendard-LICENSE.txt", "ofl " + marker),
        ("LICENSE", "agpl " + marker),
        ("SoundVisualizer.exe", "exe " + marker),
        ("onnxruntime.dll", "ort " + marker),
    };

    [Fact]
    public void 배포_zip_을_풀고_상대_경로를_돌려준다()
    {
        string zip = MakeZip(Path.Combine(_dir, "a.zip"), ReleaseLayout("new"));
        string staging = Path.Combine(_dir, "staging");

        var files = UpdatePackage.Extract(zip, staging);

        Assert.Equal(new[] { @"AIModel\yamnet.onnx", @"Fonts\Pretendard-LICENSE.txt", "LICENSE", "SoundVisualizer.exe", "onnxruntime.dll" }, files);
        Assert.Equal("model new", File.ReadAllText(Path.Combine(staging, "AIModel", "yamnet.onnx")));
    }

    [Fact]
    public void 풀_폴더에_남은_것을_먼저_지운다()
    {
        string staging = Path.Combine(_dir, "staging");
        Directory.CreateDirectory(staging);
        File.WriteAllText(Path.Combine(staging, "stale.txt"), "x");

        UpdatePackage.Extract(MakeZip(Path.Combine(_dir, "a.zip"), ReleaseLayout("new")), staging);

        Assert.False(File.Exists(Path.Combine(staging, "stale.txt")));
    }

    [Fact]
    public void 맨_위에_실행_파일이_없으면_받지_않는다()
    {
        string zip = MakeZip(Path.Combine(_dir, "a.zip"), ("sub/SoundVisualizer.exe", "x"), ("LICENSE", "y"));
        Assert.Throws<InvalidUpdatePackageException>(() => UpdatePackage.Extract(zip, Path.Combine(_dir, "staging")));
    }

    [Theory]
    [InlineData("../evil.dll")]
    [InlineData("AIModel/../../evil.dll")]
    [InlineData("/evil.dll")]
    [InlineData("C:/Windows/evil.dll")]
    [InlineData("SoundVisualizer.exe:stream")]
    [InlineData("AIModel//x.onnx")]
    [InlineData("AIModel/./x.onnx")]
    [InlineData("trailing.")]
    public void 풀_폴더_밖이나_이상한_이름은_받지_않는다(string name)
    {
        string zip = MakeZip(Path.Combine(_dir, "a.zip"), ("SoundVisualizer.exe", "x"), (name, "evil"));
        Assert.Throws<InvalidUpdatePackageException>(() => UpdatePackage.Extract(zip, Path.Combine(_dir, "staging")));
        Assert.False(File.Exists(Path.Combine(_dir, "evil.dll")));
    }

    [Fact]
    public void 대소문자만_다른_같은_이름이_두_번_있으면_받지_않는다()
    {
        string zip = MakeZip(Path.Combine(_dir, "a.zip"), ("SoundVisualizer.exe", "x"), ("LICENSE", "a"), ("license", "b"));
        Assert.Throws<InvalidUpdatePackageException>(() => UpdatePackage.Extract(zip, Path.Combine(_dir, "staging")));
    }
}
