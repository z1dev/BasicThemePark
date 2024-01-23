#pragma once

#include <memory>
#include <map>
#include "Engine/Scripting/Script.h"
#include "Engine/Core/Math/Vector2.h"
#include "Engine/Core/Collections/HashSet.h"


API_ENUM() enum class NavDir : uint8
{
    Up,
    Down,
    Left,
    Right
};


API_CLASS() class GAME_API MapNavigation : public Script
{
API_AUTO_SERIALIZATION();
DECLARE_SCRIPTING_TYPE(MapNavigation);

private:

    enum class CellType : uint16
    {
        Empty,
        Path,
    };

    enum class PathType : uint8
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
    };

    struct MapCell
    {
    };

    struct PathCell : public MapCell
    {
        PathType type;
        uint32 dirt;
    };

    struct Cell
    {
        CellType cell_type;
        std::shared_ptr<MapCell> data;
    };

    enum class ChangeType : uint8
    {
        None,
        Adding,
        Removing,
        Invalid
    };

public:
    API_FUNCTION() void SetMapData(Int2 size);
    API_FUNCTION() void AddPath(Int2 pos);
    API_FUNCTION() void RemovePath(Int2 pos);
    API_FUNCTION() void BeginChange();
    API_FUNCTION() void EndChange();
    API_FUNCTION() Int2 PickTile(Int2 pos, NavDir dir);
private:

    void SetCell(int index, CellType type);
    void ClearCell(int index);

    Int2 ForwardFrom(Int2 pos, NavDir dir) const;
    NavDir TurnDirection(NavDir orig, NavDir side) const;
    bool ValidPos(Int2 pos) const;
    int CellIndex(Int2 pos) const;

    void UpdatePathType(Int2 pos, HashSet<Int2> updated);
    void SetPathType(int index, PathType type);
    PathType GetPathType(int index) const;


    Int2 map_size;
    Array<Cell> map_cells;

    // A list of path items that are changed and need their cell type updated. 
    Array<Int2> changes;

    ChangeType change_type;
    bool changing;


};
