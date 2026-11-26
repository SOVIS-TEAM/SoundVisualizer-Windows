// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.IO.Compression;

namespace SoundVisualizer.Updates
{
    /// <summary>내려받은 zip 이 앱 배포 파일의 꼴이 아니다. 파일을 하나도 바꾸지 않은 채 업데이트를 멈춘다.</summary>
    public sealed class InvalidUpdatePackageException(string message) : Exception(message);

    /// <summary>
    /// 내려받은 릴리스 zip 을 임시 폴더에 푼다(#36). 실행 폴더에 바로 풀지 않는다. 다 풀고 확인한 뒤에야 <see cref="UpdateInstaller"/> 가
    /// 실행 폴더의 파일을 바꾼다.
    ///
    /// zip 의 항목 이름은 믿지 않는다. 절대 경로, 드라이브, "..", 대체 데이터 스트림(:)이 든 이름은 받지 않는다. 받으면 풀 폴더 밖에
    /// 파일을 쓸 수 있다. 맨 위에 SoundVisualizer.exe 가 없으면 앱 배포 파일이 아니다.
    /// </summary>
    public static class UpdatePackage
    {
        /// <summary>zip 맨 위에 있어야 하는 실행 파일 이름.</summary>
        public const string ExecutableName = "SoundVisualizer.exe";

        // 지금 배포 파일은 풀면 약 240MB 다. 이보다 훨씬 크면 배포 파일이 아니다(압축 폭탄으로 디스크를 채우지 않게).
        private const long MaxExtractedBytes = 2L * 1024 * 1024 * 1024;
        private const int MaxEntries = 2000;

        /// <summary>
        /// <paramref name="zipPath"/> 를 <paramref name="stagingDirectory"/>(비어 있지 않으면 지운다)에 풀고, 푼 파일의 상대 경로를
        /// 돌려준다. 경로는 '\' 로 나눈다.
        /// </summary>
        public static IReadOnlyList<string> Extract(string zipPath, string stagingDirectory)
        {
            string root = Path.GetFullPath(stagingDirectory);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            Directory.CreateDirectory(root);

            using var archive = ZipFile.OpenRead(zipPath);
            if (archive.Entries.Count > MaxEntries) throw new InvalidUpdatePackageException("항목이 너무 많습니다: " + archive.Entries.Count);

            var files = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long total = 0;
            foreach (var entry in archive.Entries)
            {
                // 폴더 항목("AIModel/")은 파일을 풀 때 함께 만든다.
                if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
                {
                    if (entry.Length != 0) throw new InvalidUpdatePackageException("폴더 항목에 내용이 있습니다: " + entry.FullName);
                    continue;
                }

                string relative = SafeRelativePath(entry.FullName);
                if (!seen.Add(relative)) throw new InvalidUpdatePackageException("같은 이름이 두 번 있습니다: " + entry.FullName);

                total += entry.Length;
                if (entry.Length < 0 || total > MaxExtractedBytes) throw new InvalidUpdatePackageException("풀면 너무 큽니다.");

                string target = Path.GetFullPath(Path.Combine(root, relative));
                if (!target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidUpdatePackageException("풀 폴더 밖을 가리킵니다: " + entry.FullName);
                }
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                entry.ExtractToFile(target, overwrite: false);
                files.Add(relative);
            }

            if (!files.Contains(ExecutableName, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidUpdatePackageException(ExecutableName + " 이 맨 위에 없습니다.");
            }
            return files;
        }

        /// <summary>zip 항목 이름을 '\' 로 나눈 상대 경로로 바꾼다. 풀 폴더 밖이나 다른 드라이브를 가리킬 수 있는 이름이면 예외.</summary>
        internal static string SafeRelativePath(string entryName)
        {
            string name = entryName.Replace('/', '\\');
            if (name.Length == 0 || name.StartsWith('\\') || name.Contains(':') || Path.IsPathRooted(name))
            {
                throw new InvalidUpdatePackageException("쓸 수 없는 이름입니다: " + entryName);
            }
            string[] parts = name.Split('\\');
            foreach (string part in parts)
            {
                if (part.Length == 0 || part == "." || part == ".." || part.Trim().Length == 0 || part.EndsWith('.') || part.EndsWith(' '))
                {
                    throw new InvalidUpdatePackageException("쓸 수 없는 이름입니다: " + entryName);
                }
                if (part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || part.IndexOfAny(new[] { '<', '>', '"', '|', '?', '*' }) >= 0)
                {
                    throw new InvalidUpdatePackageException("쓸 수 없는 글자가 있습니다: " + entryName);
                }
            }
            return string.Join('\\', parts);
        }
    }
}
