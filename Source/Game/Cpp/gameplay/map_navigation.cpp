#include "map_navigation.h"
#include "Engine/Debug/DebugLog.h"
#include "../util/randomizer.h"


MapNavigation::MapNavigation(const SpawnParams& params)
    : Script(params), map_size(0, 0), change_type(ChangeType::None), changing(false)
{
    // Enable ticking OnUpdate function
    //_tickUpdate = true;

}

// Call it only at the creation of the map. Any other time and it causes undefined behavior.
void MapNavigation::SetMapData(Int2 size)
{
    map_size = size;
    if (map_size.X > 0 && map_size.Y > 0)
    {
        map_cells.AddZeroed(map_size.X * map_size.Y);
    }
    else
    {
        map_size = Int2(0, 0);
        map_cells.Clear();
    }
}

void MapNavigation::AddPath(Int2 pos)
{
    if (!changing)
    {
        DebugLog::LogError(TEXT("Trying to add to navigation when not in change."));
        return;
    }

    if (change_type == ChangeType::None)
        change_type = ChangeType::Adding;
    else if (change_type != ChangeType::Adding)
    {
        if (change_type != ChangeType::Invalid)
            DebugLog::LogError(TEXT("Cannot add and remove cells in the same operation."));
        change_type = ChangeType::Invalid;
        return;
    }

    int index = CellIndex(pos);
    if (index < 0)
        return;
    if (map_cells[index].cell_type == CellType::Empty)
        changes.Add(pos);
}

void MapNavigation::RemovePath(Int2 pos)
{
    if (!changing)
    {
        DebugLog::LogError(TEXT("Trying to remove to navigation when not in change."));
        return;
    }

    if (change_type == ChangeType::None)
        change_type = ChangeType::Removing;
    else if (change_type != ChangeType::Removing)
    {
        if (change_type != ChangeType::Invalid)
            DebugLog::LogError(TEXT("Cannot add and remove cells in the same operation."));
        change_type = ChangeType::Invalid;
        return;
    }

    int index = CellIndex(pos);
    if (index < 0)
        return;
    if (map_cells[index].cell_type != CellType::Empty)
        changes.Add(pos);
}

void MapNavigation::BeginChange()
{
    if (changing)
        DebugLog::LogError(TEXT("Already changing navigation."));
    changing = true;
}

void MapNavigation::EndChange()
{
    if (!changing || changes.Count() == 0 || change_type == ChangeType::None || change_type == ChangeType::Invalid)
    {
        if (!changing)
            DebugLog::LogError(TEXT("Can't end change when not changing navigation."));
        changing = false;
        changes.Clear();
        change_type = ChangeType::None;
        return;
    }

    for (Int2 pos : changes)
    {
        int index = CellIndex(pos);
        if (change_type == ChangeType::Adding && map_cells[index].cell_type == CellType::Empty)
            SetCell(index, CellType::Path);
        else if (change_type == ChangeType::Removing && map_cells[index].cell_type != CellType::Empty)
            ClearCell(index);
    }

    HashSet<Int2> updated;

    Array<Int2> directions = { Int2(1, 0), Int2(-1, 0), Int2(0, 1), Int2(0, -1),
            Int2(1, 1), Int2(-1, -1), Int2(-1, 1), Int2(1, -1), };

    for (Int2 pos : changes)
    {
        if (change_type == ChangeType::Adding)
            UpdatePathType(pos, updated);
        for (Int2 dir : directions)
            UpdatePathType(pos + dir, updated);
    }

    changing = false;
    change_type = ChangeType::None;
    changes.Clear();
}

