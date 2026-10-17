using System;
using System.Collections.Generic;
using Godot;

namespace Soverance.SoundBoard;

/// <summary>The board's left column: every entry under its category, with its status and a licence mark.</summary>
public partial class BoardList : Tree
{
    public event Action<BoardEntry>? EntryPicked;

    public BoardStyle Style { get; set; } = BoardStyle.Default;

    private readonly List<BoardEntry> _entries = [];
    private readonly Dictionary<string, TreeItem> _items = [];

    public override void _Ready()
    {
        HideRoot = true;
        Columns = 3;
        SelectMode = SelectModeEnum.Row;
        SetColumnExpand(1, false);
        SetColumnCustomMinimumWidth(1, 110);
        SetColumnExpand(2, false);
        SetColumnCustomMinimumWidth(2, 80);
        ItemSelected += () =>
        {
            if (GetSelected() is { } item && item.GetMetadata(0).VariantType == Variant.Type.Int)
            {
                EntryPicked?.Invoke(_entries[item.GetMetadata(0).AsInt32()]);
            }
        };
    }

    /// <summary>Lists every entry; <paramref name="needsConfirm"/> says which play a file whose licence is marked
    /// Confirm (shown in the third column).</summary>
    public void Build(IReadOnlyList<BoardEntry> entries, ReviewData review, Func<BoardEntry, bool> needsConfirm)
    {
        Clear();
        _entries.Clear();
        _items.Clear();
        TreeItem root = CreateItem();
        TreeItem? category = null;
        string current = "";
        foreach (BoardEntry entry in entries)
        {
            if (entry.Category != current || category is null)
            {
                current = entry.Category;
                category = CreateItem(root);
                category.SetText(0, current.ToUpperInvariant());
                category.SetCustomColor(0, Style.TextDim);
                for (int column = 0; column < Columns; column++)
                {
                    category.SetSelectable(column, false);
                }
            }
            TreeItem item = CreateItem(category);
            item.SetText(0, entry.Label);
            item.SetMetadata(0, _entries.Count);
            if (needsConfirm(entry))
            {
                item.SetText(2, "confirm");
                item.SetCustomColor(2, Style.Warn);
                item.SetTooltipText(2, "Its licence is marked Confirm in the sources file: resolve before release.");
            }
            _entries.Add(entry);
            _items[entry.Id] = item;
            Refresh(entry, review);
        }
    }

    public void Refresh(BoardEntry entry, ReviewData review)
    {
        (string text, Color color) = Status(entry, review, Style);
        _items[entry.Id].SetText(1, text);
        _items[entry.Id].SetCustomColor(1, color);
    }

    /// <summary>Every row's status, as a new round changes them all.</summary>
    public void RefreshAll(ReviewData review)
    {
        foreach (BoardEntry entry in _entries)
        {
            Refresh(entry, review);
        }
    }

    /// <summary>The status the entry's row shows (test access).</summary>
    public string StatusOf(BoardEntry entry) => _items[entry.Id].GetText(1);

    /// <summary>Whether the entry's row carries the licence Confirm mark (test access).</summary>
    public bool IsConfirmMarked(BoardEntry entry) => _items[entry.Id].GetText(2).Length > 0;

    /// <summary>Selects the entry's row and scrolls to it.</summary>
    public void Pick(BoardEntry entry)
    {
        TreeItem item = _items[entry.Id];
        if (GetSelected() != item)
        {
            item.Select(0);
        }
        ScrollToItem(item);
    }

    /// <summary>An applied verdict reads "done", or "pick" while candidates cut for a Replace wait; an open one says what
    /// was asked for; one from before the round, "re-hear".</summary>
    public static (string Text, Color Color) Status(BoardEntry entry, ReviewData review, BoardStyle style)
    {
        if (!review.Entries.TryGetValue(entry.Id, out ReviewEntry? verdict))
        {
            return entry.Silent ? ("SILENT", style.Silent) : ("unheard", style.TextFaint);
        }
        if (BoardRound.ToReHear(entry, review))
        {
            return ("re-hear", style.TextDim);
        }
        if (verdict.Applied is not null)
        {
            return BoardRound.ToPick(verdict) ? ("pick", style.Accent) : ("done", style.Good);
        }
        return verdict.Verdict switch
        {
            Verdicts.Keep => ("kept", style.Good),
            Verdicts.SilentOk => ("silent ok", style.Good),
            Verdicts.NeedsSound => ("needs sound", style.Warn),
            _ => ("replace", style.Warn),
        };
    }
}
