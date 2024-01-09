using System;
using FlaxEngine;

namespace Game;


using Vector3 = FlaxEngine.Vector3;

/// <summary>
/// VisitorBehavior Script.
/// </summary>
public class VisitorBehavior : Script
{

    public float WalkingSpeed = 100.0f;
    public float MaximumTurnRadius = 50.0f;
    // The distance of one road lane to the side of the lane, in fraction to tile dimension
    public float LaneSideDistance = 0.25f;

    private float TileDim = 0.0f;

    private enum MapDir
    {
        PlusY,
        MinusY,
        PlusX,
        MinusX
    }

    private enum State
    {
        ParkEntry,
        Walking
    }

    private enum LineSide
    {
        NegativeSide = 0,
        PositiveSide = 1,
#pragma warning disable CA1069 // Enums values should not be duplicated
        CCWSide = 0,
        CWSide = 1
#pragma warning restore CA1069 // Enums values should not be duplicated
    }

    private enum MoveState
    {
        // The cell or destination position is approximately forward. Move directly towards the goal..
        GoForward,
        // The cell destination is to a side, but we need to get close to the vertical or horizontal line of direction first.
        ApproachTurn,
        // Near the line of direction. Turning towards destination.
        Turning,
        // When the destination cell is the one behind, walk forward into the current grid cell to make move look less unnatural.
        ApproachFullTurn,
        // Initial turn when having to turn back to the grid cell behind. It is followed by normal ApproachTurn and Turning.
        TurningFullTurn
    }

    private State state = State.ParkEntry;
    private Int2 currentTile;
    private MapDir currentDir;
    private Vector3 currentVec;
    // Next tile grid coordinates to walk to.
    private Int2 destTile;
    // Next position to walk to.
    private Vector3 destPos;
    // Direction to face when arriving at the destination position.
    private MapDir destDir = MapDir.PlusY;
    private Vector3 destVec;


    private MoveState moveState;
    // Which side of the facing direction to turn towards when turning.
    private LineSide turnSide;
    // How much to go until the direction's line is close enough to start turning towards it.
    private float turnDistance;
    // Radius of turning until facing the move direction (or line of move direction on full turn around).
    private float turnRadius;
    // How much of a 90 degree turn is left to turn, between 0 and 1 (depends on turn direction if it's negative)
    private float turnArc;
    // Center of circle to walk around when turning.
    private Vector3 turnCenter;
    // How much left to walk to reach the destination
    private float walkDistance;

    private int initDrawModes = 0;

    /// <inheritdoc/>
    public override void OnStart()
    {
        TileDim = MapGlobals.TileDimension;

        Actor.Position = ComputeArrivePosition(MapDir.PlusY, new Int2(MapGlobals.EntryTiles[^1] , -MapGlobals.EntryGridDistance));

        //currentTile = destTile;
        destPos = Actor.Position;
        state = State.ParkEntry;
        (Actor as AnimatedModel).SetParameterValue("Walking", true);
        //(Actor as AnimatedModel).DrawModes = DrawPass.None;

        destTile = new Int2(MapGlobals.EntryTiles[RandomUtil.Random.Next() % MapGlobals.EntryTiles.Length], -MapGlobals.EntryGridDistance);
        currentTile = new Int2((int)(Actor.Position.X / TileDim), (int)(Actor.Position.Z / TileDim));
        destDir = ComputeWalkDirection(MapDir.PlusY);

        Actor.Orientation = ComputeOrientation(MapDir.PlusY);
        currentDir = MapDir.PlusY;
        currentVec = GetWalkVector(currentDir);

        destPos = ComputeArrivePosition(destDir, destTile);
        destVec = GetWalkVector(destDir);

        if (currentTile.X == destTile.X)
        {
            //destTile.Y = -1;
            moveState = MoveState.GoForward;
            CalculateForward();
        }
        else
        {
            moveState = MoveState.ApproachTurn;
            destDir = currentTile.X > destTile.X ? MapDir.MinusX : MapDir.PlusX;
            CalculateTurn();
        }

    }

    private bool PositionOnLine(Vector3 pos, Vector3 linePt, Vector3 lineDir)
    {
        if (linePt == pos)
            return true;
        var dif = linePt - pos;
        if (dif.X == 0.0 || lineDir.X == 0.0)
            return lineDir.X == dif.X && lineDir.Z != 0;
        if (dif.Z == 0.0 || lineDir.Z == 0.0)
            return lineDir.Z == dif.Z && lineDir.X != 0;
        return dif.X / lineDir.X == dif.Z / lineDir.Z;
    }
    
