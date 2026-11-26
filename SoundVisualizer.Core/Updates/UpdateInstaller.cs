// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer.Updates
{
    /// <summary>
    /// 임시 폴더에 푼 새 버전으로 실행 폴더의 파일을 바꾼다(#36).
    ///
    /// 실행 중인 exe 와 불러온 DLL 은 덮어쓸 수 없지만 이름은 바꿀 수 있다. 그래서 파일마다 지금 것을 "이름.svupdate-old" 로 옮기고
    /// 새 것을 제자리에 둔다. 새 버전이 시작하면 <see cref="CleanupOld"/> 가 옛 파일을 지운다. 중간에 하나라도 실패하면 이미 바꾼 파일을
    /// 거꾸로 되돌려, 실행 폴더는 업데이트 전과 같아진다.
    ///
    /// 배포 파일에 있는 이름만 건드린다. 실행 파일 옆의 설정 파일(settings.json)이나 AI 기록처럼 배포 파일에 없는 것은 그대로다.
    /// </summary>
    public static class UpdateInstaller
    {
        /// <summary>바꾸기 전 파일에 붙이는 꼬리. 이 꼬리가 붙은 파일만 지운다(사용자가 둔 ".old" 파일은 건드리지 않는다).</summary>
        public const string OldSuffix = ".svupdate-old";

        /// <summary>
        /// <paramref name="files"/>(새 버전 폴더 기준 상대 경로)를 <paramref name="installDirectory"/> 에 넣는다. 성공하면 true.
        /// 실패하면 바꾼 파일을 되돌리고 false 와 원인을 돌려준다. 되돌리기까지 실패한 파일은 <paramref name="error"/> 에 적힌다.
        /// </summary>
        public static bool Apply(string stagingDirectory, string installDirectory, IReadOnlyList<string> files, out Exception? error)
        {
            error = null;
            var done = new Stack<(string Target, string? Old)>();
            try
            {
                foreach (string relative in files)
                {
                    string source = Path.Combine(stagingDirectory, relative);
                    string target = Path.Combine(installDirectory, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);

                    string? old = null;
                    if (File.Exists(target))
                    {
                        old = target + OldSuffix;
                        // 지난 업데이트의 옛 파일이 아직 남아 있으면(지우지 못한 경우) 먼저 지운다. 지울 수 없으면 여기서 멈춘다.
                        if (File.Exists(old)) File.Delete(old);
                        File.Move(target, old);
                    }
                    done.Push((target, old));
                    File.Copy(source, target, overwrite: false);
                }
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                error = ex;
                var failures = Rollback(done);
                if (failures.Count > 0) error = new AggregateException("되돌리지 못한 파일이 있습니다: " + string.Join(", ", failures), ex);
                return false;
            }
        }

        private static List<string> Rollback(Stack<(string Target, string? Old)> done)
        {
            var failures = new List<string>();
            while (done.Count > 0)
            {
                var (target, old) = done.Pop();
                try
                {
                    if (File.Exists(target)) File.Delete(target);
                    if (old != null && File.Exists(old)) File.Move(old, target);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    failures.Add(target);
                }
            }
            return failures;
        }

        /// <summary>
        /// 지난 업데이트가 남긴 옛 파일(<see cref="OldSuffix"/>)을 지운다. 옛 버전이 아직 끝나는 중이면 지우지 못하고, 다음에 켤 때 다시 지운다.
        /// 지운 개수를 돌려준다.
        /// </summary>
        public static int CleanupOld(string installDirectory)
        {
            if (!Directory.Exists(installDirectory)) return 0;
            int removed = 0;
            IEnumerable<string> leftovers;
            try
            {
                leftovers = Directory.EnumerateFiles(installDirectory, "*" + OldSuffix, SearchOption.AllDirectories).ToList();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return 0;
            }
            foreach (string file in leftovers)
            {
                // "*.svupdate-old" 검색은 짧은 이름(8.3)으로도 맞을 수 있어 꼬리를 다시 본다.
                if (!file.EndsWith(OldSuffix, StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    File.Delete(file);
                    removed++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // 다음에 다시 해 본다.
                }
            }
            return removed;
        }

        /// <summary>아직 지우지 못한 옛 파일(<see cref="OldSuffix"/>)의 수.</summary>
        public static int CountOld(string installDirectory)
        {
            if (!Directory.Exists(installDirectory)) return 0;
            try
            {
                return Directory.EnumerateFiles(installDirectory, "*" + OldSuffix, SearchOption.AllDirectories)
                    .Count(f => f.EndsWith(OldSuffix, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return 0;
            }
        }

        /// <summary>
        /// 옛 파일을 지우고, 남으면 <paramref name="retryDelays"/> 만큼 기다렸다가 다시 지운다. 옛 버전이 막 끝난 때는 그 실행 파일과
        /// 불러온 DLL 이 잠깐 더 잡혀 있어 첫 시도에서 남는다(이 PC 에서 2.3.0 → 2.3.1 로 바꿀 때 15개 가운데 4개가 남았다).
        /// 다 지웠거나 기다리기가 끝나면 남은 수를 돌려준다.
        /// </summary>
        public static async Task<int> CleanupOldWithRetryAsync(string installDirectory, IReadOnlyList<TimeSpan> retryDelays, CancellationToken cancellationToken = default)
        {
            CleanupOld(installDirectory);
            foreach (var delay in retryDelays)
            {
                int remaining = CountOld(installDirectory);
                if (remaining == 0) return 0;
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                CleanupOld(installDirectory);
            }
            return CountOld(installDirectory);
        }

        /// <summary>실행 폴더에 파일을 만들고 이름을 바꿀 수 있는지. Program Files 처럼 쓸 수 없는 곳이면 앱이 직접 바꾸지 않는다.</summary>
        public static bool CanWrite(string installDirectory)
        {
            string probe = Path.Combine(installDirectory, ".svupdate-probe-" + Guid.NewGuid().ToString("N"));
            try
            {
                File.WriteAllBytes(probe, Array.Empty<byte>());
                File.Move(probe, probe + OldSuffix);
                File.Delete(probe + OldSuffix);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                try { if (File.Exists(probe)) File.Delete(probe); } catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException) { }
                return false;
            }
        }
    }
}
