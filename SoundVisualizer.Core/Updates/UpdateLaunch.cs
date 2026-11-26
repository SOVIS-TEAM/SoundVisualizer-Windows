// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Globalization;

namespace SoundVisualizer.Updates
{
    /// <summary>
    /// 업데이트를 마친 옛 버전이 새 버전을 띄울 때 넘기는 명령줄(#36). 새 버전은 옛 프로세스가 끝나기를 기다린 뒤 설정을 읽는다.
    /// 그러지 않으면 옛 버전이 끝나며 쓰는 설정 파일을 새 버전이 반쯤 읽을 수 있다.
    ///
    /// 옛 버전이 새 버전에 넘기는 약속이라, 다음 버전들도 이 이름을 그대로 알아들어야 한다(UpdateLaunchTests 가 지킨다).
    /// </summary>
    public static class UpdateLaunch
    {
        /// <summary>"--updated-from 1234": 1234 번 프로세스(옛 버전)가 이 버전으로 바꾸고 띄웠다.</summary>
        public const string UpdatedFromArgument = "--updated-from";

        /// <summary>새 버전을 띄울 때 넘길 인자.</summary>
        public static string[] Arguments(int previousProcessId) =>
            new[] { UpdatedFromArgument, previousProcessId.ToString(CultureInfo.InvariantCulture) };

        /// <summary>명령줄에 옛 프로세스 번호가 있으면 그 번호. 없거나 숫자가 아니면 null.</summary>
        public static int? PreviousProcessId(IReadOnlyList<string> args)
        {
            for (int i = 0; i + 1 < args.Count; i++)
            {
                if (string.Equals(args[i], UpdatedFromArgument, StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out int id) && id > 0)
                {
                    return id;
                }
            }
            return null;
        }
    }
}
