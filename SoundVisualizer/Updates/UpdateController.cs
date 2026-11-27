// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using SoundVisualizer.Localization;

namespace SoundVisualizer.Updates
{
    /// <summary>
    /// 앱 안의 업데이트(#36). zip 판만 GitHub 에 묻고, 내려받아 실행 폴더의 파일을 바꾼 뒤 새 버전을 띄우고 끝난다.
    /// Store 판과 Chocolatey 판은 인터넷에 접속하지 않는다(그쪽이 업데이트한다). 무엇을 보일지는 Core 의 <see cref="UpdateView"/> 가 정한다.
    ///
    /// 모든 메서드는 UI 스레드에서 부르고 <see cref="Changed"/> 도 UI 스레드에서 알린다. 파일 일은 작업 스레드로 넘기고 돌아온다.
    /// </summary>
    internal sealed class UpdateController
    {
        private const string Tag = "update";

        // 지난 업데이트의 옛 파일을 다시 지워 보는 간격. 옛 버전이 막 끝난 때는 그 exe 와 DLL 이 잠깐 더 잡혀 있다.
        private static readonly TimeSpan[] CleanupRetryDelays = { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30) };

        private static UpdateController? _instance;

        /// <summary>앱에 하나. 처음 부를 때(UI 스레드) 만든다.</summary>
        public static UpdateController Instance => _instance ??= new UpdateController();

        /// <summary>지금 실행 폴더의 파일을 바꾸는 중인지. 컨트롤러를 만들지 않고 본다(종료 처리에서 부른다).</summary>
        public static bool InstallingNow => _instance?.BlocksExit ?? false;

        private readonly UpdateClient _client;
        private bool _busy;
        private CancellationTokenSource? _cleanupCancel;
        private Task? _cleanupTask;

        private UpdateController()
        {
            string? informational = typeof(UpdateController).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            CurrentVersion = UpdateVersion.FromInformational(informational) ?? new Version(0, 0, 0);
            Channel = UpdateChannels.Detect(AppPaths.IsPackaged, AppContext.BaseDirectory, Environment.GetEnvironmentVariable("ChocolateyInstall"));

            var feed = UpdateFeed.Resolve(Environment.GetEnvironmentVariable(UpdateFeed.EnvironmentVariable));
            if (!ReferenceEquals(feed, UpdateFeed.GitHub)) AppLog.Info(Tag, "개발용 업데이트 주소를 씁니다: " + feed.ApiUrl);
            // 응답 머리까지 기다리는 시간. 내려받는 본문은 끊김(60초 동안 아무것도 오지 않음)으로만 멈춘다(UpdateClient).
            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            // 포터블 판(실행 파일 안에 모델이 든 빌드)은 포터블 zip 을 받아 포터블로 남는다(#66).
            var kind = AppModels.IsBundled ? UpdatePackageKind.Portable : UpdatePackageKind.Standard;
            _client = new UpdateClient(http, CurrentVersion, feed, kind);
        }

        public UpdateChannel Channel { get; }
        public Version CurrentVersion { get; }
        public UpdatePhase Phase { get; private set; } = UpdatePhase.Idle;
        public UpdateProblem Problem { get; private set; }
        public ReleaseInfo? Release { get; private set; }
        public int Percent { get; private set; }

        /// <summary>홈 안내를 닫았다. 상태가 바뀌면(새 버전을 찾음, 실패) 다시 보인다.</summary>
        public bool HomeDismissed { get; private set; }

        /// <summary>확인·내려받기·바꾸기 중이다. 그동안 버튼을 막는다.</summary>
        public bool IsBusy => _busy;

        public event EventHandler? Changed;

        /// <summary>앱이 직접 GitHub 에 묻고 바꾸는 판인지(zip 판).</summary>
        public bool IsPortable => Channel == UpdateChannel.Portable;

        public UpdateSnapshot Snapshot => new(Channel, Phase, Problem, Release, CurrentVersion, _busy, HomeDismissed, Percent);

        public bool CanInstall => UpdateView.CanInstall(Snapshot);
        public bool ShowsDownloadPage => UpdateView.ShowsDownloadPage(Snapshot);
        public bool ShowsOnHome => UpdateView.ShowsOnHome(Snapshot);

