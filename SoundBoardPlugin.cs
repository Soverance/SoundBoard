#if TOOLS
using Godot;

namespace Soverance.SoundBoard;

/// <summary>
/// Adds a Sound Board button to the editor's top toolbar that runs the project's board scene, so opening it is one
/// click. The scene is <see cref="DefaultScene"/>, or the one named by the project setting <c>sound_board/scene</c>
/// (read only; the plugin never writes project settings).
/// </summary>
[Tool]
public partial class SoundBoardPlugin : EditorPlugin
{
    public const string DefaultScene = "res://tools/audio/sound_board.tscn";
    public const string SceneSetting = "sound_board/scene";

    private Button? _button;

    public override void _EnterTree()
    {
        _button = new Button
        {
            Text = "Sound Board",
            TooltipText = "Run the sound board: hear every sound in the game and leave verdicts for the agent.",
            Flat = true,
        };
        _button.Pressed += OnPressed;
        AddControlToContainer(CustomControlContainer.Toolbar, _button);
    }

    public override void _ExitTree()
    {
        if (_button is null)
        {
            return;
        }
        RemoveControlFromContainer(CustomControlContainer.Toolbar, _button);
        _button.QueueFree();
        _button = null;
    }

    private static void OnPressed()
    {
        string scene = ProjectSettings.HasSetting(SceneSetting) ? ProjectSettings.GetSetting(SceneSetting).AsString() : DefaultScene;
        if (!ResourceLoader.Exists(scene))
        {
            GD.PushError($"[sound-board] no board scene at {scene}; make one (see the plugin's README) or set {SceneSetting}.");
            return;
        }
        EditorInterface.Singleton.PlayCustomScene(scene);
    }
}
#endif
