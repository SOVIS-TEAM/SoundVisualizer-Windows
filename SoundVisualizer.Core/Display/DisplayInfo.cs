// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer.Display
{
    /// <summary>
    /// 연결된 모니터 하나. 오버레이를 어느 모니터에 띄울지 정할 때 쓴다(<see cref="OverlayDisplays"/>).
    ///
    /// 자리와 크기는 이 프로세스가 보는 화면 좌표(픽셀)다. 창을 옮기는 Win32 함수(SetWindowPos)가 받는 좌표와 같아서 그대로 넘기면 된다.
    /// 주 모니터의 왼쪽 위가 (0, 0) 이고, 그 왼쪽이나 위에 놓인 모니터는 음수 좌표를 가진다.
    /// </summary>
    /// <param name="DeviceName">Windows 가 붙인 출력 이름(<c>\\.\DISPLAY1</c>). 드라이버를 다시 깔거나 케이블을 옮기면 번호가 바뀔 수 있다.</param>
    /// <param name="DevicePath">그 출력에 물린 모니터의 장치 경로. 같은 모니터를 같은 단자에 꽂아 두는 동안 그대로다. 알아내지 못하면 빈 문자열.</param>
    /// <param name="IsPrimary">Windows 디스플레이 설정의 주 모니터인지.</param>
    /// <param name="RefreshRate">지금 주사율(Hz). 알아내지 못하면 0.</param>
    /// <param name="PixelWidth">지금 해상도의 가로 픽셀 수. 알아내지 못하면 0 이고, 그때는 <see cref="Width"/> 를 보인다.</param>
    /// <param name="PixelHeight">지금 해상도의 세로 픽셀 수. 알아내지 못하면 0.</param>
    public sealed record DisplayInfo(
        string DeviceName,
        string DevicePath,
        int Left,
        int Top,
        int Width,
        int Height,
        bool IsPrimary,
        int RefreshRate,
        int PixelWidth = 0,
        int PixelHeight = 0)
    {
        /// <summary>
        /// 설정에 저장하는 이름. 장치 경로가 있으면 그것을 쓴다. 출력 번호(<see cref="DeviceName"/>)보다 오래 같은 모니터를 가리킨다.
        /// </summary>
        public string Key => DevicePath.Length > 0 ? DevicePath : DeviceName;

        public int Right => Left + Width;

        public int Bottom => Top + Height;

        /// <summary>
        /// 칸에 적는 해상도(가로). 배율이 서로 다른 모니터를 함께 쓰면 <see cref="Width"/> 는 Windows 가 이 프로세스에 맞춰 바꾼 값이라
        /// 실제 해상도와 다를 수 있다. 사람이 아는 숫자는 실제 해상도다.
        /// </summary>
        public int LabelWidth => PixelWidth > 0 && PixelHeight > 0 ? PixelWidth : Width;

        /// <summary>칸에 적는 해상도(세로).</summary>
        public int LabelHeight => PixelWidth > 0 && PixelHeight > 0 ? PixelHeight : Height;
    }
}
