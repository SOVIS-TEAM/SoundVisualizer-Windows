// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Updates;

namespace SoundVisualizer.Tests.Updates;

public class UpdateScheduleTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 12, 0, 0, TimeSpan.FromHours(9));

    [Fact]
    public void 확인한_적이_없으면_지금_확인한다()
    {
        Assert.True(UpdateSchedule.IsDue(Now, null));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(23.9, false)]
    [InlineData(24, true)]
    [InlineData(72, true)]
    public void 하루에_한_번만_확인한다(double hoursAgo, bool due)
    {
        Assert.Equal(due, UpdateSchedule.IsDue(Now, Now.AddHours(-hoursAgo)));
    }

    [Fact]
    public void 마지막_확인이_미래에_있으면_시계를_돌린_것이라_지금_확인한다()
    {
        Assert.True(UpdateSchedule.IsDue(Now, Now.AddDays(3)));
    }

    [Fact]
    public void 시간대가_달라도_같은_순간으로_비교한다()
    {
        var utcTenHoursAgo = Now.ToUniversalTime().AddHours(-10);
        Assert.False(UpdateSchedule.IsDue(Now, utcTenHoursAgo));
    }
}
