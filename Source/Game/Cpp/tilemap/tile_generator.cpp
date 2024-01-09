#include <vector>

#include "tile_generator.h"
#include "../script_globals.h"

#include <memory>
#include "Engine/Content/Content.h"
#include "Engine/Core/Types/BaseTypes.h"
#include "Engine/Graphics/Models/Mesh.h"
#include "Engine/Debug/DebugLog.h"

std::map<FloorGroup, std::map<TexType, Rectangle>> TileGenerator::floor_uv_data = {
        { FloorGroup::Grass, { { TexType::FullTile, Rectangle(2, 2, 64, 64) } } },
        { FloorGroup::WalkwayOnGrass, {
            { TexType::FullTile, Rectangle(70, 2, 64, 64) },
            { TexType::InCornerTopLeft, Rectangle(221, 75, 9, 9) },
            { TexType::InCornerTopRight, Rectangle(212, 75, 9, 9) },
            { TexType::InCornerBottomLeft, Rectangle(221, 2, 9, 9) },
            { TexType::InCornerBottomRight, Rectangle(212, 2, 9, 9) },
            { TexType::OutCornerTopLeft, Rectangle(138, 2, 35, 35) },
            { TexType::OutCornerTopRight, Rectangle(173, 2, 35, 35) },
            { TexType::OutCornerBottomLeft, Rectangle(138, 37, 35, 35) },
            { TexType::OutCornerBottomRight, Rectangle(173, 37, 35, 35) },
            { TexType::OutSharpCornerTopLeft, Rectangle(70, 70, 9, 9) },
            { TexType::OutSharpCornerTopRight, Rectangle(83, 70, 9, 9) },
            { TexType::OutSharpCornerBottomLeft, Rectangle(70, 83, 9, 9) },
            { TexType::OutSharpCornerBottomRight, Rectangle(83, 83, 9, 9) },
            { TexType::OutEdgeLeft, Rectangle(221, 11, 9, 64) },
            { TexType::OutEdgeRight, Rectangle(212, 11, 9, 64) },
            { TexType::OutEdgeTop, Rectangle(2, 79, 64, 9) },
            { TexType::OutEdgeBottom, Rectangle(2, 70, 64, 9) }
        } }
};


std::map<FloorGroup, Array<FloorType>> TileGenerator::floor_gen_data = {
    { FloorGroup::Grass, { FloorType::FullTile } },
    { FloorGroup::WalkwayOnGrass, {
        FloorType::FullTile,
        FloorType::HorzLane,
        FloorType::VertLane,
        FloorType::EdgeLeft,
        FloorType::EdgeTop,
        FloorType::EdgeRight,
        FloorType::EdgeBottom,
        FloorType::TurnTopRight,
        FloorType::TurnTopLeft,
        FloorType::TurnBottomRight,
        FloorType::TurnBottomLeft,
        FloorType::EdgeBottomLeft,
        FloorType::EdgeBottomRight,
        FloorType::EdgeTopLeft,
        FloorType::EdgeTopRight,
        FloorType::VertCrossRight,
        FloorType::VertCrossLeft,
        FloorType::HorzCrossTop,
        FloorType::HorzCrossBottom,
        FloorType::EdgeLeftCornerTopRight,
        FloorType::EdgeLeftCornerBottomRight,
        FloorType::EdgeRightCornerTopLeft,
        FloorType::EdgeRightCornerBottomLeft,
        FloorType::EdgeTopCornerBottomRight,
        FloorType::EdgeTopCornerBottomLeft,
        FloorType::EdgeBottomCornerTopRight,
        FloorType::EdgeBottomCornerTopLeft,
        FloorType::CornerExceptTopLeft,
        FloorType::CornerExceptTopRight,
        FloorType::CornerExceptBottomLeft,
        FloorType::CornerExceptBottomRight,
        FloorType::CornerAll,
        FloorType::CornerBothTop,
        FloorType::CornerBothRight,
        FloorType::CornerBothBottom,
        FloorType::CornerBothLeft,
        FloorType::CornerTopLeftBottomRight,
        FloorType::CornerTopRightBottomLeft,
        FloorType::OnlyCornerTopRight,
        FloorType::OnlyCornerTopLeft,
        FloorType::OnlyCornerBottomRight,
        FloorType::OnlyCornerBottomLeft,
        FloorType::IsolatedTile,
        FloorType::DeadendLeft,
        FloorType::DeadendRight,
        FloorType::DeadendTop,
        FloorType::DeadendBottom
    } }
};


TileGenerator::TileGenerator(const SpawnParams& params)
    : Script(params), texture_size(0, 0), tile_size(0, 0)
{
    // Enable ticking OnUpdate function
    //_tickUpdate = true;
}

void TileGenerator::OnAwake()
{
    BuildInstances();
}

void TileGenerator::OnDestroy()
{
}

void TileGenerator::BuildInstances()
{
    for (const auto &data : floor_gen_data)
    {
        FloorGroup group = data.first;

        std::map<FloorType, KeepAlive<Model>> models;
        for (FloorType floorType : data.second)
        {
            Array<Float3> verts;
            Array<Float2> uvs;
            Array<uint32> indexes;
            Array<Float3> normals;
            GetInstanceData(group, floorType, verts, indexes, uvs, normals);

            Model *new_model = Content::CreateVirtualAsset<Model>();
            int32 tmp = 1;
            new_model->SetupLODs(Span<int32>(&tmp, 1));
            new_model->LODs[0].Meshes[0].UpdateMesh((uint32)verts.Count(), (uint32)(indexes.Count() / 3), (Float3*)verts.Get(), indexes.Get(), normals.Get(), (Float3*)nullptr, uvs.Get(), (Color32*)nullptr);

            models[floorType] = std::move(KeepAlive<Model>(new_model));
        }
        floor_models[group] = std::move(models);
    }
}

Model* TileGenerator::GetModel(FloorGroup group, FloorType ftype)
{
    return floor_models[group][ftype];
}

FloorType TileGenerator::FloorTypeForSides(TileSide tile_sides)
{
    const uint32_t all_sides = (uint32_t)TileSide::Top | (uint32_t)TileSide::Right | (uint32_t)TileSide::Left | (uint32_t)TileSide::Bottom;
    if ((all_sides & (uint32_t)tile_sides) == 0)
        return FloorType::IsolatedTile;
    if (((uint32_t)tile_sides & (all_sides & ~(uint32_t)TileSide::Top)) == 0)
        return FloorType::DeadendBottom;
    if (((uint32_t)tile_sides & (all_sides & ~(uint32_t)TileSide::Left)) == 0)
        return FloorType::DeadendRight;
    if (((uint32_t)tile_sides & (all_sides & ~(uint32_t)TileSide::Bottom)) == 0)
        return FloorType::DeadendTop;
    if (((uint32_t)tile_sides & (all_sides & ~(uint32_t)TileSide::Right)) == 0)
        return FloorType::DeadendLeft;
    if ((uint32_t)tile_sides == all_sides)
        return FloorType::CornerAll;

    // Tiles above and below.
    if (((uint32_t)tile_sides & ((uint32_t)TileSide::Top | (uint32_t)TileSide::Bottom)) == ((uint32_t)TileSide::Top | (uint32_t)TileSide::Bottom))
    {
        if (((uint32_t)tile_sides & (uint32_t)TileSide::Left) == 0)
        {
            if (((uint32_t)tile_sides & (uint32_t)TileSide::Right) == 0)
                return FloorType::VertLane;
            if (((uint32_t)tile_sides & (uint32_t)TileSide::TopRight) == 0)
            {
                if (((uint32_t)tile_sides & (uint32_t)TileSide::BottomRight) == 0)
                    return FloorType::VertCrossRight;
                return FloorType::EdgeLeftCornerTopRight;
            }
            if (((uint32_t)tile_sides & (uint32_t)TileSide::BottomRight) == 0)
                return FloorType::EdgeLeftCornerBottomRight;
            return FloorType::EdgeLeft;
        }
        if (((uint32_t)tile_sides & (uint32_t)TileSide::Right) == 0)
        {
            if (((uint32_t)tile_sides & (uint32_t)TileSide::TopLeft) == 0)
            {
                if (((uint32_t)tile_sides & (uint32_t)TileSide::BottomLeft) == 0)
                    return FloorType::VertCrossLeft;
                return FloorType::EdgeRightCornerTopLeft;
            }
            if (((uint32_t)tile_sides & (uint32_t)TileSide::BottomLeft) == 0)
                return FloorType::EdgeRightCornerBottomLeft;
            return FloorType::EdgeRight;
        }
        // Road on all sides. Check for diagonals
        if (((uint32_t)tile_sides & (uint32_t)TileSide::TopLeft) == 0)
        {
            if (((uint32_t)tile_sides & (uint32_t)TileSide::TopRight) == 0)
            {
                if (((uint32_t)tile_sides & (uint32_t)TileSide::BottomLeft) == 0)
                {
                    if (((uint32_t)tile_sides & (uint32_t)TileSide::BottomRight) == 0)
                        return FloorType::CornerAll;
                    return FloorType::CornerExceptBottomRight;
                }
                if (((uint32_t)tile_sides & (uint32_t)TileSide::BottomRight) == 0)
                    return FloorType::CornerExceptBottomLeft;
                return FloorType::CornerBothTop;
            }
            if (((uint32_t)tile_sides & (uint32_t)TileSide::BottomLeft) == 0)
            {
                if (((uint32_t)tile_sides & (uint32_t)TileSide::BottomRight) == 0)
                    return FloorType::CornerExceptTopRight;
                return FloorType::CornerBothLeft;
            }
            if (((uint32_t)tile_sides & (uint32_t)TileSide::BottomRight) == 0)
                return FloorType::CornerTopLeftBottomRight;
            return FloorType::OnlyCornerTopLeft;
        }
        if (((uint32_t)tile_sides & (uint32_t)TileSide::TopRight) == 0)
        {
            if (((uint32_t)tile_sides & (uint32_t)TileSide::BottomLeft) == 0)
            {
                if (((uint32_t)tile_sides & (uint32_t)TileSide::BottomRight) == 0)
                    return FloorType::CornerExceptTopLeft;
                return FloorType::CornerTopRightBottomLeft;
            }
            if (((uint32_t)tile_sides & (uint32_t)TileSide::BottomRight) == 0)
                return FloorType::CornerBothRight;
            return FloorType::OnlyCornerTopRight;
        }
        if (((uint32_t)tile_sides & (uint32_t)TileSide::BottomLeft) == 0)
        {
            if (((uint32_t)tile_sides & (uint32_t)TileSide::BottomRight) == 0)
                return FloorType::CornerBothBottom;
            return FloorType::OnlyCornerBottomLeft;
        }
        if (((uint32_t)tile_sides & (uint32_t)TileSide::BottomRight) == 0)
            return FloorType::OnlyCornerBottomRight;
        return FloorType::FullTile;
    }
    // Tiles left and right.
    if (((uint32_t)tile_sides & ((uint32_t)TileSide::Left | (uint32_t)TileSide::Right)) == ((uint32_t)TileSide::Left | (uint32_t)TileSide::Right))
    {
        if (((uint32_t)tile_sides & (uint32_t)TileSide::Top) == 0)
        {
            if (((uint32_t)tile_sides & (uint32_t)TileSide::Bottom) == 0)
                return FloorType::HorzLane;
            if (((uint32_t)tile_sides & (uint32_t)TileSide::BottomLeft) == 0)
            {
                if (((uint32_t)tile_sides & (uint32_t)TileSide::BottomRight) == 0)
                    return FloorType::HorzCrossBottom;
                return FloorType::EdgeTopCornerBottomLeft;
            }
            if (((uint32_t)tile_sides & (uint32_t)TileSide::BottomRight) == 0)
                return FloorType::EdgeTopCornerBottomRight;
            return FloorType::EdgeTop;
        }
        if (((uint32_t)tile_sides & (uint32_t)TileSide::Bottom) == 0)
        {
            if (((uint32_t)tile_sides & (uint32_t)TileSide::TopLeft) == 0)
            {
                if (((uint32_t)tile_sides & (uint32_t)TileSide::TopRight) == 0)
                    return FloorType::HorzCrossTop;
                return FloorType::EdgeBottomCornerTopLeft;
            }
            if (((uint32_t)tile_sides & (uint32_t)TileSide::TopRight) == 0)
                return FloorType::EdgeBottomCornerTopRight;
            return FloorType::EdgeBottom;
        }
    }

    if (((uint32_t)tile_sides & (uint32_t)TileSide::Left) == 0)
    {
        if (((uint32_t)tile_sides & (uint32_t)TileSide::Top) == 0)
        {
            if (((uint32_t)tile_sides & (uint32_t)TileSide::BottomRight) == 0)
                return FloorType::TurnBottomRight;
            return FloorType::EdgeTopLeft;
        }
        if (((uint32_t)tile_sides & (uint32_t)TileSide::TopRight) == 0)
            return FloorType::TurnTopRight;
        return FloorType::EdgeBottomLeft;
    }
    if (((uint32_t)tile_sides & (uint32_t)TileSide::Top) == 0)
    {
        if (((uint32_t)tile_sides & (uint32_t)TileSide::BottomLeft) == 0)
            return FloorType::TurnBottomLeft;
        return FloorType::EdgeTopRight;
    }

    if (((uint32_t)tile_sides & (uint32_t)TileSide::TopLeft) == 0)
        return FloorType::TurnTopLeft;
    return FloorType::EdgeBottomRight;
}

namespace
{
    struct TileDataCache
    {
        //DECLARE_SCRIPTING_TYPE_STRUCTURE(TileDataCache);

        Array<Float3> verts;
        Array<Float2> uvs;
        Array<uint32> indexes;
        Array<Float3> normals;

        //TileDataCache& operator=(TileDataCache &&other)
        //{
        //    return *this;
        //}
        //TileDataCache& operator=(const TileDataCache &other)
        //{
        //    return *this;
        //}
    };

}

