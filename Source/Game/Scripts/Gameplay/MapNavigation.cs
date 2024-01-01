using System.Collections.Generic;
using FlaxEngine;

namespace Game;

/// <summary>
/// MapNavigation Script.
/// </summary>
public class MapNavigation : Script
{
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
    public void SetMapSize(Int2 size)
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
            changes.Add(pos);
    }

    public void RemovePath(Int2 pos)
    {
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
    }

    public void EndChange()
    {
        if (changes.Count == 0 || changeType == ChangeType.None || changeType == ChangeType.Invalid)
        {
            changes = [];
            changeType = ChangeType.None;
            return;
        }

        changeType = ChangeType.None;

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

        changes = [];
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
                pos.X == 0 || pos.Y == 0 || mapCells[index - mapSize.X - 1].ctype != CellType.Empty,
                pos.X == 0 || mapCells[index - 1].ctype != CellType.Empty,
                pos.X == 0 || pos.Y >= mapSize.Y - 1 || mapCells[index + mapSize.X - 1].ctype != CellType.Empty,
            },
            {
                pos.Y == 0 || mapCells[index - mapSize.X].ctype != CellType.Empty,
                true,
                pos.Y >= mapSize.Y - 1 || mapCells[index + mapSize.X].ctype != CellType.Empty
            },
            {
                pos.X >= mapSize.X - 1 || pos.Y == 0 || mapCells[index - mapSize.X + 1].ctype != CellType.Empty,
                pos.X >= mapSize.X - 1 || mapCells[index + 1].ctype != CellType.Empty,
                pos.X >= mapSize.X - 1 || pos.Y >= mapSize.Y - 1 || mapCells[index + mapSize.X + 1].ctype != CellType.Empty
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
        else if (horz == 1 && vert == 1 && diagonals != 0)
        {
            if ((sides[0, 0] && sides[0, 1] && sides[1, 0]) || (sides[2, 0] && sides[1, 0] && sides[2, 1]) ||
                    (sides[0, 2] && sides[0, 1] && sides[1, 2]) || (sides[2, 2] && sides[1, 2] && sides[2, 1]))
                mapCells[index].ctype = CellType.OuterCorner;
            else
                mapCells[index].ctype = CellType.Turn;
        }
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
