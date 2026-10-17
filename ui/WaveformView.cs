using System;
using Godot;

namespace Soverance.SoundBoard;

/// <summary>Draws a peak envelope (<see cref="Waveform.Envelope"/>) across the control.</summary>
public partial class WaveformView : Control
{
    private Vector2[] _envelope = [];

    public BoardStyle Style { get; set; } = BoardStyle.Default;

    public Vector2[] Envelope
    {
        get => _envelope;
        set
        {
            _envelope = value;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        Vector2 size = Size;
        DrawRect(new Rect2(Vector2.Zero, size), Style.WaveBack);
        float mid = size.Y * 0.5f;
        DrawLine(new Vector2(0f, mid), new Vector2(size.X, mid), Style.WaveLine);
        if (_envelope.Length == 0)
        {
            return;
        }
        float step = size.X / _envelope.Length;
        for (int i = 0; i < _envelope.Length; i++)
        {
            float x = (i + 0.5f) * step;
            DrawLine(new Vector2(x, mid - _envelope[i].Y * mid), new Vector2(x, mid - _envelope[i].X * mid),
                     Style.Accent, Math.Max(1f, step));
        }
    }
}
