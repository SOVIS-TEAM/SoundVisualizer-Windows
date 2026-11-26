// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Text;
using System.Text.Json;

namespace SoundVisualizer.Updates
{
    /// <summary>
    /// 임시 폴더에 푼 새 버전으로 실행 폴더의 파일을 바꾼다(#36).
    ///
    /// 두 단계로 바꾼다. 먼저 새 파일을 모두 "이름.svupdate-new" 로 실행 폴더에 복사한다(지금 파일은 건드리지 않는다). 다 복사하면
    /// 파일마다 지금 것을 "이름.svupdate-old" 로, 새 것을 제자리로 이름만 바꾼다. 이름 바꾸기는 순식간이라, 바꾸는 도중 꺼져도 exe 가
    /// 없거나 반쯤 쓰인 채 남을 틈이 거의 없다. 실행 중인 exe 와 불러온 DLL 도 이름은 바꿀 수 있다.
    ///
    /// 어디까지 바꿨는지는 실행 폴더의 기록 파일(<see cref="JournalName"/>)에 남긴다(디스크에 바로 쓴다). 바꾸는 도중 앱이 꺼지면(정전,
    /// 강제 종료) 기록이 "바꾸는 중" 으로 남고, 다음에 켤 때 <see cref="RecoverAtStartup"/> 가 옛 파일로 되돌린다. 옛 파일은 기록이
    /// "다 바꿈" 일 때만 지운다. 기록이 없으면 아무것도 지우지 않는다(실행 폴더를 뒤지지 않는다).
    ///
    /// 앱을 두 번 켜도 둘이 함께 파일을 옮기지 않게, 바꾸기·되돌리기·정리는 잠금 파일(<see cref="LockName"/>)을 잡은 프로세스만 한다.
    ///
    /// 기록의 꼴은 옛 버전이 쓰고 새 버전이 읽는 약속이라 바꾸지 않는다(UpdateJournalFormatTests).
    /// 배포 파일에 있는 이름만 건드린다. 실행 파일 옆의 설정 파일(settings.json)이나 AI 기록처럼 배포 파일에 없는 것은 그대로다.
    /// </summary>
    public static class UpdateInstaller
    {
        /// <summary>바꾸기 전 파일에 붙이는 꼬리.</summary>
        public const string OldSuffix = ".svupdate-old";

        /// <summary>바꿔 끼우기 전에 새 파일을 복사해 두는 꼬리.</summary>
        public const string NewSuffix = ".svupdate-new";

        /// <summary>되돌릴 때 지울 수 없던 새 파일(실행 중인 exe)에 붙이는 꼬리. 다음에 켤 때 지운다.</summary>
        public const string DiscardSuffix = ".svupdate-discard";

        /// <summary>실행 폴더에 두는 업데이트 기록.</summary>
        public const string JournalName = ".svupdate-journal";

        /// <summary>바꾸기·되돌리기·정리를 한 프로세스만 하게 잡는 잠금 파일. 닫으면 지워진다.</summary>
        public const string LockName = ".svupdate-lock";

        private const string Applying = "applying";
        private const string Applied = "applied";
        private const string Cleanup = "cleanup";

        /// <summary>
        /// <paramref name="files"/>(새 버전 폴더 기준 상대 경로)를 <paramref name="installDirectory"/> 에 넣는다. 성공하면 true.
        /// 실패하면 바꾼 파일을 되돌리고 false 와 원인을 돌려준다. 되돌리기까지 다 못 했으면 기록이 "바꾸는 중" 으로 남아 다음에 켤 때
        /// 마저 되돌린다. 다른 프로세스가 업데이트 중이거나 지난 업데이트가 되돌려지지 않았으면 아무것도 바꾸지 않고 false 다.
        /// </summary>
        public static bool Apply(string stagingDirectory, string installDirectory, IReadOnlyList<string> files, out Exception? error)
        {
            error = null;
            using var guard = TryLock(installDirectory);
            if (guard is null)
            {
                error = new IOException("다른 SoundVisualizer 가 실행 폴더를 바꾸는 중입니다.");
                return false;
            }

            // 바꾸기 전에 남은 정리를 마친다.
            CleanupLocked(installDirectory);
            var prior = ReadJournal(installDirectory);
            if (prior?.State == Applying)
            {
                error = new IOException("지난 업데이트를 아직 되돌리지 못했습니다. 앱을 다시 켜면 되돌립니다.");
                return false;
            }
            var carried = prior?.Leftovers ?? new List<string>();

            try
            {
                ResolveOrphans(installDirectory, files);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                error = ex;
                return false;
            }

            var entries = files.Select(f => new JournalEntry(f, File.Exists(Path.Combine(installDirectory, f)))).ToList();
            try
            {
                WriteJournal(installDirectory, new Journal(Applying, entries, carried));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                error = ex;
                return false;
            }

            try
            {
                // 1. 새 파일을 모두 옆에 복사한다. 이 동안 꺼져도 지금 파일은 그대로다.
                foreach (var entry in entries)
                {
                    string target = Path.Combine(installDirectory, entry.Path);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(Path.Combine(stagingDirectory, entry.Path), target + NewSuffix, overwrite: true);
                }
                // 2. 이름만 바꿔 끼운다.
                foreach (var entry in entries)
                {
                    string target = Path.Combine(installDirectory, entry.Path);
                    if (entry.Existed) File.Move(target, target + OldSuffix);
                    File.Move(target + NewSuffix, target);
                }
                WriteJournal(installDirectory, new Journal(Applied, entries, carried));
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                error = ex;
                var result = Restore(installDirectory, entries, carried);
                if (!result.Complete) error = new AggregateException("되돌리지 못한 파일이 있습니다: " + string.Join(", ", result.Failed), ex);
                return false;
            }
        }

