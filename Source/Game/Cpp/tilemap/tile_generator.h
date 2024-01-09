#pragma once

#include <map>
#include "Engine/Scripting/Script.h"
#include "Engine/Core/Math/Rectangle.h"
#include "Engine/Content/Assets/Model.h"
#include "Engine/Core/Collections/Dictionary.h"

#include "../util/keep_alive.h"
//#include "Engine/Core/Types/Variant.h"


API_ENUM() enum class FloorGroup
{
    None,
    Grass,
    WalkwayOnGrass,
};

API_ENUM() enum class TexType
{
    // Texture should cover the whole tile
    FullTile,
    // Small inner corner for turns or crossings
    InCornerTopLeft,
    InCornerTopRight,
    InCornerBottomLeft,
    InCornerBottomRight,
    // Big curved outer corner for turning roads or area edges
    OutCornerTopLeft,
    OutCornerTopRight,
    OutCornerBottomLeft,
    OutCornerBottomRight,
    // Small outer corner on dead ends.
    OutSharpCornerTopLeft,
    OutSharpCornerTopRight,
    OutSharpCornerBottomLeft,
    OutSharpCornerBottomRight,
    // Edge only on one side for transition
    OutEdgeLeft,
    OutEdgeRight,
    OutEdgeTop,
    OutEdgeBottom
};

API_ENUM() enum class FloorType
{
    FullTile,
    // both sides are edges
    HorzLane,
    VertLane,
    // edge on one side only
    EdgeLeft,
    EdgeTop,
    EdgeRight,
    EdgeBottom,
    // Curve with edge on the outside (opposite side of name) and a tiny inner corner
    TurnTopRight,
    TurnTopLeft,
    TurnBottomRight,
    TurnBottomLeft,
    // Edges on the named side and no inner corner
    EdgeBottomLeft,
    EdgeBottomRight,
    EdgeTopLeft,
    EdgeTopRight,
    // Vertical and horizontal lanes with both inner corners on the named side
    VertCrossRight,
    VertCrossLeft,
    HorzCrossTop,
    HorzCrossBottom,
    // Vertical or horizontal edge with a single inner corner on the named side
    EdgeLeftCornerTopRight,
    EdgeLeftCornerBottomRight,
    EdgeRightCornerTopLeft,
    EdgeRightCornerBottomLeft,
    EdgeTopCornerBottomRight,
    EdgeTopCornerBottomLeft,
    EdgeBottomCornerTopRight,
    EdgeBottomCornerTopLeft,
    // Only inner corners on the given positions, or every other corner if "Except" is in the name
    CornerExceptTopLeft,
    CornerExceptTopRight,
    CornerExceptBottomLeft,
    CornerExceptBottomRight,
    CornerAll,
    CornerBothTop,
    CornerBothRight,
    CornerBothBottom,
    CornerBothLeft,
    CornerTopLeftBottomRight,
    CornerTopRightBottomLeft,
    OnlyCornerTopRight,
    OnlyCornerTopLeft,
    OnlyCornerBottomRight,
    OnlyCornerBottomLeft,
    // The dead end tiles with abruptly cut off ends on one or both sides
    IsolatedTile,
    DeadendLeft,
    DeadendRight,
    DeadendTop,
    DeadendBottom,

    ValueMax
};

API_ENUM() enum class TileSide
{
    None = 0,
    Left = 1,
    Top = 2,
    Right = 4,
    Bottom = 8,
    TopLeft = 16,
    TopRight = 32,
    BottomLeft = 64,
    BottomRight = 128
};


API_STRUCT() struct GAME_API GroupFloor
{
    DECLARE_SCRIPTING_TYPE_STRUCTURE(GroupFloor);

    GroupFloor() = default;
    FORCE_INLINE GroupFloor(FloorGroup g, FloorType t) : group(g), floor(t)
    {}

    API_FIELD() FloorGroup group;
    API_FIELD() FloorType floor;
};

