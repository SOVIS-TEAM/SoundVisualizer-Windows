// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Text.Json;

namespace SoundVisualizer.Updates
{
    /// <summary>
    /// 임시 폴더에 푼 새 버전으로 실행 폴더의 파일을 바꾼다(#36).
    ///
    /// 실행 중인 exe 와 불러온 DLL 은 덮어쓸 수 없지만 이름은 바꿀 수 있다. 그래서 파일마다 지금 것을 "이름.svupdate-old" 로 옮기고
    /// 새 것을 제자리에 둔다. 중간에 하나라도 실패하면 이미 바꾼 파일을 거꾸로 되돌려, 실행 폴더는 업데이트 전과 같아진다.
    ///
    /// 어디까지 바꿨는지는 실행 폴더의 기록 파일(<see cref="JournalName"/>)에 남긴다. 바꾸는 도중 앱이 꺼지면(정전, 강제 종료) 기록이
    /// "바꾸는 중" 으로 남고, 다음에 켤 때 <see cref="RecoverAtStartup"/> 가 옛 파일로 되돌린다. 옛 파일은 기록이 "다 바꿈" 일 때만
    /// 지운다. 기록이 없으면 아무것도 지우지 않는다(실행 폴더를 뒤지지 않는다).
    ///
    /// 배포 파일에 있는 이름만 건드린다. 실행 파일 옆의 설정 파일(settings.json)이나 AI 기록처럼 배포 파일에 없는 것은 그대로다.
    /// </summary>
    public static class UpdateInstaller
    {
        /// <summary>바꾸기 전 파일에 붙이는 꼬리.</summary>
        public const string OldSuffix = ".svupdate-old";

        /// <summary>되돌릴 때 지울 수 없던 새 파일(실행 중인 exe)에 붙이는 꼬리. 다음에 켤 때 지운다.</summary>
        public const string DiscardSuffix = ".svupdate-discard";

        /// <summary>실행 폴더에 두는 업데이트 기록.</summary>
        public const string JournalName = ".svupdate-journal";

        private const string Applying = "applying";
        private const string Applied = "applied";
        private const string Cleanup = "cleanup";

        /// <summary>
        /// <paramref name="files"/>(새 버전 폴더 기준 상대 경로)를 <paramref name="installDirectory"/> 에 넣는다. 성공하면 true.
        /// 실패하면 바꾼 파일을 되돌리고 false 와 원인을 돌려준다. 되돌리기까지 다 못 했으면 기록이 "바꾸는 중" 으로 남아 다음에 켤 때
        /// 마저 되돌린다. 지난 업데이트의 옛 파일을 지우지 못했으면 아무것도 바꾸지 않고 false 다.
        /// </summary>
        public static bool Apply(string stagingDirectory, string installDirectory, IReadOnlyList<string> files, out Exception? error)
        {
            error = null;
            // 바꾸기 전에 남은 정리를 마친다. 옛 파일이 남아 있으면 이번 업데이트의 백업과 섞이므로 시작하지 않는다.
            CleanupNow(installDirectory);
            var stale = files.Where(f => File.Exists(Path.Combine(installDirectory, f) + OldSuffix)).ToList();
            if (stale.Count > 0)
            {
                error = new IOException("지난 업데이트의 옛 파일을 지우지 못했습니다: " + string.Join(", ", stale));
                return false;
            }

            var entries = files.Select(f => new JournalEntry(f, File.Exists(Path.Combine(installDirectory, f)))).ToList();
            try
            {
                WriteJournal(installDirectory, new Journal(Applying, entries, new List<string>()));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                error = ex;
                return false;
            }

            try
            {
                foreach (var entry in entries)
                {
                    string source = Path.Combine(stagingDirectory, entry.Path);
                    string target = Path.Combine(installDirectory, entry.Path);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    if (entry.Existed) File.Move(target, target + OldSuffix);
                    File.Copy(source, target, overwrite: false);
                }
                WriteJournal(installDirectory, new Journal(Applied, entries, new List<string>()));
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                error = ex;
                var result = Restore(installDirectory, entries);
                if (!result.Complete) error = new AggregateException("되돌리지 못한 파일이 있습니다: " + string.Join(", ", result.Failed), ex);
                return false;
            }
        }

        /// <summary>
        /// 바꾸기를 마쳤지만 새 버전을 띄우지 못했을 때 옛 파일로 되돌린다(옛 버전은 아직 돌고 있다). 다 되돌렸으면 true.
        /// 다 못 했으면 기록을 "바꾸는 중" 으로 남겨 다음에 켤 때 마저 되돌린다.
        /// </summary>
        public static bool Revert(string installDirectory)
        {
            var journal = ReadJournal(installDirectory);
            if (journal is null || journal.State != Applied) return false;
            return Restore(installDirectory, journal.Entries).Complete;
        }

        /// <summary>앱을 켤 때 지난 업데이트를 마무리한 결과.</summary>
        public enum StartupOutcome
        {
            /// <summary>할 일이 없다.</summary>
            None,

            /// <summary>옛 파일을 지웠거나, 아직 다 못 지워 뒤에서 다시 지운다(<see cref="CleanupNow"/>).</summary>
            CleanupPending,

            /// <summary>바꾸는 도중 꺼진 업데이트를 옛 파일로 되돌렸다.</summary>
            Recovered,

            /// <summary>되돌렸는데 지금 돌고 있는 exe 가 되돌리기 전의 새 파일이다. 되돌린 exe 로 다시 열어야 한다.</summary>
            RecoveredNeedsRestart,
        }

