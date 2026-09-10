using QuickSIP.Core.Sip;
using Xunit;

namespace QuickSIP.Tests;

public sealed class DtmfMapperTests
{
    [Theory]
    [InlineData('0', (byte)0)]
    [InlineData('5', (byte)5)]
    [InlineData('9', (byte)9)]
    [InlineData('*', DtmfMapper.Star)]
    [InlineData('#', DtmfMapper.Pound)]
    [InlineData('A', (byte)12)]
    [InlineData('d', (byte)15)]
    public void FromChar(char c, byte expected)
    {
        Assert.True(DtmfMapper.TryFromChar(c, out var id));
        Assert.Equal(expected, id);
        Assert.Equal(char.ToUpperInvariant(c), DtmfMapper.ToChar(id));
    }

    [Fact]
    public void NumpadStar_IsAsterisk()
    {
        Assert.True(DtmfMapper.TryFromKeyName("Multiply", shift: false, out var id));
        Assert.Equal(DtmfMapper.Star, id);
        Assert.Equal('*', DtmfMapper.ToChar(id));
    }

    [Fact]
    public void NumpadDivide_IsPound()
    {
        Assert.True(DtmfMapper.TryFromKeyName("Divide", shift: false, out var id));
        Assert.Equal(DtmfMapper.Pound, id);
        Assert.Equal('#', DtmfMapper.ToChar(id));
    }

    [Fact]
    public void Shift3_IsPound()
    {
        Assert.True(DtmfMapper.TryFromKeyName("D3", shift: true, out var id));
        Assert.Equal(DtmfMapper.Pound, id);
    }

    [Fact]
    public void Shift8_IsStar()
    {
        Assert.True(DtmfMapper.TryFromKeyName("D8", shift: true, out var id));
        Assert.Equal(DtmfMapper.Star, id);
    }

    [Fact]
    public void NumPadDigits()
    {
        Assert.True(DtmfMapper.TryFromKeyName("NumPad4", shift: false, out var id));
        Assert.Equal((byte)4, id);
    }

    [Fact]
    public void InvalidChar_Fails()
    {
        Assert.False(DtmfMapper.TryFromChar('x', out _));
    }
}
