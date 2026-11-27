// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SoundVisualizer.Ai;

namespace SoundVisualizer.Tests.Ai;

/// <summary>
/// 포터블 판의 모델 읽기(#58). 실행 파일 안의 리소스는 시험용 모델 폴더(Models)의 파일을 여는 함수로 흉내 낸다.
/// </summary>
public class BundledModelsTests
{
    [Fact]
    public void 실행_파일_안에_모델이_없으면_옆_AIModel_폴더를_쓴다()
    {
        using var root = new TempDirectory();
        string baseDirectory = Path.Combine(root.Path, "app");
        string extractRoot = Path.Combine(root.Path, "temp");

        Assert.False(BundledModels.IsBundled(_ => null));
        string directory = BundledModels.ResolveDirectory(baseDirectory, _ => null, extractRoot);

        Assert.Equal(Path.Combine(baseDirectory, "AIModel"), directory);
        Assert.False(Directory.Exists(extractRoot)); // 아무것도 꺼내지 않는다
    }

    [Fact]
    public void 실행_파일_안의_모델을_임시_폴더에_꺼내고_배포한_파일과_같은지_확인한다()
    {
        using var root = new TempDirectory();
        var resources = new FakeResources();

        Assert.True(BundledModels.IsBundled(resources.Open));
        string directory = BundledModels.ResolveDirectory(Path.Combine(root.Path, "app"), resources.Open, root.Path);

        Assert.Equal(Path.Combine(root.Path, "AIModel-" + BundledModels.ContentKey()), directory);
        ModelFileIntegrity.Verify(directory, BundledModels.ModelFiles);
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        // 소리 목록은 실행 파일에서 바로 읽으므로 꺼내지 않는다.
        Assert.False(File.Exists(Path.Combine(directory, BundledModels.ClassMapName)));
    }

    [Fact]
    public void 이미_맞게_꺼낸_파일은_다시_쓰지_않는다()
    {
        using var root = new TempDirectory();
        var resources = new FakeResources();
        string first = BundledModels.ResolveDirectory(Path.Combine(root.Path, "app"), resources.Open, root.Path);
        var written = BundledModels.ModelFiles.ToDictionary(f => f.Name, f => File.GetLastWriteTimeUtc(Path.Combine(first, f.Name)));
        resources.Opened.Clear();

        string second = BundledModels.ResolveDirectory(Path.Combine(root.Path, "app"), resources.Open, root.Path);

        Assert.Equal(first, second);
        Assert.Equal(new[] { BundledModels.ResourcePrefix + BundledModels.ClassMapName }, resources.Opened); // 모델 리소스는 열지 않았다
        foreach (var (name, time) in written) Assert.Equal(time, File.GetLastWriteTimeUtc(Path.Combine(second, name)));
    }

    [Fact]
    public void 꺼내_둔_파일이_깨졌으면_다시_꺼낸다()
    {
        using var root = new TempDirectory();
        var resources = new FakeResources();
        string directory = BundledModels.ResolveDirectory(Path.Combine(root.Path, "app"), resources.Open, root.Path);
        string data = Path.Combine(directory, "yamnet.data");
        using (var stream = new FileStream(data, FileMode.Open, FileAccess.Write))
        {
            stream.Position = 1000;
            stream.WriteByte(0x5A); // 크기는 같고 내용만 다르다
        }
        Assert.False(ModelFileIntegrity.IsValid(data, YamnetModelFiles.Data));

        BundledModels.ResolveDirectory(Path.Combine(root.Path, "app"), resources.Open, root.Path);

        ModelFileIntegrity.Verify(directory, BundledModels.ModelFiles);
    }

    [Fact]
    public void 실행_파일_안의_모델이_배포한_파일과_다르면_예외이고_제_이름으로_남기지_않는다()
    {
        using var root = new TempDirectory();
        var resources = new FakeResources { Replace = ("yamnet.onnx", new byte[] { 1, 2, 3 }) };

        Assert.Throws<InvalidDataException>(() => BundledModels.ResolveDirectory(Path.Combine(root.Path, "app"), resources.Open, root.Path));

        string directory = Path.Combine(root.Path, "AIModel-" + BundledModels.ContentKey());
        Assert.False(File.Exists(Path.Combine(directory, "yamnet.onnx")));
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    }