Model* TileGenerator::CreateModel(Array<GroupFloor> &data, int width, int height)
{
    std::map<GroupFloor, TileDataCache> cache;
    int vert_count = 0;
    int index_count = 0;
    for (const GroupFloor &pair : data)
    {
        if (pair.group == FloorGroup::None)
            continue;

        auto it = cache.find(pair);
        TileDataCache *tile_data;
        if (it == cache.end())
        {
            TileDataCache tmp;
            GetInstanceData(pair.group, pair.floor, tmp.verts, tmp.indexes, tmp.uvs, tmp.normals);
            auto it = cache.emplace(pair, std::move(tmp));
            tile_data = &it.first->second;
        }
        else
            tile_data = &it->second;
        vert_count += tile_data->verts.Count();
        index_count += tile_data->indexes.Count();
    }

    Array<Float3> verts;
    Array<Float2> uvs;
    Array<uint32> indexes;
    Array<Float3> normals;

    verts.AddDefault(vert_count);
    uvs.AddDefault(vert_count);
    indexes.AddDefault(index_count);
    normals.AddDefault(vert_count);

    int pos_x = 0;
    int pos_y = 0;
    int vert_pos = 0;
    int index_pos = 0;

    for (const GroupFloor &pair : data)
    {
        if (pair.group != FloorGroup::None)
        {
            TileDataCache &tile_data = cache[pair];

            if (vert_pos + tile_data.verts.Count() > verts.Count())
                DebugLog::Log(TEXT("These values are nuts!"));
            else if (index_pos + tile_data.indexes.Count() > indexes.Count())
                DebugLog::Log(TEXT("Even the indexes are crazy!"));
            else
            {
                std::memcpy(verts.Get() + vert_pos, tile_data.verts.Get(), sizeof(Float3) * tile_data.verts.Count());
                std::memcpy(uvs.Get() + vert_pos, tile_data.uvs.Get(), sizeof(Float2) * tile_data.uvs.Count());
                std::memcpy(indexes.Get() + index_pos, tile_data.indexes.Get(), sizeof(uint32) * tile_data.indexes.Count());
                std::memcpy(normals.Get() + vert_pos, tile_data.normals.Get(), sizeof(Float3) * tile_data.normals.Count());
            }

            for (int ix = 0, siz = tile_data.verts.Count(); ix < siz; ++ix)
            {
                verts[vert_pos + ix].X += ScriptGlobals::tile_dimension * pos_x;
                verts[vert_pos + ix].Z += ScriptGlobals::tile_dimension * pos_y;
            }

            for (int ix = 0, siz = tile_data.indexes.Count(); ix < siz; ++ix)
                indexes[index_pos + ix] += vert_pos;

            vert_pos += tile_data.verts.Count();
            index_pos += tile_data.indexes.Count();
        }

        ++pos_x;
        if (pos_x >= width)
        {
            pos_x = 0;
            ++pos_y;
        }
        if (pos_y >= height)
            break;
    }

    //// Check verts:
    //for (int ix = 0, siz = indexes.Count() / 3; ix < siz; ++ix)
    //{
    //    uint32 a = indexes[ix * 3];
    //    uint32 b = indexes[ix * 3 + 1];
    //    uint32 c = indexes[ix * 3 + 2];
    //    if (a >= verts.Count() || b >= verts.Count() || c >= verts.Count())
    //        DebugLog::LogError(TEXT("Index vertices out of bounds"));
    //    else
    //    {
    //        Float3 &v1 = verts[a];
    //        Float3 &v2 = verts[b];
    //        Float3 &v3 = verts[c];

    //        if ((v1 - v2).LengthSquared() > 80010.f || (v2 - v3).LengthSquared() > 80010.f || (v1 - v3).LengthSquared() > 80010.f)
    //            DebugLog::LogError(TEXT("Weird vertex sizes!"));
    //    }
    //}

    Model *new_model = Content::CreateVirtualAsset<Model>();

    int32 i = 1;
    new_model->SetupLODs(Span<int32>(&i, 1));
    new_model->LODs[0].Meshes[0].UpdateMesh((uint32)verts.Count(), (uint32)(indexes.Count() / 3), (Float3*)verts.Get(), indexes.Get(), normals.Get(), (Float3*)nullptr, uvs.Get(), (Color32*)nullptr);
    return new_model;
}

struct VertexBuffers
{
    int material_slot = 0;

    Array<uint32> indexes;
    Array<Float3> verts;
    Array<Float3> normals;
    Array<Float2> uvs;
    //Array<Color32> colors;
};

struct Vertex1
{
    Half2 TexCoord;
    FloatR10G10B10A2 Normal;
    FloatR10G10B10A2 Tangent;
    Half2 LightmapUVs;
};

//struct Vertex2
//{
//    Color32 Color;
//};

void DownloadVertexBuffers(const Mesh &mesh, VertexBuffers &buffers)
{
    BytesContainer result;
    int32 result_count;

    mesh.DownloadDataCPU(MeshBufferType::Vertex0, result, result_count);
    buffers.material_slot = mesh.GetMaterialSlotIndex();

    int vertex_count = result_count;
    buffers.verts.AddUninitialized(result_count);
    memcpy(buffers.verts.Get(), result.Get(), sizeof(Float3) * result_count);
    
    mesh.DownloadDataCPU(MeshBufferType::Vertex1, result, result_count);
    Vertex1 *verts = (Vertex1*)result.Get();
    buffers.normals.AddUninitialized(result_count);
    buffers.uvs.AddUninitialized(result_count);
    for (int ix = 0; ix < result_count; ++ix)
    {
        buffers.normals[ix] = verts[ix].Normal.ToFloat3() * 2.0f - 1.0f;
        buffers.uvs[ix] = verts[ix].TexCoord.ToFloat2();
    }

    //mesh.DownloadDataCPU(MeshBufferType::Vertex2, result, result_count);
    //Vertex2 *verts2 = (Vertex2*)result.Get();
    //buffers.colors.AddUninitialized(result_count);
    //for (int ix = 0; ix < result_count; ++ix)
    //    buffers.colors[ix] = verts2[ix].Color;

    mesh.DownloadDataCPU(MeshBufferType::Index, result, result_count);
    buffers.indexes.AddUninitialized(result_count);
    if (result.Length() / result_count == sizeof(uint32))
        memcpy(buffers.indexes.Get(), result.Get(), sizeof(uint32) * result_count);
    else
    {
        uint16 *d = result.Get<uint16>();
        for (int ix = 0, siz = result_count; ix < siz; ++ix)
            buffers.indexes[ix] = *(d + ix);
    }
}

Model* TileGenerator::CreateRowOfModel(const Model *model, int columns, int rows, float offsetX, float offsetZ)
{
    if (model == nullptr || columns <= 0 || rows <= 0)
        return nullptr;

    if (!model->IsLoaded() && !model->WaitForLoaded())
        return nullptr;

    // First gather some data. Read the size of arrays needed to create a copy of the model from
    // every LOD and sub mesh.

    int meshCount = 0;
    Array<int32> lod_meshes;
    lod_meshes.AddUninitialized(model->LODs.Count());
    for (int lix = 0, lsiz = model->LODs.Count(); lix < lsiz; ++lix)
    {
        meshCount += model->LODs[lix].Meshes.Count();
        lod_meshes[lix] = model->LODs[lix].Meshes.Count();
    }

    Model *new_model = Content::CreateVirtualAsset<Model>();
    new_model->SetupLODs(Span<int32>(lod_meshes.Get(), lod_meshes.Count()));
    new_model->SetupMaterialSlots(model->GetMaterialSlotsCount());

    for (int lix = 0, lsiz = model->LODs.Count(); lix < lsiz; ++lix)
    {
        for (int mix = 0, msiz = model->LODs[lix].Meshes.Count(); mix < msiz; ++mix)
        {

            VertexBuffers buffers;
            DownloadVertexBuffers(model->LODs[lix].Meshes[mix], buffers);

            Array<uint32> indexes;
            indexes.AddUninitialized(buffers.indexes.Count() * columns * rows);
            Array<Float3> verts;
            verts.AddUninitialized(buffers.verts.Count() * columns * rows);
            Array<Float3> normals;
            normals.AddUninitialized(buffers.normals.Count() * columns * rows);
            Array<Float2> uvs;
            uvs.AddUninitialized(buffers.uvs.Count() * columns * rows);
            //Array<Color32> colors;
            //colors.AddUninitialized(buffers.colors.Count() * columns * rows);

            int index_count = buffers.indexes.Count();
            int index_pos = 0;
            int vertex_count = buffers.verts.Count();
            int vertex_pos = 0;
            //int colors_count = buffers.colors.Count();
            //int colors_pos = 0;

            for (int ix = 0; ix < columns; ++ix)
            {
                for (int iy = 0; iy < rows; ++iy)
                {
                    for (int nix = 0; nix < index_count; ++nix)
                    {
                        indexes[nix + index_pos] = buffers.indexes[nix] + vertex_pos;
                    }
                    index_pos += index_count;

                    for (int vix = 0; vix < vertex_count; ++vix)
                    {
                        verts[vix + vertex_pos] = buffers.verts[vix] + Float3(offsetX * ix, 0.0f, offsetZ * iy);
                        normals[vix + vertex_pos] = buffers.normals[vix];
                        uvs[vix + vertex_pos] = buffers.uvs[vix];
                    }
                    //for (int vix = 0; vix < colors_count; ++vix)
                    //{
                    //        colors[vix + colors_pos] = buffers.colors[vix];
                    //}
                    vertex_pos += vertex_count;
                    //colors_pos += colors_count;
                }
            }

            new_model->LODs[lix].Meshes[mix].UpdateMesh((uint32)verts.Count(), (uint32)(indexes.Count() / 3), (Float3*)verts.Get(), indexes.Get(), normals.Get(), (Float3*)nullptr, uvs.Get(), (Color32*)nullptr/*colors.Get()*/);
            new_model->LODs[lix].Meshes[mix].SetMaterialSlotIndex(buffers.material_slot);
        }
        for (int six = 0, ssiz = model->GetMaterialSlotsCount(); six < ssiz; ++six)
        {
            new_model->MaterialSlots[six].Material = model->MaterialSlots[six].Material;
            new_model->MaterialSlots[six].Name = model->MaterialSlots[six].Name;
            new_model->MaterialSlots[six].ShadowsMode = model->MaterialSlots[six].ShadowsMode;
        }
    }

    return new_model;
}


Model* TileGenerator::CreateCompoundModel(const Array<Model*> &models, const Array<int> &model_indexes, const Array<Vector3> &model_positions)
{
    // Generates a model made up of all the models in the `models` array. `model_indexes` and `model_positions`
    // determine which model is placed at what location. These arrays must have the same number of elements.
    // `model_indexes` holds indexes of items in `models` and `model_positions` determines where the models in
    // `models` are placed.
    // If the models don't have the same number of LODs, only the least number of LODs are generated.
    // Currently meshes in models with the same material are not merged, and for each model, a separate mesh
    // is created in the new model.
    //public Model CreateCompoundModel(in Model[] models, in int[] model_indexes, in Vector3[] model_positions)

    if (models.Count() == 0 || model_indexes.Count() == 0 || model_positions.Count() == 0)
    {
        DebugLog::LogError(L"Missing data in CreateCompoundModel");
        return nullptr;
    }
    if (model_indexes.Count() != model_positions.Count())
    {
        DebugLog::LogError(L"Model indexes and positions arrays must have the same number of values");
        return nullptr;
    }

    int lod_count = 0;
    for (Model *m : models)
    {
        if (lod_count == 0)
            lod_count = m->LODs.Count();
        else
            lod_count = std::min(lod_count, m->LODs.Count());
    }

    if (lod_count == 0)
    {
        DebugLog::LogError(L"No LODs found in meshes.");
        return nullptr;
    }

    Array<int> lod_meshes;
    lod_meshes.AddZeroed(lod_count);
    int mat_count = 0;
    for (int lix = 0; lix < lod_count; ++lix)
    {
        for (Model *m : models)
        {
            lod_meshes[lix] += m->LODs[lix].Meshes.Count();
            mat_count += m->GetMaterialSlotsCount();
        }
    }

    Model *new_model = Content::CreateVirtualAsset<Model>();
    new_model->SetupLODs(Span<int>(lod_meshes.Get(), lod_meshes.Count()));
    new_model->SetupMaterialSlots(mat_count);

    Array<int> uses;
    uses.AddZeroed(model_indexes.Count());
    for (int lix = 0; lix < lod_count; ++lix)
    {
        int skippedSlotCount = 0;
        int skipped_mesh_cnt = 0;
        for (int mix = 0, msiz = models.Count(); mix < msiz; ++mix)
        {
            Model *m = models[mix];

            // Create the meshes for this model at the current LOD.
            // We need to first count the amount of data that has to be collected.

            int use_cnt = 0;
            for (int ixix = 0, ixsiz = model_indexes.Count(); ixix < ixsiz; ++ixix)
            {
                if (model_indexes[ixix] == mix)
                {
                    uses[use_cnt] = ixix;
                    ++use_cnt;
                }
            }

            // Model generation creates arrays to hold the given mesh as many times as it was
            // found, then fills the arrays with clone of the original data and the position
            // adjusted.

            for (int meshix = 0, meshsiz = m->LODs[lix].Meshes.Count(); meshix < meshsiz && use_cnt != 0; ++meshix)
            {
                VertexBuffers buffers;
                DownloadVertexBuffers(m->LODs[lix].Meshes[meshix], buffers);

                Array<uint32> indexes;
                indexes.AddUninitialized(buffers.indexes.Count() * use_cnt);
                Array<Float3> verts;
                verts.AddUninitialized(buffers.verts.Count() * use_cnt);
                Array<Float3> normals;
                normals.AddUninitialized(buffers.verts.Count() * use_cnt);
                Array<Float2> uvs;
                uvs.AddUninitialized(buffers.verts.Count() * use_cnt);
                //Array<Color32> colors;
                //colors.AddUninitialized(buffers.verts.Count() * use_cnt);

                int index_count = buffers.indexes.Count();
                int index_pos = 0;
                int vertex_count = buffers.verts.Count();
                int vertex_pos = 0;
                //int colors_count = buffers.colors.Count();
                //int colors_pos = 0;

                for (int iy = 0; iy < use_cnt; ++iy)
                {
                    for (int nix = 0; nix < index_count; ++nix)
                    {
                        indexes[nix + index_pos] = buffers.indexes[nix] + vertex_pos;
                    }
                    index_pos += index_count;

                    for (int vix = 0; vix < vertex_count; ++vix)
                    {
                        verts[vix + vertex_pos] = buffers.verts[vix] + model_positions[uses[iy]];
                        normals[vix + vertex_pos] = buffers.normals[vix];
                        uvs[vix + vertex_pos] = buffers.uvs[vix];
                    }
                    //for (int vix = 0; vix < colors_count; ++vix)
                    //{
                    //    colors[vix + colors_pos] = buffers.colors[vix];
                    //}
                    vertex_pos += vertex_count;
                    //colors_pos += colors_count;
                }

                new_model->LODs[lix].Meshes[meshix + skipped_mesh_cnt].UpdateMesh((uint32)verts.Count(), (uint32)(indexes.Count() / 3), (Float3*)verts.Get(), indexes.Get(), normals.Get(), (Float3*)nullptr, uvs.Get(), (Color32*)nullptr/*colors.Get()*/);
                new_model->LODs[lix].Meshes[meshix + skipped_mesh_cnt].SetMaterialSlotIndex(buffers.material_slot + skippedSlotCount);

            }

            for (int six = 0, ssiz = m->GetMaterialSlotsCount(); six < ssiz; ++six)
            {
                new_model->MaterialSlots[six + skippedSlotCount].Material = m->MaterialSlots[six].Material;
                new_model->MaterialSlots[six + skippedSlotCount].Name = m->MaterialSlots[six].Name;
                new_model->MaterialSlots[six + skippedSlotCount].ShadowsMode = m->MaterialSlots[six].ShadowsMode;
            }
            skippedSlotCount += models[mix]->GetMaterialSlotsCount();
            skipped_mesh_cnt += models[mix]->LODs[lix].Meshes.Count();
        }
    }

    return new_model;

}


