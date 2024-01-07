using System;
using System.Collections.Generic;
using FlaxEngine;

namespace Game;


// Filled by the TileMap object
public static class MapGlobals
{
    public static float TileDimension;
    public static int[] EntryTiles;
    public static int EntryGridDistance;
    public static MapNavigation mapNavigation;
}


/// <summary>
/// Globals Script.
/// </summary>
public class GlobalValues : Script
{
    // /// <inheritdoc/>
    // public override void OnEnable()
    // {
    //     // Here you can add code that needs to be called when script is enabled (eg. register for events)
    // }

    // /// <inheritdoc/>
    // public override void OnDisable()
    // {
    //     // Here you can add code that needs to be called when script is disabled (eg. unregister from events)
    // }

    // /// <inheritdoc/>
    // public override void OnUpdate()
    // {
    //     // Here you can add code that needs to be called every frame
    // }

    public float TileDimension = 200.0f;
    public float VisitorWalkingSpeed = 100.0f;
    public MapNavigation mapNavigation = null;

    public override void OnAwake()
    {
        MapGlobals.TileDimension = TileDimension;
        MapGlobals.mapNavigation = mapNavigation;
        Debug.Log(mapNavigation);
    }

}
