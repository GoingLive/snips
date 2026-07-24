using System.Windows;
using System.Windows.Input;
using Snips.Core.Domain;
using Snips.Core.Repositories;

namespace Snips.App;

/// <summary>
/// Captures the next key combination pressed and saves it as the given snippet's shortcut
/// (SPEC.md §5.8). Persists directly via IShortcutRepository — DialogResult=true means
/// something changed (saved or cleared) and the caller should refresh its list.
/// </summary>
public partial class ShortcutCaptureWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly string _snippetId;
    private readonly IShortcutRepository _shortcuts;
    private int _capturedModifiers;
    private int _capturedVirtualKey;
    private bool _hasCapture;

    public ShortcutCaptureWindow(string snippetName, string snippetId, IShortcutRepository shortcuts, Shortcut? existing)
    {
        InitializeComponent();
        _snippetId = snippetId;
        _shortcuts = shortcuts;

        TitleText.Text = UiStrings.Get("Str_ShortcutCaptureTitleFormat", snippetName);

        if (existing is not null)
        {
            CapturedText.Text = HotkeyFormatting.Format(existing.Modifiers, existing.VirtualKey);
            _capturedModifiers = existing.Modifiers;
            _capturedVirtualKey = existing.VirtualKey;
            _hasCapture = true;
            SaveButton.IsEnabled = true;
        }

        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        e.Handled = true;

        if (key == Key.Escape)
        {
            DialogResult = false;
            return;
        }

        if (IsPureModifierKey(key))
            return;

        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        var modifiers = (int)Keyboard.Modifiers;

        _capturedModifiers = modifiers;
        _capturedVirtualKey = virtualKey;
        _hasCapture = true;

        CapturedText.Text = HotkeyFormatting.Format(modifiers, virtualKey);

        if (!HotkeyValidator.HasRequiredModifierOrIsFunctionKey(modifiers, virtualKey))
        {
            ShowError(UiStrings.Get("Str_ModifierRequiredError"));
        }
        else if (HotkeyValidator.IsReserved(modifiers, virtualKey))
        {
            ShowError(UiStrings.Get("Str_ReservedComboError"));
        }
        else
        {
            HideError();
        }

        // Independent of the error/valid state above — advisory, so it can show alongside a
        // perfectly valid, savable combo.
        WarningText.Visibility = HotkeyValidator.IsLikelyAltGrCollision(modifiers, virtualKey)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private static bool IsPureModifierKey(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or
        Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.System;

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
        SaveButton.IsEnabled = false;
    }

    private void HideError()
    {
        ErrorText.Visibility = Visibility.Collapsed;
        SaveButton.IsEnabled = true;
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_hasCapture)
            return;

        try
        {
            await _shortcuts.SetAsync(_snippetId, _capturedModifiers, _capturedVirtualKey);
            DialogResult = true;
        }
        catch (DuplicateShortcutException ex)
        {
            ShowError(ex.Message);
        }
    }

    private async void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        await _shortcuts.RemoveAsync(_snippetId);
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
