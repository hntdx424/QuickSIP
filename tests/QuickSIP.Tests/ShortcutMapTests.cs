using QuickSIP.Core.Shortcuts;
using Xunit;

namespace QuickSIP.Tests;

public sealed class ShortcutMapTests
{
    [Theory]
    [InlineData("D", PhoneAction.Dial)]
    [InlineData("A", PhoneAction.Answer)]
    [InlineData("H", PhoneAction.Hangup)]
    [InlineData("O", PhoneAction.Hold)]
    [InlineData("M", PhoneAction.Mute)]
    [InlineData("B", PhoneAction.BlindTransfer)]
    [InlineData("T", PhoneAction.AttendedTransfer)]
    [InlineData("C", PhoneAction.Record)]
    [InlineData("R", PhoneAction.Register)]
    public void CtrlShift_Letter(string key, PhoneAction expected)
    {
        Assert.True(ShortcutMap.TryMatch(ctrl: true, shift: true, alt: false, key, out var action));
        Assert.Equal(expected, action);
    }

    [Fact]
    public void WpfDKey_StillMatches()
    {
        Assert.True(ShortcutMap.TryMatch(true, true, false, "D", out var action));
        Assert.Equal(PhoneAction.Dial, action);
    }

    [Fact]
    public void MissingModifier_DoesNotMatch()
    {
        Assert.False(ShortcutMap.TryMatch(ctrl: true, shift: false, alt: false, "A", out _));
    }

    [Fact]
    public void EscapeAndEnter()
    {
        Assert.True(ShortcutMap.IsEscapeHangup(false, false, false, "Escape"));
        Assert.True(ShortcutMap.IsEnterDial(false, false, false, "Return"));
        Assert.False(ShortcutMap.IsEnterDial(true, false, false, "Enter"));
    }

    [Fact]
    public void Bindings_CoverRequiredActions()
    {
        Assert.Equal(9, ShortcutMap.Bindings.Count);
        Assert.All(ShortcutMap.Bindings, b =>
        {
            Assert.True(b.Ctrl);
            Assert.True(b.Shift);
            Assert.False(b.Alt);
        });
    }
}
