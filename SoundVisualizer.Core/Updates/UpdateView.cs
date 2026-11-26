// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer.Updates
{
    /// <summary>업데이트가 어디까지 왔는지. 홈 안내와 도움말의 앱 정보가 같은 값을 본다(#36).</summary>
    public enum UpdatePhase
    {
        Idle,
        Checking,
        Latest,
        Available,
        Downloading,
        Installing,
        Failed,

        /// <summary>방금 새 버전으로 바뀌어 열렸다(옛 버전이 --updated-from 으로 띄웠다).</summary>
        Updated,
    }

    /// <summary><see cref="UpdatePhase.Failed"/> 의 이유. 이유마다 안내와 다음에 누를 버튼이 다르다.</summary>
    public enum UpdateProblem
    {
        None,

        /// <summary>GitHub 에 묻지 못했다.</summary>
        Check,

        /// <summary>내려받지 못했다.</summary>
        Download,

        /// <summary>내려받은 파일의 크기·SHA-256·zip 꼴·실행 파일 버전이 맞지 않았다.</summary>
        Verify,

        /// <summary>실행 폴더의 파일을 바꾸지 못해 되돌렸거나, 새 버전을 띄우지 못해 되돌렸다. 지금 버전 그대로다.</summary>
        Apply,

        /// <summary>
        /// 새 버전을 띄우지 못했는데 옛 파일로 다 되돌리지도 못했다. 다음에 켤 때 마저 되돌린다(되돌리기 전에 기록을 고치지 못했으면
        /// 실행 폴더는 새 버전으로 온전하고, 다음에 켜면 새 버전이 열린다). 어느 쪽이든 앱을 닫고 다시 열면 된다.
        /// </summary>
        Restart,

        /// <summary>실행 폴더에 쓸 수 없다.</summary>
        ReadOnly,
    }

    /// <summary>화면이 그릴 때 보는 업데이트 상태 한 벌.</summary>
    public sealed record UpdateSnapshot(
        UpdateChannel Channel,
        UpdatePhase Phase,
        UpdateProblem Problem,
        ReleaseInfo? Release,
        Version Current,
        bool Busy,
        bool HomeDismissed,
        int Percent);

    /// <summary>상태 글 한 조각. 키는 문자열 표의 키이고, 인자는 {0} 자리에 들어간다.</summary>
    public sealed record UpdateText(string Key, params object[] Args);

    /// <summary>
    /// 업데이트 상태에서 무엇을 보일지 정한다(#36). 화면(WPF) 없이 시험할 수 있게 Core 에 둔다.
    /// </summary>
    public static class UpdateView
    {
        /// <summary>지금보다 새 버전을 알고 있는지.</summary>
        public static bool KnowsNewer(UpdateSnapshot s) => s.Release != null && UpdateVersion.IsNewer(s.Release.Version, s.Current);

        /// <summary>
        /// "지금 업데이트" 를 누를 수 있는지. 받을 zip 이 있는 새 버전을 알고, 새 버전을 찾았거나 확인·내려받기·검증에서 실패했을 때다
        /// (다시 해 볼 수 있다). 바꾸다 실패했거나 쓸 수 없는 폴더면 받기 페이지로 보낸다.
        /// </summary>
        public static bool CanInstall(UpdateSnapshot s) =>
            s.Channel == UpdateChannel.Portable && !s.Busy && s.Release?.Asset != null && KnowsNewer(s)
            && (s.Phase == UpdatePhase.Available
                || (s.Phase == UpdatePhase.Failed && s.Problem is UpdateProblem.Check or UpdateProblem.Download or UpdateProblem.Verify));

        /// <summary>
        /// 받기 페이지를 안내할지. 앱이 받을 zip 이 없는 새 버전, 확인·검증·바꾸기가 실패했거나 쓸 수 없는 폴더일 때다.
        /// 검증 실패(예: 릴리스의 실행 파일 버전이 태그와 다름)는 다시 받아도 같을 수 있어 페이지도 함께 보인다.
        /// </summary>
        public static bool ShowsDownloadPage(UpdateSnapshot s) =>
            s.Channel == UpdateChannel.Portable && !s.Busy
            && ((s.Phase == UpdatePhase.Available && s.Release?.Asset == null)
                || (s.Phase == UpdatePhase.Failed && s.Problem is UpdateProblem.Check or UpdateProblem.Verify or UpdateProblem.Apply or UpdateProblem.ReadOnly));

        /// <summary>홈에 안내를 보일지. 저절로 확인한 결과 가운데 최신이라는 답과 확인 실패는 홈에 보이지 않는다(도움말에서 볼 수 있다).</summary>
        public static bool ShowsOnHome(UpdateSnapshot s) =>
            s.Channel == UpdateChannel.Portable && !s.HomeDismissed && s.Phase switch
            {
                UpdatePhase.Available or UpdatePhase.Downloading or UpdatePhase.Installing or UpdatePhase.Updated => true,
                UpdatePhase.Failed => s.Problem != UpdateProblem.Check,
                _ => false,
            };

        /// <summary>앱을 끝내면 안 되는 때(실행 폴더의 파일을 바꾸는 중).</summary>
        public static bool BlocksExit(UpdateSnapshot s) => s.Phase == UpdatePhase.Installing;

        /// <summary>지금 상태를 알리는 글. 알릴 것이 없으면 빈 목록. 조각은 띄어 써서 잇는다.</summary>
        public static IReadOnlyList<UpdateText> Status(UpdateSnapshot s)
        {
            string? available = s.Release != null ? UpdateVersion.Display(s.Release.Version) : null;
            return s.Phase switch
            {
                UpdatePhase.Checking => new[] { new UpdateText("update.checking") },
                UpdatePhase.Latest => new[] { new UpdateText("update.latest") },
                UpdatePhase.Available when available != null && s.Release!.Asset == null =>
                    new[] { new UpdateText("update.available", available), new UpdateText("update.manual") },
                UpdatePhase.Available when available != null => new[] { new UpdateText("update.available", available) },
                UpdatePhase.Downloading => new[] { new UpdateText("update.downloading", s.Percent) },
                UpdatePhase.Installing => new[] { new UpdateText("update.installing") },
                UpdatePhase.Updated => new[] { new UpdateText("update.done", UpdateVersion.Display(s.Current)) },
                UpdatePhase.Failed => s.Problem switch
                {
                    UpdateProblem.Check => new[] { new UpdateText("update.failed_check") },
                    UpdateProblem.Download => new[] { new UpdateText("update.failed_download") },
                    UpdateProblem.Verify => new[] { new UpdateText("update.failed_verify") },
                    UpdateProblem.Apply => new[] { new UpdateText("update.failed_apply") },
                    UpdateProblem.Restart => new[] { new UpdateText("update.failed_restart") },
                    UpdateProblem.ReadOnly => new[] { new UpdateText("update.readonly") },
                    _ => Array.Empty<UpdateText>(),
                },
                _ => Array.Empty<UpdateText>(),
            };
        }
    }
}
