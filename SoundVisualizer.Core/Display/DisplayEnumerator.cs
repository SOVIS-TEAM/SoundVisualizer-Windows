// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Runtime.InteropServices;

namespace SoundVisualizer.Display
{
    /// <summary>
    /// 지금 연결된 모니터를 Windows 에서 읽어 온다(#29). 어느 모니터에 띄울지는 <see cref="OverlayDisplays"/> 가 정한다.
    ///
    /// 자리와 크기는 GetMonitorInfo 가 주는 값 그대로다. 이 프로세스가 보는 좌표라서 창을 옮길 때(SetWindowPos) 그대로 쓸 수 있다.
    /// 주사율과 해상도는 그 출력의 지금 화면 모드(EnumDisplaySettings)에서 읽는다.
    /// 읽다가 실패하면 그때까지 읽은 것만 돌려준다. 하나도 못 읽었으면 빈 목록이고, 부르는 쪽은 모니터를 고르지 않던 때처럼 동작한다.
    ///
    /// 한 번 읽는 데 0.5~1ms 가 든다(모니터 두 대로 잰 값이고, 기계가 바쁘면 몇 ms 다). 런처는 설정 화면을 다시 그릴 때마다 묻는데,
    /// 오버레이 편집 패널(F4)에서 슬라이더를 끄는 동안에는 값이 바뀔 때마다 다시 그린다. 그래서 <see cref="Current"/> 는 읽은 것을
    /// 1초 동안 그대로 돌려준다. 모니터 구성이 바뀌었다는 Windows 의 알림을 받은 쪽은 <see cref="Refresh"/> 로 바로 다시 읽는다.
    /// </summary>
    public static class DisplayEnumerator
    {
        private const long FreshMilliseconds = 1000;

        private static readonly BriefCache<IReadOnlyList<DisplayInfo>> Cache = new(Read, FreshMilliseconds);

        /// <summary>지금 연결된 모니터. 방금(1초 안에) 읽은 것이 있으면 그것을 돌려준다.</summary>
        public static IReadOnlyList<DisplayInfo> Current() => Cache.Get();

        /// <summary>읽어 둔 것을 버리고 지금 다시 읽는다. 모니터 구성이 바뀌었다는 알림을 받았을 때 부른다.</summary>
        public static IReadOnlyList<DisplayInfo> Refresh() => Cache.Refresh();

        private static IReadOnlyList<DisplayInfo> Read()
        {
            var displays = new List<DisplayInfo>();
            try
            {
                MonitorEnumProc collect = (IntPtr monitor, IntPtr hdc, ref RECT clip, IntPtr data) =>
                {
                    var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
                    if (GetMonitorInfo(monitor, ref info))
                    {
                        string name = info.szDevice ?? "";
                        ReadMode(name, out int refreshRate, out int pixelWidth, out int pixelHeight);
                        displays.Add(new DisplayInfo(
                            name,
                            ReadDevicePath(name),
                            info.rcMonitor.Left,
                            info.rcMonitor.Top,
                            info.rcMonitor.Right - info.rcMonitor.Left,
                            info.rcMonitor.Bottom - info.rcMonitor.Top,
                            (info.dwFlags & MONITORINFOF_PRIMARY) != 0,
                            refreshRate,
                            pixelWidth,
                            pixelHeight));
                    }
                    return true;
                };
                EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, collect, IntPtr.Zero);
                GC.KeepAlive(collect);
            }
            catch (Exception ex)
            {
                AppLog.Warn("display", "연결된 모니터를 읽지 못했습니다.", ex);
            }
            // 읽어 둔 목록을 여러 곳이 함께 쓴다. 아무도 고칠 수 없게 배열로 굳혀 돌려준다.
            return displays.ToArray();
        }

        /// <summary>
        /// 그 출력(<c>\\.\DISPLAY1</c>)의 지금 주사율과 해상도. 드라이버가 주사율을 "기본값"(0 이나 1)으로 알리면 모르는 것으로 친다.
        /// </summary>
        private static void ReadMode(string deviceName, out int refreshRate, out int pixelWidth, out int pixelHeight)
        {
            refreshRate = 0;
            pixelWidth = 0;
            pixelHeight = 0;
            if (deviceName.Length == 0) return;
            try
            {
                var mode = new DEVMODE { dmSize = (short)Marshal.SizeOf<DEVMODE>() };
                if (!EnumDisplaySettings(deviceName, ENUM_CURRENT_SETTINGS, ref mode)) return;
                if (mode.dmDisplayFrequency > 1) refreshRate = mode.dmDisplayFrequency;
                pixelWidth = mode.dmPelsWidth;
                pixelHeight = mode.dmPelsHeight;
            }
            catch (Exception ex)
            {
                AppLog.Warn("display", "모니터의 화면 모드를 읽지 못했습니다.", ex);
            }
        }

        /// <summary>
        /// 그 출력에 물린 모니터의 장치 경로(<c>\\?\DISPLAY#...</c>). 켜져 있는 모니터를 먼저 고른다. 알아내지 못하면 빈 문자열이고,
        /// 그때는 출력 이름으로 모니터를 기억한다(<see cref="DisplayInfo.Key"/>).
        /// </summary>
        private static string ReadDevicePath(string deviceName)
        {
            if (deviceName.Length == 0) return "";
            try
            {
                string first = "";
                for (uint index = 0; index < MaxMonitorsPerOutput; index++)
                {
                    var device = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
                    if (!EnumDisplayDevices(deviceName, index, ref device, EDD_GET_DEVICE_INTERFACE_NAME)) break;
                    string path = device.DeviceID ?? "";
                    if (path.Length == 0) continue;
                    if ((device.StateFlags & DISPLAY_DEVICE_ACTIVE) != 0) return path;
                    if (first.Length == 0) first = path;
                }
                return first;
            }
            catch (Exception ex)
            {
                AppLog.Warn("display", "모니터의 장치 경로를 읽지 못했습니다.", ex);
                return "";
            }
        }

        // 한 출력에 물린 모니터를 찾을 때 훑는 개수. 복제 모드에서도 몇 대를 넘지 않는다. 드라이버가 끝없이 true 를 돌려주는 경우를 막는다.
        private const uint MaxMonitorsPerOutput = 16;

        private const uint MONITORINFOF_PRIMARY = 0x00000001;
        private const int ENUM_CURRENT_SETTINGS = -1;
        private const uint EDD_GET_DEVICE_INTERFACE_NAME = 0x00000001;
        private const int DISPLAY_DEVICE_ACTIVE = 0x00000001;

        private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, ref RECT clip, IntPtr data);

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

        // DEVMODEW 가운데 화면 모드에 쓰는 꼴(220 바이트). 프린터용 칸 자리에는 화면의 자리·방향이 온다.
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DISPLAY_DEVICE
        {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;
            public int StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFOEX info);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumDisplaySettings(string deviceName, int modeNumber, ref DEVMODE mode);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumDisplayDevices(string deviceName, uint index, ref DISPLAY_DEVICE device, uint flags);
    }
}
