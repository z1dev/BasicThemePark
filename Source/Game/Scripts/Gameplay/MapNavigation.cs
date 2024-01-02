using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using FlaxEngine;

namespace Game;

/// <summary>
/// MapNavigation Script.
/// </summary>
public class MapNavigation : Script
{
    public enum Direction
    {
        Up,
        Down,
        Left,
        Right
    }

    private enum CellType
    {
        // No path in cell.
        Empty,
        // Path cell on each side, including diagonals. Low probability of turning.
        Middle,
        // Path cell on most sides, apart from non-crossing diagonals.
        InnerCorner,
        // Path cell to exactly one vertical and one horizontal side, and the corner between. The rest of the diagonals are ignored.
        OuterCorner,
        // At least one path to a side with both neighboring diagonals missing.
        Crossing,
        // Only path to one side. Diagonals are ignored.
        DeadEnd,
        // No cells on either side. Diagonals are ignored.
        Isolated,
        // Cell with two other cells to the sides. Either both horizontal or both vertical.
        Straight,
        // A cell on the horizontal and a cell on the vertical side
        Turn,
        // All three cells in one side are empty, every other one is not. Low probability of turning
        Side,
    }

    private struct Cell
    {
        public CellType ctype;
    }

    private enum ChangeType
    {
        None,
        Adding,
        Removing,
        Invalid
    }

    private Int2 mapSize;
    private Cell[] mapCells;

    // A list of path items that are changed and need their cell type updated. 
    private List<Int2> changes = [];
    private ChangeType changeType;
    private bool changing = false;


    // /// <inheritdoc/>
    // public override void OnStart()
    // {
    //     // Here you can add code that needs to be called when script is created, just before the first game update
    // }

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

    // Call it only at the creation of the map. Any other time and it causes undefined behavior.
    public void SetMapData(Int2 size)
    {
        mapSize = size;
        if (mapSize.X > 0 && mapSize.Y > 0)
        {
            mapCells = new Cell[mapSize.X * mapSize.Y];
        }
        else
        {
            mapSize = new();
            mapCells = [];
        }
    }

    public void AddPath(Int2 pos)
    {
        if (!changing)
        {
            Debug.LogError("Trying to add to navigation when not in change.");
            return;
        }

        if (changeType == ChangeType.None)
            changeType = ChangeType.Adding;
        else if (changeType != ChangeType.Adding)
        {
            if (changeType != ChangeType.Invalid)
                Debug.LogError("Cannot add and remove cells in the same operation.");
            changeType = ChangeType.Invalid;
            return;
        }

        var index = CellIndex(pos);
        if (index < 0)
            return;
        if (mapCells[index].ctype == CellType.Empty)
        {
            changes.Add(pos);
        }
    }

    public void RemovePath(Int2 pos)
    {
        if (!changing)
        {
            Debug.LogError("Trying to remove to navigation when not in change.");
            return;
        }

        if (changeType == ChangeType.None)
            changeType = ChangeType.Removing;
        else if (changeType != ChangeType.Removing)
        {
            if (changeType != ChangeType.Invalid)
                Debug.LogError("Cannot add and remove cells in the same operation.");
            changeType = ChangeType.Invalid;
            return;
        }

        var index = CellIndex(pos);
        if (index < 0)
            return;
        if (mapCells[index].ctype != CellType.Empty)
            changes.Add(pos);
    }

    public void BeginChange()
    {
        if (changing)
            Debug.LogError("Already changing navigation.");
        changing = true;
    }

    public void EndChange()
    {
        if (!changing || changes.Count == 0 || changeType == ChangeType.None || changeType == ChangeType.Invalid)
        {
            if (!changing)
                Debug.LogError("Can't end change when not changing navigation.");
            changing = false;
            changes = [];
            changeType = ChangeType.None;
            return;
        }

        foreach (var pos in changes)
        {
            var index = CellIndex(pos);
            if (changeType == ChangeType.Adding && mapCells[index].ctype == CellType.Empty)
                mapCells[index].ctype = CellType.Middle;
            else if (changeType == ChangeType.Removing && mapCells[index].ctype != CellType.Empty)
                mapCells[index].ctype = CellType.Empty;
        }

        HashSet<Int2> updated = [];

        Int2[] directions = [new Int2(1, 0), new Int2(-1, 0), new Int2(0, 1), new Int2(0, -1),
                new Int2(1, 1), new Int2(-1, -1), new Int2(-1, 1), new Int2(1, -1), ];

        foreach (var pos in changes)
        {
            if (changeType == ChangeType.Adding)
                UpdateCellType(pos, updated);
            foreach (var dir in directions)
                UpdateCellType(pos + dir, updated);
        }

        changing = false;
        changeType = ChangeType.None;
        changes = [];
    }