        /// <summary>
        /// 앱을 켤 때 부른다(설정을 읽기 전). 기록이 "바꾸는 중" 이면 옛 파일로 되돌리고, "다 바꿈" 이면 옛 파일을 지운다.
        /// <paramref name="runningExecutable"/> 는 지금 돌고 있는 exe 의 전체 경로다.
        /// </summary>
        public static StartupOutcome RecoverAtStartup(string installDirectory, string? runningExecutable)
        {
            var journal = ReadJournal(installDirectory);
            if (journal is null) return StartupOutcome.None;
            if (journal.State != Applying)
            {
                CleanupNow(installDirectory);
                return StartupOutcome.CleanupPending;
            }

            bool exeRestored = runningExecutable != null && journal.Entries.Any(e =>
                string.Equals(Path.GetFullPath(Path.Combine(installDirectory, e.Path)), Path.GetFullPath(runningExecutable), StringComparison.OrdinalIgnoreCase)
                && File.Exists(Path.Combine(installDirectory, e.Path) + OldSuffix));
            var result = Restore(installDirectory, journal.Entries);
            if (!result.Complete) return StartupOutcome.Recovered; // 기록이 남아 다음에 다시 해 본다
            return exeRestored ? StartupOutcome.RecoveredNeedsRestart : StartupOutcome.Recovered;
        }

        /// <summary>
        /// 기록이 "다 바꿈" 이거나 "정리" 이면 남은 옛 파일을 지운다. 다 지우면 기록도 지우고, 못 지운 것은 기록에 남긴다.
        /// 남은 파일 수를 돌려준다. 기록이 없거나 "바꾸는 중" 이면 아무것도 지우지 않는다.
        /// </summary>
        public static int CleanupNow(string installDirectory)
        {
            var journal = ReadJournal(installDirectory);
            if (journal is null || journal.State == Applying) return 0;
            var targets = journal.State == Applied
                ? journal.Entries.Where(e => e.Existed).Select(e => e.Path + OldSuffix).Concat(journal.Leftovers)
                : journal.Leftovers;
            var remaining = new List<string>();
            foreach (string relative in targets.Distinct(StringComparer.OrdinalIgnoreCase))
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
            return journal.State == Applied
                ? journal.Entries.Count(e => e.Existed && File.Exists(Path.Combine(installDirectory, e.Path) + OldSuffix)) + journal.Leftovers.Count
                : journal.Leftovers.Count;
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

        // ---- 되돌리기 ----

        private sealed record RestoreResult(bool Complete, List<string> Failed);

        /// <summary>
        /// <paramref name="entries"/> 를 거꾸로 되돌린다. 옛 파일이 있으면 지금 파일을 치우고(지울 수 없으면 이름을 바꿔 두고) 옛 파일을 제자리에
        /// 둔다. 원래 없던 파일은 지운다. 옛 파일이 없으면 아직 바꾸지 않은 것이라 그대로 둔다(바꾸기 전에 옛 파일이 남지 않았음을 확인했다).
        /// 다 되돌리면 기록을 "정리"(치워 둔 파일 목록)로 바꾸거나 지우고, 못 한 것이 있으면 "바꾸는 중" 으로 남긴다.
        /// </summary>
        private static RestoreResult Restore(string installDirectory, IReadOnlyList<JournalEntry> entries)
        {
            var failed = new List<string>();
            var discarded = new List<string>();
            foreach (var entry in entries.Reverse())
            {
                string target = Path.Combine(installDirectory, entry.Path);
                string old = target + OldSuffix;
                try
                {
                    if (entry.Existed)
                    {
                        if (!File.Exists(old)) continue;
                        if (File.Exists(target) && !Remove(target)) discarded.Add(entry.Path + DiscardSuffix);
                        File.Move(old, target);
                    }
                    else if (File.Exists(target) && !Remove(target))
                    {
                        discarded.Add(entry.Path + DiscardSuffix);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    failed.Add(entry.Path);
                }
            }

            try
            {
                if (failed.Count > 0) WriteJournal(installDirectory, new Journal(Applying, entries.ToList(), discarded));
                else if (discarded.Count > 0) WriteJournal(installDirectory, new Journal(Cleanup, new List<JournalEntry>(), discarded));
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

        // ---- 기록 ----

        private sealed record JournalEntry(string Path, bool Existed);

        private sealed record Journal(string State, List<JournalEntry> Entries, List<string> Leftovers);

        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        private static string JournalPath(string installDirectory) => Path.Combine(installDirectory, JournalName);

        private static void WriteJournal(string installDirectory, Journal journal)
        {
            // 쓰다가 꺼져도 반쪽짜리 기록이 남지 않게 옆 파일에 쓰고 바꿔 끼운다.
            string path = JournalPath(installDirectory);
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(journal, JsonOptions));
            File.Move(temp, path, overwrite: true);
        }

        private static void DeleteJournal(string installDirectory)
        {
            string path = JournalPath(installDirectory);
            if (File.Exists(path)) File.Delete(path);
        }

        /// <summary>기록을 읽는다. 없거나 읽을 수 없으면 null(읽을 수 없는 기록으로는 아무것도 지우거나 되돌리지 않는다).</summary>
        private static Journal? ReadJournal(string installDirectory)
        {
            string path = JournalPath(installDirectory);
            try
            {
                if (!File.Exists(path)) return null;
                var journal = JsonSerializer.Deserialize<Journal>(File.ReadAllText(path));
                if (journal is null || journal.Entries is null || journal.Leftovers is null) return null;
                if (journal.State is not (Applying or Applied or Cleanup)) return null;
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
