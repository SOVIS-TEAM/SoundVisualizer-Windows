// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Updates;

namespace SoundVisualizer.Tests.Updates;

public class UpdateLaunchTests
{
    [Fact]
    public void 옛_버전과_새_버전이_주고받는_인자_이름은_바뀌지_않는다()
    {
        // 2.3 이 바꾸고 띄운 다음 버전도 이 이름을 알아들어야 한다. 이름을 바꾸면 옛 버전이 넘긴 값을 새 버전이 모른다.
        Assert.Equal("--updated-from", UpdateLaunch.UpdatedFromArgument);
        Assert.Equal(new[] { "--updated-from", "4321" }, UpdateLaunch.Arguments(4321));
    }

    [Fact]
    public void 넘긴_프로세스_번호를_읽는다()
    {
        Assert.Equal(4321, UpdateLaunch.PreviousProcessId(UpdateLaunch.Arguments(4321)));
        Assert.Equal(77, UpdateLaunch.PreviousProcessId(new[] { "--other", "--UPDATED-FROM", "77" }));
    }

    [Theory]
    [InlineData]
    [InlineData("--updated-from")]
    [InlineData("--updated-from", "abc")]
    [InlineData("--updated-from", "-5")]
    [InlineData("--updated-from", "0")]
    [InlineData("4321")]
    public void 번호가_없거나_숫자가_아니면_없다고_본다(params string[] args)
    {
        Assert.Null(UpdateLaunch.PreviousProcessId(args));
    }
}