    public Int2 PickTile(Int2 pos, Direction dir)
    {
        if (pos.Y < 0)
        {
            var nextCell = ForwardFrom(pos, dir);
            var index = CellIndex(nextCell);
            // First time entering:
            if (index == -1 || mapCells[index].ctype == CellType.Empty)
                return pos;
            return nextCell;
        }

        var cindex = CellIndex(pos);
        CellType upCell = pos.Y >= mapSize.Y - 1 ? CellType.Empty : mapCells[cindex + mapSize.X].ctype;
        CellType downCell = pos.Y == 0 ? CellType.Empty : mapCells[cindex - mapSize.X].ctype;
        CellType leftCell = pos.X == 0 ? CellType.Empty : mapCells[cindex - 1].ctype;
        CellType rightCell = pos.X >= mapSize.X - 1 ? CellType.Empty : mapCells[cindex + 1].ctype;

        const float MIDDLE_TURN_PROBABILITY = 0.1f;
        const float INNER_CORNER_TURN_PROBABILITY = 0.2f;
        const float CROSSING_TURN_PROBABILITY = 0.25f;

        //Debug.Log(mapCells[cindex].ctype);
        switch (mapCells[cindex].ctype)
        {
            // Path cell on each side, including diagonals. Low probability of turning.
            case CellType.Middle:
                if (RandomUtil.Rand() < MIDDLE_TURN_PROBABILITY)
                {
                    var dirRng = RandomUtil.Rand();
                    if (dirRng < 0.5f)
                        return ForwardFrom(pos, TurnDirection(dir, Direction.Left));
                    else
                        return ForwardFrom(pos, TurnDirection(dir, Direction.Right));

                }
                break;
            case CellType.Side:
            {
                var fwd = ForwardFrom(pos, dir);
                var index = CellIndex(fwd);
                if (index != -1 && mapCells[index].ctype != CellType.Empty && RandomUtil.Rand() < MIDDLE_TURN_PROBABILITY)
                {
                    var turned = ForwardFrom(pos, TurnDirection(dir, Direction.Left));
                    index = CellIndex(turned);
                    if (index > -1 && mapCells[index].ctype != CellType.Empty)
                        return turned;
                    turned = ForwardFrom(pos, TurnDirection(dir, Direction.Right));
                    index = CellIndex(turned);
                    if (index > -1 && mapCells[index].ctype != CellType.Empty)
                        return turned;
                }
                break;
            }
            // Path cell on most sides, apart from non-crossing diagonals.
            case CellType.InnerCorner:
                if (RandomUtil.Rand() < INNER_CORNER_TURN_PROBABILITY)
                {
                    var dirRng = RandomUtil.Rand();
                    if (dirRng < 0.5f)
                    {
                        var fwd = ForwardFrom(pos, TurnDirection(dir, Direction.Left));
                        var fwdIndex = CellIndex(fwd);
                        if (fwdIndex != -1 && mapCells[fwdIndex].ctype != CellType.Empty)
                            return fwd;
                    }
                    else
                    {
                        var fwd = ForwardFrom(pos, TurnDirection(dir, Direction.Right));
                        var fwdIndex = CellIndex(fwd);
                        if (fwdIndex != -1 && mapCells[fwdIndex].ctype != CellType.Empty)
                            return fwd;
                    }

                }
                break;
            case CellType.Turn:
                goto case CellType.OuterCorner;
            case CellType.OuterCorner:
                if (leftCell != CellType.Empty && dir != Direction.Right)
                    return ForwardFrom(pos, Direction.Left);
                if (rightCell != CellType.Empty && dir != Direction.Left)
                    return ForwardFrom(pos, Direction.Right);
                if (upCell != CellType.Empty && dir != Direction.Down)
                    return ForwardFrom(pos, Direction.Up);
                return ForwardFrom(pos, Direction.Down);
            // At least one path to a side with both neighboring diagonals missing.
            case CellType.Crossing:
                if (RandomUtil.Rand() < CROSSING_TURN_PROBABILITY)
                {
                    var turnCnt = 0;
                    if (dir != Direction.Up && (downCell == CellType.Straight || downCell == CellType.DeadEnd))
                        turnCnt++;
                    if (dir != Direction.Left && (rightCell == CellType.Straight || rightCell == CellType.DeadEnd))
                        turnCnt++;
                    if (dir != Direction.Right && (leftCell == CellType.Straight || leftCell == CellType.DeadEnd))
                        turnCnt++;
                    if (dir != Direction.Down && (upCell == CellType.Straight || upCell == CellType.DeadEnd))
                        turnCnt++;
                    if (turnCnt != 0)
                    {
                        var turnSide = RandomUtil.Rand();
                        if (dir != Direction.Up && (downCell == CellType.Straight || downCell == CellType.DeadEnd))
                        {
                            if (turnSide < (1.0f / turnCnt))
                                return ForwardFrom(pos, Direction.Down);
                            turnSide -= 1.0f / turnCnt;
                        }
                        if (dir != Direction.Left && (rightCell == CellType.Straight || rightCell == CellType.DeadEnd))
                        {
                            if (turnSide < (1.0f / turnCnt))
                                return ForwardFrom(pos, Direction.Right);
                            turnSide -= 1.0f / turnCnt;
                        }
                        if (dir != Direction.Right && (leftCell == CellType.Straight || leftCell == CellType.DeadEnd))
                        {
                            if (turnSide < (1.0f / turnCnt))
                                return ForwardFrom(pos, Direction.Left);
                            turnSide -= 1.0f / turnCnt;
                        }
                        return ForwardFrom(pos, Direction.Up);
                    }
                }
                break;
            case CellType.DeadEnd:
            {
                if (upCell != CellType.Empty)
                    return ForwardFrom(pos, Direction.Up);
                if (downCell != CellType.Empty)
                    return ForwardFrom(pos, Direction.Down);
                if (leftCell != CellType.Empty)
                    return ForwardFrom(pos, Direction.Left);
                if (rightCell != CellType.Empty)
                    return ForwardFrom(pos, Direction.Right);
                return pos;
            }
            case CellType.Isolated:
                return pos;
            // // Cell with two other cells to the sides. Either both horizontal or both vertical.
            // default: // case CellType.Straight:
            //     return ForwardFrom(pos, dir);
        }
        var nextPos = ForwardFrom(pos, dir);
        var nextIndex = CellIndex(nextPos);
        if (nextIndex == -1 || mapCells[nextIndex].ctype == CellType.Empty)
        {
            // Must turn left or right.
            var leftPos = ForwardFrom(pos, TurnDirection(dir, Direction.Left));
            var rightPos = ForwardFrom(pos, TurnDirection(dir, Direction.Right));
            var leftEmpty = CellIndex(leftPos) == -1 || mapCells[CellIndex(leftPos)].ctype == CellType.Empty;
            var rightEmpty = CellIndex(rightPos) == -1 || mapCells[CellIndex(rightPos)].ctype == CellType.Empty;
            if (leftEmpty && !rightEmpty)
                return rightPos;
            if (rightEmpty && !leftEmpty)
                return leftPos;
            else if (!leftEmpty && !rightEmpty)
                return RandomUtil.Rand() < 0.5 ? leftPos : rightPos;
        }

        return nextPos;
    }

