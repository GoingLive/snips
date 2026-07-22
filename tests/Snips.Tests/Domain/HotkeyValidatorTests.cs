using Snips.Core.Domain;

namespace Snips.Tests.Domain;

public class HotkeyValidatorTests
{
    [Theory]
    [InlineData(HotkeyValidator.ModControl | HotkeyValidator.ModAlt, 0x4D, true)] // Ctrl+Alt+M
    [InlineData(0, 0x70, true)]  // bare F1
    [InlineData(0, 0x87, true)]  // bare F24
    [InlineData(0, 0x4D, false)] // bare M — no modifier, not a function key
    public void HasRequiredModifierOrIsFunctionKey_MatchesSpec(int modifiers, int vk, bool expected)
    {
        Assert.Equal(expected, HotkeyValidator.HasRequiredModifierOrIsFunctionKey(modifiers, vk));
    }

    [Theory]
    [InlineData(HotkeyValidator.ModControl | HotkeyValidator.ModAlt, 0x2E)] // Ctrl+Alt+Del
    [InlineData(HotkeyValidator.ModWin, 0x4C)] // Win+L
    [InlineData(HotkeyValidator.ModWin, 0x44)] // Win+D
    [InlineData(HotkeyValidator.ModWin, 0x09)] // Win+Tab
    [InlineData(HotkeyValidator.ModAlt, 0x09)] // Alt+Tab
    [InlineData(HotkeyValidator.ModControl | HotkeyValidator.ModShift, 0x1B)] // Ctrl+Shift+Esc
    [InlineData(HotkeyValidator.ModWin, 0x47)] // Win+G
    [InlineData(HotkeyValidator.ModAlt, 0x73)] // Alt+F4
    [InlineData(0, 0x2C)] // PrtScn
    public void IsReserved_MatchesTheExplicitListInSpec(int modifiers, int vk)
    {
        Assert.True(HotkeyValidator.IsReserved(modifiers, vk));
        Assert.False(HotkeyValidator.IsValid(modifiers, vk));
    }

    [Fact]
    public void OrdinaryComboWithAModifier_IsValid()
    {
        Assert.True(HotkeyValidator.IsValid(HotkeyValidator.ModControl | HotkeyValidator.ModAlt, 0x4D));
    }

    [Fact]
    public void NoModifierAndNotAFunctionKey_IsInvalid()
    {
        Assert.False(HotkeyValidator.IsValid(0, 0x4D));
    }
}
