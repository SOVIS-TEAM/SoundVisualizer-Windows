// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Globalization;
using System.Text.RegularExpressions;

namespace SoundVisualizer.Updates
{
    /// <summary>
    /// 릴리스 태그와 실행 파일의 버전을 같은 꼴(주.부.수정)로 읽고 비교한다(#36).
    ///
    /// 릴리스 태그는 "SoundVisualizer_v2.2", "SoundVisualizer_v2.1.1" 처럼 짧게 쓰고, 실행 파일은 "2.2.0+21d87a3…" 처럼 세 자리에 빌드한
    /// 커밋이 붙는다. 둘 다 세 자리로 맞춰야 2.2 와 2.2.0 이 같은 버전으로 읽힌다.
    /// </summary>
    public static class UpdateVersion
    {
        // 태그 끝의 "v2.2" 나 "v2.1.1". 앞의 이름(SoundVisualizer_)은 바뀌어도 된다. 네 자리(2.2.0.1)까지 받는다.
        private static readonly Regex TagVersion = new(@"(?:^|[^0-9A-Za-z])v(\d{1,4}(?:\.\d{1,5}){1,3})$", RegexOptions.CultureInvariant);

        /// <summary>릴리스 태그의 버전. 태그가 "…v주.부[.수정]" 으로 끝나지 않으면 null.</summary>
        public static Version? FromTag(string? tag)
        {
            var match = TagVersion.Match((tag ?? "").Trim());
            return match.Success ? Parse(match.Groups[1].Value) : null;
        }

        /// <summary>실행 파일의 버전(AssemblyInformationalVersion, "2.2.0+커밋"). 숫자로 시작하지 않으면 null.</summary>
        public static Version? FromInformational(string? informationalVersion)
        {
            string value = (informationalVersion ?? "").Trim();
            int cut = value.IndexOfAny(new[] { '+', '-', ' ' });
            if (cut >= 0) value = value[..cut];
            return Parse(value);
        }

        /// <summary>화면에 보일 버전. 릴리스 이름처럼 수정 번호가 0 이면 뺀다(2.2.0 → "2.2", 2.1.1 → "2.1.1").</summary>
        public static string Display(Version version)
        {
            var v = Normalize(version);
            return v.Build > 0
                ? string.Create(CultureInfo.InvariantCulture, $"{v.Major}.{v.Minor}.{v.Build}")
                : string.Create(CultureInfo.InvariantCulture, $"{v.Major}.{v.Minor}");
        }

        /// <summary>네 번째 자리를 버리고 빈 수정 번호를 0 으로 채운다. 2.2 와 2.2.0 을 같게 비교하려고 쓴다.</summary>
        public static Version Normalize(Version version) => new(version.Major, version.Minor, Math.Max(0, version.Build));

        /// <summary><paramref name="candidate"/> 가 <paramref name="current"/> 보다 새 버전인지.</summary>
        public static bool IsNewer(Version candidate, Version current) => Normalize(candidate) > Normalize(current);

        private static Version? Parse(string text)
        {
            if (!Version.TryParse(text, out var version)) return null;
            return version.Major < 0 || version.Minor < 0 ? null : Normalize(version);
        }
    }
}