void TileGenerator::GetInstanceData(FloorGroup group, FloorType floor_type, Array<Float3> &verts, Array<uint32> &indexes, Array<Float2> &uvs, Array<Float3> &normals)
{
    TileRectDataCollection data;
    TileRectData data_base;
    TileRectData data_top;
    TileRectData data_bottom;
    TileRectData data_left;
    TileRectData data_right;
    TileRectData curve_data;
    TileRectData data_corner1;
    TileRectData data_corner2;
    TileRectData data_corner3;
    TileRectData data_corner4;

    switch (floor_type)
    {
        case FloorType::FullTile:
        {
            verts = {
                Float3(0.0f, 0.0f, 0.0f),
                Float3(ScriptGlobals::tile_dimension, 0.0f, 0.0f),
                Float3(0.0f, 0.0f, ScriptGlobals::tile_dimension),
                Float3(ScriptGlobals::tile_dimension, 0.0f, ScriptGlobals::tile_dimension)
            };

            BuildUVArrayFromData(group, TexType::FullTile, uvs);
            indexes = { 0, 1, 2, 1, 3, 2 };
            break;
        }
        case FloorType::HorzLane:
        {
            BuildRectData(group, TexType::OutEdgeTop, data_top);
            BuildRectData(group, TexType::OutEdgeBottom, data_bottom);
            BuildRectData(group, TexType::FullTile, data_base);
            data.top = &data_top;
            data.bottom = &data_bottom;
            AdjustRectData(data_base, data);
            data_bottom.side = TileSide::Bottom;
            BuildMeshData(verts, uvs, indexes, { data_top, data_base, data_bottom });
            break;
        }
        case FloorType::VertLane:
        {
            BuildRectData(group, TexType::OutEdgeLeft, data_left);
            BuildRectData(group, TexType::OutEdgeRight, data_right);
            data_right.side = TileSide::Right;
            BuildRectData(group, TexType::FullTile, data_base);
            data.left = &data_left;
            data.right = &data_right;
            AdjustRectData(data_base, data);
            BuildMeshData(verts, uvs, indexes, { data_left, data_base, data_right });
            break;
        }
        case FloorType::EdgeLeft:
        {
            BuildRectData(group, TexType::OutEdgeLeft, data_left);
            BuildRectData(group, TexType::FullTile, data_base);
            data.left = &data_left;
            AdjustRectData(data_base, data);
            BuildMeshData(verts, uvs, indexes, { data_left, data_base });
            break;
        }
        case FloorType::EdgeTop:
        {
            BuildRectData(group, TexType::OutEdgeTop, data_top);
            BuildRectData(group, TexType::FullTile, data_base);
            data.top = &data_top;
            AdjustRectData(data_base, data);
            BuildMeshData(verts, uvs, indexes, { data_top, data_base });
            break;
        }
        case FloorType::EdgeRight:
        {
            BuildRectData(group, TexType::OutEdgeRight, data_right);
            BuildRectData(group, TexType::FullTile, data_base);
            data.right = &data_right;
            AdjustRectData(data_base, data);
            data_right.side = TileSide::Right;
            BuildMeshData(verts, uvs, indexes, { data_base, data_right });
            break;
        }
        case FloorType::EdgeBottom:
        {
            BuildRectData(group, TexType::OutEdgeBottom, data_bottom);
            BuildRectData(group, TexType::FullTile, data_base);
            data.bottom = &data_bottom;
            AdjustRectData(data_base, data);
            data_bottom.side = TileSide::Bottom;
            BuildMeshData(verts, uvs, indexes, { data_base, data_bottom });
            break;
        }
        case FloorType::TurnBottomLeft:
        {
            BuildPolyData(group,
                { TexType::OutCornerTopRight, TexType::OutEdgeRight, TexType::OutEdgeTop },
                { { TileSide::TopLeft}, { TileSide::TopRight}, { TileSide::BottomLeft},
                { TileSide::TopLeft, {}, { {TileOp::Add, 2 } } },
                { TileSide::BottomRight, { { TileOp::Sub, 1 } }, {} },
                { TileSide::BottomRight } }, curve_data);

            curve_data.indexes = { 0, 1, 3, 1, 4, 3, 1, 5, 4, 3, 4, 2 };

            BuildPolyData(group,
                { TexType::FullTile, TexType::OutCornerTopRight, TexType::OutEdgeRight, TexType::OutEdgeTop, TexType::InCornerBottomLeft },
                {
                { TileSide::TopLeft, {}, { { TileOp::Add, 3 } } },
                { TileSide::TopRight, {{TileOp::Sub, 1}},  {{TileOp::Add, 3}} },
                { TileSide::TopRight, {{TileOp::Sub, 1}},  {{TileOp::Add, 1}} },
                { TileSide::TopRight, {{TileOp::Sub, 2}},  {{TileOp::Add, 1}} },
                { TileSide::BottomRight, {{TileOp::Sub, 2}}, {} },
                { TileSide::BottomLeft, {{TileOp::Add, 4}}, {{TileOp::Sub, 4}} },
                { TileSide::BottomLeft, {}, {{TileOp::Sub, 4}} },
                { TileSide::BottomLeft, {{TileOp::Add, 4}}, {} } },
                data_base);

            data_base.indexes = { 0, 1, 2, 2, 3, 4, 0, 2, 5, 2, 4, 5, 0, 5, 6, 5, 4, 7 };

            BuildRectData(group, TexType::OutEdgeRight, data_right);
            data.top = &curve_data;
            AdjustRectData(data_right, data);
            data.top = nullptr;
            BuildRectData(group, TexType::OutEdgeTop, data_top);
            data.right = &curve_data;
            AdjustRectData(data_top, data);
            BuildRectData(group, TexType::InCornerBottomLeft, data_corner1);

            data_right.side = TileSide::BottomRight;
            data_corner1.side = TileSide::BottomLeft;
            curve_data.side = TileSide::TopRight;
            BuildMeshData(verts, uvs, indexes, { data_top, curve_data, data_base,
                data_right, data_corner1 });
            break;
        }
        case FloorType::TurnBottomRight:
        {
            BuildPolyData(group,
                { TexType::OutCornerTopLeft, TexType::OutEdgeLeft, TexType::OutEdgeTop },
                { { TileSide::TopLeft}, { TileSide::TopRight}, { TileSide::BottomLeft},
                { TileSide::TopRight, {},  {{TileOp::Add, 2}} },
                { TileSide::BottomLeft, {{TileOp::Add, 1}}, {} },
                { TileSide::BottomRight } },
                curve_data);

            curve_data.indexes = { 0, 1, 3, 0, 3, 4, 0, 4, 2, 3, 5, 4 };

            BuildPolyData(group,
                { TexType::FullTile, TexType::OutCornerTopLeft, TexType::OutEdgeLeft, TexType::OutEdgeTop, TexType::InCornerBottomRight },
                {
                { TileSide::TopLeft,  {{TileOp::Add, 1}},  {{TileOp::Add, 3}} },
                { TileSide::TopRight, {}, {{TileOp::Add, 3}} },
                { TileSide::TopLeft, {{TileOp::Add, 1}},  {{TileOp::Add, 1}} },
                { TileSide::TopLeft, {{TileOp::Add, 2}},  {{TileOp::Add, 1}} },
                { TileSide::BottomLeft, {{TileOp::Add, 2}}, {} },
                { TileSide::BottomRight, {{TileOp::Sub, 4}},  {{TileOp::Sub, 4}} },
                { TileSide::BottomRight, {},  {{TileOp::Sub, 4}} },
                { TileSide::BottomRight, {{TileOp::Sub, 4}}, {} } },
                data_base);
            data_base.indexes = { 0, 1, 2, 3, 2, 4, 1, 5, 2, 2, 5, 4, 1, 6, 5, 5, 7, 4 };

            BuildRectData(group, TexType::OutEdgeLeft, data_left);
            data.top = &curve_data;
            AdjustRectData(data_left, data);
            data.top = nullptr;
            BuildRectData(group, TexType::OutEdgeTop, data_top);
            data.left = &curve_data;
            AdjustRectData(data_top, data);
            BuildRectData(group, TexType::InCornerBottomRight, data_corner1);

            data_top.side = TileSide::TopRight;
            data_left.side = TileSide::BottomLeft;
            data_corner1.side = TileSide::BottomRight;
            BuildMeshData(verts, uvs, indexes, { curve_data, data_top, data_base,
                    data_left, data_base, data_corner1 });
            break;
        }
        case FloorType::TurnTopLeft:
        {
            BuildPolyData(group,
                { TexType::OutCornerBottomRight, TexType::OutEdgeRight, TexType::OutEdgeBottom },
                {
                    { TileSide::TopLeft}, { TileSide::TopRight},
                { TileSide::TopRight, {{TileOp::Sub, 1}}, {}},
                    { TileSide::BottomLeft, {},  {{TileOp::Sub, 2}}},
                { TileSide::BottomLeft},
                { TileSide::BottomRight} }, curve_data);

            curve_data.indexes = { 0, 2, 3, 2, 1, 5, 2, 5, 3, 3, 5, 4 };

            BuildPolyData(group,
                { TexType::FullTile, TexType::OutCornerBottomRight, TexType::OutEdgeRight, TexType::OutEdgeBottom, TexType::InCornerTopLeft },
                {
                { TileSide::TopLeft, {{TileOp::Add, 4}}, {}},
                 { TileSide::TopLeft, {{TileOp::Add, 4}},  {{TileOp::Add, 4}}},
                    { TileSide::TopLeft, {},  {{TileOp::Add, 4}}},
                    { TileSide::TopRight,  {{TileOp::Sub, 2}}, {}},
                    { TileSide::BottomRight, {{TileOp::Sub, 1}},  {{TileOp::Sub, 1}} },
                { TileSide::BottomRight, {{TileOp::Sub, 2}},  {{TileOp::Sub, 1}} },

            { TileSide::BottomLeft, {},  {{TileOp::Sub, 3}} },
            { TileSide::BottomRight, {{TileOp::Sub, 1}}, {{TileOp::Sub, 3}} }
                },
                data_base);

            data_base.indexes = { 0, 3, 1, 3, 4, 1, 3, 5, 4, 2, 1, 6, 1, 4, 6, 4, 7, 6 };

            BuildRectData(group, TexType::OutEdgeRight, data_right);
            data.bottom = &curve_data;
            AdjustRectData(data_right, data);
            data.bottom = nullptr;
            BuildRectData(group, TexType::OutEdgeBottom, data_bottom);
            data.right = &curve_data;
            AdjustRectData(data_bottom, data);
            data.right = nullptr;
            BuildRectData(group, TexType::InCornerTopLeft, data_corner1);
            data_right.side = TileSide::TopRight;
            data_bottom.side = TileSide::BottomLeft;
            curve_data.side = TileSide::BottomRight;
            BuildMeshData(verts, uvs, indexes, { data_corner1, data_base,
                    data_right, data_bottom,
                    curve_data });
            break;
        }
        case FloorType::TurnTopRight:
        {
            BuildPolyData(group,
                { TexType::OutCornerBottomLeft, TexType::OutEdgeLeft, TexType::OutEdgeBottom },
                {
                    { TileSide::TopLeft}, { TileSide::TopRight},
                { TileSide::TopLeft, {{TileOp::Add, 1}}, {} },
                    { TileSide::BottomRight, {},  {{TileOp::Sub, 2}} },
                    { TileSide::BottomLeft },
                    { TileSide::BottomRight }
                }, curve_data);

            curve_data.indexes = { 0, 2, 4, 2, 1, 3, 2, 3, 4, 3, 5, 4 };

            BuildPolyData(group,
                { TexType::FullTile, TexType::OutCornerBottomLeft, TexType::OutEdgeLeft, TexType::OutEdgeBottom, TexType::InCornerTopRight },
                {
                    { TileSide::TopLeft, {{TileOp::Add, 2}}, {} },
                    { TileSide::TopRight, {{TileOp::Sub, 4}}, {} },
                    { TileSide::TopRight, {{TileOp::Sub, 4}},  {{TileOp::Add, 4}} },
                    { TileSide::TopRight, {},  {{TileOp::Add, 4}} },
                    { TileSide::BottomLeft, {{TileOp::Add, 2}},  {{TileOp::Sub, 1}} },
                    { TileSide::BottomLeft, {{TileOp::Add, 1}},  {{TileOp::Sub, 1}} },
                    { TileSide::BottomLeft, {{TileOp::Add, 1}},  {{TileOp::Sub, 3}} },
                    { TileSide::BottomRight, {},  {{TileOp::Sub, 3}} } 
                },
                    data_base);
            data_base.indexes = { 0, 1, 2, 0, 2, 5, 0, 5, 4, 2, 3, 7, 2, 7, 5, 5, 7, 6 };

            BuildRectData(group, TexType::OutEdgeLeft, data_left);
            data.bottom = &curve_data;
            AdjustRectData(data_left, data);
            data.bottom = nullptr;
            BuildRectData(group, TexType::OutEdgeBottom, data_bottom);
            data.left = &curve_data;
            AdjustRectData(data_bottom, data);
            data.left = nullptr;
            BuildRectData(group, TexType::InCornerTopRight, data_corner1);

            data_corner1.side = TileSide::TopRight;
            curve_data.side = TileSide::BottomLeft;
            data_bottom.side = TileSide::BottomRight;
            BuildMeshData(verts, uvs, indexes, { data_left, data_base,
                data_corner1, curve_data,
                data_bottom });
            break;
        }
        case FloorType::EdgeTopRight:
        {
            BuildPolyData(group,
                { TexType::OutCornerTopRight, TexType::OutEdgeRight, TexType::OutEdgeTop },
                {
                    { TileSide::TopLeft}, { TileSide::TopRight}, { TileSide::BottomLeft},
                    { TileSide::TopLeft, {},  {{TileOp::Add, 2}} },
                    { TileSide::BottomRight, {{TileOp::Sub, 1}}, {} },
                { TileSide::BottomRight}
                }, curve_data);

            curve_data.indexes = { 0, 1, 3, 1, 4, 3, 1, 5, 4, 3, 4, 2 };

            BuildPolyData(group,
                { TexType::FullTile, TexType::OutCornerTopRight, TexType::OutEdgeRight, TexType::OutEdgeTop },
                {
                    { TileSide::TopLeft, {},  {{TileOp::Add, 3}} },
                    { TileSide::TopRight, {{TileOp::Sub, 1}},  {{TileOp::Add, 3}} },
                    { TileSide::TopRight, {{TileOp::Sub, 1}},  {{TileOp::Add, 1}} },
                    { TileSide::TopRight, {{TileOp::Sub, 2}},  {{TileOp::Add, 1}} },
                    TileSide::BottomLeft,
                    { TileSide::BottomRight, {{TileOp::Sub, 2}}, {} }
                }, data_base);
            data_base.indexes = { 0, 1, 2, 2, 3, 5, 0, 2, 4, 2, 5, 4 };

            BuildRectData(group, TexType::OutEdgeRight, data_right);
            data.top = &curve_data;
            AdjustRectData(data_right, data);
            data.top = nullptr;
            BuildRectData(group, TexType::OutEdgeTop, data_top);
            data.right = &curve_data;
            AdjustRectData(data_top, data);
            data.right = nullptr;
            curve_data.side = TileSide::TopRight;
            data_right.side = TileSide::BottomRight;
            BuildMeshData(verts, uvs, indexes, { data_top, curve_data,
                data_base, data_right
        });
            break;
        }
        case FloorType::EdgeTopLeft:
        {
            BuildPolyData(group,
                {TexType::OutCornerTopLeft, TexType::OutEdgeLeft, TexType::OutEdgeTop
        },
                {
                    {TileSide::TopLeft}, {TileSide::TopRight}, {TileSide::BottomLeft},
                    { TileSide::TopRight, {},  {{TileOp::Add, 2}} },
                    { TileSide::BottomLeft, {{TileOp::Add, 1}}, {} },
                    TileSide::BottomRight}, curve_data);

            curve_data.indexes = { 0, 1, 3, 0, 3, 4, 0, 4, 2, 3, 5, 4 };

            BuildPolyData(group,
                { TexType::FullTile, TexType::OutCornerTopLeft, TexType::OutEdgeLeft, TexType::OutEdgeTop },
                {
                    { TileSide::TopLeft, {{TileOp::Add, 1}},  {{TileOp::Add, 3}} },
                    { TileSide::TopRight, {},  {{TileOp::Add, 3}} },
                    { TileSide::TopLeft, {{TileOp::Add, 1}},  {{TileOp::Add, 1}} },
                    { TileSide::TopLeft, {{TileOp::Add, 2}},  {{TileOp::Add, 1}} },
                    { TileSide::BottomLeft, {{TileOp::Add, 2}}, {} },
                    TileSide::BottomRight }, data_base);
            data_base.indexes = { 0, 1, 2, 1, 5, 2, 3, 2, 4, 2, 5, 4 };

            BuildRectData(group, TexType::OutEdgeLeft, data_left);
            data.top = &curve_data;
            AdjustRectData(data_left, data);
            data.top = nullptr;
            BuildRectData(group, TexType::OutEdgeTop, data_top);
            data.left = &curve_data;
            AdjustRectData(data_top, data);
            data.left = nullptr;
            data_top.side = TileSide::TopRight;
            BuildMeshData(verts, uvs, indexes, { curve_data, data_top,
                    data_left, data_base });
            break;
        }

        case FloorType::EdgeBottomRight:
        {
            BuildPolyData(group,
                { TexType::OutCornerBottomRight, TexType::OutEdgeRight, TexType::OutEdgeBottom },
                {
                    {TileSide::TopLeft }, {TileSide::TopRight },
                    { TileSide::TopRight, {{TileOp::Sub, 1}}, {} },
                    { TileSide::BottomLeft, {},  {{TileOp::Sub, 2}} },
                {TileSide::BottomLeft},
                    {TileSide::BottomRight} }, curve_data);

            curve_data.indexes = { 0, 2, 3, 2, 1, 5, 2, 5, 3, 3, 5, 4 };

            BuildPolyData(group,
                { TexType::FullTile, TexType::OutCornerBottomRight, TexType::OutEdgeRight, TexType::OutEdgeBottom },
                {
                {TileSide::TopLeft },
                    { TileSide::TopRight, {{TileOp::Sub, 2}}, {} },
                    { TileSide::BottomRight, {{TileOp::Sub, 1}},  {{TileOp::Sub, 1}} },
                    { TileSide::BottomRight, {{TileOp::Sub, 2}},  {{TileOp::Sub, 1}} },
                    { TileSide::BottomLeft, {},  {{TileOp::Sub, 3}} },
                    { TileSide::BottomRight, {{TileOp::Sub, 1}},  {{TileOp::Sub, 3}} }
                    }, data_base);
            data_base.indexes = { 0, 1, 2, 1, 3, 2, 0, 2, 4, 2, 5, 4 };

            BuildRectData(group, TexType::OutEdgeRight, data_right);
            data.bottom = &curve_data;
            AdjustRectData(data_right, data);
            data.bottom = nullptr;
            BuildRectData(group, TexType::OutEdgeBottom, data_bottom);
            data.right = &curve_data;
            AdjustRectData(data_bottom, data);
            data.right = nullptr;

            data_right.side = TileSide::TopRight;
            data_bottom.side = TileSide::BottomLeft;
            curve_data.side = TileSide::BottomRight;
            BuildMeshData(verts, uvs, indexes, { data_base, data_right,
                    data_bottom, curve_data });
            break;
        }
        case FloorType::EdgeBottomLeft:
        {
            BuildPolyData(group,
                { TexType::OutCornerBottomLeft, TexType::OutEdgeLeft, TexType::OutEdgeBottom },
                {
                    {TileSide::TopLeft}, {TileSide::TopRight},
                    { TileSide::TopLeft, {{TileOp::Add, 1}}, {} },
                    { TileSide::BottomRight, {},  {{TileOp::Sub, 2}} },
                {TileSide::BottomLeft},
                {TileSide::BottomRight} }, curve_data);

            curve_data.indexes = { 0, 2, 4, 2, 1, 3, 2, 3, 4, 3, 5, 4 };

            BuildPolyData(group,
                { TexType::FullTile, TexType::OutCornerBottomLeft, TexType::OutEdgeLeft, TexType::OutEdgeBottom },
                {
                    { TileSide::TopLeft, {{TileOp::Add, 2}}, {} },
                    { TileSide::TopRight },
                    { TileSide::BottomLeft, {{TileOp::Add, 2}},  {{TileOp::Sub, 1}} },
                    { TileSide::BottomLeft, {{TileOp::Add, 1}},  {{TileOp::Sub, 1}} },
                    { TileSide::BottomLeft, {{TileOp::Add, 1}},  {{TileOp::Sub, 3}} },
                    { TileSide::BottomRight, {},  {{TileOp::Sub, 3}} } 
                }, data_base);
            data_base.indexes = { 0, 1, 3, 0, 3, 2, 1, 5, 3, 3, 5, 4 };

            BuildRectData(group, TexType::OutEdgeLeft, data_left);
            data.bottom = &curve_data;
            AdjustRectData(data_left, data);
            data.bottom = nullptr;
            BuildRectData(group, TexType::OutEdgeBottom, data_bottom);
            data.left = &curve_data;
            AdjustRectData(data_bottom, data);
            data.left = nullptr;

            data_bottom.side = TileSide::BottomRight;
            curve_data.side = TileSide::BottomLeft;
            BuildMeshData(verts, uvs, indexes, { data_left, data_base,
                    curve_data, data_bottom });
            break;
        }
        case FloorType::VertCrossRight:
        {
            BuildPolyData(group,
                    { TexType::FullTile, TexType::OutEdgeLeft, TexType::InCornerTopRight, TexType::InCornerBottomRight },
                {
                    { TileSide::TopLeft, {{TileOp::Add, 1}}, {} },
                    { TileSide::TopRight, {{TileOp::Sub, 2}}, {} },
                    { TileSide::TopRight, {{TileOp::Sub, 2}},  {{TileOp::Add, 2}} },
                    { TileSide::TopRight, {},  {{TileOp::Add, 2}} },
                    { TileSide::BottomRight, {},  {{TileOp::Sub, 3}} },
                    { TileSide::BottomRight, {{TileOp::Sub, 3}},  {{TileOp::Sub, 3}} },
                    { TileSide::BottomLeft, {{TileOp::Add, 1}}, {} },
                    { TileSide::BottomRight, {{TileOp::Sub, 3}}, {} } },
                    data_base);
            data_base.indexes = { 0, 1, 2, 0, 2, 5, 0, 5, 6, 5, 7, 6, 2, 3, 5, 3, 4, 5 };

            BuildRectData(group, TexType::OutEdgeLeft, data_left);
            BuildRectData(group, TexType::InCornerTopRight, data_corner1);
            BuildRectData(group, TexType::InCornerBottomRight, data_corner2);

            data_corner1.side = TileSide::TopRight;
            data_corner2.side = TileSide::BottomRight;
            BuildMeshData(verts, uvs, indexes, { data_left, data_base,
                    data_corner1, data_corner2 });
            break;
        }
        case FloorType::VertCrossLeft:
        {
            BuildPolyData(group,
                    { TexType::FullTile, TexType::OutEdgeRight, TexType::InCornerTopLeft, TexType::InCornerBottomLeft },
                {
                    { TileSide::TopLeft, {{TileOp::Add, 2}}, {} },
                    { TileSide::TopRight, {{TileOp::Sub, 1}}, {} },
                    { TileSide::TopLeft, {},  {{TileOp::Add, 2}} },
                    { TileSide::TopLeft, {{TileOp::Add, 2}},  {{TileOp::Add, 2}} },
                    { TileSide::BottomLeft, {},  {{TileOp::Sub, 3}} },
                    { TileSide::BottomLeft, {{TileOp::Add, 3}},  {{TileOp::Sub, 3}} },
                    { TileSide::BottomLeft, {{TileOp::Add, 3}}, {} },
                    { TileSide::BottomRight, {{TileOp::Sub, 1}}, {} } },
                    data_base);
            data_base.indexes = { 2, 3, 5, 2, 5, 4, 0, 1, 3, 1, 5, 3, 1, 7, 5, 5, 7, 6 };

            BuildRectData(group, TexType::OutEdgeRight, data_right);
            BuildRectData(group, TexType::InCornerTopLeft, data_corner1);
            BuildRectData(group, TexType::InCornerBottomLeft, data_corner2 );

            data_right.side = TileSide::TopRight;
            data_corner2.side = TileSide::BottomLeft;
            BuildMeshData(verts, uvs, indexes, { data_corner1, data_base,
                    data_right, data_corner2 });
            break;
        }
        case FloorType::HorzCrossTop:
        {
            BuildPolyData(group,
                    { TexType::FullTile, TexType::OutEdgeBottom, TexType::InCornerTopLeft, TexType::InCornerTopRight },
                {
                    { TileSide::TopLeft, {{TileOp::Add, 2}}, {} },
                    { TileSide::TopRight, {{TileOp::Sub, 3}}, {} },
                    { TileSide::TopLeft, {},  {{TileOp::Add, 2}} },
                    { TileSide::TopLeft, {{TileOp::Add, 2}},  {{TileOp::Add, 2}} },
                    { TileSide::TopRight, {{TileOp::Sub, 3}},  {{TileOp::Add, 3}} },
                    { TileSide::TopRight, {},  {{TileOp::Add, 3}} },
                    { TileSide::BottomLeft, {},  {{TileOp::Sub, 1}} },
                    { TileSide::BottomRight, {},  {{TileOp::Sub, 1}} } },
                    data_base);
            data_base.indexes = { 0, 1, 3, 1, 4, 3, 3, 6, 2, 4, 5, 7, 3, 4, 6, 4, 7, 6 };

            BuildRectData(group, TexType::OutEdgeBottom, data_bottom);
            BuildRectData(group, TexType::InCornerTopLeft, data_corner1);
            BuildRectData(group, TexType::InCornerTopRight, data_corner2);

            data_corner2.side = TileSide::TopRight;
            data_bottom.side = TileSide::BottomLeft;
            BuildMeshData(verts, uvs, indexes, { data_corner1, data_base, data_corner2, data_bottom });
            break;
        }
        case FloorType::HorzCrossBottom:
        {
            BuildPolyData(group,
                    { TexType::FullTile, TexType::OutEdgeTop, TexType::InCornerBottomLeft, TexType::InCornerBottomRight },
                {
                    { TileSide::TopLeft, {},  {{TileOp::Add, 1}} },
                    { TileSide::TopRight, {},  {{TileOp::Add, 1}} },
                    { TileSide::BottomLeft, {},  {{TileOp::Sub, 2}} },
                    { TileSide::BottomLeft, {{TileOp::Add, 2}},  {{TileOp::Sub, 2}} },
                    { TileSide::BottomRight, {{TileOp::Sub, 3}},  {{TileOp::Sub, 3}} },
                    { TileSide::BottomRight, {},  {{TileOp::Sub, 3}} },
                    { TileSide::BottomLeft, {{TileOp::Add, 2}}, {} },
                    { TileSide::BottomRight, {{TileOp::Sub, 3}}, {} } },
                    data_base);
            data_base.indexes = { 0, 3, 2, 0, 1, 3, 1, 4, 3, 1, 5, 4, 3, 4, 7, 3, 7, 6 };

            BuildRectData(group, TexType::OutEdgeTop, data_top);
            BuildRectData(group, TexType::InCornerBottomLeft, data_corner1);
            BuildRectData(group, TexType::InCornerBottomRight, data_corner2);

            data_corner1.side = TileSide::BottomLeft; 
            data_corner2.side = TileSide::BottomRight;
            BuildMeshData(verts, uvs, indexes, { data_top, data_base, data_corner1, data_corner2 });
            break;
        }
        case FloorType::EdgeLeftCornerTopRight:
        {
            BuildPolyData(group,
                    { TexType::FullTile, TexType::OutEdgeLeft, TexType::InCornerTopRight },
                {
                    { TileSide::TopLeft, {{TileOp::Add, 1}}, {} },
                    { TileSide::TopRight, {{TileOp::Sub, 2}}, {} },
                    { TileSide::TopRight, {{TileOp::Sub, 2}},  {{TileOp::Add, 2}} },
                    { TileSide::TopRight, {},  {{TileOp::Add, 2}} },
                    { TileSide::BottomLeft, {{TileOp::Add, 1}}, {} },
                {TileSide::BottomRight} },
                    data_base);
            data_base.indexes = { 0, 1, 2, 0, 2, 4, 2, 5, 4, 2, 3, 5 };

            BuildRectData(group, TexType::OutEdgeLeft, data_left);
            BuildRectData(group, TexType::InCornerTopRight, data_corner1);

            data_corner1.side = TileSide::TopRight;
            BuildMeshData(verts, uvs, indexes, { data_left, data_base,
                    data_corner1 });
            break;
        }
        case FloorType::EdgeLeftCornerBottomRight:
        {
            BuildPolyData(group,
                    { TexType::FullTile, TexType::OutEdgeLeft, TexType::InCornerBottomRight },
                {
                    { TileSide::TopLeft, {{TileOp::Add, 1}}, {} },
                { TileSide::TopRight },
                    { TileSide::BottomLeft, {{TileOp::Add, 1}}, {} },
                    { TileSide::BottomRight, {{TileOp::Sub, 2}},  {{TileOp::Sub, 2}} },
                    { TileSide::BottomRight, {},  {{TileOp::Sub, 2}} },
                    { TileSide::BottomRight, {{TileOp::Sub, 2}}, {} } },
                    data_base);
            data_base.indexes = { 0, 1, 3, 1, 4, 3, 0, 3, 2, 3, 5, 2 };

            BuildRectData(group, TexType::OutEdgeLeft, data_left);
            BuildRectData(group, TexType::InCornerBottomRight, data_corner1);

            data_corner1.side = TileSide::BottomRight;
            BuildMeshData(verts, uvs, indexes, { data_left, data_base,
                    data_corner1 });
            break;
        }
        case FloorType::EdgeRightCornerTopLeft:
        {
            BuildPolyData(group,
                    { TexType::FullTile, TexType::OutEdgeRight, TexType::InCornerTopLeft },
                {
                    { TileSide::TopLeft, {{TileOp::Add, 2}}, {} },
                    { TileSide::TopRight, {{TileOp::Sub, 1}}, {} },
                    { TileSide::TopLeft, {},  {{TileOp::Add, 2}} },
                    { TileSide::TopLeft, {{TileOp::Add, 2}},  {{TileOp::Add, 2}} },
                    TileSide::BottomLeft,
                    { TileSide::BottomRight, {{TileOp::Sub, 1}}, {} } },
                    data_base);
            data_base.indexes = { 0, 1, 3, 1, 5, 3, 3, 5, 4, 2, 3, 4 };

            BuildRectData(group, TexType::OutEdgeRight, data_right);
            BuildRectData(group, TexType::InCornerTopLeft, data_corner1);

            data_right.side = TileSide::TopRight;
            BuildMeshData(verts, uvs, indexes, { data_corner1, data_base,
                    data_right });
            break;
        }
        case FloorType::EdgeRightCornerBottomLeft:
        {
            BuildPolyData(group,
                    { TexType::FullTile, TexType::OutEdgeRight, TexType::InCornerBottomLeft },
                {
                    TileSide::TopLeft,
                    { TileSide::TopRight, {{TileOp::Sub, 1}}, {} },
                    { TileSide::BottomLeft, {},  {{TileOp::Sub, 2}} },
                    { TileSide::BottomLeft, {{TileOp::Add, 2}},  {{TileOp::Sub, 2}} },
                    { TileSide::BottomLeft, {{TileOp::Add, 2}}, {} },
                    { TileSide::BottomRight, {{TileOp::Sub, 1}}, {} } },
                    data_base);
            data_base.indexes = { 0, 3, 2, 0, 1, 3, 1, 5, 3, 3, 5, 4 };

            BuildRectData(group, TexType::OutEdgeRight, data_right);
            BuildRectData(group, TexType::InCornerBottomLeft, data_corner1);

            data_corner1.side = TileSide::BottomLeft;
            data_right.side = TileSide::TopRight;
            BuildMeshData(verts, uvs, indexes, { data_base, data_right,
                    data_corner1 });
            break;
        }
        case FloorType::EdgeTopCornerBottomRight:
        {
            BuildPolyData(group,
                    { TexType::FullTile, TexType::OutEdgeTop, TexType::InCornerBottomRight },
                {
                    { TileSide::TopLeft, {},  {{TileOp::Add, 1}} },
                    { TileSide::TopRight, {},  {{TileOp::Add, 1}} },
                    { TileSide::BottomRight, {{TileOp::Sub, 2}},  {{TileOp::Sub, 2}} },
                    { TileSide::BottomRight, {},  {{TileOp::Sub, 2}} },
                    TileSide::BottomLeft,
                    { TileSide::BottomRight, {{TileOp::Sub, 2}}, {} } },
                    data_base);
            data_base.indexes = { 0, 1, 2, 1, 3, 2, 0, 2, 4, 2, 5, 4 };

            BuildRectData(group, TexType::OutEdgeTop, data_top);
            BuildRectData(group, TexType::InCornerBottomRight, data_corner1);

            data_corner1.side = TileSide::BottomRight;
            BuildMeshData(verts, uvs, indexes, { data_top, data_base,
                    data_corner1 });
            break;
        }
        case FloorType::EdgeTopCornerBottomLeft:
        {
            BuildPolyData(group,
                    { TexType::FullTile, TexType::OutEdgeTop, TexType::InCornerBottomLeft },
                {
                    { TileSide::TopLeft, {},  {{TileOp::Add, 1}} },
                    { TileSide::TopRight, {},  {{TileOp::Add, 1}} },
                    { TileSide::BottomLeft, {},  {{TileOp::Sub, 2}} },
                    { TileSide::BottomLeft, {{TileOp::Add, 2}},  {{TileOp::Sub, 2}} },
                    { TileSide::BottomLeft, {{TileOp::Add, 2}}, {} },
                {TileSide::BottomRight} },
                    data_base);
            data_base.indexes = { 0, 3, 2, 0, 1, 3, 1, 5, 3, 3, 5, 4 };

            BuildRectData(group, TexType::OutEdgeTop, data_top);
            BuildRectData(group, TexType::InCornerBottomLeft, data_corner1);

            data_corner1.side = TileSide::BottomLeft;
            BuildMeshData(verts, uvs, indexes, { data_top, data_base,
                    data_corner1 });
            break;
        }
        case FloorType::EdgeBottomCornerTopRight:
        {
            BuildPolyData(group,
                    { TexType::FullTile, TexType::OutEdgeBottom, TexType::InCornerTopRight },
                {
                    {TileSide::TopLeft},
                    { TileSide::TopRight, {{TileOp::Sub, 2}}, {} },
                    { TileSide::TopRight, {{TileOp::Sub, 2}},  {{TileOp::Add, 2}} },
                    { TileSide::TopRight, {},  {{TileOp::Add, 2}} },
                    { TileSide::BottomLeft, {},  {{TileOp::Sub, 1}} },
                    { TileSide::BottomRight, {},  {{TileOp::Sub, 1}} } },
                    data_base);
            data_base.indexes = { 0, 1, 2, 0, 2, 4, 2, 3, 5, 2, 5, 4 };

            BuildRectData(group, TexType::OutEdgeBottom, data_bottom);
            BuildRectData(group, TexType::InCornerTopRight, data_corner1);

            data_corner1.side = TileSide::TopRight;
            data_bottom.side = TileSide::BottomLeft;
            BuildMeshData(verts, uvs, indexes, { data_base, data_corner1,
                    data_bottom });
            break;
        }
        case FloorType::EdgeBottomCornerTopLeft:
        {
            BuildPolyData(group,
                    { TexType::FullTile, TexType::OutEdgeBottom, TexType::InCornerTopLeft },
                {
                    { TileSide::TopLeft, {{TileOp::Add, 2}}, {} },
                    TileSide::TopRight,
                    { TileSide::TopLeft, {},  {{TileOp::Add, 2}} },
                    { TileSide::TopLeft, {{TileOp::Add, 2}},  {{TileOp::Add, 2}} },
                    { TileSide::BottomLeft, {},  {{TileOp::Sub, 1}} },
                    { TileSide::BottomRight, {},  {{TileOp::Sub, 1}} } },
                    data_base);
            data_base.indexes = { 0, 1, 3, 2, 3, 4, 1, 5, 3, 3, 5, 4 };

            BuildRectData(group, TexType::OutEdgeBottom, data_bottom);
            BuildRectData(group, TexType::InCornerTopLeft, data_corner1);

            data_bottom.side = TileSide::BottomLeft;
            BuildMeshData(verts, uvs, indexes, { data_corner1, data_base,
                    data_bottom });
            break;
        }
        case FloorType::CornerExceptTopLeft:
        {
            BuildPolyData(group,
                    { TexType::FullTile, TexType::InCornerTopRight, TexType::InCornerBottomLeft, TexType::InCornerBottomRight },
                {
                { TileSide::TopLeft },
                    { TileSide::TopRight, {{TileOp::Sub, 1}}, {} },
                    { TileSide::TopRight, {{TileOp::Sub, 1}},  {{TileOp::Add, 1}} },
                    { TileSide::TopRight, {},  {{TileOp::Add, 1}} },
                    { TileSide::BottomLeft, {},  {{TileOp::Sub, 2}} },
                    { TileSide::BottomLeft, {{TileOp::Add, 2}},  {{TileOp::Sub, 2}} },
                    { TileSide::BottomLeft, {{TileOp::Add, 2}}, {} },
                    { TileSide::BottomRight, {{TileOp::Sub, 3}}, {} },
                    { TileSide::BottomRight, {{TileOp::Sub, 3}},  {{TileOp::Sub, 3}} },
                    { TileSide::BottomRight, {},  {{TileOp::Sub, 3}} } },
                    data_base);
            data_base.indexes = { 0, 1, 2, 0, 2, 8, 0, 8, 5, 0, 5, 4, 2, 3, 9, 2, 9, 8, 5, 8, 7, 5, 7, 6 };

            BuildRectData(group, TexType::InCornerTopRight, data_corner1);
            BuildRectData(group, TexType::InCornerBottomLeft, data_corner2);
            BuildRectData(group, TexType::InCornerBottomRight, data_corner3);

            data_corner1.side = TileSide::TopRight;
            data_corner2.side = TileSide::BottomLeft;
            data_corner3.side = TileSide::BottomRight;
            BuildMeshData(verts, uvs, indexes, { data_base, data_corner1,
                    data_corner2, data_corner3 });

            break;
        }
        case FloorType::CornerExceptTopRight:
        {
            BuildPolyData(group,
                    { TexType::FullTile, TexType::InCornerTopLeft, TexType::InCornerBottomLeft, TexType::InCornerBottomRight },
                {
                    { TileSide::TopLeft, {{TileOp::Add, 1}}, {} },
                    { TileSide::TopRight },
                    { TileSide::TopLeft, {},  {{TileOp::Add, 1}} },
                    { TileSide::TopLeft, {{TileOp::Add, 1}},  {{TileOp::Add, 1}} },
                    { TileSide::BottomLeft, {},  {{TileOp::Sub, 2}} },
                    { TileSide::BottomLeft, {{TileOp::Add, 2}},  {{TileOp::Sub, 2}} },
                    { TileSide::BottomLeft, {{TileOp::Add, 2}}, {} },
                    { TileSide::BottomRight, {{TileOp::Sub, 3}}, {} },
                    { TileSide::BottomRight, {{TileOp::Sub, 3}},  {{TileOp::Sub, 3}} },
                    { TileSide::BottomRight, {},  {{TileOp::Sub, 3}} } },
                    data_base);
            data_base.indexes = { 0, 1, 3, 2, 3, 4, 3, 5, 4, 1, 5, 3, 1, 8, 5, 1, 9, 8, 5, 8, 6, 8, 7, 6 };

            BuildRectData(group, TexType::InCornerTopLeft, data_corner1);
            BuildRectData(group, TexType::InCornerBottomLeft, data_corner2);
            BuildRectData(group, TexType::InCornerBottomRight, data_corner3);

            data_corner2.side = TileSide::BottomLeft;
            data_corner3.side = TileSide::BottomRight;
            BuildMeshData(verts, uvs, indexes, { data_corner1, data_base,
                    data_corner2, data_corner3 });

            break;
        }
        case FloorType::CornerExceptBottomLeft:
        {
            BuildPolyData(group,
                    { TexType::FullTile, TexType::InCornerTopLeft, TexType::InCornerTopRight, TexType::InCornerBottomRight },
                {
                    { TileSide::TopLeft, {{TileOp::Add, 1}}, {} },
                    { TileSide::TopRight, {{TileOp::Sub, 2}}, {} },
                    { TileSide::TopLeft, {},  {{TileOp::Add, 1}} },
                    { TileSide::TopLeft, {{TileOp::Add, 1}},  {{TileOp::Add, 1}} },
                    { TileSide::TopRight, {{TileOp::Sub, 2}},  {{TileOp::Add, 1}} },
                    { TileSide::TopRight, {},  {{TileOp::Add, 2}} },
                    TileSide::BottomLeft,
                    { TileSide::BottomRight, {{TileOp::Sub, 3}}, {} },
                    { TileSide::BottomRight, {{TileOp::Sub, 3}},  {{TileOp::Sub, 3}} },
                    { TileSide::BottomRight, {},  {{TileOp::Sub, 3}} } },
                    data_base);
            data_base.indexes = { 0, 1, 3, 1, 4, 3, 2, 3, 6, 3, 4, 6, 4, 8, 6, 8, 7, 6, 4, 5, 8, 5, 9, 8 };

            BuildRectData(group, TexType::InCornerTopLeft, data_corner1);
            BuildRectData(group, TexType::InCornerTopRight, data_corner2);
            BuildRectData(group, TexType::InCornerBottomRight, data_corner3);

            data_corner2.side = TileSide::TopRight;
            data_corner3.side = TileSide::BottomRight;
            BuildMeshData(verts, uvs, indexes, { data_corner1, data_base,
                    data_corner2, data_corner3 });

            break;
        }
        case FloorType::CornerExceptBottomRight:
        {
            BuildPolyData(group,
                    { TexType::FullTile, TexType::InCornerTopLeft, TexType::InCornerTopRight, TexType::InCornerBottomLeft },
                {
                    { TileSide::TopLeft, {{TileOp::Add, 1}}, {} },
                    { TileSide::TopRight, {{TileOp::Sub, 2}}, {} },
                    { TileSide::TopLeft, {},  {{TileOp::Add, 1}} },
                    { TileSide::TopLeft, {{TileOp::Add, 1}},  {{TileOp::Add, 1}} },
                    { TileSide::TopRight, {{TileOp::Sub, 2}},  {{TileOp::Add, 2}} },
                    { TileSide::TopRight, {},  {{TileOp::Add, 2}} },
                    { TileSide::BottomLeft, {},  {{TileOp::Sub, 3}} },
                    { TileSide::BottomLeft, {{TileOp::Add, 3}},  {{TileOp::Sub, 3}} },
                    { TileSide::BottomLeft, {{TileOp::Add, 3}}, {} },
                {TileSide::BottomRight} },
                    data_base);
            data_base.indexes = { 0, 1, 4, 0, 4, 3, 2, 3, 7, 2, 7, 6, 3, 4, 9, 4, 5, 9, 3, 9, 7, 7, 9, 8 };

            BuildRectData(group, TexType::InCornerTopLeft, data_corner1);
            BuildRectData(group, TexType::InCornerTopRight, data_corner2);
            BuildRectData(group, TexType::InCornerBottomLeft, data_corner3);

            data_corner2.side = TileSide::TopRight;
            data_corner3.side = TileSide::BottomLeft;
            BuildMeshData(verts, uvs, indexes, { data_corner1, data_base,
                    data_corner2, data_corner3 });

            break;
        }
        case FloorType::CornerAll:
        {
            BuildPolyData(group,
                    { TexType::FullTile, TexType::InCornerTopLeft, TexType::InCornerTopRight,
                    TexType::InCornerBottomLeft, TexType::InCornerBottomRight },
                {
                    { TileSide::TopLeft, {{TileOp::Add, 1}}, {} },
                    { TileSide::TopRight, {{TileOp::Sub, 2}}, {} },
                    { TileSide::TopLeft, {},  {{TileOp::Add, 1}} },
                    { TileSide::TopLeft, {{TileOp::Add, 1}},  {{TileOp::Add, 1}} },
                    { TileSide::TopRight, {{TileOp::Sub, 2}},  {{TileOp::Add, 2}} },
                    { TileSide::TopRight, {},  {{TileOp::Add, 2}} },
                    { TileSide::BottomLeft, {},  {{TileOp::Sub, 3}} },
                    { TileSide::BottomLeft, {{TileOp::Add, 3}},  {{TileOp::Sub, 3}} },
                    { TileSide::BottomLeft, {{TileOp::Add, 3}}, {} },
                    { TileSide::BottomRight, {{TileOp::Sub, 4}}, {} },
                    { TileSide::BottomRight, {{TileOp::Sub, 4}},  {{TileOp::Sub, 4}} },
                    { TileSide::BottomRight, {},  {{TileOp::Sub, 4}} }  },
                data_base);
            data_base.indexes = { 0, 1, 3, 1, 4, 3, 2, 3, 6, 3, 7, 6, 4, 5, 10, 5, 11, 10, 3, 4, 7, 4, 10, 7, 7, 10, 8, 10, 9, 8 };

            BuildRectData(group, TexType::InCornerTopLeft, data_corner1);
            BuildRectData(group, TexType::InCornerTopRight, data_corner2);
            BuildRectData(group, TexType::InCornerBottomLeft, data_corner3);
            BuildRectData(group, TexType::InCornerBottomRight, data_corner4);

            data_corner2.side = TileSide::TopRight;
            data_corner3.side = TileSide::BottomLeft;
            data_corner4.side = TileSide::BottomRight;
            BuildMeshData(verts, uvs, indexes, { data_corner1, data_base,
                    data_corner2, data_corner3, data_corner4, });

            break;
        }
        case FloorType::CornerBothTop:
        {
            BuildPolyData(group,
                    { TexType::FullTile, TexType::InCornerTopLeft, TexType::InCornerTopRight },
                {
                    { TileSide::TopLeft, {{TileOp::Add, 1}}, {} },
                    { TileSide::TopRight, {{TileOp::Sub, 2}}, {} },
                    { TileSide::TopLeft, {},  {{TileOp::Add, 1}} },
                    { TileSide::TopLeft, {{TileOp::Add, 1}},  {{TileOp::Add, 1}} },
                    { TileSide::TopRight, {{TileOp::Sub, 2}},  {{TileOp::Add, 2}} },
                    { TileSide::TopRight, {},  {{TileOp::Add, 2}} },
                {TileSide::BottomLeft},
                {TileSide::BottomRight} },
                    data_base);
            data_base.indexes = { 0, 1, 4, 0, 4, 3, 2, 3, 6, 3, 7, 6, 3, 4, 7, 4, 5, 7 };

            BuildRectData(group, TexType::InCornerTopLeft, data_corner1);
            BuildRectData(group, TexType::InCornerTopRight, data_corner2);

            data_corner2.side = TileSide::TopRight;
            BuildMeshData(verts, uvs, indexes, { data_corner1, data_base,
                    data_corner2 });

            break;
        }
        case FloorType::CornerBothRight:
        {
            BuildPolyData(group,
                    { TexType::FullTile, TexType::InCornerTopRight, TexType::InCornerBottomRight },
                {
                    TileSide::TopLeft,
                    { TileSide::TopRight, {{TileOp::Sub, 1}}, {} },
                    { TileSide::TopRight, {{TileOp::Sub, 1}},  {{TileOp::Add, 1}} },
                    { TileSide::TopRight, {},  {{TileOp::Add, 1}} },
                    TileSide::BottomLeft,
                    { TileSide::BottomRight, {{TileOp::Sub, 2}}, {} },
                    { TileSide::BottomRight, {{TileOp::Sub, 2}},  {{TileOp::Sub, 2}} },
                    { TileSide::BottomRight, {},  {{TileOp::Sub, 2}} } }, 
                    data_base);
            data_base.indexes = { 0, 1, 2, 0, 2, 4, 2, 6, 4, 6, 5, 4, 2, 3, 6, 3, 7, 6 };

            BuildRectData(group, TexType::InCornerTopRight, data_corner1);
            BuildRectData(group, TexType::InCornerBottomRight, data_corner2);

            data_corner1.side = TileSide::TopRight;
            data_corner2.side = TileSide::BottomRight;
            BuildMeshData(verts, uvs, indexes, { data_base, data_corner1, data_corner2 });
            break;
        }
        case FloorType::CornerBothBottom:
        {
            BuildPolyData(group,
                    { TexType::FullTile, TexType::InCornerBottomLeft, TexType::InCornerBottomRight },
                {
                    TileSide::TopLeft,
                    TileSide::TopRight,
                    { TileSide::BottomLeft, {},  {{TileOp::Sub, 1}} },
                    { TileSide::BottomLeft, {{TileOp::Add, 1}},  {{TileOp::Sub, 1}} },
                    { TileSide::BottomLeft, {{TileOp::Add, 1}}, {} },
                    { TileSide::BottomRight, {{TileOp::Sub, 2}}, {} },
                    { TileSide::BottomRight, {{TileOp::Sub, 2}},  {{TileOp::Sub, 2}} },
                    { TileSide::BottomRight, {},  {{TileOp::Sub, 2}} } },
                    data_base);
            data_base.indexes = { 0, 3, 2, 0, 6, 3, 0, 1, 6, 1, 7, 6, 3, 6, 5, 3, 5, 4 };

            BuildRectData(group, TexType::InCornerBottomLeft, data_corner1);
            BuildRectData(group, TexType::InCornerBottomRight, data_corner2);

            data_corner1.side = TileSide::BottomLeft;
            data_corner2.side = TileSide::BottomRight;
            BuildMeshData(verts, uvs, indexes, { data_base, data_corner1, data_corner2 });
            break;
        }
        case FloorType::CornerBothLeft:
        {
            BuildPolyData(group,
                    { TexType::FullTile, TexType::InCornerTopLeft, TexType::InCornerBottomLeft },
                {
                    { TileSide::TopLeft, {{TileOp::Add, 1}}, {} },
                {TileSide::TopRight},
                    { TileSide::TopLeft, {},  {{TileOp::Add, 1}} },
                    { TileSide::TopLeft, {{TileOp::Add, 1}},  {{TileOp::Add, 1}} },
                    { TileSide::BottomLeft, {},  {{TileOp::Sub, 2}} },
                    { TileSide::BottomLeft, {{TileOp::Add, 2}},  {{TileOp::Sub, 2}} },
                    { TileSide::BottomLeft, {{TileOp::Add, 2}}, {} },
                {TileSide::BottomRight} },
                    data_base);
            data_base.indexes = { 0, 1, 3, 2, 3, 4, 3, 5, 4, 1, 5, 3, 1, 7, 5, 5, 7, 6 };

            BuildRectData(group, TexType::InCornerTopLeft, data_corner1);
            BuildRectData(group, TexType::InCornerBottomLeft, data_corner2);

            data_corner2.side = TileSide::BottomLeft;
            BuildMeshData(verts, uvs, indexes, { data_corner1, data_base, data_corner2 });
            break;
        }
        case FloorType::CornerTopLeftBottomRight:
        {
            BuildPolyData(group,
                    { TexType::FullTile, TexType::InCornerTopLeft, TexType::InCornerBottomRight },
                {
                    { TileSide::TopLeft, {{TileOp::Add, 1}}, {} },
                    TileSide::TopRight,
                    { TileSide::TopLeft, {},  {{TileOp::Add, 1}} },
                    { TileSide::TopLeft, {{TileOp::Add, 1}},  {{TileOp::Add, 1}} },
                    TileSide::BottomLeft,
                    { TileSide::BottomRight, {{TileOp::Sub, 2}}, {} },
                    { TileSide::BottomRight, {{TileOp::Sub, 2}},  {{TileOp::Sub, 2}} },
                    { TileSide::BottomRight, {},  {{TileOp::Sub, 2}} } },
                    data_base);
            data_base.indexes = { 0, 1, 3, 2, 3, 4, 1, 6, 3, 1, 7, 6, 3, 6, 4, 6, 5, 4 };

            BuildRectData(group, TexType::InCornerTopLeft, data_corner1);
            BuildRectData(group, TexType::InCornerBottomRight, data_corner2);

            data_corner2.side = TileSide::BottomRight;
            BuildMeshData(verts, uvs, indexes, { data_corner1, data_base, data_corner2 });
            break;
        }
        case FloorType::CornerTopRightBottomLeft:
        {
            BuildPolyData(group,
                    { TexType::FullTile, TexType::InCornerTopRight, TexType::InCornerBottomLeft },
                {
                    {TileSide::TopLeft},
                    { TileSide::TopRight, {{TileOp::Sub, 1}}, {} },
                    { TileSide::TopRight, {{TileOp::Sub, 1}},  {{TileOp::Add, 1}} },
                    { TileSide::TopRight, {},  {{TileOp::Add, 1}} },
                    { TileSide::BottomLeft, {},  {{TileOp::Sub, 2}} },
                    { TileSide::BottomLeft, {{TileOp::Add, 2}},  {{TileOp::Sub, 2}} },
                    { TileSide::BottomLeft, {{TileOp::Add, 2}}, {} },
                {TileSide::BottomRight} },
                    data_base);
            data_base.indexes = { 0, 1, 2, 0, 2, 5, 0, 5, 4, 2, 3, 7, 2, 7, 5, 5, 7, 6 };

            BuildRectData(group, TexType::InCornerTopRight, data_corner1);
            BuildRectData(group, TexType::InCornerBottomLeft, data_corner2);

            data_corner1.side = TileSide::TopRight;
            data_corner2.side = TileSide::BottomLeft;
            BuildMeshData(verts, uvs, indexes, { data_base, data_corner1, data_corner2 });
            break;
        }
        case FloorType::OnlyCornerTopRight:
        {
            BuildPolyData(group,
                    { TexType::FullTile, TexType::InCornerTopRight },
                {
                    {TileSide::TopLeft},
                    { TileSide::TopRight, {{TileOp::Sub, 1}}, {} },
                    { TileSide::TopRight, {{TileOp::Sub, 1}},  {{TileOp::Add, 1}} },
                    { TileSide::TopRight, {},  {{TileOp::Add, 1}} },
                {TileSide::BottomLeft},
                {TileSide::BottomRight} },
                    data_base);
            data_base.indexes = { 0, 1, 2, 0, 2, 4, 2, 3, 5, 2, 5, 4 };

            BuildRectData(group, TexType::InCornerTopRight, data_corner1);

            data_corner1.side = TileSide::TopRight;
            BuildMeshData(verts, uvs, indexes, { data_base, data_corner1 });
            break;
        }
        case FloorType::OnlyCornerTopLeft:
        {
            BuildPolyData(group,
                    { TexType::FullTile, TexType::InCornerTopLeft },
                {
                    { TileSide::TopLeft, {{TileOp::Add, 1}}, {} },
                {TileSide::TopRight},
                    { TileSide::TopLeft, {},  {{TileOp::Add, 1}} },
                    { TileSide::TopLeft, {{TileOp::Add, 1}},  {{TileOp::Add, 1}} },
                {TileSide::BottomLeft},
                {TileSide::BottomRight} },
                    data_base);
            data_base.indexes = { 0, 1, 3, 2, 3, 4, 1, 5, 3, 3, 5, 4 };

            BuildRectData(group, TexType::InCornerTopLeft, data_corner1);

            BuildMeshData(verts, uvs, indexes, { data_corner1, data_base } );
            break;
        }
        case FloorType::OnlyCornerBottomRight:
        {
            BuildPolyData(group,
                    { TexType::FullTile, TexType::InCornerBottomRight },
                {
                    {TileSide::TopLeft},
                {TileSide::TopRight},
                {TileSide::BottomLeft},
                    { TileSide::BottomRight, {{TileOp::Sub, 1}}, {} },
                    { TileSide::BottomRight, {{TileOp::Sub, 1}},  {{TileOp::Sub, 1}} },
                    { TileSide::BottomRight, {},  {{TileOp::Sub, 1}} } },
                    data_base);
            data_base.indexes = { 1, 5, 4, 0, 1, 4, 0, 4, 2, 4, 3, 2 };

            BuildRectData(group, TexType::InCornerBottomRight, data_corner1);

            data_corner1.side = TileSide::BottomRight;
            BuildMeshData(verts, uvs, indexes, { data_base, data_corner1 });
            break;
        }
        case FloorType::OnlyCornerBottomLeft:
        {
            BuildPolyData(group,
                    { TexType::FullTile, TexType::InCornerBottomLeft },
                {
                    {TileSide::TopLeft},
                {TileSide::TopRight},
                    { TileSide::BottomLeft, {},  {{TileOp::Sub, 1}} },
                    { TileSide::BottomLeft, {{TileOp::Add, 1}},  {{TileOp::Sub, 1}} },
                    { TileSide::BottomLeft, {{TileOp::Add, 1}}, {} },
                    TileSide::BottomRight },
                    data_base);
            data_base.indexes = { 0, 3, 2, 0, 1, 3, 1, 5, 3, 3, 5, 4 };

            BuildRectData(group, TexType::InCornerBottomLeft, data_corner1);

            data_corner1.side = TileSide::BottomLeft;
            BuildMeshData(verts, uvs, indexes, { data_base, data_corner1 });
            break;
        }
        case FloorType::IsolatedTile:
        {
            BuildPolyData(group,
                    { TexType::FullTile, TexType::OutSharpCornerTopLeft,
                    TexType::OutSharpCornerTopRight, TexType::OutSharpCornerBottomLeft,
                    TexType::OutSharpCornerBottomRight },
                {
                    { TileSide::TopLeft, {{TileOp::Add, 1}},  {{TileOp::Add, 1}} },
                    { TileSide::TopRight, {{TileOp::Sub, 2}},  {{TileOp::Add, 2}} },
                    { TileSide::BottomLeft, {{TileOp::Add, 3}},  {{TileOp::Sub, 3}} },
                    { TileSide::BottomRight, {{TileOp::Sub, 4}},  {{TileOp::Sub, 4}} } },
                    data_base);
            data_base.indexes = { 0, 1, 3, 0, 3, 2 };

            BuildRectData(group, TexType::OutSharpCornerTopLeft, data_corner1);
            BuildRectData(group, TexType::OutSharpCornerTopRight, data_corner2);
            BuildRectData(group, TexType::OutSharpCornerBottomLeft, data_corner3);
            BuildRectData(group, TexType::OutSharpCornerBottomRight, data_corner4);

            BuildRectData(group, TexType::OutEdgeTop, data_top);
            data.left = &data_corner1;
            data.right = &data_corner2;
            AdjustRectData(data_top, data);
            data.left = nullptr;
            data.right = nullptr;
            BuildRectData(group, TexType::OutEdgeLeft, data_left);
            data.top = &data_corner1;
            data.bottom = &data_corner3;
            AdjustRectData(data_left, data);
            data.top = nullptr;
            data.bottom = nullptr;
            BuildRectData(group, TexType::OutEdgeBottom, data_bottom);
            data.left = &data_corner3;
            data.right = &data_corner4;
            AdjustRectData(data_bottom, data);
            data.left = nullptr;
            data.right = nullptr;
            BuildRectData(group, TexType::OutEdgeRight, data_right);
            data.top = &data_corner2;
            data.bottom = &data_corner4;
            AdjustRectData(data_right, data);
            data.top = nullptr;
            data.bottom = nullptr;

            data_top.side = TileSide::Top;
            data_corner2.side = TileSide::TopRight;
            data_left.side = TileSide::Left;
            data_right.side = TileSide::Right;
            data_corner3.side = TileSide::BottomLeft;
            data_bottom.side = TileSide::Bottom;
            data_corner4.side = TileSide::BottomRight;
            BuildMeshData(verts, uvs, indexes, { data_corner1,
                    data_top,
                    data_corner2,
                    data_left,
                    data_base,
                    data_right,
                    data_corner3,
                    data_bottom,
                    data_corner4 });
            break;
        }
        case FloorType::DeadendLeft:
        {
            BuildPolyData(group,
                    { TexType::FullTile, TexType::OutEdgeTop, TexType::OutEdgeBottom,
                    TexType::OutSharpCornerTopLeft, TexType::OutSharpCornerBottomLeft },
                {
                    { TileSide::TopLeft, {{TileOp::Add, 3}},  {{TileOp::Add, 3}} },
                    { TileSide::TopRight, {},  {{TileOp::Add, 1}} },
                    { TileSide::BottomLeft, {{TileOp::Add, 4}},  {{TileOp::Sub, 4}} },
                    { TileSide::BottomRight, {},  {{TileOp::Sub, 2}} } },
                    data_base);
            data_base.indexes = { 0, 1, 3, 0, 3, 2 };

            BuildRectData(group, TexType::OutSharpCornerTopLeft, data_corner1);
            BuildRectData(group, TexType::OutSharpCornerBottomLeft, data_corner2);
            BuildRectData(group, TexType::OutEdgeTop, data_top);
            data.left = &data_corner1;
            AdjustRectData(data_top, data);
            data.left = nullptr;
            BuildRectData(group, TexType::OutEdgeLeft, data_left);
            data.top = &data_corner1;
            data.bottom = &data_corner2;
            AdjustRectData(data_left, data);
            data.top = nullptr;
            data.bottom = nullptr;
            BuildRectData(group, TexType::OutEdgeBottom, data_bottom);
            data.left = &data_corner2;
            AdjustRectData(data_bottom, data);
            data.left = nullptr;

            data_top.side = TileSide::Top;
            data_left.side = TileSide::Left;
            data_corner2.side = TileSide::BottomLeft;
            data_bottom.side = TileSide::Bottom;
            BuildMeshData(verts, uvs, indexes, { data_corner1,
                    data_top,
                    data_left,
                    data_base,
                    data_corner2, 
                    data_bottom });
            break;
        }
        case FloorType::DeadendTop:
        {
            BuildPolyData(group,
                    { TexType::FullTile, TexType::OutEdgeLeft, TexType::OutEdgeRight,
                    TexType::OutSharpCornerTopLeft, TexType::OutSharpCornerTopRight },
                {
                    { TileSide::TopLeft, {{TileOp::Add, 3}},  {{TileOp::Add, 3}} },
                    { TileSide::TopRight, {{TileOp::Sub, 4}},  {{TileOp::Add, 4}} },
                    { TileSide::BottomLeft, {{TileOp::Add, 1}}, {} },
                    { TileSide::BottomRight, {{TileOp::Sub, 2}}, {} } },
                    data_base);
            data_base.indexes = { 0, 1, 3, 0, 3, 2 };

            BuildRectData(group, TexType::OutSharpCornerTopLeft, data_corner1);
            BuildRectData(group, TexType::OutSharpCornerTopRight, data_corner2);
            BuildRectData(group, TexType::OutEdgeTop, data_top);
            data.left = &data_corner1;
            data.right = &data_corner2;
            AdjustRectData(data_top, data);
            data.left = nullptr;
            data.right = nullptr;
            BuildRectData(group, TexType::OutEdgeLeft, data_left);
            data.top = &data_corner1;
            AdjustRectData(data_left, data);
            data.top = nullptr;
            BuildRectData(group, TexType::OutEdgeRight, data_right);
            data.top = &data_corner2;
            AdjustRectData(data_right, data);
            data.top = nullptr;

            data_top.side = TileSide::Top;
            data_corner2.side = TileSide::TopRight;
            data_left.side = TileSide::Left;
            data_right.side = TileSide::Right;
            BuildMeshData(verts, uvs, indexes, { data_corner1,
                    data_top,
                    data_corner2,
                    data_left,
                    data_base,
                    data_right });
            break;
        }
        case FloorType::DeadendRight:
        {
            BuildPolyData(group,
                    { TexType::FullTile, TexType::OutEdgeTop, TexType::OutEdgeBottom,
                    TexType::OutSharpCornerTopRight, TexType::OutSharpCornerBottomRight },
                {
                    { TileSide::TopLeft, {},  {{TileOp::Add, 1}} },
                    { TileSide::TopRight, {{TileOp::Sub, 3}},  {{TileOp::Add, 3}} },
                    { TileSide::BottomLeft, {},  {{TileOp::Sub, 2}} },
                    { TileSide::BottomRight, {{TileOp::Sub, 4}},  {{TileOp::Sub, 4}} } },
                    data_base);
            data_base.indexes = { 0, 1, 3, 0, 3, 2 };

            BuildRectData(group, TexType::OutSharpCornerTopRight, data_corner1);
            BuildRectData(group, TexType::OutSharpCornerBottomRight, data_corner2);
            BuildRectData(group, TexType::OutEdgeTop, data_top);
            data.right = &data_corner1;
            AdjustRectData(data_top, data);
            data.right = nullptr;
            BuildRectData(group, TexType::OutEdgeRight, data_right);
            data.top = &data_corner1;
            data.bottom = &data_corner1;
            AdjustRectData(data_right, data);
            data.top = nullptr;
            data.bottom = nullptr;
            BuildRectData(group, TexType::OutEdgeBottom, data_bottom);
            data.right = &data_corner2;
            AdjustRectData(data_bottom, data);
            data.right = nullptr;

            data_top.side = TileSide::TopLeft;
            data_corner1.side = TileSide::TopRight;
            data_right.side = TileSide::Right;
            data_bottom.side = TileSide::BottomLeft;
            data_corner2.side = TileSide::BottomRight;
            BuildMeshData(verts, uvs, indexes, {
                    data_top,
                    data_corner1,
                    data_base,
                    data_right,
                    data_bottom,
                    data_corner2 });
            break;
        }
        case FloorType::DeadendBottom:
        {
            BuildPolyData(group,
                { TexType::FullTile, TexType::OutEdgeLeft, TexType::OutEdgeRight,
                TexType::OutSharpCornerBottomLeft, TexType::OutSharpCornerBottomRight },
                {
                { TileSide::TopLeft, { {TileOp::Add, 1} }, {} },
                { TileSide::TopRight, {{TileOp::Sub, 2}}, {} },
                { TileSide::BottomLeft, {{TileOp::Add, 3}},  {{TileOp::Sub, 3}} },
                { TileSide::BottomRight, {{TileOp::Sub, 4}},  {{TileOp::Sub, 4}} }
                },
                data_base);
            data_base.indexes = { 0, 1, 3, 0, 3, 2 };

            BuildRectData(group, TexType::OutSharpCornerBottomLeft, data_corner1);
            BuildRectData(group, TexType::OutSharpCornerBottomRight, data_corner2);
            BuildRectData(group, TexType::OutEdgeBottom, data_bottom);
            data.left = &data_corner1;
            data.right = &data_corner2;
            AdjustRectData(data_bottom, data);
            data.left = nullptr;
            data.right = nullptr;
            BuildRectData(group, TexType::OutEdgeLeft, data_left);
            data.bottom = &data_corner1;
            AdjustRectData(data_left, data);
            data.bottom = nullptr;
            BuildRectData(group, TexType::OutEdgeRight, data_right);
            data.bottom = &data_corner2;
            AdjustRectData(data_right, data);
            data.bottom = nullptr;

            data_left.side = TileSide::TopLeft;
                data_right.side = TileSide::TopRight;
                data_corner1.side = TileSide::BottomLeft;
                data_bottom.side = TileSide::Bottom;
                data_corner2.side = TileSide::BottomRight;
            BuildMeshData(verts, uvs, indexes, {
                data_left,
                data_base,
                data_right,
                data_corner1,
                data_bottom,
                data_corner2
            });
            break;
        }
        default:
        {
            verts = {};
            uvs = {};
            indexes = {};
            normals = {};
            return;
        }
    }

    normals.Resize(verts.Count(), false);
    for (int ix = 0, siz = verts.Count(); ix < siz; ++ix)
    {
        normals[ix] = Float3::Up;

        // The data was generated for a different engine originally, which has a reverse Z direction to Flax.
        Float3 &v = verts[ix];
        v.Z *= -1;
        v.Z += ScriptGlobals::tile_dimension;
    }
}

