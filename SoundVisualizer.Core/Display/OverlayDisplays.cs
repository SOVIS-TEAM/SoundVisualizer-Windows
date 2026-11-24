// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer.Display
{
    /// <summary>모니터 배치 그림 안에서 모니터 하나가 차지하는 네모(그림 좌표).</summary>
    public readonly record struct DisplayBox(double X, double Y, double Width, double Height);

    /// <summary>
    /// 오버레이를 표시할 모니터를 정하는 규칙(#29). 모니터를 두 대 이상 쓰는 사람이 게임이나 영상을 보는 모니터에 오버레이를 띄울 수 있게 한다.
    ///
    /// - 설정(<see cref="AppSettings.OverlayDisplay"/>)에는 선택한 모니터의 이름(<see cref="DisplayInfo.Key"/>)을 적는다. 선택한 적이 없으면
    ///   빈 문자열(<see cref="Primary"/>)이고 주 모니터에 띄운다.
    /// - 선택한 모니터가 지금 연결되어 있지 않으면 주 모니터에 띄운다. 저장된 선택은 지우지 않으므로 다시 연결하면 그 모니터로 돌아간다.
    ///
    /// 화면은 이 규칙대로 칸을 보이고 창을 옮기기만 한다. 연결된 모니터 목록은 <see cref="DisplayEnumerator"/> 가 읽어 온다.
    /// </summary>
    public static class OverlayDisplays
    {
        /// <summary>선택한 적이 없을 때의 설정값. 주 모니터를 따른다.</summary>
        public const string Primary = "";

        /// <summary>
        /// 저장된 선택과 같은 모니터. 장치 경로를 먼저 맞춰 보고, 없으면 출력 이름으로 찾는다(경로를 알아내지 못한 채 저장한 선택).
        /// 선택한 적이 없거나 그 모니터가 연결되어 있지 않으면 null.
        /// </summary>
        public static DisplayInfo? Find(IReadOnlyList<DisplayInfo> displays, string? saved)
        {
            if (string.IsNullOrEmpty(saved)) return null;
            foreach (var display in displays)
            {
                if (display.DevicePath.Length > 0 && string.Equals(display.DevicePath, saved, StringComparison.OrdinalIgnoreCase)) return display;
            }
            foreach (var display in displays)
            {
                if (string.Equals(display.DeviceName, saved, StringComparison.OrdinalIgnoreCase)) return display;
            }
            return null;
        }

        /// <summary>
        /// 오버레이를 띄울 모니터. 저장된 선택이 연결되어 있으면 그 모니터, 아니면 주 모니터다. 모니터를 하나도 읽지 못했으면 null 이고,
        /// 그때는 창을 옮기지 않는다(Windows 가 정한 자리에 뜬다).
        /// </summary>
        public static DisplayInfo? Resolve(IReadOnlyList<DisplayInfo> displays, string? saved)
        {
            if (displays.Count == 0) return null;
            return Find(displays, saved) ?? displays.FirstOrDefault(display => display.IsPrimary) ?? displays[0];
        }

        /// <summary>모니터를 선택해 두었는데 지금 연결되어 있지 않은지. 그렇다면 설정 화면이 주 모니터에 표시한다고 알린다.</summary>
        public static bool IsMissing(IReadOnlyList<DisplayInfo> displays, string? saved) =>
            !string.IsNullOrEmpty(saved) && Find(displays, saved) == null;

        /// <summary>
        /// 설정 화면에 칸을 놓는 차례: 책상에 놓인 대로 왼쪽에서 오른쪽, 같은 자리면 위에서 아래다. 흔한 좌우 배치에서 칸의 차례가
        /// 모니터가 놓인 차례와 같아진다.
        /// </summary>
        public static IReadOnlyList<DisplayInfo> Ordered(IEnumerable<DisplayInfo> displays) =>
            displays.OrderBy(display => display.Left)
                .ThenBy(display => display.Top)
                .ThenBy(display => display.DeviceName, StringComparer.OrdinalIgnoreCase)
                .ToList();

        /// <summary>
        /// 칸에 적는 모니터 번호. 출력 이름 끝의 번호(<c>\\.\DISPLAY2</c> 의 2)를 쓴다. 대개 Windows 디스플레이 설정의 번호와 같다.
        /// 이름에서 번호를 읽지 못하거나 같은 번호가 둘 이상이면 모든 모니터에 칸의 차례대로 1 부터 매긴다.
        /// </summary>
        /// <param name="ordered"><see cref="Ordered"/> 로 차례를 정한 목록.</param>
        public static IReadOnlyList<int> Numbers(IReadOnlyList<DisplayInfo> ordered)
        {
            var numbers = ordered.Select(display => TrailingNumber(display.DeviceName)).ToList();
            bool usable = numbers.All(number => number > 0) && numbers.Distinct().Count() == numbers.Count;
            return usable ? numbers : Enumerable.Range(1, ordered.Count).ToList();
        }

        /// <summary>
        /// 모니터 배치 그림. 모니터들이 놓인 모양을 그대로 줄여 <paramref name="width"/> × <paramref name="height"/> 안 가운데에 넣는다.
        /// 돌려주는 네모의 차례는 <paramref name="displays"/> 의 차례와 같다.
        /// </summary>
        public static IReadOnlyList<DisplayBox> Layout(IReadOnlyList<DisplayInfo> displays, double width, double height)
        {
            if (displays.Count == 0 || width <= 0 || height <= 0) return Array.Empty<DisplayBox>();

            long left = displays.Min(display => (long)display.Left);
            long top = displays.Min(display => (long)display.Top);
            long right = displays.Max(display => (long)display.Right);
            long bottom = displays.Max(display => (long)display.Bottom);
            double spanX = Math.Max(1, right - left);
            double spanY = Math.Max(1, bottom - top);

            double scale = Math.Min(width / spanX, height / spanY);
            double offsetX = (width - spanX * scale) / 2;
            double offsetY = (height - spanY * scale) / 2;

            return displays
                .Select(display => new DisplayBox(
                    offsetX + (display.Left - left) * scale,
                    offsetY + (display.Top - top) * scale,
                    Math.Max(0, display.Width) * scale,
                    Math.Max(0, display.Height) * scale))
                .ToList();
        }

        private static int TrailingNumber(string name)
        {
            int end = name.Length;
            int start = end;
            while (start > 0 && char.IsAsciiDigit(name[start - 1])) start--;
            if (start == end) return 0;
            return int.TryParse(name.AsSpan(start, end - start), out int number) ? number : 0;
        }
    }
}
