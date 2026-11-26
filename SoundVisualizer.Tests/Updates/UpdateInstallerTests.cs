// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Updates;

namespace SoundVisualizer.Tests.Updates;

public sealed class UpdateInstallerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sv-inst-" + Guid.NewGuid().ToString("N"));
    private readonly string _install;
    private readonly string _staging;
    private readonly IReadOnlyList<string> _files;

    public UpdateInstallerTests()
    {
        _install = Path.Combine(_root, "app");
        _staging = Path.Combine(_root, "staging");
        Directory.CreateDirectory(_root);

        // 지금 설치된 옛 버전: 배포 파일 + 사용자 설정(배포 파일에 없다)
        UpdatePackage.Extract(UpdatePackageTests.MakeZip(Path.Combine(_root, "old.zip"), UpdatePackageTests.ReleaseLayout("old")), _install);
        File.WriteAllText(Path.Combine(_install, "settings.json"), "{\"mine\":true}");

        _files = UpdatePackage.Extract(UpdatePackageTests.MakeZip(Path.Combine(_root, "new.zip"), UpdatePackageTests.ReleaseLayout("new")), _staging);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { _install }.Concat(parts).ToArray()));

    [Fact]
    public void 배포_파일을_새_것으로_바꾸고_설정_파일은_그대로_둔다()
    {
        Assert.True(UpdateInstaller.Apply(_staging, _install, _files, out var error), error?.ToString());

        Assert.Equal("exe new", Read("SoundVisualizer.exe"));
        Assert.Equal("model new", Read("AIModel", "yamnet.onnx"));
        Assert.Equal("{\"mine\":true}", Read("settings.json"));
        // 옛 파일은 새 버전이 시작할 때 지운다.
        Assert.Equal("exe old", Read("SoundVisualizer.exe" + UpdateInstaller.OldSuffix));
    }

    [Fact]
    public void 새로_생긴_파일과_폴더도_넣는다()
    {
        string extra = Path.Combine(_staging, "Models2", "new.onnx");
        Directory.CreateDirectory(Path.GetDirectoryName(extra)!);
        File.WriteAllText(extra, "fresh");

        Assert.True(UpdateInstaller.Apply(_staging, _install, _files.Append(@"Models2\new.onnx").ToList(), out _));

        Assert.Equal("fresh", Read("Models2", "new.onnx"));
    }

    [Fact]
    public void 실행_중인_파일처럼_이름을_바꿀_수_없는_파일이_있으면_모두_되돌린다()
    {
        // 다른 프로세스가 지우기 공유 없이 열어 둔 파일은 이름을 바꿀 수 없다(오버레이가 아직 모델을 쥐고 있는 경우).
        using (new FileStream(Path.Combine(_install, "onnxruntime.dll"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.False(UpdateInstaller.Apply(_staging, _install, _files, out var error));
            Assert.NotNull(error);
        }

        Assert.Equal("exe old", Read("SoundVisualizer.exe"));
        Assert.Equal("model old", Read("AIModel", "yamnet.onnx"));
        Assert.Equal("ort old", Read("onnxruntime.dll"));
        Assert.Empty(Directory.EnumerateFiles(_install, "*" + UpdateInstaller.OldSuffix, SearchOption.AllDirectories));
    }

    [Fact]
    public void 지난_업데이트의_옛_파일이_남아_있어도_다시_바꿀_수_있다()
    {
        File.WriteAllText(Path.Combine(_install, "SoundVisualizer.exe" + UpdateInstaller.OldSuffix), "older");

        Assert.True(UpdateInstaller.Apply(_staging, _install, _files, out _));

        Assert.Equal("exe old", Read("SoundVisualizer.exe" + UpdateInstaller.OldSuffix));
    }

    [Fact]
    public void 시작할_때_꼬리가_붙은_옛_파일만_지운다()
    {
        Assert.True(UpdateInstaller.Apply(_staging, _install, _files, out _));
        File.WriteAllText(Path.Combine(_install, "notes.old"), "user file");

        int removed = UpdateInstaller.CleanupOld(_install);

        Assert.Equal(_files.Count, removed);
        Assert.Empty(Directory.EnumerateFiles(_install, "*" + UpdateInstaller.OldSuffix, SearchOption.AllDirectories));
        Assert.True(File.Exists(Path.Combine(_install, "notes.old")));
        Assert.Equal("exe new", Read("SoundVisualizer.exe"));
    }

    [Fact]
    public void 지울_수_없는_옛_파일은_다음에_다시_지운다()
    {
        Assert.True(UpdateInstaller.Apply(_staging, _install, _files, out _));
        string locked = Path.Combine(_install, "SoundVisualizer.exe" + UpdateInstaller.OldSuffix);
        using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.Equal(_files.Count - 1, UpdateInstaller.CleanupOld(_install));
        }
        Assert.Equal(1, UpdateInstaller.CleanupOld(_install));
    }

    [Fact]
    public async Task 옛_버전이_막_끝나_잡혀_있던_파일은_잠시_뒤_다시_지운다()
    {
        Assert.True(UpdateInstaller.Apply(_staging, _install, _files, out _));
        Assert.Equal(_files.Count, UpdateInstaller.CountOld(_install));
        string held = Path.Combine(_install, "SoundVisualizer.exe" + UpdateInstaller.OldSuffix);
        var stream = new FileStream(held, FileMode.Open, FileAccess.Read, FileShare.Read);

        // 첫 시도에서는 잡힌 파일만 남고, 그 사이에 옛 버전(여기서는 이 스트림)이 놓으면 다음 시도에서 지운다.
        var cleanup = UpdateInstaller.CleanupOldWithRetryAsync(_install, new[] { TimeSpan.FromMilliseconds(300), TimeSpan.FromSeconds(5) });
        await Task.Delay(100);
        Assert.Equal(1, UpdateInstaller.CountOld(_install));
        stream.Dispose();

        Assert.Equal(0, await cleanup);
        Assert.False(File.Exists(held));
    }

    [Fact]
    public async Task 끝까지_잡혀_있으면_남은_수를_알리고_다음에_지운다()
    {
        Assert.True(UpdateInstaller.Apply(_staging, _install, _files, out _));
        string held = Path.Combine(_install, "onnxruntime.dll" + UpdateInstaller.OldSuffix);
        using (new FileStream(held, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.Equal(1, await UpdateInstaller.CleanupOldWithRetryAsync(_install, new[] { TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(20) }));
        }
        Assert.Equal(0, await UpdateInstaller.CleanupOldWithRetryAsync(_install, Array.Empty<TimeSpan>()));
    }

    [Fact]
    public void 쓸_수_있는_폴더인지_보고_흔적을_남기지_않는다()
    {
        Assert.True(UpdateInstaller.CanWrite(_install));
        Assert.False(UpdateInstaller.CanWrite(Path.Combine(_root, "no-such-folder")));
        Assert.DoesNotContain(Directory.EnumerateFiles(_install), f => Path.GetFileName(f).StartsWith(".svupdate-probe", StringComparison.Ordinal));
    }
}