void TileGenerator::BuildUVArrayFromData(FloorGroup group, TexType ttype, Array<Float2> &result) const
{
    Rectangle rect = floor_uv_data[group][ttype];
    Float2 texSize = Float2((float)texture_size.X, (float)texture_size.Y);
    result = { rect.Location / texSize,
            rect.GetUpperRight() / texSize,
            rect.GetBottomLeft() / texSize,
            rect.GetBottomRight() / texSize };
}

void TileGenerator::BuildRectData(FloorGroup group, TexType ttype, TileRectData &result)
{
    //var data = new TileRectData();
    result.indexes = { 0, 1, 2, 1, 3, 2 };
    Rectangle r = floor_uv_data[group][ttype];
    result.verts = {
        Float3(0.0f, 0.0f, 0.0f),
        Float3(r.Size.X / tile_size.X * ScriptGlobals::tile_dimension, 0.0f, 0.0f),
        Float3(0.0f, 0.0f, r.Size.Y / tile_size.Y * ScriptGlobals::tile_dimension),
        Float3(r.Size.X / tile_size.X * ScriptGlobals::tile_dimension, 0.0f, r.Size.Y / tile_size.Y * ScriptGlobals::tile_dimension)
        };
    BuildUVArrayFromData(group, ttype, result.uvs);
}

    // Creates a mesh build data object from the given array of [TileRectData, TileSide (or null)]
    // pairs. Makes sure the verts, uvs and indexes are properly ordered. The TileSide member
    // of the pair determines if the given vertices should be moved to be aligned some way or not.
