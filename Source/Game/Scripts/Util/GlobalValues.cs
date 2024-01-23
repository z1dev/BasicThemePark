using FlaxEngine;

namespace Game;


// Filled by the TileMap object
public static class MapGlobals
{
    public static float TileDimension;
    public static int[] EntryTiles;
    public static int EntryGridDistance;
    public static MapNavigation MapNavigation;
    public static TileMap TileMap;
}


/// <summary>
/// Globals Script.
/// </summary>
public class GlobalValues : Script
{
    //public float TileDimension = 200.0f;
    public float VisitorWalkingSpeed = 100.0f;
    //public MapNavigation mapNavigation = null;
    public ScriptGlobals scriptGlobals;
    public TileMap TileMap;

    public override void OnAwake()
    {
        MapGlobals.TileDimension = scriptGlobals.TileDimension;
        MapGlobals.MapNavigation = (MapNavigation)scriptGlobals.MapNavigation;
        MapGlobals.TileMap = TileMap;
    }

}
