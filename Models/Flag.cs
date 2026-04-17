// AgBoundaryTool
// Flag model for marking locations in fields

using CommunityToolkit.Mvvm.ComponentModel;

namespace AgBoundaryTool.Models;

public enum FlagColor
{
    Red = 0,
    Green = 1,
    Yellow = 2,
    Blue = 3,
    Orange = 4,
    Purple = 5,
    Cyan = 6,
    Pink = 7,
    White = 8,
    Black = 9
}

public partial class Flag : ObservableObject
{
    [ObservableProperty]
    private FlagColor _color;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _notes;

    public double Easting { get; set; }
    public double Northing { get; set; }
    public int Id { get; set; }

    public Flag(double easting, double northing, FlagColor color, int id, string name = "", string notes = "")
    {
        Easting = easting;
        Northing = northing;
        Color = color;
        Id = id;
        Name = string.IsNullOrEmpty(name) ? $"Flag {id}" : name;
        Notes = notes;
    }

    public static string ColorToHex(FlagColor color) => color switch
    {
        FlagColor.Red => "#FF0000",
        FlagColor.Green => "#00CC00",
        FlagColor.Yellow => "#FFCC00",
        FlagColor.Blue => "#2080E0",
        FlagColor.Orange => "#FF8800",
        FlagColor.Purple => "#9933CC",
        FlagColor.Cyan => "#00BBCC",
        FlagColor.Pink => "#FF66AA",
        FlagColor.White => "#FFFFFF",
        FlagColor.Black => "#333333",
        _ => "#FF0000"
    };
}