        /// <summary>
        /// 바꾸기를 마쳤지만 새 버전을 띄우지 못했을 때 옛 파일로 되돌린다(옛 버전은 아직 돌고 있다). 다 되돌렸으면 true.
        /// 되돌리기 전에 기록을 "바꾸는 중" 으로 먼저 바꾼다. 그러지 못하면 되돌리지 않는다(그때 실행 폴더는 새 버전으로 온전하고,
        /// 다음에 켜면 새 버전이 열린다). 다 못 되돌렸으면 기록이 "바꾸는 중" 으로 남아 다음에 켤 때 마저 되돌린다.
        /// </summary>
        public static bool Revert(string installDirectory)
        {
            using var guard = TryLock(installDirectory);
            if (guard is null) return false;
            var journal = ReadJournal(installDirectory);
            if (journal is null || journal.State != Applied) return false;
            try
            {
                WriteJournal(installDirectory, journal with { State = Applying });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return false;
            }
            return Restore(installDirectory, journal.Entries, journal.Leftovers).Complete;
        }

        /// <summary>앱을 켤 때 지난 업데이트를 마무리한 결과.</summary>
        public enum StartupOutcome
        {
            /// <summary>할 일이 없다(기록이 없거나, 다른 프로세스가 업데이트 중이다).</summary>
            None,

            /// <summary>옛 파일을 지웠거나, 아직 다 못 지워 뒤에서 다시 지운다(<see cref="CleanupNow"/>).</summary>
            CleanupPending,

            /// <summary>바꾸는 도중 꺼진 업데이트를 옛 파일로 되돌렸다(다 못 했으면 기록이 남아 다음에 다시 해 본다).</summary>
            Recovered,

            /// <summary>되돌렸는데 지금 돌고 있는 exe 가 되돌리기 전의 새 파일이다. 되돌린 exe 로 다시 열어야 한다.</summary>
            RecoveredNeedsRestart,
        }

        /// <summary>
        /// 앱을 켤 때 부른다(설정을 읽기 전). 기록이 "바꾸는 중" 이면 옛 파일로 되돌리고, "다 바꿈"·"정리" 면 옛 파일을 지운다.
        /// <paramref name="runningExecutable"/> 는 지금 돌고 있는 exe 의 전체 경로다.
        /// </summary>
        public static StartupOutcome RecoverAtStartup(string installDirectory, string? runningExecutable)
        {
            using var guard = TryLock(installDirectory);
            if (guard is null) return StartupOutcome.None;
            var journal = ReadJournal(installDirectory);
            if (journal is null) return StartupOutcome.None;
            if (journal.State != Applying)
            {
                CleanupLocked(installDirectory);
                return StartupOutcome.CleanupPending;
            }

            bool exeRestored = runningExecutable != null && journal.Entries.Any(e =>
                string.Equals(Path.GetFullPath(Path.Combine(installDirectory, e.Path)), Path.GetFullPath(runningExecutable), StringComparison.OrdinalIgnoreCase)
                && File.Exists(Path.Combine(installDirectory, e.Path) + OldSuffix));
            var result = Restore(installDirectory, journal.Entries, journal.Leftovers);
            if (!result.Complete) return StartupOutcome.Recovered;
            return exeRestored ? StartupOutcome.RecoveredNeedsRestart : StartupOutcome.Recovered;
        }