void TileGenerator::BuildMeshData(Array<Float3> &verts, Array<Float2> &uvs, Array<uint32> &indexes, const Array<TileRectData> &dataparams) const
{
    int vert_count = 0;
    for (const TileRectData &data : dataparams)
        vert_count += data.verts.Count();

    //Array<Float3> verts;
    verts.AddUninitialized(vert_count);
    //Array<Float2> uvs;
    uvs.AddUninitialized(vert_count);

    Array<TileRectData> vert_array;
    vert_array.AddDefault(dataparams.Count());
    int ix = 0;
    int vert_pos = 0;

    for (const TileRectData &data : dataparams)
    {
        if (data.side == TileSide::None)
            memcpy(verts.Get() + vert_pos, data.verts.Get(), sizeof(Float3) * data.verts.Count());
        else
            AlignRectPoints(data.verts, data.side, verts, vert_pos);

        //data.uvs.CopyTo(uvs, vert_pos);
        memcpy(uvs.Get() + vert_pos, data.uvs.Get(), sizeof(Float2) * data.uvs.Count());
        vert_pos += data.verts.Count();
        vert_array[ix++] = data;
    }

    BuildIndexArray(vert_array, indexes);
}

// Constructs a tile data of vertexes and uvs, but not indexes.
// group: where all the texture and rectangle data will come from
// types: a list of texture types to use when creating vertices. The first one must be
//        the main type for the uv mapping.
// specs: a list with one element for each vertex position. The vertex positions are calculated
//        by taking the commands in these elements. The elements are arrays.
// Each element in specs starts with a corner for the vertex initial position in the base (first)
// type from `types`. If the element array doesn't contain anything else, this position is used
// as is. Otherwise the array needs two more arrays. One for the x and one for the y coordinates.
// These arrays will be a pair of operations and index in `types. The two operations currently are
// OP_ADD and OP_SUBTRACT. For the x array, OP_ADD will add the full width of the passed tex type,
// and OP_SUBTRACT subtracts it. For the y array it's the height of the tex type rectangle that's
// used.
void TileGenerator::BuildPolyData(FloorGroup group, const Array<TexType> &texes, const Array<VertModifierWithAlign> &modifiers, TileRectData &out) const
{
    Array<Rectangle> rects(texes.Count());
    
    for (TexType t : texes)
        rects.Add(floor_uv_data[group][t]);

    TileRectData data;
    Float2 basePos = rects[0].Location;
    Float2 baseSize = rects[0].Size;

    for (VertModifierWithAlign m : modifiers)
    {
        Float3 v;
        Float2 uv;
        switch (m.side)
        {
            case TileSide::TopLeft:
                v = Float3::Zero;
                uv = basePos / Float2((float)texture_size.X, (float)texture_size.Y);
                break;
            case TileSide::TopRight:
                v = Float3(baseSize.X / (float)tile_size.X, 0.0f, 0.0f) * ScriptGlobals::tile_dimension;
                uv = Float2(basePos.X + baseSize.X, basePos.Y) / Float2((float)texture_size.X, (float)texture_size.Y);
                break;
            case TileSide::BottomLeft:
                v = Float3(0.0f, 0.0f, baseSize.Y / (float)tile_size.Y) * ScriptGlobals::tile_dimension;
                uv = Float2(basePos.X, basePos.Y + baseSize.Y) / Float2((float)texture_size.X, (float)texture_size.Y);
                break;
            default:
                v = Float3(baseSize.X / (float)tile_size.X, 0.0f, baseSize.Y / (float)tile_size.Y) * ScriptGlobals::tile_dimension;
                uv = Float2(basePos.X + baseSize.X, basePos.Y + baseSize.Y) / Float2((float)texture_size.X, (float)texture_size.Y);
                break;
        }

        if (m.xmods.Count() != 0)
        {
            for (const TileOpPair &t : m.xmods)
            {
                if (t.op == TileOp::Add)
                {
                    const Rectangle &r = rects[t.index];
                    v.X += r.Size.X * ScriptGlobals::tile_dimension / (float)tile_size.X;
                    uv.X += r.Size.X / (float)texture_size.X;
                }
                else if (t.op == TileOp::Sub)
                {
                    const Rectangle &r = rects[t.index];
                    v.X -= r.Size.X * ScriptGlobals::tile_dimension / (float)tile_size.X;
                    uv.X -= r.Size.X / (float)texture_size.X;
                }
            }
        }
        if ( m.ymods.Count() != 0)
        {

            for (const TileOpPair &t : m.ymods)
            {
                if (t.op == TileOp::Add)
                {
                    const Rectangle &r = rects[t.index];
                    v.Z += r.Size.Y * ScriptGlobals::tile_dimension / (float)tile_size.Y;
                    uv.Y += r.Size.Y / (float)texture_size.Y;
                }
                else if (t.op == TileOp::Sub)
                {
                    const Rectangle &r = rects[t.index];
                    v.Z -= r.Size.Y * ScriptGlobals::tile_dimension / (float)tile_size.Y;
                    uv.Y -= r.Size.Y / (float)texture_size.Y;
                }
            }
        }
        out.verts.Add(v);
        out.uvs.Add(uv);
    }
}


