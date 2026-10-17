using Godot;

namespace Soverance.SoundBoard;

/// <summary>Labels in the project theme's font, at a size and colour.</summary>
public static class BoardText
{
    public static Label Make(string text, int size, Color color, bool wrap = false)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        if (wrap)
        {
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        }
        return label;
    }
}
