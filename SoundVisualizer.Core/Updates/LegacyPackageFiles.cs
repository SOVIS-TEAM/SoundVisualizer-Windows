// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.

namespace SoundVisualizer.Updates
{
    /// <summary>
    /// 2.3.2 까지의 zip 판이 실행 파일 옆에 두던 파일(#58).
    ///
    /// 포터블 판은 이 DLL 과 모델을 실행 파일 안에 넣었다. 앱 안의 업데이트(#36)는 새 zip 에 든 이름만 바꾸므로, 옛 zip 판에서
    /// 업데이트해 넘어오면 이 파일들이 실행 폴더에 그대로 남는다. 새 버전은 이것들을 쓰지 않는다(DLL 은 .NET 이 임시 폴더에 푼 것을
    /// 읽는다). 앱 안의 업데이트로 막 켜졌을 때만 지운다. 아무 폴더에서나 지우면 같은 폴더에 둔 다른 프로그램의 파일을 지울 수 있다.
    /// </summary>
    public static class LegacyPackageFiles
    {
        /// <summary>지울 파일. 실행 폴더 기준 상대 경로이고, 이 이름과 정확히 같은 것만 지운다.</summary>
        public static IReadOnlyList<string> Names { get; } =
        [
            "D3DCompiler_47_cor3.dll",
            "PenImc_cor3.dll",
            "PresentationNative_cor3.dll",
            "vcruntime140_cor3.dll",
            "wpfgfx_cor3.dll",
            "onnxruntime.dll",
            "onnxruntime_providers_shared.dll",
            @"AIModel\silero_vad.onnx",
            @"AIModel\yamnet.data",
            @"AIModel\yamnet.onnx",
            @"AIModel\yamnet_class_map.csv",
        ];

        /// <summary>옛 모델 폴더. 비었을 때만 지운다.</summary>
        public const string LegacyModelFolder = "AIModel";

        /// <summary>
        /// <paramref name="installDirectory"/> 에서 <see cref="Names"/> 를 지운다. 없는 파일은 건너뛴다. 지우지 못한 파일 수를 돌려준다
        /// (옛 버전이 막 끝나 아직 잡혀 있을 수 있다). 옛 모델 폴더는 다 비었을 때만 지운다.
        /// </summary>
        public static int Remove(string installDirectory)
        {
            ArgumentException.ThrowIfNullOrEmpty(installDirectory);
            int failed = 0;
            foreach (string name in Names)
            {
                string path = Path.Combine(installDirectory, name);
                try
                {
                    if (File.Exists(path)) File.Delete(path);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    failed++;
                }
            }

            string folder = Path.Combine(installDirectory, LegacyModelFolder);
            try
            {
                if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // 비우지 못한 폴더는 다음에 지운다.
            }
            return failed;
        }

        /// <summary>남아 있는 옛 파일 수.</summary>
        public static int Remaining(string installDirectory)
        {
            ArgumentException.ThrowIfNullOrEmpty(installDirectory);
            return Names.Count(name => File.Exists(Path.Combine(installDirectory, name)));
        }
    }
}