        /// <summary>
        /// 실행 폴더의 파일을 바꾸는 중이라 앱을 끝내면 안 된다(창 닫기, 트레이 종료, 로그오프를 막는다). 다 바꾸고 새 버전을 띄운 뒤
        /// 스스로 끝낼 때는 막지 않는다.
        /// </summary>
        public bool BlocksExit => !_handingOver && UpdateView.BlocksExit(Snapshot);

        // 새 버전을 띄웠고 이제 이 프로세스가 끝난다.
        private bool _handingOver;

        /// <summary>지금 상태를 알리는 글. 알릴 것이 없으면 빈 문자열.</summary>
        public string StatusText() => string.Join(" ", UpdateView.Status(Snapshot).Select(t => t.Args.Length == 0 ? Loc.Get(t.Key) : Loc.Format(t.Key, t.Args)));

        /// <summary>옛 버전이 바꾸고 띄운 새 버전이다. 홈에 "업데이트했습니다" 를 보인다.</summary>
        public void MarkUpdated()
        {
            Phase = UpdatePhase.Updated;
            Raise();
        }

        public void DismissHome()
        {
            HomeDismissed = true;
            Raise();
        }

        /// <summary>
        /// 지난 업데이트의 옛 파일이 남았으면 뒤에서 몇 번 더 지워 본다(앱 시작 때 App 이 부른다). 다음 업데이트를 시작하면 멈추고,
        /// 그 업데이트가 정리를 넘겨받는다(이번 업데이트의 백업을 지우지 않게).
        /// </summary>
        public void StartBackgroundCleanup()
        {
            string directory = AppContext.BaseDirectory;
            if (UpdateInstaller.PendingCleanupCount(directory) == 0) return;
            var cancel = new CancellationTokenSource();
            _cleanupCancel = cancel;
            _cleanupTask = Task.Run(async () =>
            {
                try
                {
                    int remaining = await UpdateInstaller.CleanupWithRetryAsync(directory, CleanupRetryDelays, cancel.Token);
                    if (remaining > 0) AppLog.Warn(Tag, $"지난 업데이트의 옛 파일 {remaining}개를 아직 지우지 못했습니다. 다음에 켤 때 다시 지웁니다.");
                    else AppLog.Info(Tag, "지난 업데이트의 옛 파일을 모두 지웠습니다.");
                }
                catch (OperationCanceledException)
                {
                    // 다음 업데이트가 넘겨받았다.
                }
            });
        }

        /// <summary>
        /// 옛 zip 판(2.3.2 이하)에서 앱 안의 업데이트로 포터블 판이 막 켜졌으면, 옛 판이 실행 폴더에 남긴 DLL 과 모델 파일을 뒤에서
        /// 지운다(#58, <see cref="LegacyPackageFiles"/>). 옛 버전이 막 끝나 파일이 잠깐 잡혀 있을 수 있어 몇 번 더 해 본다.
        /// 앱 안의 업데이트로 켜졌을 때만 부른다(그때만 이 폴더가 앱 폴더임을 안다).
        /// </summary>
        public void StartLegacyCleanup()
        {
            string directory = AppContext.BaseDirectory;
            if (LegacyPackageFiles.Remaining(directory) == 0) return;
            Task.Run(async () =>
            {
                int failed = LegacyPackageFiles.Remove(directory);
                foreach (var delay in CleanupRetryDelays)
                {
                    if (failed == 0) break;
                    await Task.Delay(delay);
                    failed = LegacyPackageFiles.Remove(directory);
                }
                if (failed > 0) AppLog.Warn(Tag, $"옛 zip 판의 파일 {failed}개를 지우지 못했습니다. 새 버전은 이 파일을 쓰지 않습니다.");
                else AppLog.Info(Tag, "옛 zip 판이 남긴 DLL 과 모델 파일을 지웠습니다.");
            });
        }

        private async Task StopBackgroundCleanupAsync()
        {
            _cleanupCancel?.Cancel();
            if (_cleanupTask != null) await _cleanupTask;
            _cleanupTask = null;
            _cleanupCancel?.Dispose();
            _cleanupCancel = null;
        }

        /// <summary>런처를 열 때. zip 판이고 스위치가 켜져 있고 하루가 지났을 때만 묻는다. 실패해도 조용히 지나간다.</summary>
        public Task CheckOnStartupAsync()
        {
            if (!IsPortable || !AppSettings.CheckUpdatesOnStart) return Task.CompletedTask;
            if (!UpdateSchedule.IsDue(DateTimeOffset.Now, AppSettings.LastUpdateCheck)) return Task.CompletedTask;
            return CheckAsync(manual: false);
        }