Int2 MapNavigation::PickTile(Int2 pos, NavDir dir)
{
    if (pos.Y < 0)
    {
        Int2 nextCell = ForwardFrom(pos, dir);
        int index = CellIndex(nextCell);
        // First time entering:
        if (index == -1 || map_cells[index].cell_type == CellType::Empty)
            return pos;
        return nextCell;
    }

    int cindex = CellIndex(pos);
    PathType upCell = pos.Y >= map_size.Y - 1 ? PathType::Empty : GetPathType(cindex + map_size.X);
    PathType downCell = pos.Y == 0 ? PathType::Empty : GetPathType(cindex - map_size.X);
    PathType leftCell = pos.X == 0 ? PathType::Empty : GetPathType(cindex - 1);
    PathType rightCell = pos.X >= map_size.X - 1 ? PathType::Empty : GetPathType(cindex + 1);

    const float MIDDLE_TURN_PROBABILITY = 0.1f;
    const float INNER_CORNER_TURN_PROBABILITY = 0.2f;
    const float CROSSING_TURN_PROBABILITY = 0.25f;

    switch (GetPathType(cindex))
    {
        // Path cell on each side, including diagonals. Low probability of turning.
        case PathType::Middle:
            if (Randomizer::Rand() < MIDDLE_TURN_PROBABILITY)
            {
                float dirRng = Randomizer::Rand();
                if (dirRng < 0.5f)
                    return ForwardFrom(pos, TurnDirection(dir, NavDir::Left));
                else
                    return ForwardFrom(pos, TurnDirection(dir, NavDir::Right));

            }
            break;
        case PathType::Side:
        {
            Int2 fwd = ForwardFrom(pos, dir);
            int index = CellIndex(fwd);
            if (index != -1 && GetPathType(index) != PathType::Empty && Randomizer::Rand() < MIDDLE_TURN_PROBABILITY)
            {
                Int2 turned = ForwardFrom(pos, TurnDirection(dir, NavDir::Left));
                index = CellIndex(turned);
                if (index > -1 && GetPathType(index) != PathType::Empty)
                    return turned;
                turned = ForwardFrom(pos, TurnDirection(dir, NavDir::Right));
                index = CellIndex(turned);
                if (index > -1 && GetPathType(index) != PathType::Empty)
                    return turned;
            }
            break;
        }
        // Path cell on most sides, apart from non-crossing diagonals.
        case PathType::InnerCorner:
            if (Randomizer::Rand() < INNER_CORNER_TURN_PROBABILITY)
            {
                float dirRng = Randomizer::Rand();
                if (dirRng < 0.5f)
                {
                    Int2 fwd = ForwardFrom(pos, TurnDirection(dir, NavDir::Left));
                    int fwdIndex = CellIndex(fwd);
                    if (fwdIndex != -1 && GetPathType(fwdIndex) != PathType::Empty)
                        return fwd;
                }
                else
                {
                    Int2 fwd = ForwardFrom(pos, TurnDirection(dir, NavDir::Right));
                    int fwdIndex = CellIndex(fwd);
                    if (fwdIndex != -1 && GetPathType(fwdIndex) != PathType::Empty)
                        return fwd;
                }

            }
            break;
        case PathType::Turn:
        case PathType::OuterCorner:
            if (leftCell != PathType::Empty && dir != NavDir::Right)
                return ForwardFrom(pos, NavDir::Left);
            if (rightCell != PathType::Empty && dir != NavDir::Left)
                return ForwardFrom(pos, NavDir::Right);
            if (upCell != PathType::Empty && dir != NavDir::Down)
                return ForwardFrom(pos, NavDir::Up);
            return ForwardFrom(pos, NavDir::Down);
        // At least one path to a side with both neighboring diagonals missing.
        case PathType::Crossing:
            if (Randomizer::Rand() < CROSSING_TURN_PROBABILITY)
            {
                int turnCnt = 0;
                if (dir != NavDir::Up && (downCell == PathType::Straight || downCell == PathType::DeadEnd))
                    turnCnt++;
                if (dir != NavDir::Left && (rightCell == PathType::Straight || rightCell == PathType::DeadEnd))
                    turnCnt++;
                if (dir != NavDir::Right && (leftCell == PathType::Straight || leftCell == PathType::DeadEnd))
                    turnCnt++;
                if (dir != NavDir::Down && (upCell == PathType::Straight || upCell == PathType::DeadEnd))
                    turnCnt++;
                if (turnCnt != 0)
                {
                    float turnSide = Randomizer::Rand();
                    if (dir != NavDir::Up && (downCell == PathType::Straight || downCell == PathType::DeadEnd))
                    {
                        if (turnSide < (1.0f / turnCnt))
                            return ForwardFrom(pos, NavDir::Down);
                        turnSide -= 1.0f / turnCnt;
                    }
                    if (dir != NavDir::Left && (rightCell == PathType::Straight || rightCell == PathType::DeadEnd))
                    {
                        if (turnSide < (1.0f / turnCnt))
                            return ForwardFrom(pos, NavDir::Right);
                        turnSide -= 1.0f / turnCnt;
                    }
                    if (dir != NavDir::Right && (leftCell == PathType::Straight || leftCell == PathType::DeadEnd))
                    {
                        if (turnSide < (1.0f / turnCnt))
                            return ForwardFrom(pos, NavDir::Left);
                        turnSide -= 1.0f / turnCnt;
                    }
                    return ForwardFrom(pos, NavDir::Up);
                }
            }
            break;
        case PathType::DeadEnd:
        {
            if (upCell != PathType::Empty)
                return ForwardFrom(pos, NavDir::Up);
            if (downCell != PathType::Empty)
                return ForwardFrom(pos, NavDir::Down);
            if (leftCell != PathType::Empty)
                return ForwardFrom(pos, NavDir::Left);
            if (rightCell != PathType::Empty)
                return ForwardFrom(pos, NavDir::Right);
            return pos;
        }
        case PathType::Isolated:
            return pos;
        // // Cell with two other cells to the sides. Either both horizontal or both vertical.
        // default: // case PathType::Straight:
        //     return ForwardFrom(pos, dir);
    }
    Int2 nextPos = ForwardFrom(pos, dir);
    int nextIndex = CellIndex(nextPos);
    if (nextIndex == -1 || GetPathType(nextIndex) == PathType::Empty)
    {
        // Must turn left or right.
        Int2 leftPos = ForwardFrom(pos, TurnDirection(dir, NavDir::Left));
        Int2 rightPos = ForwardFrom(pos, TurnDirection(dir, NavDir::Right));
        int leftEmpty = CellIndex(leftPos) == -1 || GetPathType(CellIndex(leftPos)) == PathType::Empty;
        int rightEmpty = CellIndex(rightPos) == -1 || GetPathType(CellIndex(rightPos)) == PathType::Empty;
        if (leftEmpty && !rightEmpty)
            return rightPos;
        if (rightEmpty && !leftEmpty)
            return leftPos;
        else if (!leftEmpty && !rightEmpty)
            return Randomizer::Rand() < 0.5 ? leftPos : rightPos;
    }

    return nextPos;
}

