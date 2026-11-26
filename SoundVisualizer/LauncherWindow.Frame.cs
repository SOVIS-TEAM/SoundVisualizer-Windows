// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using SoundVisualizer.Localization;

namespace SoundVisualizer
{
    // 런처 창 틀(#44): 최대화·이전 크기로 버튼, 제목 줄 두 번 누르기, 가장자리로 크기 조절(XAML 의 WindowChrome), 최대화 때 여백, 직각 모서리.
    //
    // Windows 창 틀(SingleBorderWindow)을 쓰고 제목 줄은 WindowChrome 으로 감춘다. 테두리 없는 창(WindowStyle=None)은 최대화하면 작업
    // 표시줄까지 덮고 가장자리로 크기를 바꿀 수 없다. 창 틀이 있는 창은 최대화하면 Windows 가 틀 두께만큼(100% 배율에서 8px) 화면 밖으로
    // 내보내는데, 틀까지 앱이 그리므로 가장자리가 잘린다. 그래서 최대화한 동안은 화면 밖으로 나간 만큼 안쪽 여백을 둔다
    // (숨은 데스크톱에서 잰 값: 1920x1032 작업 영역에서 창이 -8,-8 1936x1048).
    public partial class LauncherWindow
    {
        // 처음 크기(XAML 의 880x700)가 작업 영역보다 크면 줄일 때 남길 여백.
        private const double StartupMargin = 24;

        private void InitFrame()
        {
            FitToWorkArea();
            SourceInitialized += (s, e) => KeepSquareCorners(new WindowInteropHelper(this).Handle);
            StateChanged += (s, e) =>
            {
                UpdateMaximizeButton();
                // 창 위치가 다 바뀐 뒤에 잰다.
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(ApplyMaximizedInset));
            };
            SizeChanged += (s, e) => { if (WindowState == WindowState.Maximized) ApplyMaximizedInset(); };
            UpdateMaximizeButton();
        }

        /// <summary>작은 화면(1366x768 노트북 등)에서는 처음 크기와 최소 크기를 작업 영역에 맞춘다.</summary>
        private void FitToWorkArea()
        {
            var area = SystemParameters.WorkArea;
            MinWidth = Math.Min(MinWidth, area.Width);
            MinHeight = Math.Min(MinHeight, area.Height);
            if (Width > area.Width - StartupMargin) Width = Math.Max(MinWidth, area.Width - StartupMargin);
            if (Height > area.Height - StartupMargin) Height = Math.Max(MinHeight, area.Height - StartupMargin);
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();

        private void ToggleMaximize() =>
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        /// <summary>누른 곳이 맨 윗줄(언어 칸과 창 버튼이 있는 줄)인지. 그 줄의 위쪽 여백도 제목 줄로 친다.</summary>
        private bool IsInTitleRow(MouseButtonEventArgs e) => e.GetPosition(TitleRow).Y <= TitleRow.ActualHeight;

        /// <summary>최대화 버튼의 모양과 화면 읽어주기 이름을 창 상태에 맞춘다. 언어가 바뀔 때도 부른다.</summary>
        private void UpdateMaximizeButton()
        {
            bool maximized = WindowState == WindowState.Maximized;
            // Segoe MDL2 Assets: E922 최대화, E923 이전 크기로. 앱 소스의 문자열에는 ASCII 만 쓰므로 글자 코드로 적는다.
            BtnMaximize.Content = ((char)(maximized ? 0xE923 : 0xE922)).ToString();
            AutomationProperties.SetName(BtnMaximize, Loc.Get(maximized ? "common.restore" : "common.maximize"));
        }

        /// <summary>
        /// 최대화한 동안은 창이 모니터의 작업 영역 밖으로 나간 만큼 안쪽 여백을 둔다. 그래야 맨 위 줄과 오른쪽 창 버튼, 아래 끝이 잘리지 않는다.
        /// 이전 크기로 돌아오면 여백을 없앤다.
        /// </summary>
        private void ApplyMaximizedInset()
        {
            if (WindowState != WindowState.Maximized)
            {
                RootFrame.Margin = new Thickness(0);
                return;
            }
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out var window)) return;
            IntPtr monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info)) return;

            // 화면 픽셀을 WPF 단위로 바꾼다(배율 125%, 150% 에서도 맞게).
            var toDip = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
            RootFrame.Margin = new Thickness(
                Math.Max(0, info.Work.Left - window.Left) * toDip.M11,
                Math.Max(0, info.Work.Top - window.Top) * toDip.M22,
                Math.Max(0, window.Right - info.Work.Right) * toDip.M11,
                Math.Max(0, window.Bottom - info.Work.Bottom) * toDip.M22);
        }

        private const int MonitorDefaultToNearest = 2;
        private const int DwmWindowCornerPreference = 33;
        private const int DwmCornerDoNotRound = 1;

        /// <summary>Windows 11 은 창 모서리를 둥글게 그린다. 런처는 직각으로 둔다(#37). Windows 10 에는 이 설정이 없어 무시된다.</summary>
        private static void KeepSquareCorners(IntPtr hwnd)
        {
            int preference = DwmCornerDoNotRound;
            _ = DwmSetWindowAttribute(hwnd, DwmWindowCornerPreference, ref preference, sizeof(int));
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect32
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            public int Size;
            public Rect32 Monitor;
            public Rect32 Work;
            public uint Flags;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hwnd, out Rect32 rect);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    }
}