        /// <summary>
        /// 기록이 "다 바꿈" 이거나 "정리" 이면 남은 옛 파일을 지운다. 다 지우면 기록도 지우고, 못 지운 것은 기록에 남긴다.
        /// 남은 파일 수를 돌려준다. 기록이 없거나 "바꾸는 중" 이면 아무것도 지우지 않는다. 다른 프로세스가 업데이트 중이면 아무것도
        /// 하지 않고 남은 수만 돌려준다.
        /// </summary>
        public static int CleanupNow(string installDirectory)
        {
            using var guard = TryLock(installDirectory);
            return guard is null ? Math.Max(1, PendingCleanupCount(installDirectory)) : CleanupLocked(installDirectory);
        }

        /// <summary>
        /// 남은 옛 파일을 지우고, 남으면 <paramref name="retryDelays"/> 만큼 기다렸다가 다시 지운다. 옛 버전이 막 끝난 때는 그 실행 파일과
        /// 불러온 DLL 이 잠깐 더 잡혀 있어 첫 시도에서 남는다(이 PC 에서 2.3.0 → 2.3.1 로 바꿀 때 15개 가운데 4개가 남았다).
        /// 다 지웠거나 기다리기가 끝나면 남은 수를 돌려준다. 취소하면 그 자리에서 멈춘다(다음 업데이트가 정리를 넘겨받는다).
        /// </summary>
        public static async Task<int> CleanupWithRetryAsync(string installDirectory, IReadOnlyList<TimeSpan> retryDelays, CancellationToken cancellationToken = default)
        {
            int remaining = CleanupNow(installDirectory);
            foreach (var delay in retryDelays)
            {
                if (remaining == 0) return 0;
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                remaining = CleanupNow(installDirectory);
            }
            return remaining;
        }

