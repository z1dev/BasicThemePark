using System;
using System.Collections.Generic;
using FlaxEngine;
using FlaxEngine.GUI;

namespace Game;

/// <summary>
/// VisitorBehavior Script.
/// </summary>
public class VisitorBehavior : Script
{

    private enum WalkDir
    {
        PlusY,
        MinusY,
        PlusX,
        MinusX
    }

    private enum State
    {
        Init,
        ParkEntry,
        Walking
    }

    private float TileDim = 0.0f;

    private State state = State.Init;
    private Int2 currentTile;
    private Int2 destTile;
    private Vector3 destPos;
    private WalkDir walkDir = WalkDir.PlusY;

    /// <inheritdoc/>
    public override void OnStart()
    {
        TileDim = TileGlobals.TileDimension;

        destTile = new Int2(MapGlobals.EntryTiles[^1], -6);
        currentTile = destTile;

        var pos = Actor.Position;

        ComputeDestination();


        Actor.Orientation = ComputeOrientation();
    }

    /// <inheritdoc/>
    public override void OnUpdate()
    {
        var pos = Actor.Position;
        var posLen = (pos - destPos).LengthSquared;
        if (posLen > 1.0)
        {
            var delta = (destPos - pos).Normalized * VisitorGlobals.VisitorWalkingSpeed * Time.DeltaTime;
            if (delta.LengthSquared > posLen)
                delta = destPos - pos;
            pos += delta;
            Actor.Position = pos;
        }
        else
        {
            Actor.Position = destPos;
            currentTile = destTile;
            if (state == State.Init)
            {
                state = State.ParkEntry;
                var newDest = new Int2(MapGlobals.EntryTiles[RandomUtil.Random.Next() % MapGlobals.EntryTiles.Length], -6);
                if (newDest != destTile)
                {
                    destTile = newDest;
                    ComputeWalkDirection();
                    ComputeDestination();
                    Actor.Orientation = ComputeOrientation();
                    (Actor as AnimatedModel).SetParameterValue("Walking", true);
                }
                else
                    (Actor as AnimatedModel).SetParameterValue("Walking", false);
                return;
            }
            if (state == State.ParkEntry)
            {
                destTile.Y = -1;
                ComputeWalkDirection();
                ComputeDestination();
                Actor.Orientation = ComputeOrientation();
                (Actor as AnimatedModel).SetParameterValue("Walking", true);
                state = State.Walking;
                return;
            }

            if (state == State.Walking)
            {
                (Actor as AnimatedModel).SetParameterValue("Walking", false);
                CalculateNext();
            }
        }
    }

    private void ComputeWalkDirection()
    {
        if (destTile.X < currentTile.X)
            walkDir = WalkDir.MinusX;
        else if (destTile.X > currentTile.X)
            walkDir = WalkDir.PlusX;
        else if (destTile.Y < currentTile.Y)
            walkDir = WalkDir.MinusY;
        else if (destTile.Y > currentTile.Y)
            walkDir = WalkDir.PlusY;
    }

    private void ComputeDestination()
    {
        destPos = new Vector3(destTile.X * TileDim + TileDim * 0.5f, 0.0, destTile.Y * TileDim + TileDim * 0.5f);
        switch (walkDir)
        {
            case WalkDir.PlusY:
                destPos.X += TileDim * 0.25f;
                destPos.Z -= TileDim * 0.25f;
                break;
            case WalkDir.MinusY:
                destPos.X -= TileDim * 0.25f;
                destPos.Z += TileDim * 0.25f;
                break;
            case WalkDir.PlusX:
                destPos.X -= TileDim * 0.25f;
                destPos.Z -= TileDim * 0.25f;
                break;
            case WalkDir.MinusX:
                destPos.X += TileDim * 0.25f;
                destPos.Z += TileDim * 0.25f;
                break;
        }
    }

    private Quaternion ComputeOrientation()
    {
        switch (walkDir)
        {
            case WalkDir.MinusY:
                return Quaternion.Euler(0.0f, 0.0f, 0.0f);
            case WalkDir.PlusX:
                return Quaternion.Euler(0.0f, -90.0f, 0.0f);
            case WalkDir.MinusX:
                return Quaternion.Euler(0.0f, 90.0f, 0.0f);
            default:
                return Quaternion.Euler(0.0f, 180.0f, 0.0f);
        }
    }

    private void CalculateNext()
    {
        destTile = MapGlobals.mapNavigation.PickTile(currentTile, walkDir == WalkDir.PlusY ? MapNavigation.Direction.Up :
                (walkDir == WalkDir.MinusY ? MapNavigation.Direction.Down :
                (walkDir == WalkDir.PlusX ? MapNavigation.Direction.Right : MapNavigation.Direction.Left)));

        (Actor as AnimatedModel).SetParameterValue("Walking", destTile != currentTile);
        if (destTile != currentTile)
        {
            ComputeWalkDirection();
            ComputeDestination();
            Actor.Orientation = ComputeOrientation();
        }
    }
}
