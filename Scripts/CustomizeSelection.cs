using Godot;
using System;
using Godot.Collections;
using System.Linq;

public partial class CustomizeSelection : VBoxContainer
{
    public enum SelectionType { Character, Face, Hat, Accessory }

    [Export] public SelectionType selectionType;

    [ExportSubgroup("Buttons")]
    [Export] public MenuButton previousButton;
    [Export] public MenuButton nextButton;

    [Signal] public delegate void SelectionChangedEventHandler(SelectionType type, int index);

    public Label selectionLabel => GetNode<Label>("Separator/SelectionLabel");

    private int currentIndex = 0;

    public FacialExpression CurrentFace => facialExpressions.ElementAtOrDefault(currentIndex);
    public CharacterTypePreset CurrentCharacter => characterPresets.ElementAtOrDefault(currentIndex);
    public Cosmetic CurrentHat => hats.ElementAtOrDefault(currentIndex);
    public Cosmetic CurrentAccessory => accessories.ElementAtOrDefault(currentIndex);

    private Array<FacialExpression> facialExpressions = new();
    private Array<CharacterTypePreset> characterPresets = new();
    private Array<Cosmetic> hats = new();
    private Array<Cosmetic> accessories = new();

    public override void _Ready()
    {
        switch (selectionType)
        {
            case SelectionType.Face:
                facialExpressions = new(ResourceDatabase.Expressions.Values.ToArray());
                currentIndex = Math.Max(0, facialExpressions.IndexOf(ResourceDatabase.Expressions[SettingsManager.CurrentSettings.SavedExpressionID]));
                break;
            case SelectionType.Character:
                characterPresets = new(ResourceDatabase.Characters.ToArray());
                break;
            case SelectionType.Hat:
                hats = new(ResourceDatabase.Hats.Values.ToArray());
                currentIndex = Math.Max(0, hats.IndexOf(ResourceDatabase.Hats[SettingsManager.CurrentSettings.SavedHatID]));
                break;
            case SelectionType.Accessory:
                accessories = new(ResourceDatabase.Accessories.ToArray());
                break;
        }

        UpdateLabel();

        previousButton.Pressed += () => ChangeIndex(-1);
        nextButton.Pressed += () => ChangeIndex(1);
    }

    private void ChangeIndex(int direction)
    {
        int count = GetCurrentCount();
        if (count == 0) return;

        currentIndex = (currentIndex + direction + count) % count;
        UpdateLabel();
        EmitSignal(SignalName.SelectionChanged, (int)selectionType, currentIndex);
    }

    private void UpdateLabel()
    {
        string label = selectionType switch
        {
            SelectionType.Face => CurrentFace?.ExpressionId.Capitalize() ?? "None",
            SelectionType.Character => CurrentCharacter?.Name ?? "None",
            SelectionType.Hat => CurrentHat?.CosmeticID.Capitalize() ?? "None",
            SelectionType.Accessory => CurrentAccessory?.CosmeticID.Capitalize() ?? "None",
            _ => "None"
        };

        selectionLabel.Text = $"{selectionType}: {label}";
    }

    private int GetCurrentCount()
    {
        return selectionType switch
        {
            SelectionType.Face => facialExpressions.Count,
            SelectionType.Character => characterPresets.Count,
            SelectionType.Hat => hats.Count,
            SelectionType.Accessory => accessories.Count,
            _ => 0
        };
    }
}
