// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer.Updates
{
    /// <summary>앱을 어디서 받아 설치했는지. 새 버전을 받는 길이 저마다 다르다(#36).</summary>
    public enum UpdateChannel
    {
        /// <summary>GitHub 릴리스의 zip 을 푼 것. 앱이 GitHub 에 새 버전을 묻고 직접 받아 바꾼다.</summary>
        Portable,

        /// <summary>Microsoft Store(MSIX). Store 가 업데이트하므로 앱은 인터넷에 접속하지 않고 Store 페이지만 연다.</summary>
        Store,

        /// <summary>Chocolatey. choco 가 버전을 관리하므로 앱은 인터넷에 접속하지 않고 choco 명령만 알려 준다.</summary>
        Chocolatey,
    }

    public static class UpdateChannels
    {
        /// <summary>Microsoft Store 의 앱 페이지. Store 앱이 열리고, 새 버전이 있으면 거기서 받는다.</summary>
        public const string StoreProductUri = "ms-windows-store://pdp/?productid=9NGH48VCRFW2";

        /// <summary>Chocolatey 로 설치한 앱을 새 버전으로 올리는 명령. 관리자 권한이 필요하다.</summary>
        public const string ChocolateyUpgradeCommand = "choco upgrade soundvisualizer -y";

        /// <summary>최신 릴리스 페이지. 앱이 직접 바꾸지 못할 때 여기서 받게 한다.</summary>
        public const string LatestReleasePageUrl = "https://github.com/SOVIS-Team/SoundVisualizer-Windows/releases/latest";

        /// <summary>
        /// 실행 중인 앱의 설치 경로로 정한다. MSIX 패키지면 Store, Chocolatey 의 lib 폴더 아래면 Chocolatey, 그 밖은 zip 판이다.
        /// Chocolatey 는 보통 %ChocolateyInstall%\lib\soundvisualizer\tools 에 푼다. 환경 변수가 없어도 경로에 \chocolatey\lib\ 가 있으면 친다.
        /// </summary>
        public static UpdateChannel Detect(bool packaged, string baseDirectory, string? chocolateyInstall)
        {
            if (packaged) return UpdateChannel.Store;
            string dir = WithSeparator(baseDirectory);
            if (!string.IsNullOrWhiteSpace(chocolateyInstall)
                && dir.StartsWith(WithSeparator(Path.Combine(chocolateyInstall.Trim(), "lib")), StringComparison.OrdinalIgnoreCase))
            {
                return UpdateChannel.Chocolatey;
            }
            return dir.Contains(@"\chocolatey\lib\", StringComparison.OrdinalIgnoreCase) ? UpdateChannel.Chocolatey : UpdateChannel.Portable;
        }

        private static string WithSeparator(string path)
        {
            string p = path.Replace('/', '\\');
            return p.EndsWith('\\') ? p : p + "\\";
        }
    }
}