inline bool operator<(const GroupFloor &a, const GroupFloor &b)
{
    if ((int)a.group != (int)b.group)
        return (int)a.group < (int)b.group;
    return (int)a.floor < (int)b.floor;
}
inline bool operator>(const GroupFloor &a, const GroupFloor &b)
{
    if ((int)a.group != (int)b.group)
        return (int)a.group > (int)b.group;
    return (int)a.floor > (int)b.floor;
}




API_CLASS() class GAME_API TileGenerator : public Script
{
API_AUTO_SERIALIZATION();
DECLARE_SCRIPTING_TYPE(TileGenerator);

public:
    // [Script]
    void OnAwake() override;
    void OnDestroy() override;

    void BuildInstances();

    API_FUNCTION() Model* GetModel(FloorGroup group, FloorType ftype);
    API_FUNCTION() static FloorType FloorTypeForSides(TileSide tile_sides);

    // Creates a single model from multiple tiles as specified in the passed data array. The array should
    // be a continuous array of every tile. The passed width and height is used to determine the placement
    // of the tiles in the generated model.
    API_FUNCTION() Model* CreateModel(Array<GroupFloor> &data, int width, int height);

    API_FUNCTION() Model* CreateRowOfModel(const Model *model, int columns, int rows, float offsetX, float offsetZ);

    API_FUNCTION() Model* CreateCompoundModel(const Array<Model*> &models, const Array<int> &modelIndexes, const Array<Vector3> &modelPositions);

    API_FIELD() Int2 texture_size;
    API_FIELD() Int2 tile_size;

    TileGenerator& operator=(const TileGenerator &other) = delete;
    const TileGenerator& operator=(const TileGenerator &other) const = delete;
    TileGenerator(const TileGenerator &other) = delete;

private:
    enum class TileOp
    {
        Add,
        Sub
    };

    struct TileOpPair
    {
        TileOp op;
        int index;
    };

    struct TileRectData
    {
        Array<Float3> verts;
        Array<Float2> uvs;
        Array<int> indexes;

        TileSide side = TileSide::None;
    };

    struct TileRectDataCollection
    {
        TileRectData *left = nullptr;
        TileRectData *top = nullptr;
        TileRectData *right = nullptr;
        TileRectData *bottom = nullptr;
    };

    struct VertModifierWithAlign
    {
        TileSide side;

        Array<TileOpPair> xmods;
        Array<TileOpPair> ymods;

        VertModifierWithAlign(TileSide side) : side(side) { ; }
        VertModifierWithAlign(TileSide side, const Array<TileOpPair> &x, const Array<TileOpPair> &y) : side(side), xmods(x), ymods(y) { ; }

    };


    std::map<FloorGroup, std::map<FloorType, KeepAlive<Model>>> floor_models;

    static std::map<FloorGroup, std::map<TexType, Rectangle>> floor_uv_data;
    static std::map<FloorGroup, Array<FloorType>> floor_gen_data;

    void GetInstanceData(FloorGroup group, FloorType floor_type, Array<Float3> &verts, Array<uint32> &indexes, Array<Float2> &uvs, Array<Float3> &normals);
    void BuildUVArrayFromData(FloorGroup group, TexType ttype, Array<Float2> &result) const;

    void BuildRectData(FloorGroup group, TexType ttype, TileRectData &result);
    void AdjustRectData(TileRectData &data, const TileRectDataCollection &sides) const;
    void BuildMeshData(Array<Float3> &verts, Array<Float2> &uvs, Array<uint32> &indexes, const Array<TileRectData> &dataparams) const;
    void AlignRectPoints(const Array<Float3> &verts, TileSide side, Array<Float3> &out_verts, int array_index) const;
    void BuildIndexArray(const Array<TileRectData> &from, Array<uint32> &indexes) const;
    Rectangle CalculateRectBounds(const Array<Float3> &verts) const;
    void BuildPolyData(FloorGroup group, const Array<TexType> &texes, const Array<VertModifierWithAlign> &modifiers, TileRectData &out) const;
};
