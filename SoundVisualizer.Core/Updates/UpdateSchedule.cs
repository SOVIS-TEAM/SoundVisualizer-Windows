// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer.Updates
{
    /// <summary>
    /// 런처를 열 때 저절로 업데이트를 확인할지(#36). 하루에 한 번만 묻는다. 런처를 자주 껐다 켜도 GitHub 에 그만큼 묻지 않는다.
    /// </summary>
    public static class UpdateSchedule
    {
        /// <summary>저절로 확인하는 간격.</summary>
        public static readonly TimeSpan Interval = TimeSpan.FromHours(24);

        /// <summary>
        /// 확인할 때인지. 확인한 적이 없으면 지금이다. 시계를 뒤로 돌려 마지막 확인이 미래에 있으면 기다리지 않고 지금 확인한다
        /// (그러지 않으면 시계가 그 날을 지날 때까지 확인하지 않는다).
        /// </summary>
        public static bool IsDue(DateTimeOffset now, DateTimeOffset? lastCheck)
        {
            if (lastCheck is not { } last) return true;
            if (last > now) return true;
            return now - last >= Interval;
        }
    }
}
