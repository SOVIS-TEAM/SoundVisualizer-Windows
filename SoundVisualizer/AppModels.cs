// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.IO;
using SoundVisualizer.Ai;

namespace SoundVisualizer
{
    /// <summary>
    /// 이 앱이 AI 모델과 소리 목록을 읽는 곳(#58). 포터블 빌드(<c>-p:Portable=true</c>)는 모델을 실행 파일 안에 넣고
    /// (<see cref="BundledModels"/>), 그 밖의 빌드는 실행 파일 옆 AIModel 폴더를 쓴다.
    /// </summary>
    internal static class AppModels
    {
        /// <summary>실행 파일 안의 모델을 꺼내 둘 곳. .NET 이 DLL 을 푸는 곳처럼 사용자 임시 폴더 아래다.</summary>
        private static string ExtractRoot => Path.Combine(Path.GetTempPath(), "SoundVisualizer");

        /// <summary>실행 파일 안에 모델이 들어 있는지(포터블 판인지).</summary>
        public static bool IsBundled { get; } = BundledModels.IsBundled(OpenResource);

        /// <summary>모델 폴더. 포터블 판이면 임시 폴더에 꺼낸다(몇 MB 를 쓰므로 UI 스레드에서 부르지 않는다).</summary>
        public static string ResolveDirectory() => BundledModels.ResolveDirectory(AppContext.BaseDirectory, OpenResource, ExtractRoot);

        /// <summary>소리 목록. 포터블 판이면 실행 파일에서 바로 읽는다.</summary>
        public static YamnetClassMap LoadClassMap() => BundledModels.LoadClassMap(AppContext.BaseDirectory, OpenResource);

        private static Stream? OpenResource(string name) => typeof(AppModels).Assembly.GetManifestResourceStream(name);
    }
}