    private bool NearWalkAngle(Vector3 pos, Vector3 dir, Vector3 destPt, Vector3 destDir)
    {
        var dist = (destPt - pos).LengthSquared;
        return dist * (dir - destDir).LengthSquared < 0.1e-4;
    }

    private Vector3 ProjectPointOnLine(Vector3 pointToProject, Vector3 linePos, Vector3 lineDir)
    {
        Vector2 e1 = new Vector2(lineDir.X, lineDir.Z);
        Vector2 e2 = new Vector2(pointToProject.X - linePos.X, pointToProject.Z - linePos.Z);
        double dp = Vector2.Dot(e1, e2);
        double len = e1.LengthSquared;
        return new Vector3(linePos.X + dp * e1.X / len, 0.0, linePos.Z + dp * e1.Y / len);
    }

    private Vector3 PointToLineVector(Vector3 pointToProject, Vector3 linePos, Vector3 lineDir)
    {
        return ProjectPointOnLine(pointToProject, linePos, lineDir) - pointToProject;
    }

    private Vector3 LineIntersection(Vector3 posA, Vector3 dirA, Vector3 posB, Vector3 dirB)
    {
        var div = dirB.Z * dirA.X - dirB.X * dirA.Z;
        // No solution
        if (div == 0)
            return new Vector3(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);
        var dist = posA.Z * dirA.X / div + posB.X * dirA.Z / div - posA.X * dirA.Z / div - posB.Z * dirA.X / div;
        return new Vector3(posB.X + dist * dirB.X, 0.0, posB.Z + dist * dirB.Z);
    }

    private static Vector3 RotateVector(Vector3 vec, float angle, LineSide direction)
    {
        var B = (direction == LineSide.CCWSide) ? angle : -angle;
        return new Vector3(Mathf.Cos(B) * vec.X - Mathf.Sin(B) * vec.Z, 0.0, Mathf.Sin(B) * vec.X + Mathf.Cos(B) * vec.Z);
    }

    // Returns whether a point lies on the "negative" (CCW) or "positive" (CW) side of a line
    private static LineSide LineSideOfPoint(Vector3 pointToCheck, Vector3 linePos, Vector3 lineDir)
    {
        if (Math.Abs(lineDir.Z) < 0.1e-4)
            return ((pointToCheck.Z < linePos.Z) ^ (linePos.X < 0.0)) ? LineSide.CWSide : LineSide.CCWSide;
        if (Math.Abs(lineDir.X) < 0.1e-4)
            return ((pointToCheck.X > linePos.X) ^ (linePos.Z < 0.0)) ? LineSide.CWSide : LineSide.CCWSide;

        bool rev = lineDir.X < 0.1e-4;
        var Z = linePos.Z + (linePos.X - pointToCheck.X) / lineDir.X * lineDir.Z;
        return ((Z > pointToCheck.Z) ^ rev) ? LineSide.CWSide : LineSide.CCWSide;
    }

    private static LineSide OppositeSide(LineSide side)
    {
        return side == LineSide.PositiveSide ? LineSide.NegativeSide : LineSide.PositiveSide;
    }

    // Flips coordinates and negates one on a vector to make a normal vector of the original direction
    // on the specified side. Ignores Y.
    private Vector3 NormalVectorOnSide(Vector3 direction, LineSide side)
    {
        if (side == LineSide.CWSide)
            return new Vector3(direction.Z, 0.0, -direction.X);
        return new Vector3(-direction.Z, 0.0, direction.X);
    }