void TileGenerator::AlignRectPoints(const Array<Float3> &verts, TileSide side, Array<Float3> &out_verts, int array_index) const
{
    memcpy(out_verts.Get() + array_index, verts.Get(), sizeof(Float3) * verts.Count());

    Rectangle bounds = CalculateRectBounds(verts);

    if (bounds.Location.X != 0.0f && (side == TileSide::Left || side == TileSide::TopLeft || side == TileSide::BottomLeft))
    {
        for (int ix = array_index, siz = out_verts.Count(); ix < siz; ++ix)
            out_verts[ix].X -= bounds.Location.X;
    }

    if (std::abs(bounds.Location.X + bounds.GetWidth() - ScriptGlobals::tile_dimension) > 0.1e-6 && (side == TileSide::Right || side == TileSide::TopRight || side == TileSide::BottomRight))
    {
        float dif = ScriptGlobals::tile_dimension - (bounds.Location.X + bounds.GetWidth());
        for (int ix = array_index, siz = out_verts.Count(); ix < siz; ++ix)
            out_verts[ix].X += dif;
    }

    if (bounds.Location.Y != 0.0f && (side == TileSide::Top || side == TileSide::TopLeft || side == TileSide::TopRight))
    {
        for (int ix = array_index, siz = out_verts.Count(); ix < siz; ++ix)
            out_verts[ix].Z -= bounds.Location.Y;
    }

    if (std::abs(bounds.Location.Y + bounds.GetHeight() - ScriptGlobals::tile_dimension) > 0.1e-6 && (side == TileSide::Bottom || side == TileSide::BottomLeft || side == TileSide::BottomRight))
    {
        float dif = ScriptGlobals::tile_dimension - (bounds.Location.Y + bounds.GetHeight());
        for (int ix = array_index, siz = out_verts.Count(); ix < siz; ++ix)
            out_verts[ix].Z += dif;
    }
}

