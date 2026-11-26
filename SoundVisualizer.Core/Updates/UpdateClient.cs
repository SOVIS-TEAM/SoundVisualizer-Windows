// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace SoundVisualizer.Updates
{
    /// <summary>업데이트 확인 결과.</summary>
    public enum UpdateCheckOutcome
    {
        /// <summary>지금 버전이 최신이다.</summary>
        Latest,

        /// <summary>새 버전이 있다.</summary>
        Available,

        /// <summary>GitHub 에 묻지 못했거나 답을 읽지 못했다(인터넷 끊김, 시간 초과, 요청 한도).</summary>
        Failed,
    }

    public sealed record UpdateCheckResult(UpdateCheckOutcome Outcome, ReleaseInfo? Release);

    /// <summary>내려받은 파일의 크기나 SHA-256 이 GitHub 이 알려 준 값과 다르다.</summary>
    public sealed class UpdateVerificationException(string message) : Exception(message);

    /// <summary>
    /// GitHub 에 최신 릴리스를 묻고 zip 을 내려받는다(#36). 보내는 것은 요청 자체(주소, 앱 이름과 버전이 든 User-Agent)뿐이다.
    /// 설정, 소리, 사용자 정보는 보내지 않는다.
    ///
    /// 시험에서 가짜 응답을 쓰도록 HttpClient 를 받는다. 앱은 하나를 만들어 계속 쓴다.
    /// </summary>
    public sealed class UpdateClient
    {
        /// <summary>최신 릴리스를 묻는 주소. 초안과 시험판은 GitHub 이 빼고 답한다.</summary>
        public const string LatestReleaseApiUrl = "https://api.github.com/repos/SOVIS-Team/SoundVisualizer-Windows/releases/latest";

        private readonly HttpClient _http;
        private readonly Uri _latestUrl;
        private readonly Func<Uri, bool> _isTrusted;

        /// <summary><paramref name="feed"/> 를 주지 않으면 GitHub 에 묻는다(<see cref="UpdateFeed.GitHub"/>).</summary>
        public UpdateClient(HttpClient http, Version currentVersion, UpdateFeed? feed = null)
        {
            var source = feed ?? UpdateFeed.GitHub;
            _http = http;
            _latestUrl = new Uri(source.ApiUrl);
            _isTrusted = source.IsTrusted;
            CurrentVersion = UpdateVersion.Normalize(currentVersion);
        }

        public Version CurrentVersion { get; }

        /// <summary>GitHub API 가 요구하는 User-Agent. 앱 이름과 버전만 적는다.</summary>
        public static ProductInfoHeaderValue UserAgent(Version version) => new("SoundVisualizer-Windows", UpdateVersion.Display(version));

        /// <summary>최신 릴리스를 묻는다. 예외를 던지지 않고 실패는 <see cref="UpdateCheckOutcome.Failed"/> 로 돌려준다(취소만 그대로 던진다).</summary>
        public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, _latestUrl);
                request.Headers.UserAgent.Add(UserAgent(CurrentVersion));
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
                request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
                using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) return new UpdateCheckResult(UpdateCheckOutcome.Failed, null);

                string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var release = ReleaseParser.Parse(json, _isTrusted);
                if (release is null) return new UpdateCheckResult(UpdateCheckOutcome.Failed, null);
                return UpdateVersion.IsNewer(release.Version, CurrentVersion)
                    ? new UpdateCheckResult(UpdateCheckOutcome.Available, release)
                    : new UpdateCheckResult(UpdateCheckOutcome.Latest, release);
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException
                                        || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
            {
                // 시간 초과는 TaskCanceledException 으로 온다. 호출한 쪽이 취소한 것만 위로 올린다.
                return new UpdateCheckResult(UpdateCheckOutcome.Failed, null);
            }
        }

        /// <summary>
        /// <paramref name="asset"/> 을 <paramref name="targetFile"/> 에 내려받고, 크기와 SHA-256 이 GitHub 이 알려 준 값과 같은지 본다.
        /// 다르면 파일을 지우고 <see cref="UpdateVerificationException"/> 을 던진다. 받는 중에는 0~1 의 진행률을 알린다.
        /// </summary>
        public async Task DownloadAsync(ReleaseAsset asset, string targetFile, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            // 확인한 응답에서 온 주소만 받는다. 다른 곳의 자산을 건네받아도 내려받지 않는다.
            if (!_isTrusted(asset.DownloadUrl)) throw new UpdateVerificationException("믿을 수 없는 주소입니다: " + asset.DownloadUrl);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(targetFile))!);
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, asset.DownloadUrl);
                request.Headers.UserAgent.Add(UserAgent(CurrentVersion));
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                long received = 0;
                await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
                await using (var output = new FileStream(targetFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                {
                    var buffer = new byte[81920];
                    int read;
                    while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        received += read;
                        if (received > asset.Size) throw new UpdateVerificationException("알려 준 크기보다 큽니다.");
                        hash.AppendData(buffer, 0, read);
                        await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                        progress?.Report((double)received / asset.Size);
                    }
                }

                if (received != asset.Size) throw new UpdateVerificationException($"크기가 다릅니다: {received} / {asset.Size}");
                string actual = Convert.ToHexStringLower(hash.GetHashAndReset());
                if (!string.Equals(actual, asset.Sha256Hex, StringComparison.OrdinalIgnoreCase))
                {
                    throw new UpdateVerificationException("SHA-256 이 다릅니다.");
                }
            }
            catch
            {
                TryDelete(targetFile);
                throw;
            }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }
}
