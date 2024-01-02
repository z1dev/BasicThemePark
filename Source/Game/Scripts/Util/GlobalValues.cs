using System;
using System.Collections.Generic;
using FlaxEngine;

namespace Game;


public static class TileGlobals
{
    public static float TileDimension;
}

// Filled by the TileMap object
public static class MapGlobals
{
    public static int[] EntryTiles = [];
    public static MapNavigation mapNavigation = null;
}

public static class VisitorGlobals
{
    public static float VisitorWalkingSpeed;
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
        TileGlobals.TileDimension = TileDimension;
        MapGlobals.mapNavigation = mapNavigation;
        VisitorGlobals.VisitorWalkingSpeed = VisitorWalkingSpeed;
        Debug.Log(mapNavigation);
    }

}
