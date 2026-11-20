// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Runtime.InteropServices;

namespace SoundVisualizer
{
    /// <summary>
    /// 앱이 쓰는 파일을 둘 폴더.
    /// zip(설치 없이 실행) 판은 실행 파일 옆에 둔다. Microsoft Store 의 MSIX 패키지로 설치하면 설치 폴더가 읽기 전용이라
    /// 사용자 폴더(%LocalAppData%\SoundVisualizer)에 둔다. 패키지 앱이 이 폴더에 쓰면 Windows 가 패키지 전용 저장소로 옮겨 준다.
    /// </summary>
    public static class AppPaths
    {
        /// <summary>사용자 폴더 아래 앱 폴더 이름. AI 기록(<see cref="Diagnostics.AiDebugRecorder"/>)의 예비 폴더와 같다.</summary>
        public const string AppFolderName = "SoundVisualizer";

        // 패키지가 없는 프로세스에서 GetCurrentPackageFullName 이 돌려주는 값(APPMODEL_ERROR_NO_PACKAGE).
        private const int AppModelErrorNoPackage = 15700;

        /// <summary>MSIX 패키지로 설치되어 실행 중인지. 한 번만 묻는다.</summary>
        public static bool IsPackaged { get; } = DetectPackaged();

        /// <summary>설정처럼 앱이 쓰는 파일을 둘 폴더.</summary>
        public static string DataDirectory => ResolveDataDirectory(
            IsPackaged,
            AppContext.BaseDirectory,
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

        /// <summary>
        /// 패키지면 사용자 폴더 아래 앱 폴더, 아니면 실행 파일 폴더. 사용자 폴더를 찾지 못하면(빈 값) 실행 파일 폴더로 물러난다.
        /// </summary>
        public static string ResolveDataDirectory(bool packaged, string baseDirectory, string localAppData)
        {
            if (packaged && !string.IsNullOrEmpty(localAppData)) return Path.Combine(localAppData, AppFolderName);
            return baseDirectory;
        }

        private static bool DetectPackaged()
        {
            if (!OperatingSystem.IsWindows()) return false;
            try
            {
                int length = 0;
                // 패키지 앱이면 버퍼가 모자란다는 값(122)이, 아니면 15700 이 온다.
                return GetCurrentPackageFullName(ref length, IntPtr.Zero) != AppModelErrorNoPackage;
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
            {
                // Windows 7 처럼 패키지 개념이 없는 시스템이다.
                return false;
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, IntPtr packageFullName);
    }
}
