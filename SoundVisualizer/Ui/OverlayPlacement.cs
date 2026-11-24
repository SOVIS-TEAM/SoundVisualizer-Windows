// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using SoundVisualizer.Display;

namespace SoundVisualizer.Ui
{
    /// <summary>
    /// 오버레이 창을 선택한 모니터에 놓는다(#29). 어느 모니터인지는 Core 의 <see cref="OverlayDisplays"/> 가 정하고, 여기서는 창만 옮긴다.
    ///
    /// 오버레이는 최대화한 창이라 Windows 가 그 창이 놓인 모니터를 가득 채워 준다(해상도가 바뀌어도 따라간다).
    /// - 띄우기 전에는 최대화를 풀고, 창을 그 모니터 자리로 옮긴 뒤, 다시 최대화한다. 보이지 않는 동안에는 WPF 가 ShowWindow 를 부르지 않는다.
    /// - 떠 있는 창은 최대화를 풀지 않고 자리와 크기만 그 모니터로 옮긴다. 떠 있는 창의 WindowState 를 바꾸면 WPF 가
    ///   ShowWindow(SW_RESTORE·SW_MAXIMIZE)를 불러 오버레이를 활성 창으로 만든다. 그러면 런처나 게임이 키보드 초점을 잃는다.
    /// 이미 그 모니터에 최대화되어 있으면 건드리지 않는다. 모니터를 한 대만 쓰거나 주 모니터에 그대로 두는 사람에게는 아무 일도 일어나지 않는다.
    /// </summary>
    internal static class OverlayPlacement
    {
        /// <summary>
        /// 창을 <paramref name="display"/> 로 옮겨 가득 채운다. 창을 띄우기 전(창 핸들이 생긴 뒤)에도, 떠 있는 동안에도 부를 수 있다.
        /// 어느 쪽이든 창을 활성화하지 않는다.
        /// </summary>
        /// <returns>창을 옮겼으면 true, 이미 그 모니터에 있어 그대로 두었으면 false.</returns>
        public static bool MoveTo(Window window, DisplayInfo display)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return false;
            if (window.WindowState == WindowState.Maximized && IsOn(hwnd, display)) return false;

            if (window.IsVisible && window.WindowState == WindowState.Maximized
                && GetWindowRect(hwnd, out var now) && TryMonitorRect(hwnd, out var from))
            {
                // 최대화한 채로 옮긴다. 최대화한 창은 모니터보다 테두리 두께만큼 크게 놓여 바깥 테두리가 화면 밖에 숨는다(96 DPI 에서 7px).
                // 지금 모니터에서의 그 차이를 새 모니터에 그대로 옮겨, 띄울 때 최대화한 것과 같은 자리·크기가 되게 한다. 그리는 영역이 같아야
                // 테두리 효과와 편집 화면의 가이드라인이 모니터를 바꿔도 같은 자리에 온다.
                int left = display.Left + (now.Left - from.Left);
                int top = display.Top + (now.Top - from.Top);
                int right = display.Right + (now.Right - from.Right);
                int bottom = display.Bottom + (now.Bottom - from.Bottom);
                SetWindowPos(hwnd, IntPtr.Zero, left, top, right - left, bottom - top, SWP_NOZORDER | SWP_NOACTIVATE);
                return true;
            }

            window.WindowState = WindowState.Normal;
            SetWindowPos(hwnd, IntPtr.Zero, display.Left, display.Top, display.Width, display.Height, SWP_NOZORDER | SWP_NOACTIVATE);
            window.WindowState = WindowState.Maximized;
            return true;
        }

        /// <summary>창이 지금 놓인 모니터의 출력 이름(<c>\\.\DISPLAY1</c>). 알아내지 못하면 빈 문자열.</summary>
        public static string CurrentDeviceName(IntPtr hwnd)
        {
            var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor == IntPtr.Zero) return "";
            var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            return GetMonitorInfo(monitor, ref info) ? info.szDevice ?? "" : "";
        }

        private static bool IsOn(IntPtr hwnd, DisplayInfo display) =>
            string.Equals(CurrentDeviceName(hwnd), display.DeviceName, StringComparison.OrdinalIgnoreCase);

        /// <summary>창이 지금 놓인 모니터의 사각형(이 프로세스가 보는 좌표).</summary>
        private static bool TryMonitorRect(IntPtr hwnd, out RECT rect)
        {
            rect = default;
            var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor == IntPtr.Zero) return false;
            var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            if (!GetMonitorInfo(monitor, ref info)) return false;
            rect = info.rcMonitor;
            return true;
        }

        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFOEX info);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    }
}
