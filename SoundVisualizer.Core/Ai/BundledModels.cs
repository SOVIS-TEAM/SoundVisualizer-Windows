// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Security.Cryptography;
using System.Text;

namespace SoundVisualizer.Ai
{
    /// <summary>
    /// AI 모델 파일을 어디서 읽을지 정한다(#58).
    ///
    /// 포터블 판(GitHub zip)은 실행 파일 하나만 있어도 켜지게 모델을 실행 파일 안에 넣는다(리소스 이름 "AIModel/파일 이름").
    /// 그때는 쓰기 직전에 임시 폴더 아래 모델 폴더에 꺼내고, 이미 꺼낸 파일이 배포한 파일과 같으면(크기·SHA-256) 다시 쓰지 않는다.
    /// YAMNet 은 모델(yamnet.onnx)과 가중치(yamnet.data)가 나뉘어 있어 ONNX Runtime 이 경로로 읽어야 하므로 메모리에서 바로 읽지 않는다.
    /// 소리 목록(yamnet_class_map.csv)은 실행 파일에서 바로 읽는다.
    ///
    /// 실행 파일 안에 모델이 없으면(Microsoft Store 판, 개발 빌드) 지금처럼 실행 파일 옆 AIModel 폴더를 쓴다.
    /// </summary>
    public static class BundledModels
    {
        /// <summary>실행 파일 옆 모델 폴더 이름이자 리소스 이름의 앞부분.</summary>
        public const string FolderName = "AIModel";

        /// <summary>실행 파일 안 모델 리소스 이름의 앞부분. 앱 프로젝트의 LogicalName 과 같아야 한다.</summary>
        public const string ResourcePrefix = FolderName + "/";

        /// <summary>소리 목록 파일 이름.</summary>
        public const string ClassMapName = "yamnet_class_map.csv";

        /// <summary>꺼내 쓰는 모델 파일. 모두 크기와 SHA-256 이 고정되어 있다.</summary>
        internal static IReadOnlyList<ModelFileIntegrity.ExpectedFile> ModelFiles { get; } =
            [YamnetModelFiles.Onnx, YamnetModelFiles.Data, SileroModelFile.Onnx];

        /// <summary>실행 파일 안에 모델이 들어 있는지(포터블 판인지). 소리 목록 리소스가 있는지로 본다.</summary>
        public static bool IsBundled(Func<string, Stream?> openResource)
        {
            ArgumentNullException.ThrowIfNull(openResource);
            using var classMap = openResource(ResourcePrefix + ClassMapName);
            return classMap != null;
        }

        /// <summary>
        /// 모델을 읽을 폴더를 돌려준다. 실행 파일 안에 모델이 있으면 <paramref name="extractRoot"/> 아래 "AIModel-(모델 해시 12자)"
        /// 폴더에 꺼내고 그 폴더를, 없으면 <paramref name="baseDirectory"/>\AIModel 을 돌려준다. 꺼내는 데 몇 MB 를 쓰므로 UI 스레드에서
        /// 부르지 않는다.
        /// </summary>
        /// <exception cref="FileNotFoundException">실행 파일 안에 모델 리소스 하나가 없다.</exception>
        /// <exception cref="InvalidDataException">꺼낸 파일이 배포한 모델과 다르다.</exception>
        /// <exception cref="IOException">임시 폴더에 쓰지 못했다.</exception>
        /// <exception cref="UnauthorizedAccessException">임시 폴더에 쓸 권한이 없다.</exception>
        public static string ResolveDirectory(string baseDirectory, Func<string, Stream?> openResource, string extractRoot)
        {
            ArgumentException.ThrowIfNullOrEmpty(baseDirectory);
            ArgumentException.ThrowIfNullOrEmpty(extractRoot);
            if (!IsBundled(openResource)) return Path.Combine(baseDirectory, FolderName);

            string directory = Path.Combine(extractRoot, FolderName + "-" + ContentKey());
            Directory.CreateDirectory(directory);
            foreach (var expected in ModelFiles) Extract(openResource, directory, expected);
            return directory;
        }

        /// <summary>소리 목록을 읽는다. 실행 파일 안에 있으면 거기서, 없으면 <paramref name="baseDirectory"/>\AIModel 에서 읽는다.</summary>
        public static YamnetClassMap LoadClassMap(string baseDirectory, Func<string, Stream?> openResource)
        {
            ArgumentException.ThrowIfNullOrEmpty(baseDirectory);
            ArgumentNullException.ThrowIfNull(openResource);
            using (var bundled = openResource(ResourcePrefix + ClassMapName))
            {
                if (bundled != null) return YamnetClassMap.Load(bundled);
            }
            return YamnetClassMap.Load(Path.Combine(baseDirectory, FolderName, ClassMapName));
        }

        /// <summary>모델 이름과 해시로 만든 12자. 모델이 바뀌면 다른 폴더에 꺼내므로 옛 버전이 꺼낸 파일과 섞이지 않는다.</summary>
        internal static string ContentKey()
        {
            string joined = string.Join("|", ModelFiles.Select(file => file.Name + ":" + file.Sha256));
            return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(joined)))[..12];
        }

        /// <summary>
        /// 파일 하나를 꺼낸다. 이미 맞는 파일이 있으면 그대로 둔다. 같은 폴더 안의 임시 이름에 다 쓰고, 배포한 모델과 같은지(크기·SHA-256)
        /// 확인한 뒤에야 제 이름으로 바꾼다. 그래서 반쯤 쓴 파일이나 다른 파일이 제 이름으로 남지 않는다. 앱을 두 번 켜 함께 꺼낼 때
        /// 이름 바꾸기가 막혀도, 다른 쪽이 맞는 파일을 이미 두었으면 그것을 쓴다.
        /// </summary>
        private static void Extract(Func<string, Stream?> openResource, string directory, ModelFileIntegrity.ExpectedFile expected)
        {
            string target = Path.Combine(directory, expected.Name);
            if (ModelFileIntegrity.IsValid(target, expected)) return;

            using var source = openResource(ResourcePrefix + expected.Name)
                ?? throw new FileNotFoundException($"Bundled model '{expected.Name}' is missing", ResourcePrefix + expected.Name);
            string temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    source.CopyTo(output);
                }
                // 실행 파일이 깨졌으면 여기서 걸린다. 예외를 던져 앱이 AI 없이 화면만 돌게 한다.
                if (!ModelFileIntegrity.IsValid(temporary, expected))
                {
                    throw new InvalidDataException(
                        $"Bundled model '{expected.Name}' does not match the pinned size {expected.ByteCount} and SHA-256 {expected.Sha256}");
                }
                try
                {
                    File.Move(temporary, target, overwrite: true);
                }
                catch (Exception ex) when ((ex is IOException or UnauthorizedAccessException) && ModelFileIntegrity.IsValid(target, expected))
                {
                    // 다른 실행이 먼저 맞는 파일을 두고 열어 둔 채다. 그 파일을 쓴다.
                }
            }
            finally
            {
                TryDelete(temporary);
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // 임시 폴더라 남아도 해가 없다.
            }
        }
    }
}
