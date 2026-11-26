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
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private string P(params string[] parts) => Path.Combine(new[] { _install }.Concat(parts).ToArray());

    private string Read(params string[] parts) => File.ReadAllText(P(parts));

    private int Leftovers() => Directory.EnumerateFiles(_install, "*.svupdate-*", SearchOption.AllDirectories).Count();

    private bool Journal() => File.Exists(P(UpdateInstaller.JournalName));

    private void AssertOldVersion()
    {
        Assert.Equal("exe old", Read("SoundVisualizer.exe"));
        Assert.Equal("model old", Read("AIModel", "yamnet.onnx"));
        Assert.Equal("ort old", Read("onnxruntime.dll"));
        Assert.Equal("{\"mine\":true}", Read("settings.json"));
    }

    [Fact]
    public void 배포_파일을_새_것으로_바꾸고_설정_파일은_그대로_둔다()
    {
        Assert.True(UpdateInstaller.Apply(_staging, _install, _files, out var error), error?.ToString());

        Assert.Equal("exe new", Read("SoundVisualizer.exe"));
        Assert.Equal("model new", Read("AIModel", "yamnet.onnx"));
        Assert.Equal("{\"mine\":true}", Read("settings.json"));
        // 옛 파일은 새 버전이 켜질 때 지운다. 그때까지 기록이 남는다.
        Assert.Equal("exe old", Read("SoundVisualizer.exe" + UpdateInstaller.OldSuffix));
        Assert.True(Journal());
        Assert.Equal(_files.Count, UpdateInstaller.PendingCleanupCount(_install));
    }

    [Fact]
    public void 새_버전이_켜지면_옛_파일과_기록을_지운다()
    {
        Assert.True(UpdateInstaller.Apply(_staging, _install, _files, out _));

        Assert.Equal(UpdateInstaller.StartupOutcome.CleanupPending, UpdateInstaller.RecoverAtStartup(_install, P("SoundVisualizer.exe")));

        Assert.Equal(0, Leftovers());
        Assert.False(Journal());
        Assert.Equal("exe new", Read("SoundVisualizer.exe"));
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
    public void 이름을_바꿀_수_없는_파일이_있으면_모두_되돌리고_기록도_지운다()
    {
        // 다른 프로세스가 지우기 공유 없이 열어 둔 파일은 이름을 바꿀 수 없다(오버레이가 아직 모델을 쥐고 있는 경우).
        using (new FileStream(P("onnxruntime.dll"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.False(UpdateInstaller.Apply(_staging, _install, _files, out var error));
            Assert.NotNull(error);
        }

        AssertOldVersion();
        Assert.Equal(0, Leftovers());
        Assert.False(Journal());
    }

    [Fact]
    public void 새_버전을_띄우지_못하면_옛_파일로_되돌린다()
    {
        Assert.True(UpdateInstaller.Apply(_staging, _install, _files, out _));

        Assert.True(UpdateInstaller.Revert(_install));

        AssertOldVersion();
        Assert.Equal(0, Leftovers());
        Assert.False(Journal());
    }

    [Fact]
    public void 되돌릴_때_지울_수_없는_새_파일은_치워_두고_다음에_지운다()
    {
        Assert.True(UpdateInstaller.Apply(_staging, _install, _files, out _));
        // 읽기 전용 파일은 지울 수 없지만 이름은 바꿀 수 있다(돌고 있는 exe 와 같다).
        File.SetAttributes(P("SoundVisualizer.exe"), FileAttributes.ReadOnly);

        Assert.True(UpdateInstaller.Revert(_install));

        AssertOldVersion();
        string discard = P("SoundVisualizer.exe" + UpdateInstaller.DiscardSuffix);
        Assert.Equal("exe new", File.ReadAllText(discard));
        File.SetAttributes(discard, FileAttributes.Normal);
        Assert.Equal(UpdateInstaller.StartupOutcome.CleanupPending, UpdateInstaller.RecoverAtStartup(_install, P("SoundVisualizer.exe")));
        Assert.False(File.Exists(discard));
        Assert.False(Journal());
    }

    /// <summary>바꾸는 도중 꺼진 실행 폴더를 만든다: 앞의 <paramref name="done"/> 개는 바꿨고, 그다음 하나는 옛 파일로 옮긴 뒤 반쯤 복사했다.</summary>
    private void SimulateCrashAfter(int done)
    {
        // Apply 가 남기는 것과 같은 "바꾸는 중" 기록(UpdateJournalFormatTests 가 이 꼴을 지킨다).
        string entries = string.Join(",", _files.Select(f => $"{{\"Path\":{System.Text.Json.JsonSerializer.Serialize(f)},\"Existed\":true}}"));
        File.WriteAllText(P(UpdateInstaller.JournalName), $"{{\"State\":\"applying\",\"Entries\":[{entries}],\"Leftovers\":[]}}");
        for (int i = 0; i <= done && i < _files.Count; i++)
        {
            string target = P(_files[i]);
            File.Move(target, target + UpdateInstaller.OldSuffix);
            if (i < done) File.Copy(Path.Combine(_staging, _files[i]), target);
            else File.WriteAllText(target, "half"); // 복사하다 꺼짐
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    public void 바꾸는_도중_꺼졌으면_다음에_켤_때_옛_파일로_되돌린다(int done)
    {
        SimulateCrashAfter(done);

        var outcome = UpdateInstaller.RecoverAtStartup(_install, P("SoundVisualizer.exe"));

        Assert.NotEqual(UpdateInstaller.StartupOutcome.None, outcome);
        AssertOldVersion();
        Assert.Equal(0, Leftovers());
        Assert.False(Journal());
    }

    [Fact]
    public void 돌고_있는_exe_를_되돌렸으면_다시_열어야_한다고_알린다()
    {
        int exe = _files.ToList().IndexOf("SoundVisualizer.exe");
        SimulateCrashAfter(exe + 1);

        Assert.Equal(UpdateInstaller.StartupOutcome.RecoveredNeedsRestart, UpdateInstaller.RecoverAtStartup(_install, P("SoundVisualizer.exe")));
        AssertOldVersion();
    }

    [Fact]
    public void 기록이_없으면_꼬리가_붙은_파일도_건드리지_않는다()
    {
        // 기록 없이 남은 파일은 무엇인지 모르므로 지우지 않는다(실행 폴더를 뒤지지 않는다).
        File.WriteAllText(P("notes" + UpdateInstaller.OldSuffix), "user file");

        Assert.Equal(UpdateInstaller.StartupOutcome.None, UpdateInstaller.RecoverAtStartup(_install, P("SoundVisualizer.exe")));

        Assert.True(File.Exists(P("notes" + UpdateInstaller.OldSuffix)));
    }

    [Fact]
    public void 지우지_못한_옛_파일이_있으면_다음_업데이트를_시작하지_않는다()
    {
        Assert.True(UpdateInstaller.Apply(_staging, _install, _files, out _));
        // 첫 업데이트의 옛 exe 를 아직 쥐고 있다. 그 위에 새 백업을 만들면 섞인다.
        using (new FileStream(P("SoundVisualizer.exe" + UpdateInstaller.OldSuffix), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.False(UpdateInstaller.Apply(_staging, _install, _files, out var error));
            Assert.IsType<IOException>(error);
        }
        Assert.Equal("exe new", Read("SoundVisualizer.exe"));
        Assert.Equal("exe old", Read("SoundVisualizer.exe" + UpdateInstaller.OldSuffix));
    }

    [Fact]
    public void 다음_업데이트는_남은_정리를_마치고_시작한다()
    {
        Assert.True(UpdateInstaller.Apply(_staging, _install, _files, out _));

        Assert.True(UpdateInstaller.Apply(_staging, _install, _files, out var error), error?.ToString());

        // 첫 업데이트의 옛 파일은 지웠고, 이번 백업(첫 업데이트의 새 파일)이 남았다.
        Assert.Equal("exe new", Read("SoundVisualizer.exe" + UpdateInstaller.OldSuffix));
    }

    [Fact]
    public async Task 옛_버전이_막_끝나_잡혀_있던_파일은_잠시_뒤_다시_지운다()
    {
        Assert.True(UpdateInstaller.Apply(_staging, _install, _files, out _));
        string held = P("SoundVisualizer.exe" + UpdateInstaller.OldSuffix);
        var stream = new FileStream(held, FileMode.Open, FileAccess.Read, FileShare.Read);

        var cleanup = UpdateInstaller.CleanupWithRetryAsync(_install, new[] { TimeSpan.FromMilliseconds(300), TimeSpan.FromSeconds(5) });
        await Task.Delay(100);
        Assert.Equal(1, UpdateInstaller.PendingCleanupCount(_install));
        stream.Dispose();

        Assert.Equal(0, await cleanup);
        Assert.False(File.Exists(held));
        Assert.False(Journal());
    }

    [Fact]
    public async Task 끝까지_잡혀_있으면_기록에_남기고_다음에_지운다()
    {
        Assert.True(UpdateInstaller.Apply(_staging, _install, _files, out _));
        string held = P("onnxruntime.dll" + UpdateInstaller.OldSuffix);
        using (new FileStream(held, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.Equal(1, await UpdateInstaller.CleanupWithRetryAsync(_install, new[] { TimeSpan.FromMilliseconds(20) }));
        }
        Assert.True(Journal());
        Assert.Equal(UpdateInstaller.StartupOutcome.CleanupPending, UpdateInstaller.RecoverAtStartup(_install, null));
        Assert.False(File.Exists(held));
        Assert.False(Journal());
    }

    [Fact]
    public async Task 정리는_취소하면_멈춘다()
    {
        Assert.True(UpdateInstaller.Apply(_staging, _install, _files, out _));
        using var hold = new FileStream(P("onnxruntime.dll" + UpdateInstaller.OldSuffix), FileMode.Open, FileAccess.Read, FileShare.Read);
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            UpdateInstaller.CleanupWithRetryAsync(_install, new[] { TimeSpan.FromSeconds(30) }, cancel.Token));
    }

    [Fact]
    public void 읽을_수_없거나_폴더_밖을_가리키는_기록은_쓰지_않는다()
    {
        File.WriteAllText(P("victim.txt"), "keep");
        File.WriteAllText(P(UpdateInstaller.JournalName), "{\"State\":\"cleanup\",\"Entries\":[],\"Leftovers\":[\"..\\\\outside.txt\"]}");
        Assert.Equal(UpdateInstaller.StartupOutcome.None, UpdateInstaller.RecoverAtStartup(_install, null));

        File.WriteAllText(P(UpdateInstaller.JournalName), "not json");
        Assert.Equal(UpdateInstaller.StartupOutcome.None, UpdateInstaller.RecoverAtStartup(_install, null));
        Assert.Equal("keep", Read("victim.txt"));
    }

    [Fact]
    public void 쓸_수_있는_폴더인지_보고_흔적을_남기지_않는다()
    {
        Assert.True(UpdateInstaller.CanWrite(_install));
        Assert.False(UpdateInstaller.CanWrite(Path.Combine(_root, "no-such-folder")));
        Assert.DoesNotContain(Directory.EnumerateFiles(_install), f => Path.GetFileName(f).StartsWith(".svupdate-probe", StringComparison.Ordinal));
    }
}