    /// <inheritdoc/>
    public override void OnUpdate()
    {
        // if (initDrawModes < 2)
        // {
        //     initDrawModes++;
        //     if (initDrawModes == 2) 
        //         (Actor as AnimatedModel).DrawModes = DrawPass.All;
        // }

        var delta = Time.DeltaTime;

        while (delta > 0.1e-4)
        {
            if (moveState == MoveState.ApproachFullTurn)
            {
                var dist = Mathf.Min(WalkingSpeed * delta, walkDistance);
                delta = Mathf.Max(0.0f, delta - (dist / WalkingSpeed));
                walkDistance -= dist;

                Actor.Position += currentVec * dist;

                if (delta > 0.1e-4)
                    moveState = MoveState.TurningFullTurn;
            }

            if (moveState == MoveState.TurningFullTurn)
            {
                // How much distance over the turning arc is remaining
                var arcSize = turnRadius * Mathf.PiOverTwo * turnArc;
                if (arcSize > 0.1e-6 && turnArc >= 0.1e-6)
                {
                    var arcDist = Mathf.Min(WalkingSpeed * delta, arcSize);

                    delta = Mathf.Max(0.0f, delta - (arcDist / WalkingSpeed));

                    arcDist = arcDist / arcSize * turnArc;

                    Actor.Position = turnCenter + RotateVector(Actor.Position - turnCenter, Mathf.PiOverTwo * arcDist, turnSide);

                    turnArc -= arcDist;

                    currentVec = RotateVector(currentVec, Mathf.PiOverTwo * arcDist, turnSide);
                    Actor.Orientation = Quaternion.FromDirection(currentVec * -1.0f);
                }
                if (delta > 0.1e-4)
                {
                    currentDir = currentDir == MapDir.PlusY ? MapDir.MinusX :
                            currentDir == MapDir.MinusX ? MapDir.MinusY :
                            currentDir == MapDir.MinusY ? MapDir.PlusX :
                            MapDir.PlusY;
                    currentVec = GetWalkVector(currentDir);
                    CalculateTurn();
                }
            }

            if (moveState == MoveState.ApproachTurn)
            {
                var dist = Mathf.Min(WalkingSpeed * delta, turnDistance);
                delta = Mathf.Max(0.0f, delta - (dist / WalkingSpeed));
                turnDistance -= dist;

                Actor.Position += currentVec * dist;

                if (delta > 0.1e-4)
                    moveState = MoveState.Turning;
            }

            if (moveState == MoveState.Turning)
            {
                // How much distance over the turning arc is remaining
                var arcSize = turnRadius * Mathf.PiOverTwo * turnArc;
                if (arcSize > 0.1e-6 && turnArc >= 0.1e-6)
                {
                    var arcDist = Mathf.Min(WalkingSpeed * delta, arcSize);

                    delta = Mathf.Max(0.0f, delta - (arcDist / WalkingSpeed));

                    arcDist = arcDist / arcSize * turnArc;

                    Actor.Position = turnCenter + RotateVector(Actor.Position - turnCenter, Mathf.PiOverTwo * arcDist, turnSide);
                    turnArc -= arcDist;
                    currentVec = RotateVector(currentVec, Mathf.PiOverTwo * arcDist, turnSide);

                    Actor.Orientation = Quaternion.FromDirection(currentVec * -1.0f);
                }
                if (delta > 0.1e-4)
                {
                    moveState = MoveState.GoForward;
                    currentVec = destVec;
                    currentDir = destDir;
                    walkDistance = destDir == MapDir.MinusX || destDir == MapDir.PlusX ? Mathf.Abs(destPos.X - Actor.Position.X) : Mathf.Abs(destPos.Z - Actor.Position.Z);
                }
            }

            if (moveState == MoveState.GoForward)
            {
                var dist = Mathf.Min(WalkingSpeed * delta, walkDistance);
                walkDistance -= dist;
                delta = Mathf.Max(0.0f, delta - (dist / WalkingSpeed));

                Actor.Position += currentVec * dist;

                if (delta > 0.1e-4)
                {
                    if (!CalculateNext())
                        delta = 0.0f;
                }
            }

        }
    }

    private MapDir ComputeWalkDirection(MapDir defaultDir)
    {
        if (destTile.X < currentTile.X)
            return MapDir.MinusX;
        else if (destTile.X > currentTile.X)
            return MapDir.PlusX;
        else if (destTile.Y < currentTile.Y)
            return MapDir.MinusY;
        else if (destTile.Y > currentTile.Y)
            return MapDir.PlusY;
        return defaultDir;
    }

    private Vector3 ComputeArrivePosition(MapDir dir, Int2 tile)
    {
        var result = new Vector3(tile.X * TileDim + TileDim * 0.5f, 0.0, tile.Y * TileDim + TileDim * 0.5f);
        switch (dir)
        {
            case MapDir.PlusY:
                result.X += TileDim * (0.5f - LaneSideDistance);
                result.Z -= TileDim * 0.5f;
                break;
            case MapDir.MinusY:
                result.X -= TileDim * (0.5f - LaneSideDistance);
                result.Z += TileDim * 0.5f;
                break;
            case MapDir.PlusX:
                result.X -= TileDim * 0.5f;
                result.Z -= TileDim * (0.5f - LaneSideDistance);
                break;
            case MapDir.MinusX:
                result.X += TileDim * 0.5f;
                result.Z += TileDim * (0.5f - LaneSideDistance);
                break;
        }
        return result;
    }

    private Quaternion ComputeOrientation(MapDir dir)
    {
        switch (dir)
        {
            case MapDir.MinusY:
                return Quaternion.Euler(0.0f, 0.0f, 0.0f);
            case MapDir.PlusX:
                return Quaternion.Euler(0.0f, -90.0f, 0.0f);
            case MapDir.MinusX:
                return Quaternion.Euler(0.0f, 90.0f, 0.0f);
            default:
                return Quaternion.Euler(0.0f, 180.0f, 0.0f);
        }
    }

