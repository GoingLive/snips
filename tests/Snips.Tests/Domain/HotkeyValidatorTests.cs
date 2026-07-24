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

    [Theory]
    [InlineData(HotkeyValidator.ModControl | HotkeyValidator.ModAlt, 0x31)] // Ctrl+Alt+1
    [InlineData(HotkeyValidator.ModControl | HotkeyValidator.ModAlt, 0x4D)] // Ctrl+Alt+M
    public void CtrlAltWithoutShift_OnAPrintableKey_IsFlaggedAsAltGrRisk(int modifiers, int vk)
    {
        Assert.True(HotkeyValidator.IsLikelyAltGrCollision(modifiers, vk));
        // Advisory only — must not affect whether the combo is actually usable.
        Assert.True(HotkeyValidator.IsValid(modifiers, vk));
    }

    [Theory]
    [InlineData(HotkeyValidator.ModControl | HotkeyValidator.ModAlt | HotkeyValidator.ModShift, 0x31)] // + Shift disambiguates from AltGr
    [InlineData(HotkeyValidator.ModControl | HotkeyValidator.ModShift, 0x31)] // no Alt at all
    [InlineData(HotkeyValidator.ModControl | HotkeyValidator.ModAlt, 0x70)] // Ctrl+Alt+F1 — F-keys aren't AltGr compositions
    public void CombosThatArentPlainCtrlAltOnAPrintableKey_AreNotFlagged(int modifiers, int vk)
    {
        Assert.False(HotkeyValidator.IsLikelyAltGrCollision(modifiers, vk));
    }
}
