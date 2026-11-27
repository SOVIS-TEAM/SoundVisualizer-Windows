// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Configuration;
using System.Data;
using System.Diagnostics;
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
            // 지난 업데이트를 마무리한다(실행 폴더의 기록 파일을 본다. 기록이 없으면 아무것도 하지 않는다).
            // 파일을 바꾸는 도중 꺼졌으면 옛 파일로 되돌리고, 다 바꿨으면 옛 파일을 지운다.
            // 업데이트 마무리가 무슨 일로 실패해도 앱은 켜져야 한다.
            var outcome = UpdateInstaller.StartupOutcome.None;
            try
            {
                outcome = UpdateInstaller.RecoverAtStartup(AppContext.BaseDirectory, Environment.ProcessPath);
            }
            catch (Exception ex)
            {
                AppLog.Warn("update", "지난 업데이트를 마무리하지 못했습니다.", ex);
            }
            if (outcome is UpdateInstaller.StartupOutcome.Recovered or UpdateInstaller.StartupOutcome.RecoveredNeedsRestart)
            {
                AppLog.Warn("update", "지난 업데이트가 파일을 바꾸는 도중 멈춰 옛 파일로 되돌렸습니다.");
            }
            if (outcome == UpdateInstaller.StartupOutcome.RecoveredNeedsRestart)
            {
                // 지금 돌고 있는 것은 되돌리기 전의 새 exe 다. 되돌린 exe 로 다시 연다. 아직 설정도 창도 없어 바로 끝내도 잃는 것이 없다.
                // "업데이트했습니다" 를 띄우지 않게 --updated-from 은 넘기지 않는다. 치워 둔 새 exe 는 되돌린 쪽이 뒤에서 지운다.
                if (UpdateController.StartNewVersion(AppContext.BaseDirectory, Array.Empty<string>())) Environment.Exit(0);
            }

            AppSettings.Load();

            // 첫 창을 띄우기 전에 화면 언어와 그 글꼴을 정한다. 창의 {l:Loc} 바인딩이 처음부터 이 언어로 읽힌다.
            Loc.Apply(AppSettings.Language);

            // 옛 버전이 막 끝났으면 그 exe 와 DLL 이 잠깐 더 잡혀 있어, 남은 옛 파일은 화면을 막지 않고 뒤에서 몇 번 더 지워 본다.
            UpdateController.Instance.StartBackgroundCleanup();
            if (previousProcess != null) UpdateController.Instance.MarkUpdated();
            // 옛 zip 판에서 포터블 판으로 업데이트했으면, 이제 쓰지 않는 옛 DLL 과 모델 파일을 치운다(#58).
            if (previousProcess != null && AppModels.IsBundled) UpdateController.Instance.StartLegacyCleanup();
        }

        /// <summary>파일을 바꾸는 몇 초 동안은 로그오프·종료로 끝나지 않게 한다(반쯤 바뀐 채 남지 않게, #36).</summary>
        protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
        {
            if (UpdateController.InstallingNow)
            {
                e.Cancel = true;
                AppLog.Warn("update", "파일을 바꾸는 중이라 Windows 종료를 잠시 막았습니다.");
            }
            base.OnSessionEnding(e);
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