void MapNavigation::SetCell(int index, CellType type)
{
    if (map_cells[index].cell_type != CellType::Empty)
        return;

    Cell &cell = map_cells[index];
    cell.cell_type = CellType::Path;
}

void MapNavigation::ClearCell(int index)
{
    if (map_cells[index].cell_type == CellType::Empty)
        return;

    Cell &cell = map_cells[index];
    cell.cell_type = CellType::Empty;
    cell.data.reset();
}



Int2 MapNavigation::ForwardFrom(Int2 pos, NavDir dir) const
{
    switch(dir)
    {
        case NavDir::Up:
            return Int2(pos.X, pos.Y + 1);
        case NavDir::Down:
            return Int2(pos.X, pos.Y - 1);
        case NavDir::Left:
            return Int2(pos.X - 1, pos.Y);
        case NavDir::Right:
            return Int2(pos.X + 1, pos.Y);
    }
    return pos;
}

NavDir MapNavigation::TurnDirection(NavDir orig, NavDir side) const
{
    if (orig == NavDir::Up)
        return side;
    if (orig == NavDir::Left)
        return side == NavDir::Left ? NavDir::Down : NavDir::Up;
    if (orig == NavDir::Right)
        return side == NavDir::Left ? NavDir::Up : NavDir::Down;
    return side == NavDir::Left ? NavDir::Right : NavDir::Left;
}

bool MapNavigation::ValidPos(Int2 pos) const
{
    return pos.X >= 0 && pos.Y >= 0 && pos.X < map_size.X && pos.Y < map_size.Y;
}