    [Fact]
    public void 실행_파일_안에_모델_하나가_빠졌으면_FileNotFoundException()
    {
        using var root = new TempDirectory();
        var resources = new FakeResources { Missing = "silero_vad.onnx" };

        Assert.Throws<FileNotFoundException>(() => BundledModels.ResolveDirectory(Path.Combine(root.Path, "app"), resources.Open, root.Path));
    }

    [Fact]
    public void 소리_목록은_실행_파일_안의_것을_먼저_읽고_없으면_옆_폴더에서_읽는다()
    {
        using var root = new TempDirectory();
        var bundled = BundledModels.LoadClassMap(Path.Combine(root.Path, "app-without-models"), new FakeResources().Open);
        Assert.Equal(521, bundled.Count);

        string baseDirectory = Path.Combine(root.Path, "app");
        Directory.CreateDirectory(Path.Combine(baseDirectory, "AIModel"));
        File.Copy(AiTestData.ClassMapPath, Path.Combine(baseDirectory, "AIModel", BundledModels.ClassMapName));
        var beside = BundledModels.LoadClassMap(baseDirectory, _ => null);
        Assert.Equal(bundled, beside);
    }

    [Fact]
    public void 모델이_바뀌면_다른_폴더에_꺼내도록_이름에_모델_해시가_들어간다()
    {
        Assert.Matches(new Regex("^[0-9a-f]{12}$"), BundledModels.ContentKey());
        Assert.Equal(BundledModels.ContentKey(), BundledModels.ContentKey());
        Assert.Equal(new[] { "yamnet.onnx", "yamnet.data", "silero_vad.onnx" }, BundledModels.ModelFiles.Select(f => f.Name));
    }

    [Fact]
    public void 앱_프로젝트는_포터블_빌드에서_모델_네_개를_같은_리소스_이름으로_넣는다()
    {
        // 리소스 이름(LogicalName)이 BundledModels 가 찾는 이름과 어긋나면 포터블 판은 AI 와 소리 탭 없이 켜진다.
        var project = XDocument.Load(Path.Combine(RepositoryPaths.Root, "SoundVisualizer", "SoundVisualizer.csproj"));
        var embedded = project.Descendants("EmbeddedResource")
            .Where(e => ((string?)e.Parent?.Attribute("Condition") ?? "").Contains("'$(Portable)' == 'true'"))
            .ToList();
        var single = Assert.Single(embedded);
        Assert.Equal("AIModel/%(Filename)%(Extension)", (string?)single.Attribute("LogicalName"));
        Assert.Equal("AIModel/", BundledModels.ResourcePrefix);
        var included = ((string?)single.Attribute("Include") ?? "").Split(';').Select(p => Path.GetFileName(p)).OrderBy(n => n, StringComparer.Ordinal);
        var needed = BundledModels.ModelFiles.Select(f => f.Name).Append(BundledModels.ClassMapName).OrderBy(n => n, StringComparer.Ordinal);
        Assert.Equal(needed, included);

        // 포터블 빌드는 DLL 도 실행 파일에 넣는다.
        var portableProperties = project.Descendants("PropertyGroup")
            .Where(g => ((string?)g.Attribute("Condition") ?? "").Contains("'$(Portable)' == 'true'"));
        Assert.Contains(portableProperties.Elements("IncludeNativeLibrariesForSelfExtract"), e => e.Value == "true");
    }

    /// <summary>"AIModel/이름" 리소스를 시험용 모델 폴더의 파일로 연다. 연 이름을 적어 두고, 하나를 바꾸거나 뺄 수 있다.</summary>
    private sealed class FakeResources
    {
        public List<string> Opened { get; } = new();
        public (string Name, byte[] Bytes)? Replace { get; init; }
        public string? Missing { get; init; }

        public Stream? Open(string resourceName)
        {
            Opened.Add(resourceName);
            if (!resourceName.StartsWith(BundledModels.ResourcePrefix, StringComparison.Ordinal)) return null;
            string name = resourceName[BundledModels.ResourcePrefix.Length..];
            if (name == Missing) return null;
            if (Replace is { } r && r.Name == name) return new MemoryStream(r.Bytes);
            string path = Path.Combine(AiTestData.ModelDirectory, name);
            return File.Exists(path) ? File.OpenRead(path) : null;
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sv-bundled-" + Guid.NewGuid().ToString("N"));

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
