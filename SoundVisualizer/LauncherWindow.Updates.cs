// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using SoundVisualizer.Localization;
using SoundVisualizer.Updates;

namespace SoundVisualizer
{
    // 앱 안의 업데이트(#36): 홈의 업데이트 안내, 설정의 "시작할 때 업데이트 확인" 스위치, 런처를 열 때의 확인, 지금 업데이트.
    // 무엇을 할지는 UpdateController 가 정한다. 도움말의 앱 정보도 같은 컨트롤러를 보고, 지금 업데이트는 이 창의 StartUpdateInstall 을 부른다.
    public partial class LauncherWindow
    {
        // 화면 읽어주기에 마지막으로 알린 업데이트 안내. 언어만 바뀐 때나 같은 글을 다시 넣을 때는 다시 읽지 않는다.
        private string _announcedUpdate = "";

        private void WatchUpdates()
        {
            var updates = UpdateController.Instance;
            // Store·Chocolatey 판은 그쪽이 업데이트하므로 스위치를 두지 않는다.
            ChkCheckUpdates.Visibility = updates.IsPortable ? Visibility.Visible : Visibility.Collapsed;
            updates.Changed += OnUpdateChanged;
            UpdateUpdateNotice(announce: false);

            // 첫 화면을 그린 뒤 조용할 때 묻는다. 답을 기다리는 동안에도 런처는 그대로 쓸 수 있다.
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => _ = updates.CheckOnStartupAsync()));
        }

        private void UnwatchUpdates() => UpdateController.Instance.Changed -= OnUpdateChanged;

        private void OnUpdateChanged(object? sender, EventArgs e) => UpdateUpdateNotice(announce: true);

        /// <summary>홈의 업데이트 안내를 지금 상태와 언어로 다시 넣는다. 글이 새로 생기거나 바뀌면 화면 읽어주기가 읽고 지나가게 한다.</summary>
        private void UpdateUpdateNotice(bool announce)
        {
            var updates = UpdateController.Instance;
            bool show = updates.ShowsOnHome;
            string text = show ? updates.StatusText() : "";
            PanelUpdate.Visibility = show && text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            TxtUpdateNotice.Text = text;

            BtnHomeUpdateInstall.Visibility = updates.CanInstall ? Visibility.Visible : Visibility.Collapsed;
            BtnHomeUpdatePage.Visibility = updates.ShowsDownloadPage ? Visibility.Visible : Visibility.Collapsed;
            // 내려받고 바꾸는 동안에는 닫지 않는다(곧 다시 열린다).
            BtnHomeUpdateDismiss.Visibility = updates.IsBusy ? Visibility.Collapsed : Visibility.Visible;

            // 진행률은 1% 마다 바뀐다. 화면 읽어주기가 숫자마다 읽지 않게, 상태가 바뀔 때(진행률 말고)만 알린다.
            string key = updates.Phase == UpdatePhase.Downloading ? nameof(UpdatePhase.Downloading) : text;
            if (announce && show && key != _announcedUpdate) Announce(TxtUpdateNotice);
            _announcedUpdate = show ? key : "";
        }

        private void BtnUpdateInstall_Click(object sender, RoutedEventArgs e) => StartUpdateInstall();

        private void BtnUpdatePage_Click(object sender, RoutedEventArgs e) => OpenUpdatePage();

        private void BtnUpdateDismiss_Click(object sender, RoutedEventArgs e)
        {
            UpdateController.Instance.DismissHome();
            // 누른 버튼이 사라지므로 키보드 초점을 실행 버튼으로 옮긴다.
            BtnLaunch.Focus();
        }

        /// <summary>
        /// 지금 업데이트. 오버레이가 켜져 있으면 꺼진다는 것을 먼저 묻는다. 파일을 바꾸기 직전에 오버레이를 끄고 설정을 저장한다
        /// (오버레이가 쥔 AI 모델 파일을 놓아야 바꿀 수 있다). 홈 안내와 도움말의 버튼이 함께 쓴다.
        /// </summary>
        internal async void StartUpdateInstall()
        {
            var updates = UpdateController.Instance;
            if (!updates.CanInstall) return;
            if (_overlayWindow != null
                && !ConfirmWindow.Ask(this, Loc.Get("update.confirm_title"), Loc.Get("update.confirm_message"), Loc.Get("update.install"), Loc.Get("update.cancel")))
            {
                return;
            }
            await updates.InstallAsync(() =>
            {
                StopOverlay();
                AppSettings.Flush();
            });
        }

        /// <summary>받기 페이지(최신 릴리스)를 브라우저로 연다. 열지 못하면 주소를 알린다.</summary>
        internal static void OpenUpdatePage()
        {
            string url = UpdateController.Instance.Release?.PageUrl.AbsoluteUri ?? UpdateChannels.LatestReleasePageUrl;
            try
            {
                using var process = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                AppLog.Warn("update", "받기 페이지를 열지 못했습니다.", ex);
                Dialogs.Show(Loc.Format("dialog.link_error", url));
            }
        }
    }
}