int MapNavigation::CellIndex(Int2 pos) const
{
    if (!ValidPos(pos))
        return -1;

    return pos.X + pos.Y * map_size.X;
}

void MapNavigation::UpdatePathType(Int2 pos, HashSet<Int2> updated)
{
    if (!ValidPos(pos) || !updated.Add(pos))
        return;

    int index = CellIndex(pos);
    if (map_cells[index].cell_type != CellType::Path)
        return;

    Array<Array<bool>> sides = {
        {
            pos.X > 0 && pos.Y < map_size.Y - 1 && GetPathType(index + map_size.X - 1) != PathType::Empty,
            pos.X > 0 && GetPathType(index - 1) != PathType::Empty,
            pos.X > 0 && pos.Y > 0 && GetPathType(index - map_size.X - 1) != PathType::Empty
        },
        {
            pos.Y < map_size.Y - 1 && GetPathType(index + map_size.X) != PathType::Empty,
            true,
            pos.Y > 0 && GetPathType(index - map_size.X) != PathType::Empty
        },
        {
            pos.X < map_size.X - 1 && pos.Y < map_size.Y - 1 && GetPathType(index + map_size.X + 1) != PathType::Empty,
            pos.X < map_size.X - 1 && GetPathType(index + 1) != PathType::Empty,
            pos.X < map_size.X - 1 && pos.Y > 0 && GetPathType(index - map_size.X + 1) != PathType::Empty
        }
    };

    int horz = (sides[0][1] ? 1 : 0) + (sides[2][1] ? 1 : 0);
    int vert = (sides[1][0] ? 1 : 0) + (sides[1][2] ? 1 : 0);
    int diagonals = (sides[0][0] ? 1 : 0) + (sides[2][2] ? 1 : 0) + (sides[0][2] ? 1 : 0) + (sides[2][0] ? 1 : 0);

    if (horz == 2 && vert == 2 && diagonals == 4)
         SetPathType(index, PathType::Middle);
    else if (horz == 0 && vert == 0)
         SetPathType(index, PathType::Isolated);
    else if (horz == 0 || vert == 0)
    {
        if (horz == 2 || vert == 2)
             SetPathType(index, PathType::Straight);
        else if (horz == 1 || vert == 1)
             SetPathType(index, PathType::DeadEnd);
    }
    else if (horz == 1 && vert == 1)
    {
        if ((sides[0][0] && sides[0][1] && sides[1][0]) || (sides[2][0] && sides[1][0] && sides[2][1]) ||
                (sides[0][2] && sides[0][1] && sides[1][2]) || (sides[2][2] && sides[1][2] && sides[2][1]))
             SetPathType(index, PathType::OuterCorner);
        else
             SetPathType(index, PathType::Turn);
    }
    else if (horz + vert == 3 && diagonals == 2 && ((!sides[0][0] && !sides[1][0] && !sides[2][0]) ||
            (!sides[0][0] && !sides[0][1] && !sides[0][2]) || (!sides[2][0] && !sides[2][1] && !sides[2][2]) ||
            (!sides[0][2] && !sides[1][2] && !sides[2][2])))
         SetPathType(index, PathType::Side);
    else if ((!sides[0][0] && !sides[2][0]) || (!sides[0][0] && !sides[0][2]) || (!sides[2][0] && !sides[2][2]) || (!sides[0][2] && !sides[2][2]))
         SetPathType(index, PathType::Crossing);
    else
         SetPathType(index, PathType::InnerCorner);

}

auto MapNavigation::GetPathType(int index) const -> PathType
{
    const Cell &cell = map_cells[index];
    if (cell.cell_type != CellType::Path || !cell.data)
        return PathType::Empty;

    return ((PathCell*)&*cell.data)->type;
}

void MapNavigation::SetPathType(int index, PathType type)
{
    Cell &cell = map_cells[index];
    if (cell.cell_type != CellType::Path)
        return;
    if (!cell.data)
        cell.data.reset(new PathCell());
    ((PathCell*)&*cell.data)->type = type;
}
