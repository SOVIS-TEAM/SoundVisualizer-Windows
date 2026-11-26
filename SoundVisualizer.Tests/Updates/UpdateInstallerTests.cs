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

    /// <summary>Apply 가 바꾸기 전에 남기는 것과 같은 "바꾸는 중" 기록(UpdateJournalFormatTests 가 이 꼴을 지킨다).</summary>
    private void WriteApplyingJournal()
    {
        string entries = string.Join(",", _files.Select(f => $"{{\"Path\":{System.Text.Json.JsonSerializer.Serialize(f)},\"Existed\":true}}"));
        File.WriteAllText(P(UpdateInstaller.JournalName), $"{{\"State\":\"applying\",\"Entries\":[{entries}],\"Leftovers\":[]}}");
    }

    /// <summary>1단계(새 파일을 옆에 복사)에서 꺼졌다: 앞의 <paramref name="copied"/> 개는 복사했고 그다음 하나는 반쯤 복사했다.</summary>
    private void SimulateCrashWhileCopying(int copied)
    {
        WriteApplyingJournal();
        for (int i = 0; i <= copied && i < _files.Count; i++)
        {
            string fresh = P(_files[i]) + UpdateInstaller.NewSuffix;
            if (i < copied) File.Copy(Path.Combine(_staging, _files[i]), fresh);
            else File.WriteAllText(fresh, "half");
        }
    }

    /// <summary>2단계(이름 바꿔 끼우기)에서 꺼졌다: 앞의 <paramref name="swapped"/> 개는 바꿨고, <paramref name="midSwap"/> 이면 그다음 하나는 옛 파일로만 옮겼다.</summary>
    private void SimulateCrashWhileSwapping(int swapped, bool midSwap)
    {
        WriteApplyingJournal();
        foreach (var f in _files) File.Copy(Path.Combine(_staging, f), P(f) + UpdateInstaller.NewSuffix);
        for (int i = 0; i < swapped; i++)
        {
            File.Move(P(_files[i]), P(_files[i]) + UpdateInstaller.OldSuffix);
            File.Move(P(_files[i]) + UpdateInstaller.NewSuffix, P(_files[i]));
        }
        if (midSwap) File.Move(P(_files[swapped]), P(_files[swapped]) + UpdateInstaller.OldSuffix); // 제자리 파일이 없는 순간
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    public void 새_파일을_복사하다_꺼졌으면_지금_파일은_그대로고_복사본만_지운다(int copied)
    {
        SimulateCrashWhileCopying(copied);

        UpdateInstaller.RecoverAtStartup(_install, P("SoundVisualizer.exe"));

        AssertOldVersion();
        Assert.Equal(0, Leftovers());
        Assert.False(Journal());
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    [InlineData(4, true)]
    public void 바꿔_끼우다_꺼졌으면_다음에_켤_때_옛_파일로_되돌린다(int swapped, bool midSwap)
    {
        SimulateCrashWhileSwapping(swapped, midSwap);

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
        SimulateCrashWhileSwapping(exe + 1, midSwap: false);

        Assert.Equal(UpdateInstaller.StartupOutcome.RecoveredNeedsRestart, UpdateInstaller.RecoverAtStartup(_install, P("SoundVisualizer.exe")));
        AssertOldVersion();
    }

    [Fact]
    public void 다른_프로세스가_잠그고_있으면_바꾸지도_되돌리지도_않는다()
    {
        using (new FileStream(P(UpdateInstaller.LockName), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.False(UpdateInstaller.Apply(_staging, _install, _files, out var error));
            Assert.NotNull(error);
            AssertOldVersion();

            SimulateCrashWhileSwapping(2, midSwap: false);
            Assert.Equal(UpdateInstaller.StartupOutcome.None, UpdateInstaller.RecoverAtStartup(_install, null));
            Assert.True(Journal());
        }
        // 잠금이 풀리면 되돌린다.
        UpdateInstaller.RecoverAtStartup(_install, null);
        AssertOldVersion();
    }

    [Fact]
    public void 기록_없이_남은_옛_파일은_치우고_업데이트한다()
    {
        // 기록을 지웠거나 기록이 깨져 주인 없는 옛 파일이 남은 경우. 지금 파일이 있으면 남는 백업이라 지운다.
        File.WriteAllText(P("SoundVisualizer.exe" + UpdateInstaller.OldSuffix), "stale");

        Assert.True(UpdateInstaller.Apply(_staging, _install, _files, out var error), error?.ToString());

        Assert.Equal("exe new", Read("SoundVisualizer.exe"));
        Assert.Equal("exe old", Read("SoundVisualizer.exe" + UpdateInstaller.OldSuffix));
    }

    [Fact]
    public void 기록_없이_옛_파일만_남았으면_그것이_유일한_사본이라_제자리로_돌린_뒤_업데이트한다()
    {
        File.Move(P("onnxruntime.dll"), P("onnxruntime.dll" + UpdateInstaller.OldSuffix));

        Assert.True(UpdateInstaller.Apply(_staging, _install, _files, out var error), error?.ToString());

        Assert.Equal("ort new", Read("onnxruntime.dll"));
        Assert.True(UpdateInstaller.Revert(_install));
        Assert.Equal("ort old", Read("onnxruntime.dll"));
    }

    [Fact]
    public void 되돌리기_전에_기록을_고치지_못하면_되돌리지_않는다()
    {
        // 기록을 "바꾸는 중" 으로 못 바꾸면, 되돌리다 멈췄을 때 다음 실행이 원본을 지울 수 있다. 그래서 새 버전으로 온전한 채 둔다.
        Assert.True(UpdateInstaller.Apply(_staging, _install, _files, out _));
        File.SetAttributes(P(UpdateInstaller.JournalName), FileAttributes.ReadOnly);

        Assert.False(UpdateInstaller.Revert(_install));

        Assert.Equal("exe new", Read("SoundVisualizer.exe"));
        Assert.Equal("model new", Read("AIModel", "yamnet.onnx"));
        Assert.Equal("exe old", Read("SoundVisualizer.exe" + UpdateInstaller.OldSuffix));
    }

    [Fact]
    public void 치워_둔_파일_목록은_다음_업데이트에도_이어진다()
    {
        Assert.True(UpdateInstaller.Apply(_staging, _install, _files, out _));
        File.SetAttributes(P("SoundVisualizer.exe"), FileAttributes.ReadOnly);
        Assert.True(UpdateInstaller.Revert(_install));
        string discard = P("SoundVisualizer.exe" + UpdateInstaller.DiscardSuffix);
        Assert.True(File.Exists(discard)); // 읽기 전용이라 지우지 못하고 남았다

        Assert.True(UpdateInstaller.Apply(_staging, _install, _files, out var error), error?.ToString());

        // 지우지 못한 파일을 잊지 않는다. 지울 수 있게 되면 다음 정리가 지운다.
        File.SetAttributes(discard, FileAttributes.Normal);
        UpdateInstaller.RecoverAtStartup(_install, null);
        Assert.False(File.Exists(discard));
        Assert.Equal(0, Leftovers());
    }

    [Theory]
    [InlineData("{\"State\":\"applying\",\"Entries\":[{}],\"Leftovers\":[]}")]
    [InlineData("{\"State\":\"applying\",\"Entries\":[null],\"Leftovers\":[]}")]
    [InlineData("{\"State\":\"cleanup\",\"Entries\":[],\"Leftovers\":[null]}")]
    [InlineData("{\"State\":\"applied\",\"Entries\":null,\"Leftovers\":[]}")]
    [InlineData("{\"State\":\"other\",\"Entries\":[],\"Leftovers\":[]}")]
    [InlineData("")]
    public void 값이_빠진_기록으로_앱이_꺼지지_않는다(string json)
    {
        File.WriteAllText(P(UpdateInstaller.JournalName), json);

        Assert.Equal(UpdateInstaller.StartupOutcome.None, UpdateInstaller.RecoverAtStartup(_install, P("SoundVisualizer.exe")));
        Assert.Equal(0, UpdateInstaller.CleanupNow(_install));
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
