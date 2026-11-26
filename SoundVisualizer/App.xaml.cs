// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using SoundVisualizer.Localization;
using SoundVisualizer.Updates;

namespace SoundVisualizer
{

    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 개발용: 환경 변수 SOUNDVISUALIZER_TRACE 에 파일 경로를 주면 AppLog 기록을 그 파일에도 남긴다.
            var tracePath = Environment.GetEnvironmentVariable("SOUNDVISUALIZER_TRACE");
            if (!string.IsNullOrWhiteSpace(tracePath))
            {
                System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.TextWriterTraceListener(tracePath));
                System.Diagnostics.Trace.AutoFlush = true;
                AppLog.Info("app", $"시작합니다({DateTime.Now:yyyy-MM-dd HH:mm:ss}).");
            }

            // 앱 안의 업데이트(#36)로 옛 버전이 이 버전을 띄웠으면, 옛 버전이 설정을 쓰고 끝나기를 기다린 뒤에 설정을 읽는다.
            int? previousProcess = UpdateLaunch.PreviousProcessId(e.Args);
            if (previousProcess is { } previousId) WaitForPreviousVersion(previousId);
            // 지난 업데이트가 남긴 옛 파일을 지운다. 옛 버전이 막 끝났으면 그 실행 파일과 DLL 이 잠깐 더 잡혀 있어,
            // 화면을 막지 않고 뒤에서 몇 번 더 지워 본다. 그래도 남으면 다음에 켤 때 지운다.
            CleanupPreviousUpdate();

            AppSettings.Load();

            // 첫 창을 띄우기 전에 화면 언어와 그 글꼴을 정한다. 창의 {l:Loc} 바인딩이 처음부터 이 언어로 읽힌다.
            Loc.Apply(AppSettings.Language);

            if (previousProcess != null) UpdateController.Instance.MarkUpdated();
        }

        private static void CleanupPreviousUpdate()
        {
            string directory = AppContext.BaseDirectory;
            if (UpdateInstaller.CountOld(directory) == 0) return;
            var delays = new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30) };
            _ = Task.Run(async () =>
            {
                int remaining = await UpdateInstaller.CleanupOldWithRetryAsync(directory, delays);
                if (remaining > 0) AppLog.Warn("update", $"지난 업데이트의 옛 파일 {remaining}개를 아직 지우지 못했습니다. 다음에 켤 때 다시 지웁니다.");
                else AppLog.Info("update", "지난 업데이트의 옛 파일을 모두 지웠습니다.");
            });
        }

        /// <summary>옛 버전 프로세스가 끝나기를 15초까지 기다린다. 이미 끝났거나 다른 프로그램의 번호면 기다리지 않는다.</summary>
        private static void WaitForPreviousVersion(int processId)
        {
            try
            {
                using var previous = Process.GetProcessById(processId);
                if (!string.Equals(previous.ProcessName, Process.GetCurrentProcess().ProcessName, StringComparison.OrdinalIgnoreCase)) return;
                if (!previous.WaitForExit(15000)) AppLog.Warn("update", "옛 버전이 15초 안에 끝나지 않았습니다.");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // 이미 끝났다.
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // 슬라이더를 움직인 직후에 닫아도 마지막 값이 남게, 미뤄 둔 저장을 지금 쓴다.
            AppSettings.Flush();
            base.OnExit(e);
        }
    }
}

