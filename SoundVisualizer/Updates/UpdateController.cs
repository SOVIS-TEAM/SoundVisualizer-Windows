// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using SoundVisualizer.Localization;

namespace SoundVisualizer.Updates
{
    /// <summary>업데이트가 어디까지 왔는지. 홈 안내와 도움말의 앱 정보가 같은 값을 본다.</summary>
    internal enum UpdatePhase
    {
        Idle,
        Checking,
        Latest,
        Available,
        Downloading,
        Installing,
        Failed,

        /// <summary>방금 새 버전으로 바뀌어 열렸다(옛 버전이 --updated-from 으로 띄웠다).</summary>
        Updated,
    }

    /// <summary><see cref="UpdatePhase.Failed"/> 의 이유. 이유마다 안내와 다음에 누를 버튼이 다르다.</summary>
    internal enum UpdateProblem
    {
        None,
        Check,
        Download,
        Verify,
        Apply,
        Restart,
        ReadOnly,
        Manual,
    }

    /// <summary>
    /// 앱 안의 업데이트(#36). zip 판만 GitHub 에 묻고, 내려받아 실행 폴더의 파일을 바꾼 뒤 새 버전을 띄우고 끝난다.
    /// Store 판과 Chocolatey 판은 인터넷에 접속하지 않는다(그쪽이 업데이트한다).
    ///
    /// 모든 메서드는 UI 스레드에서 부르고 <see cref="Changed"/> 도 UI 스레드에서 알린다. 파일 일은 작업 스레드로 넘기고 돌아온다.
    /// </summary>
    internal sealed class UpdateController
    {
        private const string Tag = "update";

        private static UpdateController? _instance;

        /// <summary>앱에 하나. 처음 부를 때(UI 스레드) 만든다.</summary>
        public static UpdateController Instance => _instance ??= new UpdateController();

        private readonly UpdateClient _client;
        private bool _busy;

        private UpdateController()
        {
            string? informational = typeof(UpdateController).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            CurrentVersion = UpdateVersion.FromInformational(informational) ?? new Version(0, 0, 0);
            Channel = UpdateChannels.Detect(AppPaths.IsPackaged, AppContext.BaseDirectory, Environment.GetEnvironmentVariable("ChocolateyInstall"));

            var feed = UpdateFeed.Resolve(Environment.GetEnvironmentVariable(UpdateFeed.EnvironmentVariable));
            if (!ReferenceEquals(feed, UpdateFeed.GitHub)) AppLog.Info(Tag, "개발용 업데이트 주소를 씁니다: " + feed.ApiUrl);
            // 응답 머리까지 기다리는 시간. 내려받는 본문은 따로 정한 시간(InstallAsync)까지 기다린다.
            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            _client = new UpdateClient(http, CurrentVersion, feed);
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

        /// <summary>지금 "지금 업데이트" 를 누를 수 있는지. 내려받기나 확인에서 실패했으면 다시 해 볼 수 있다.</summary>
        public bool CanInstall => IsPortable && !_busy && Release?.Asset != null
            && (Phase == UpdatePhase.Available || (Phase == UpdatePhase.Failed && Problem is UpdateProblem.Download or UpdateProblem.Verify));

        /// <summary>받기 페이지를 안내할 때인지. 앱이 직접 바꿀 수 없거나 바꾸다 실패했다.</summary>
        public bool ShowsDownloadPage => IsPortable && Phase == UpdatePhase.Failed
            && Problem is UpdateProblem.Check or UpdateProblem.Apply or UpdateProblem.ReadOnly or UpdateProblem.Manual;

        /// <summary>홈에 안내를 보일지. 저절로 확인하다 실패한 것과 최신이라는 답은 홈에 보이지 않는다(도움말에서 볼 수 있다).</summary>
        public bool ShowsOnHome => IsPortable && !HomeDismissed && Phase switch
        {
            UpdatePhase.Available or UpdatePhase.Downloading or UpdatePhase.Installing or UpdatePhase.Updated => true,
            UpdatePhase.Failed => Problem != UpdateProblem.Check,
            _ => false,
        };

        /// <summary>지금 상태를 알리는 글. 알릴 것이 없으면 빈 문자열.</summary>
        public string StatusText() => Phase switch
        {
            UpdatePhase.Checking => Loc.Get("update.checking"),
            UpdatePhase.Latest => Loc.Get("update.latest"),
            UpdatePhase.Available when Release != null => Loc.Format("update.available", UpdateVersion.Display(Release.Version)),
            UpdatePhase.Downloading => Loc.Format("update.downloading", Percent),
            UpdatePhase.Installing => Loc.Get("update.installing"),
            UpdatePhase.Updated => Loc.Format("update.done", UpdateVersion.Display(CurrentVersion)),
            UpdatePhase.Failed => Problem switch
            {
                UpdateProblem.Check => Loc.Get("update.failed_check"),
                UpdateProblem.Download => Loc.Get("update.failed_download"),
                UpdateProblem.Verify => Loc.Get("update.failed_verify"),
                UpdateProblem.Apply => Loc.Get("update.failed_apply"),
                UpdateProblem.Restart => Loc.Get("update.failed_restart"),
                UpdateProblem.ReadOnly => Loc.Get("update.readonly"),
                UpdateProblem.Manual => Loc.Get("update.manual"),
                _ => "",
            },
            _ => "",
        };

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
                    if (manual) Fail(UpdateProblem.Check);
                    else Set(before);
                    return;
                }

