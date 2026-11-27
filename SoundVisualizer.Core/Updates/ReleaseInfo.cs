// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SoundVisualizer.Updates
{
    /// <summary>앱이 직접 내려받을 수 있는 릴리스 파일(zip). 크기와 SHA-256 은 GitHub 이 자산마다 알려 주는 값이다.</summary>
    public sealed record ReleaseAsset(string Name, Uri DownloadUrl, long Size, string Sha256Hex);

    /// <summary>앱 안의 업데이트가 받을 zip 의 종류(#66).</summary>
    public enum UpdatePackageKind
    {
        /// <summary>기본 버전 zip(SoundVisualizer_v버전.zip): exe 옆에 DLL 과 AIModel 폴더가 있다.</summary>
        Standard,

        /// <summary>포터블 zip(SoundVisualizer_v버전_portable.zip): 실행 파일 하나다. 포터블 판이 받는다.</summary>
        Portable,
    }

    /// <summary>
    /// GitHub 최신 릴리스 하나(#36). <see cref="Asset"/> 이 없으면 새 버전이 있다는 것만 알고, 받기는 릴리스 페이지에서 한다.
    /// </summary>
    public sealed record ReleaseInfo(Version Version, string Tag, Uri PageUrl, ReleaseAsset? Asset);

    /// <summary>
    /// GitHub REST API 의 "최신 릴리스" 응답(releases/latest)에서 앱이 쓸 값만 읽는다.
    ///
    /// 응답은 인터넷에서 온 값이라 믿지 않는다. 주소는 https 이고 github.com 이어야 하며, 자산 이름은 우리가 올리는 꼴
    /// (SoundVisualizer_v2.2.zip)이어야 하고, SHA-256 이 있어야 앱이 받는다. 하나라도 어긋나면 그 자산은 쓰지 않고
    /// 릴리스 페이지만 안내한다. 초안·시험판은 새 버전으로 치지 않는다.
    /// </summary>
    public static class ReleaseParser
    {
        // 릴리스에 올리는 기본 버전 zip 이름. Chocolatey 패키지와 README 가 같은 이름을 쓴다.
        private static readonly Regex AssetName = new(@"^SoundVisualizer_v\d{1,4}(\.\d{1,5}){1,3}\.zip$", RegexOptions.CultureInvariant);
        // 함께 올리는 포터블 zip 이름(#58, #64). 포터블 판만 이것을 받는다(#66).
        private static readonly Regex PortableAssetName = new(@"^SoundVisualizer_v\d{1,4}(\.\d{1,5}){1,3}_portable\.zip$", RegexOptions.CultureInvariant);
        private static readonly Regex Sha256Digest = new(@"^sha256:([0-9a-fA-F]{64})$", RegexOptions.CultureInvariant);

        /// <summary>
        /// 쓸 수 있는 릴리스면 그 정보, 아니면(형식이 다름, 초안, 시험판, 버전 없는 태그) null.
        /// <paramref name="isTrusted"/> 는 믿을 주소인지 정한다. 주지 않으면 https://github.com 만 믿는다(<see cref="UpdateFeed"/>).
        /// <paramref name="kind"/> 는 받을 zip 의 종류다. 포터블 판은 포터블 zip 을 고르고, 그 릴리스에 포터블 zip 이 없으면 기본 zip 을 받는다.
        /// </summary>
        public static ReleaseInfo? Parse(string json, Func<Uri, bool>? isTrusted = null, UpdatePackageKind kind = UpdatePackageKind.Standard)
        {
            var trusted = isTrusted ?? UpdateFeed.IsGitHubHttps;
            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object) return null;
                if (Bool(root, "draft") || Bool(root, "prerelease")) return null;

                string? tag = String(root, "tag_name");
                var version = UpdateVersion.FromTag(tag);
                var page = TrustedUri(String(root, "html_url"), trusted);
                if (tag is null || version is null || page is null) return null;

                return new ReleaseInfo(version, tag, page, FindAsset(root, trusted, kind));
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static ReleaseAsset? FindAsset(JsonElement release, Func<Uri, bool> trusted, UpdatePackageKind kind)
        {
            if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) return null;
            // 포터블 zip 이 있는데 믿을 수 없으면 기본 zip 으로 넘어가지 않고 페이지만 안내한다(다른 종류로 몰래 바꾸지 않는다).
            if (kind == UpdatePackageKind.Portable && TryFind(assets, PortableAssetName, trusted, out var portable)) return portable;
            return TryFind(assets, AssetName, trusted, out var standard) ? standard : null;
        }

        /// <summary>
        /// 이름이 맞는 첫 자산을 찾으면 true 다. 그 자산의 주소·SHA-256·크기를 믿을 수 없으면 <paramref name="asset"/> 은 null 이다.
        /// 이름이 맞는 자산이 없으면 false.
        /// </summary>
        private static bool TryFind(JsonElement assets, Regex assetName, Func<Uri, bool> trusted, out ReleaseAsset? asset)
        {
            asset = null;
            foreach (var item in assets.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                string? name = String(item, "name");
                if (name is null || !assetName.IsMatch(name)) continue;

                var url = TrustedUri(String(item, "browser_download_url"), trusted);
                var digest = Sha256Digest.Match(String(item, "digest") ?? "");
                long size = item.TryGetProperty("size", out var s) && s.ValueKind == JsonValueKind.Number && s.TryGetInt64(out long n) ? n : 0;
                if (url is not null && digest.Success && size > 0)
                {
                    asset = new ReleaseAsset(name, url, size, digest.Groups[1].Value.ToLowerInvariant());
                }
                return true;
            }
            return false;
        }

        private static Uri? TrustedUri(string? text, Func<Uri, bool> trusted) =>
            Uri.TryCreate(text, UriKind.Absolute, out var uri) && trusted(uri) ? uri : null;

        private static string? String(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        private static bool Bool(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
    }
}
