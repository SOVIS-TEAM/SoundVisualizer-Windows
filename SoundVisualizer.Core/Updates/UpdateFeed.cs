// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer.Updates
{
    /// <summary>
    /// 업데이트를 물을 곳(#36). 보통은 GitHub API 이고, 내려받기·페이지 주소는 https://github.com 만 믿는다.
    ///
    /// 개발용: 환경 변수 SOUNDVISUALIZER_UPDATE_FEED 에 이 PC 의 주소(http://127.0.0.1:포트/… 또는 localhost)를 주면 거기에
    /// 묻고, 같은 호스트·포트의 주소만 믿는다. 실제 릴리스를 올리지 않고 업데이트 전 과정을 이 PC 에서 돌려 보려고 둔다.
    /// 이 PC 밖의 주소는 받지 않는다(환경 변수 하나로 다른 서버의 파일을 믿게 되면 안 된다).
    /// </summary>
    public sealed record UpdateFeed(string ApiUrl, Func<Uri, bool> IsTrusted)
    {
        public const string EnvironmentVariable = "SOUNDVISUALIZER_UPDATE_FEED";

        /// <summary>실제 배포에서 쓰는 곳.</summary>
        public static UpdateFeed GitHub { get; } = new(UpdateClient.LatestReleaseApiUrl, IsGitHubHttps);

        /// <summary>환경 변수 값으로 정한다. 비었거나 이 PC 의 주소가 아니면 GitHub 이다.</summary>
        public static UpdateFeed Resolve(string? environmentValue)
        {
            if (!Uri.TryCreate(environmentValue?.Trim(), UriKind.Absolute, out var feed)) return GitHub;
            if (feed.Scheme != Uri.UriSchemeHttp && feed.Scheme != Uri.UriSchemeHttps) return GitHub;
            if (!feed.IsLoopback) return GitHub;
            return new UpdateFeed(feed.ToString(), uri =>
                uri.Scheme == feed.Scheme && string.Equals(uri.Host, feed.Host, StringComparison.OrdinalIgnoreCase) && uri.Port == feed.Port);
        }

        /// <summary>https 이고 호스트가 github.com 인지. 자산 주소는 github.com 에서 받는 서버로 넘겨진다.</summary>
        public static bool IsGitHubHttps(Uri uri) =>
            uri.Scheme == Uri.UriSchemeHttps && string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase);
    }
}
