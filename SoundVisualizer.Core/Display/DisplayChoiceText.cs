// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Globalization;

namespace SoundVisualizer.Display
{
    /// <summary>
    /// 모니터 선택 칸(#29)에 적는 글. 칸마다 번호가 든 이름("모니터 2"), 해상도("1920 × 1080"), 주 모니터면 그 표시를 보인다.
    /// 화면 읽어주기에는 셋을 한 번에 읽힌다. 문장 틀은 문자열 표에서 받는다(언어마다 다르다).
    /// </summary>
    public static class DisplayChoiceText
    {
        /// <summary>칸의 이름. <paramref name="itemPattern"/> 은 "모니터 {0}" 꼴이다.</summary>
        public static string Title(string itemPattern, int number) =>
            string.Format(CultureInfo.InvariantCulture, itemPattern, number);

        /// <summary>칸에 적는 해상도. <paramref name="sizePattern"/> 은 "{0} × {1}" 꼴이다.</summary>
        public static string Size(string sizePattern, DisplayInfo display) =>
            string.Format(CultureInfo.InvariantCulture, sizePattern, display.LabelWidth, display.LabelHeight);

        /// <summary>
        /// 화면 읽어주기가 읽는 칸의 이름: 이름, 해상도, (주 모니터면) 주 모니터. <paramref name="pairPattern"/> 은 둘을 잇는 "{0}, {1}" 꼴이다.
        /// </summary>
        public static string Name(string pairPattern, string title, string size, string? primary)
        {
            string name = string.Format(CultureInfo.InvariantCulture, pairPattern, title, size);
            return string.IsNullOrEmpty(primary) ? name : string.Format(CultureInfo.InvariantCulture, pairPattern, name, primary);
        }

        /// <summary>
        /// 칸들을 다시 만들어야 하는지 가리는 표식. 모니터의 구성(이름, 자리, 크기, 주 모니터, 해상도)과 화면 언어가 같으면 표식도 같다.
        /// 같을 때는 칸을 그대로 두고 선택만 맞춘다. 다시 만들면 키보드로 옮겨 다니던 초점이 사라진다.
        /// </summary>
        public static string Signature(IReadOnlyList<DisplayInfo> ordered, string languageCode) =>
            languageCode + "|" + string.Join("|", ordered.Select(display => string.Join(",",
                display.Key,
                display.DeviceName,
                display.Left.ToString(CultureInfo.InvariantCulture),
                display.Top.ToString(CultureInfo.InvariantCulture),
                display.Width.ToString(CultureInfo.InvariantCulture),
                display.Height.ToString(CultureInfo.InvariantCulture),
                display.IsPrimary ? "P" : "-",
                display.LabelWidth.ToString(CultureInfo.InvariantCulture),
                display.LabelHeight.ToString(CultureInfo.InvariantCulture))));
    }
}