        /// <summary>도움말의 "업데이트 확인". 결과(최신, 실패)도 알린다.</summary>
        public Task CheckNowAsync() => CheckAsync(manual: true);

        private async Task CheckAsync(bool manual)
        {
            if (!IsPortable || _busy) return;
            _busy = true;
            var before = Phase;
            if (manual) Set(UpdatePhase.Checking);
            else Raise(); // 버튼을 막는다
            try
            {
                UpdateCheckResult result;
                try
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
                    result = await _client.CheckAsync(timeout.Token);
                }
                catch (OperationCanceledException)
                {
                    result = new UpdateCheckResult(UpdateCheckOutcome.Failed, null);
                }

                if (result.Outcome == UpdateCheckOutcome.Failed)
                {
                    AppLog.Warn(Tag, "업데이트를 확인하지 못했습니다.");
                    // 저절로 확인한 것은 알리지 않고 다음에 런처를 열 때 다시 묻는다(마지막 확인 때를 남기지 않는다).
                    // 이미 찾은 새 버전(Release)은 그대로 두어, 실패를 알리면서도 "지금 업데이트" 를 누를 수 있다(UpdateView.CanInstall).
                    if (manual) Fail(UpdateProblem.Check);
                    else Set(before);
                    return;
                }

                AppSettings.LastUpdateCheck = DateTimeOffset.Now;
                AppSettings.Save();
                Release = result.Release;
                if (result.Outcome == UpdateCheckOutcome.Available)
                {
                    AppLog.Info(Tag, $"새 버전이 있습니다: {result.Release!.Tag}" + (result.Release.Asset == null ? " (앱이 받을 zip 없음)" : ""));
                    HomeDismissed = false;
                    Set(UpdatePhase.Available);
                }
                else
                {
                    // 방금 업데이트한 뒤의 안내는 저절로 확인한 결과로 지우지 않는다.
                    Set(manual || before != UpdatePhase.Updated ? UpdatePhase.Latest : before);
                }
            }
            finally
            {
                _busy = false;
                Raise();
            }
        }

        /// <summary>
        /// 새 버전을 내려받아 확인하고 실행 폴더의 파일을 바꾼 뒤 새 버전을 띄우고 앱을 끝낸다.
        /// <paramref name="prepareForRestart"/> 는 파일을 바꾸기 직전에 부른다(오버레이를 끄고 설정을 저장한다. 오버레이가 쥔 모델 파일을 놓아야 바꿀 수 있다).
        /// 바꾸는 동안에는 <see cref="BlocksExit"/> 가 참이라 창을 닫거나 로그오프로 끝나지 않는다.
        /// </summary>
        public async Task InstallAsync(Action prepareForRestart)
        {
            if (!CanInstall || Release?.Asset is not { } asset) return;
            var release = Release;
            string installDirectory = AppContext.BaseDirectory;
            if (!UpdateInstaller.CanWrite(installDirectory))
            {
                AppLog.Warn(Tag, "실행 폴더에 쓸 수 없어 직접 바꾸지 않습니다.");
                Fail(UpdateProblem.ReadOnly);
                return;
            }

            _busy = true;
            string work = Path.Combine(Path.GetTempPath(), "SoundVisualizer-update", UpdateVersion.Display(release.Version));
            string zip = Path.Combine(work, asset.Name);
            string staging = Path.Combine(work, "files");
            try
            {
                Percent = 0;
                Set(UpdatePhase.Downloading);
                var progress = new Progress<double>(p =>
                {
                    int percent = Math.Clamp((int)Math.Floor(p * 100), 0, 100);
                    if (percent == Percent || Phase != UpdatePhase.Downloading) return;
                    Percent = percent;
                    Raise();
                });
                try
                {
                    await _client.DownloadAsync(asset, zip, progress, CancellationToken.None);
                }
                catch (UpdateVerificationException ex)
                {
                    AppLog.Warn(Tag, "내려받은 파일이 GitHub 의 값과 다릅니다.", ex);
                    Fail(UpdateProblem.Verify);
                    return;
                }
                catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException or OperationCanceledException or TimeoutException)
                {
                    AppLog.Warn(Tag, "새 버전을 내려받지 못했습니다.", ex);
                    Fail(UpdateProblem.Download);
                    return;
                }

                var files = await Task.Run(() => StageAndVerify(zip, staging, release.Version));
                if (files is null)
                {
                    Fail(UpdateProblem.Verify);
                    return;
                }

                // 지난 업데이트의 정리가 아직 돌고 있으면 멈추고 넘겨받는다. 이번에 만들 백업을 그 정리가 지우지 않게.
                await StopBackgroundCleanupAsync();
                Set(UpdatePhase.Installing);
                prepareForRestart();
                Exception? applyError = null;
                bool applied = await Task.Run(() => UpdateInstaller.Apply(staging, installDirectory, files, out applyError));
                if (!applied)
                {
                    AppLog.Warn(Tag, "실행 폴더의 파일을 바꾸지 못해 되돌렸습니다.", applyError);
                    Fail(UpdateProblem.Apply);
                    return;
                }
                AppLog.Info(Tag, $"{release.Tag} 로 바꿨습니다. 새 버전을 엽니다.");
                TryDeleteDirectory(work);

                if (!StartNewVersion(installDirectory))
                {
                    // 새 버전을 띄우지 못했으면(백신이 막음 등) 옛 파일로 되돌린다. 옛 버전은 아직 돌고 있다.
                    bool reverted = await Task.Run(() => UpdateInstaller.Revert(installDirectory));
                    AppLog.Warn(Tag, reverted ? "새 버전을 띄우지 못해 옛 파일로 되돌렸습니다." : "새 버전을 띄우지 못했고 다 되돌리지도 못했습니다. 다음에 켤 때 마저 되돌립니다.");
                    Fail(reverted ? UpdateProblem.Apply : UpdateProblem.Restart);
                    return;
                }
                _handingOver = true;
                Application.Current.Shutdown();
            }
            finally
            {
                _busy = false;
                Raise();
            }
        }

        /// <summary>zip 을 풀고, 푼 실행 파일이 알려 준 버전인지 본다. 아니면 null.</summary>
        private static IReadOnlyList<string>? StageAndVerify(string zip, string staging, Version expected)
        {
            try
            {
                var files = UpdatePackage.Extract(zip, staging);
                string? product = FileVersionInfo.GetVersionInfo(Path.Combine(staging, UpdatePackage.ExecutableName)).ProductVersion;
                var staged = UpdateVersion.FromInformational(product);
                if (staged is null || staged != UpdateVersion.Normalize(expected))
                {
                    AppLog.Warn(Tag, $"내려받은 실행 파일의 버전({product})이 릴리스({expected})와 다릅니다.");
                    return null;
                }
                return files;
            }
            catch (Exception ex) when (ex is InvalidUpdatePackageException or InvalidDataException or IOException or UnauthorizedAccessException)
            {
                AppLog.Warn(Tag, "내려받은 zip 을 풀지 못했습니다.", ex);
                return null;
            }
        }

        /// <summary>바꾼 실행 파일을 띄운다. 새 버전은 이 프로세스가 끝나기를 기다린 뒤 설정을 읽는다(UpdateLaunch).</summary>
        internal static bool StartNewVersion(string installDirectory, IEnumerable<string>? arguments = null)
        {
            try
            {
                var start = new ProcessStartInfo(Path.Combine(installDirectory, UpdatePackage.ExecutableName))
                {
                    UseShellExecute = false,
                    WorkingDirectory = installDirectory,
                };
                foreach (string argument in arguments ?? UpdateLaunch.Arguments(Environment.ProcessId)) start.ArgumentList.Add(argument);
                using var process = Process.Start(start);
                return process != null;
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
            {
                AppLog.Warn(Tag, "새 버전을 띄우지 못했습니다.", ex);
                return false;
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { AppLog.Warn(Tag, "임시 폴더를 지우지 못했습니다.", ex); }
        }

        private void Fail(UpdateProblem problem)
        {
            Problem = problem;
            HomeDismissed = false;
            Set(UpdatePhase.Failed);
        }

        private void Set(UpdatePhase phase)
        {
            if (phase != UpdatePhase.Failed) Problem = UpdateProblem.None;
            Phase = phase;
            Raise();
        }

        private void Raise() => Changed?.Invoke(this, EventArgs.Empty);
    }
}