// Given a collection of vertex data, returns an array that concatenated the contents, but
// increasing the values to not overlap. For example if first vertex indexes are [0, 1, 2],
// the second indexes would be starting at 0 too. Increasing them by 3 avoids overlap.
void TileGenerator::BuildIndexArray(const Array<TileRectData> &from, Array<uint32> &indexes) const
{
    int vert_count = 0;
    for (const TileRectData &data : from)
        vert_count += data.indexes.Count();

    indexes.Clear();
    indexes.AddUninitialized(vert_count);
    int offset = 0;
    int vert_pos = 0;

    for (const TileRectData &data : from)
    {
        for (int i : data.indexes)
            indexes[vert_pos++] = i + offset;
        offset += data.verts.Count();
    }
}

// Calculates the bounding rectangle of a mesh of two triangles.
Rectangle TileGenerator::CalculateRectBounds(const Array<Float3> &verts) const
{
    float minX = ScriptGlobals::tile_dimension;
    float maxX = 0.0f;
    float minY = ScriptGlobals::tile_dimension;
    float maxY = 0.0f;
    for (const Float3 &v : verts)
    {
        minX = std::min(v.X, minX);
        maxX = std::max(v.X, maxX);
        minY = std::min(v.Z, minY);
        maxY = std::max(v.Z, maxY);
    }
    return Rectangle(minX, minY, maxX - minX, maxY - minY);
}