                AppSettings.LastUpdateCheck = DateTimeOffset.Now;
                AppSettings.Save();
                Release = result.Release;
                if (result.Outcome == UpdateCheckOutcome.Available)
                {
                    AppLog.Info(Tag, $"새 버전이 있습니다: {result.Release!.Tag}");
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
        /// </summary>
        public async Task InstallAsync(Action prepareForRestart)
        {
            if (!CanInstall || Release?.Asset is not { } asset) return;
            string installDirectory = AppContext.BaseDirectory;
            if (!UpdateInstaller.CanWrite(installDirectory))
            {
                AppLog.Warn(Tag, "실행 폴더에 쓸 수 없어 직접 바꾸지 않습니다.");
                Fail(UpdateProblem.ReadOnly);
                return;
            }

            _busy = true;
            string work = Path.Combine(Path.GetTempPath(), "SoundVisualizer-update", UpdateVersion.Display(Release.Version));
            string zip = Path.Combine(work, asset.Name);
            string staging = Path.Combine(work, "files");
            try
            {
                Percent = 0;
                Set(UpdatePhase.Downloading);
                var progress = new Progress<double>(p =>
                {
                    int percent = Math.Clamp((int)Math.Floor(p * 100), 0, 100);
                    if (percent == Percent) return;
                    Percent = percent;
                    Raise();
                });
                try
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
                    await _client.DownloadAsync(asset, zip, progress, timeout.Token);
                }
                catch (UpdateVerificationException ex)
                {
                    AppLog.Warn(Tag, "내려받은 파일이 GitHub 의 값과 다릅니다.", ex);
                    Fail(UpdateProblem.Verify);
                    return;
                }
                catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException or OperationCanceledException)
                {
                    AppLog.Warn(Tag, "새 버전을 내려받지 못했습니다.", ex);
                    Fail(UpdateProblem.Download);
                    return;
                }

                var files = await Task.Run(() => StageAndVerify(zip, staging, Release.Version));
                if (files is null)
                {
                    Fail(UpdateProblem.Verify);
                    return;
                }

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
                AppLog.Info(Tag, $"{Release.Tag} 로 바꿨습니다. 새 버전을 엽니다.");
                TryDeleteDirectory(work);

                if (!StartNewVersion(installDirectory))
                {
                    Fail(UpdateProblem.Restart);
                    return;
                }
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
        private static bool StartNewVersion(string installDirectory)
        {
            try
            {
                var start = new ProcessStartInfo(Path.Combine(installDirectory, UpdatePackage.ExecutableName))
                {
                    UseShellExecute = false,
                    WorkingDirectory = installDirectory,
                };
                foreach (string argument in UpdateLaunch.Arguments(Environment.ProcessId)) start.ArgumentList.Add(argument);
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