    private Int2 ForwardFrom(Int2 pos, Direction dir)
    {
        switch(dir)
        {
            case Direction.Up:
                return new Int2(pos.X, pos.Y + 1);
            case Direction.Down:
                return new Int2(pos.X, pos.Y - 1);
            case Direction.Left:
                return new Int2(pos.X - 1, pos.Y);
            case Direction.Right:
                return new Int2(pos.X + 1, pos.Y);
        }
        return pos;
    }

    private Direction TurnDirection(Direction orig, Direction side)
    {
        if (orig == Direction.Up)
            return side;
        if (orig == Direction.Left)
            return side == Direction.Left ? Direction.Down : Direction.Up;
        if (orig == Direction.Right)
            return side == Direction.Left ? Direction.Up : Direction.Down;
        return side == Direction.Left ? Direction.Right : Direction.Left;
    }

    private void UpdateCellType(Int2 pos, HashSet<Int2> updated)
    {
        if (!ValidPos(pos) || !updated.Add(pos))
            return;

        var index = CellIndex(pos);
        if (mapCells[index].ctype == CellType.Empty)
            return;

        bool[,] sides = {
            {
                pos.X > 0 && pos.Y < mapSize.Y - 1 && mapCells[index + mapSize.X - 1].ctype != CellType.Empty,
                pos.X > 0 && mapCells[index - 1].ctype != CellType.Empty,
                pos.X > 0 && pos.Y > 0 && mapCells[index - mapSize.X - 1].ctype != CellType.Empty
            },
            {
                pos.Y < mapSize.Y - 1 && mapCells[index + mapSize.X].ctype != CellType.Empty,
                true,
                pos.Y > 0 && mapCells[index - mapSize.X].ctype != CellType.Empty
            },
            {
                pos.X < mapSize.X - 1 && pos.Y < mapSize.Y - 1 && mapCells[index + mapSize.X + 1].ctype != CellType.Empty,
                pos.X < mapSize.X - 1 && mapCells[index + 1].ctype != CellType.Empty,
                pos.X < mapSize.X - 1 && pos.Y > 0 && mapCells[index - mapSize.X + 1].ctype != CellType.Empty
            }
        };
        
        var horz = (sides[0, 1] ? 1 : 0) + (sides[2, 1] ? 1 : 0);
        var vert = (sides[1, 0] ? 1 : 0) + (sides[1, 2] ? 1 : 0);
        var diagonals = (sides[0, 0] ? 1 : 0) + (sides[2, 2] ? 1 : 0) + (sides[0, 2] ? 1 : 0) + (sides[2, 0] ? 1 : 0);

        if (horz == 2 && vert == 2 && diagonals == 4)
            mapCells[index].ctype = CellType.Middle;
        else if (horz == 0 && vert == 0)
            mapCells[index].ctype = CellType.Isolated;
        else if (horz == 0 || vert == 0)
        {
            if (horz == 2 || vert == 2)
                mapCells[index].ctype = CellType.Straight;
            else if (horz == 1 || vert == 1)
                mapCells[index].ctype = CellType.DeadEnd;
        }
        else if (horz == 1 && vert == 1)
        {
            if ((sides[0, 0] && sides[0, 1] && sides[1, 0]) || (sides[2, 0] && sides[1, 0] && sides[2, 1]) ||
                    (sides[0, 2] && sides[0, 1] && sides[1, 2]) || (sides[2, 2] && sides[1, 2] && sides[2, 1]))
                mapCells[index].ctype = CellType.OuterCorner;
            else
                mapCells[index].ctype = CellType.Turn;
        }
        else if (horz + vert == 3 && diagonals == 2 && ((!sides[0, 0] && !sides[1, 0] && !sides[2, 0]) ||
                (!sides[0, 0] && !sides[0, 1] && !sides[0, 2]) || (!sides[2, 0] && !sides[2, 1] && !sides[2, 2]) ||
                (!sides[0, 2] && !sides[1, 2] && !sides[2, 2])))
            mapCells[index].ctype = CellType.Side;
        else if ((!sides[0, 0] && !sides[2, 0]) || (!sides[0, 0] && !sides[0, 2]) || (!sides[2, 0] && !sides[2, 2]) || (!sides[0, 2] && !sides[2, 2]))
            mapCells[index].ctype = CellType.Crossing;
        else
            mapCells[index].ctype = CellType.InnerCorner;

    }

    private bool ValidPos(Int2 pos)
    {
        return pos.X >= 0 && pos.Y >= 0 && pos.X < mapSize.X && pos.Y < mapSize.Y;
    }

    private int CellIndex(Int2 pos)
    {
        if (!ValidPos(pos))
            return -1;
        
        return pos.X + pos.Y * mapSize.X;
    }

}