static void UpdateV3InList(Array<Float3> &list, int index, float xdif, float zdif)
{
    Float3 &v = list[index];
    v.X += xdif;
    v.Z += zdif;
    //list[index] = v;
}

static void UpdateV2InList(Array<Float2> &list, int index, float xdif, float ydif)
{
    Float2 &v = list[index];
    v.X += xdif;
    v.Y += ydif;
    //list[index] = v;
}

// Modifies the verts and uvs arrays in data, subtracting the rectangle area of the passed
// data on left, top, right and bottom sides. There is no error checking for subtracting too much.
void TileGenerator::AdjustRectData(TileRectData &data, const TileRectDataCollection &sides) const
{
    if (sides.left)
    {
        float siz = CalculateRectBounds(sides.left->verts).Size.X;
        float usiz = siz / ScriptGlobals::tile_dimension * tile_size.X / texture_size.X;
        UpdateV3InList(data.verts, 0, siz, 0.0f);
        UpdateV3InList(data.verts, 2, siz, 0.0f);
        UpdateV2InList(data.uvs, 0, usiz, 0.0f);
        UpdateV2InList(data.uvs, 2, usiz, 0.0f);
    }

    if (sides.right)
    {
        float siz = CalculateRectBounds(sides.right->verts).Size.X;
        float usiz = siz / ScriptGlobals::tile_dimension * tile_size.X / texture_size.X;
        UpdateV3InList(data.verts, 1, -siz, 0.0f);
        UpdateV3InList(data.verts, 3, -siz, 0.0f);
        UpdateV2InList(data.uvs, 1, -usiz, 0.0f);
        UpdateV2InList(data.uvs, 3, -usiz, 0.0f);
    }

    if (sides.top)
    {
        float siz = CalculateRectBounds(sides.top->verts).Size.Y;
        float usiz = siz / ScriptGlobals::tile_dimension * tile_size.Y / texture_size.Y;
        UpdateV3InList(data.verts, 0, 0.0f, siz);
        UpdateV3InList(data.verts, 1, 0.0f, siz);
        UpdateV2InList(data.uvs, 0, 0.0f, usiz);
        UpdateV2InList(data.uvs, 1, 0.0f, usiz);
    }

    if (sides.bottom)
    {
        float siz = CalculateRectBounds(sides.bottom->verts).Size.Y;
        float usiz = siz / ScriptGlobals::tile_dimension * tile_size.Y / texture_size.Y;
        UpdateV3InList(data.verts, 2, 0.0f, -siz);
        UpdateV3InList(data.verts, 3, 0.0f, -siz);
        UpdateV2InList(data.uvs, 2, 0.0f, -usiz);
        UpdateV2InList(data.uvs, 3, 0.0f, -usiz);
    }
}