    private Vector3 GetWalkVector(MapDir dir)
    {
        switch (dir)
        {
            case MapDir.MinusY:
                return new Vector3(0.0, 0.0, -1.0);
            case MapDir.PlusX:
                return new Vector3(1.0, 0.0, 0.0);
            case MapDir.MinusX:
                return new Vector3(-1.0, 0.0, 0.0);
            default: // PlusY
                return new Vector3(0.0, 0.0, 1.0);
        }
    }

    private void CalculateFullTurn()
    {
        walkDistance = MapGlobals.TileDimension * (1.0f - LaneSideDistance * 0.5f) - MaximumTurnRadius;
        turnArc = 1.0f;
        turnRadius = Mathf.Min(MaximumTurnRadius, MapGlobals.TileDimension * (0.5f - LaneSideDistance) );
        turnSide = LineSide.CCWSide;
        turnCenter = Actor.Position + currentVec * walkDistance + NormalVectorOnSide(currentVec, turnSide) * turnRadius;
    }

    private void CalculateForward()
    {
        if (currentDir == MapDir.PlusY || currentDir == MapDir.MinusY)
            walkDistance = Mathf.Abs(destPos.Z - Actor.Position.Z);
        else
            walkDistance = Mathf.Abs(destPos.X - Actor.Position.X);
    }

    private void CalculateTurn()
    {
        var pos = Actor.Position;
        var linedist = 0.0f;
        switch (destDir)
        {
            case MapDir.PlusX:
                linedist = Mathf.Abs(destPos.Z - pos.Z);
                turnSide = currentDir == MapDir.PlusY ? LineSide.CWSide : LineSide.CCWSide;
                break;
            case MapDir.MinusX:
                linedist = Mathf.Abs(destPos.Z - pos.Z);
                turnSide = currentDir == MapDir.PlusY ? LineSide.CCWSide : LineSide.CWSide;
                break;
            case MapDir.PlusY:
                linedist = Mathf.Abs(destPos.X - pos.X);
                turnSide = currentDir == MapDir.PlusX ? LineSide.CCWSide : LineSide.CWSide;
                break;
            case MapDir.MinusY:
                linedist = Mathf.Abs(destPos.X - pos.X);
                turnSide = currentDir == MapDir.PlusX ? LineSide.CWSide : LineSide.CCWSide;
                break;
        }

        turnDistance = Mathf.Max(linedist - MaximumTurnRadius, 0.0f);
        turnRadius = linedist - turnDistance;

        if (turnDistance != 0.0f)
            moveState = MoveState.ApproachTurn;
        else
            moveState = MoveState.Turning;

        turnArc = 1.0f;

        turnCenter = Actor.Position + (currentVec * turnDistance) + NormalVectorOnSide(currentVec, turnSide) * turnRadius;
    }

    private bool CalculateNext()
    {
        currentTile = destTile;
        currentVec = destVec;
        currentDir = destDir;

        if (state == State.ParkEntry)
        {
            state = State.Walking;
            destTile.Y = -1;
        }
        else
        {
            destTile = MapGlobals.MapNavigation.PickTile(currentTile, destDir == MapDir.PlusY ? MapNavigation.Direction.Up :
                    (destDir == MapDir.MinusY ? MapNavigation.Direction.Down :
                    (destDir == MapDir.PlusX ? MapNavigation.Direction.Right : MapNavigation.Direction.Left)));
        }

        var result = destTile != currentTile;

        (Actor as AnimatedModel).SetParameterValue("Walking", result);
        if (!result)
            return false;

        destDir = ComputeWalkDirection(destDir);
        destPos = ComputeArrivePosition(destDir, destTile);
        destVec = GetWalkVector(destDir);

        if ((currentDir == MapDir.PlusY && currentTile.Y < destTile.Y) || (currentDir == MapDir.MinusY && currentTile.Y > destTile.Y) ||
            (currentDir == MapDir.PlusX && currentTile.X < destTile.X) || (currentDir == MapDir.MinusX && currentTile.X > destTile.X))
        {
            moveState = MoveState.GoForward;
            CalculateForward();
        }
        else if ((currentDir == MapDir.PlusY && currentTile.Y > destTile.Y) || (currentDir == MapDir.MinusY && currentTile.Y < destTile.Y) ||
            (currentDir == MapDir.PlusX && currentTile.X > destTile.X) || (currentDir == MapDir.MinusX && currentTile.X < destTile.X))
        {
            moveState = MoveState.ApproachFullTurn;
            CalculateFullTurn();
        }
        else
        {
            moveState = MoveState.ApproachTurn;
            CalculateTurn();
        }

        return result;
    }
}