        /// <summary>기록에 남은, 아직 지우지 못한 파일 수. 기록이 "바꾸는 중" 이면 0.</summary>
        public static int PendingCleanupCount(string installDirectory)
        {
            var journal = ReadJournal(installDirectory);
            if (journal is null || journal.State == Applying) return 0;
            return CleanupTargets(journal).Count(relative => File.Exists(Path.Combine(installDirectory, relative)));
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

        // ---- 정리 ----

        private static IEnumerable<string> CleanupTargets(Journal journal) => journal.State == Applied
            ? journal.Entries.Where(e => e.Existed).Select(e => e.Path + OldSuffix).Concat(journal.Leftovers).Distinct(StringComparer.OrdinalIgnoreCase)
            : journal.Leftovers.Distinct(StringComparer.OrdinalIgnoreCase);

        private static int CleanupLocked(string installDirectory)
        {
            var journal = ReadJournal(installDirectory);
            if (journal is null || journal.State == Applying) return 0;
            var remaining = new List<string>();
            foreach (string relative in CleanupTargets(journal))
            {
                string path = Path.Combine(installDirectory, relative);
                try
                {
                    if (File.Exists(path)) File.Delete(path);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    remaining.Add(relative);
                }
            }
            try
            {
                if (remaining.Count == 0) DeleteJournal(installDirectory);
                else WriteJournal(installDirectory, new Journal(Cleanup, new List<JournalEntry>(), remaining));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // 기록을 고치지 못하면 다음에 같은 목록으로 다시 지운다.
            }
            return remaining.Count;
        }

        /// <summary>
        /// 기록 없이 남은 꼬리 파일(기록을 지웠거나 기록이 깨진 경우)을 바꾸기 전에 치운다. 그러지 않으면 업데이트가 영영 막힌다.
        /// 지금 파일이 있으면 그 옛 파일은 남는 백업이라 지우고, 지금 파일이 없으면 옛 파일이 유일한 사본이라 제자리로 돌린다.
        /// 복사하다 만 새 파일은 지운다.
        /// </summary>
        private static void ResolveOrphans(string installDirectory, IReadOnlyList<string> files)
        {
            foreach (string relative in files)
            {
                string target = Path.Combine(installDirectory, relative);
                string old = target + OldSuffix;
                if (File.Exists(old))
                {
                    if (File.Exists(target)) File.Delete(old);
                    else File.Move(old, target);
                }
                if (File.Exists(target + NewSuffix)) File.Delete(target + NewSuffix);
            }
        }

        // ---- 되돌리기 ----

        private sealed record RestoreResult(bool Complete, List<string> Failed);

        /// <summary>
        /// <paramref name="entries"/> 를 거꾸로 되돌린다. 복사해 둔 새 파일은 지운다. 옛 파일이 있으면 지금 파일을 치우고(지울 수 없으면 이름을
        /// 바꿔 두고) 옛 파일을 제자리에 둔다. 원래 없던 파일은 지운다. 옛 파일이 없으면 아직 바꾸지 않은 것이라 그대로 둔다(바꾸기 전에
        /// 기록 없는 옛 파일을 치웠다). 다 되돌리면 기록을 "정리"(치워 둔 파일 목록)로 바꾸거나 지우고, 못 한 것이 있으면 "바꾸는 중" 으로
        /// 남긴다. 치워 둔 파일 목록은 앞의 목록에 이어 남긴다.
        /// </summary>
        private static RestoreResult Restore(string installDirectory, IReadOnlyList<JournalEntry> entries, IReadOnlyList<string> priorLeftovers)
        {
            var failed = new List<string>();
            var leftovers = new List<string>(priorLeftovers);
            foreach (var entry in entries.Reverse())
            {
                string target = Path.Combine(installDirectory, entry.Path);
                string old = target + OldSuffix;
                try
                {
                    string fresh = target + NewSuffix;
                    if (File.Exists(fresh))
                    {
                        try { File.Delete(fresh); }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { leftovers.Add(entry.Path + NewSuffix); }
                    }
                    if (entry.Existed)
                    {
                        if (!File.Exists(old)) continue;
                        if (File.Exists(target) && !Remove(target)) leftovers.Add(entry.Path + DiscardSuffix);
                        File.Move(old, target);
                    }
                    else if (File.Exists(target) && !Remove(target))
                    {
                        leftovers.Add(entry.Path + DiscardSuffix);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    failed.Add(entry.Path);
                }
            }

            var distinct = leftovers.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            try
            {
                if (failed.Count > 0) WriteJournal(installDirectory, new Journal(Applying, entries.ToList(), distinct));
                else if (distinct.Count > 0) WriteJournal(installDirectory, new Journal(Cleanup, new List<JournalEntry>(), distinct));
                else DeleteJournal(installDirectory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failed.Add(JournalName);
            }
            return new RestoreResult(failed.Count == 0, failed);
        }

        /// <summary>파일을 지운다. 지울 수 없으면(실행 중인 exe, 읽기 전용) 이름을 바꿔 치우고 false.</summary>
        private static bool Remove(string path)
        {
            try
            {
                File.Delete(path);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                string discard = path + DiscardSuffix;
                if (File.Exists(discard)) File.Delete(discard);
                File.Move(path, discard);
                return false;
            }
        }

        // ---- 잠금 ----

        /// <summary>잠금 파일을 연다. 다른 프로세스가 쥐고 있으면 null. 닫으면 파일이 지워진다.</summary>
        private static FileStream? TryLock(string installDirectory)
        {
            try
            {
                return new FileStream(Path.Combine(installDirectory, LockName), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }

        // ---- 기록 ----

        private sealed record JournalEntry(string Path, bool Existed);

        private sealed record Journal(string State, List<JournalEntry> Entries, List<string> Leftovers);

        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        private static string JournalPath(string installDirectory) => Path.Combine(installDirectory, JournalName);

        private static void WriteJournal(string installDirectory, Journal journal)
        {
            // 쓰다가 꺼져도 반쪽짜리 기록이 남지 않게 옆 파일에 디스크까지 써 두고 바꿔 끼운다.
            string path = JournalPath(installDirectory);
            string temp = path + ".tmp";
            byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(journal, JsonOptions));
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temp, path, overwrite: true);
        }

        private static void DeleteJournal(string installDirectory)
        {
            string path = JournalPath(installDirectory);
            if (File.Exists(path)) File.Delete(path);
        }

        /// <summary>기록을 읽는다. 없거나 읽을 수 없거나 값이 빠졌으면 null(그런 기록으로는 아무것도 지우거나 되돌리지 않는다).</summary>
        private static Journal? ReadJournal(string installDirectory)
        {
            string path = JournalPath(installDirectory);
            try
            {
                if (!File.Exists(path)) return null;
                var journal = JsonSerializer.Deserialize<Journal>(File.ReadAllText(path));
                if (journal is null || journal.Entries is null || journal.Leftovers is null) return null;
                if (journal.State is not (Applying or Applied or Cleanup)) return null;
                if (journal.Entries.Any(e => e is null || string.IsNullOrEmpty(e.Path)) || journal.Leftovers.Any(string.IsNullOrEmpty)) return null;
                // 기록에 든 경로도 실행 폴더 밖을 가리키면 쓰지 않는다.
                foreach (string relative in journal.Entries.Select(e => e.Path).Concat(journal.Leftovers))
                {
                    UpdatePackage.SafeRelativePath(relative);
                }
                return journal;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidUpdatePackageException or NotSupportedException)
            {
                return null;
            }
        }
    }
}
