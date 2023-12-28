using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using FlaxEngine;
using FlaxEngine.Utilities;
using Scripts;

namespace Game;

using FloorGroup = TileGenerator.FloorGroup;
using FloorType = TileGenerator.FloorType;
using FloorData = (TileGenerator.FloorGroup group, TileGenerator.FloorType ftype);
using TileSide = TileGenerator.TileSide;

/// <summary>
/// TileMap Script.
/// </summary>
public class TileMap : Script
{
    public enum ItemType {
        None,
        Tree,
        Wall
    }

    public enum SelectionMode
    {
        Demolish,
    }


    private struct ItemMapData
    {
        public ItemType mtype;
        public Int2 origin;
        public ModelInstanceActor actor;

        public ItemMapData()
        {
            mtype = ItemType.None;
            actor = null;
        }
    }

    public TileGenerator tileGenerator;
    // Grid size of the starting map
    public Int2 MapSize = new(16, 16);
    // Grid X coordinates at the bottom of the map where the park can connect
    // to the outside world.
    public int[] EntryTiles = [];

    // Material to assign to created tiles
    public MaterialBase TileMaterial;

    public Model wallModel;

    public Model treeModel;
    public MaterialBase treeMaterial;

    public Color placementColor;
    public Color destructColor;

    public Model entranceModel;

    public Model sidewalkModel;
    public Model[] busLaneModels;

    // Group and floor type pairing for each map cell position.
    private FloorData[] mapData;
    // ID of meshes placed at each map cell position.
    private StaticModel[] mapMeshes;

    private ItemMapData[] itemMap;

    private (Int2 A, Int2 B) tempPosition;
    private FloorGroup tempGroup;
    private List<StaticModel> tempTiles = [];

    private Int2 tempMapOrigin;
    private Int2 tempMapSize;
    private int[] tempMap;

    private Dictionary<ItemType, StaticModel> itemModels = [];
    private ItemType tempItemType;
    private StaticModel tempItem = null;

    private MaterialInstance tilePlacementMaterial;
    private MaterialInstance plantPlacementMaterial;

    // Determines how selected models are shown.
    private SelectionMode selectionMode;
    // Mainly used to update the material of meshes for operations, like destruction.
    private HashSet<ModelInstanceActor> selectedModels;
    // A pair of original and updated material of selected objects.
    private Dictionary<MaterialBase, MaterialInstance> selectionMaterials;
    // Reverse of selectionMaterials.
    private Dictionary<MaterialInstance, MaterialBase> deselectionMaterials;

    /// <inheritdoc/>
    public override void OnStart()
    {
        tilePlacementMaterial = TileMaterial.CreateVirtualInstance();
        tilePlacementMaterial.SetParameterValue("EmissiveColor", placementColor);

        plantPlacementMaterial = treeMaterial.CreateVirtualInstance();
        plantPlacementMaterial.SetParameterValue("EmissiveColor", placementColor);

        selectedModels = [];
        selectionMaterials = [];
        deselectionMaterials = [];

        GenerateMap();
    }
    
    /// <inheritdoc/>
    public override void OnEnable()
    {
        // Here you can add code that needs to be called when script is enabled (eg. register for events)
    }

    /// <inheritdoc/>
    public override void OnDisable()
    {
        // Here you can add code that needs to be called when script is disabled (eg. unregister from events)
    }

    /// <inheritdoc/>
    public override void OnUpdate()
    {
        // Here you can add code that needs to be called every frame
    }

    public Int2 FindTilePosition(Ray ray)
    {
        var tileDim = tileGenerator.TileDimension;
        var hit = ray.GetPoint(ray.Position.Y / -ray.Direction.Y);
        if (hit.X < 0 || hit.Z < 0 || hit.X > MapSize.X * tileDim || hit.Z > MapSize.Y * tileDim)
            return new Int2(-1, -1);

        return new Int2((int)(hit.X / tileDim), (int)(hit.Z / tileDim));
    }

    public FloorGroup TileGroupAt(Int2 tilePos)
    {
        return TileGroupAt(tilePos.X, tilePos.Y);
    }

    public FloorGroup TileGroupAt(int pos_x, int pos_y)
    {
        var index = TileIndex(pos_x, pos_y);
        if (index < 0)
            return FloorGroup.None;

        return mapData[TileIndex(pos_x, pos_y)].group;
    }

    public (FloorGroup, FloorType) TileAt(Int2 tilePos)
    {
        return TileAt(tilePos.X, tilePos.Y);
    }

    public (FloorGroup, FloorType) TileAt(int pos_x, int pos_y)
    {
        var index = TileIndex(pos_x, pos_y);
        if (index < 0)
            return (FloorGroup.None, FloorType.FullTile);

        return mapData[TileIndex(pos_x, pos_y)];
    }

    public ItemType ItemTypeAt(Int2 tilePos)
    {
        return ItemTypeAt(tilePos.X, tilePos.Y);
    }

    public ItemType ItemTypeAt(int pos_x, int pos_y)
    {
        var index = TileIndex(pos_x, pos_y);
        if (index < 0)
            return ItemType.None;
        return itemMap[index].mtype;
    }

    public void PlaceTileSpan(Int2 posA, Int2 posB, FloorGroup group, bool flipped = false)
    {
        InnerPlaceTileSpan(posA, posB, group, flipped, false, false);
    }

    public void ShowTemporaryTile(Int2 tilePos, FloorGroup group)
    {
        InnerPlaceTileSpan(tilePos, tilePos, group, false, true, false);
    }

    // Shows one or multiple temporary tiles to demonstrate the effect of placing something on the map.
    // Nothing is shown if the starting position is invalid.
    public void ShowTemporaryTileSpan(Int2 posA, Int2 posB, FloorGroup group, bool flipped, bool forceUpdate)
    {
        InnerPlaceTileSpan(posA, posB, group, flipped, true, forceUpdate);
    }

    public void ShowTemporaryModel(Int2 tilePos, ItemType mtype)
    {
        if (tempPosition.A == tilePos && tempItemType == mtype)
            return;

        tempPosition.A = tilePos;
        tempItemType = mtype;

        HideTemporaryModels();

        if (TileGroupAt(tilePos) != FloorGroup.Grass || ItemTypeAt(tilePos) != ItemType.None)
            return;

        StaticModel smodel;
        if (!itemModels.TryGetValue(mtype, out smodel))
        {
            smodel = new StaticModel
            {
                Model = treeModel
            };
            smodel.SetMaterial(0, plantPlacementMaterial);
            itemModels[mtype] = smodel;
            smodel.SetParent(Actor, false);
        }
        var tileDim = tileGenerator.TileDimension;
        smodel.Position = new(tilePos.X * tileDim, 0.0, tilePos.Y * tileDim);
        smodel.IsActive = true;
        tempItem = smodel;
    }

    public void PlaceModel(Int2 tilePos, ItemType mtype)
    {
        var group = TileGroupAt(tilePos);
        if (group != FloorGroup.Grass)
            return;

        if (ItemTypeAt(tilePos) != ItemType.None)
            return;

        HideTemporaryModels();

        var tileDim = tileGenerator.TileDimension;
        var smodel = Actor.AddChild<StaticModel>();
        smodel.Model = treeModel;
        smodel.Position = new(tilePos.X * tileDim, 0.0, tilePos.Y * tileDim);
        var index = TileIndex(tilePos);
        itemMap[index].mtype = mtype;
        itemMap[index].origin = tilePos;
        itemMap[index].actor = smodel;
    }

    public void ShowDemolishObjects(Int2 posA, Int2 posB)
    {
        HideTemporaryModels();

        var index = TileIndex(posA);

        selectionMode = SelectionMode.Demolish;
        
        if (index < 0 /*|| (itemMap[index].mtype == ItemType.None && (mapData[index].group == FloorGroup.None || mapData[index].group == FloorGroup.Grass))*/)
        {
            DeselectAll();
            return;
        }
        
        HashSet<ModelInstanceActor> newSelection = [];
        for (int ix = Mathf.Min(posA.X, posB.X), siz = Mathf.Max(posA.X, posB.X) + 1; ix < siz; ++ix)
        {
            if (ix < 0 || ix >= MapSize.X)
                continue;
            
            for (int iy = Mathf.Min(posA.Y, posB.Y), ysiz = Mathf.Max(posA.Y, posB.Y) + 1; iy < ysiz; ++iy)
            {
                if (iy < 0)
                    continue;
                if (iy >= MapSize.Y)
                    break;
                
                index = TileIndex(ix, iy);

                if (itemMap[index].mtype != ItemType.None)
                    newSelection.Add(itemMap[index].actor);
                else
                    newSelection.Add(mapMeshes[index]);
            }
        }
        UpdateSelectionModels(newSelection);
    }

    public void DeselectAll()
    {
        if (selectedModels.Count == 0)
            return;
        UpdateSelectionModels(null);
    }

    private void UpdateSelectionModels(HashSet<ModelInstanceActor> models)
    {
        var deselect = models == null ? selectedModels : selectedModels.Except(models);
        var newselect = models?.Except(selectedModels);

        foreach (var actor in deselect)
        {
            for (int ix = 0, siz = actor.MaterialSlots.Length; ix < siz; ++ix)
            {
                actor.SetMaterial(ix, deselectionMaterials[actor.GetMaterial(ix) as MaterialInstance]);
            }
        }        

        if (newselect != null && newselect.Any())
        {
            Color color;
            switch(selectionMode)
            {
                case SelectionMode.Demolish:
                    color = destructColor;
                    break;
                default:
                    color = Color.Black;
                    break;
            }

            foreach (var actor in newselect)
            {
                for (int ix = 0, siz = actor.MaterialSlots.Length; ix < siz; ++ix)
                {
                    //actor.SetMaterial(0, null);
                    var mat = actor.GetMaterial(ix);
                    
                    MaterialInstance selmat;
                    if (!selectionMaterials.TryGetValue(mat, out selmat))
                    {
                        selmat = mat.CreateVirtualInstance();
                        selmat.SetParameterValue("EmissiveColor", color);
                        selectionMaterials.Add(mat, selmat);
                        deselectionMaterials.Add(selmat, mat);
                    }
                    actor.SetMaterial(ix, selmat);
                }
            }
        }
        selectedModels = models ?? ([]);
    }

    private void InnerPlaceTileSpan(Int2 posA, Int2 posB, FloorGroup group, bool flipped, bool temp, bool forceUpdate)
    {
         if (temp)
        {
            if (!forceUpdate && tempPosition == (posA, posB) && tempGroup == group)
                return;

            tempPosition = (posA, posB);
            tempGroup = group;
        }

        HideTemporaryModels();

        (int indexA, int indexB) = (TileIndex(posA), TileIndex(posB));
        bool invalidIndex = indexA < 0 || indexB < 0;

        if (invalidIndex || mapData[indexA].group == group || itemMap[indexA].mtype != ItemType.None)
            return;

        CreateTemporaryMap(posA, posB);
        
        if ((Mathf.Abs(posA.X - posB.X) >= Mathf.Abs(posB.Y - posA.Y )) ^ flipped)
        {
            for (int dif = posA.X < posB.X ? 1 : -1, x = posA.X, end = posB.X + dif; x != end; x += dif)
                if (TileGroupAt(x, posA.Y) != group && ItemTypeAt(x, posA.Y) == ItemType.None)
                    PlotOnTemporaryMap(x, posA.Y);
            for (int dif = posA.Y < posB.Y ? 1 : -1, y = posA.Y, end = posB.Y + dif; y != end; y += dif)
                if (TileGroupAt(posB.X, y) != group && ItemTypeAt(posB.X, y) == ItemType.None)
                    PlotOnTemporaryMap(posB.X, y);
        }
        else
        {
            for (int dif = posA.Y < posB.Y ? 1 : -1, y = posA.Y, end = posB.Y + dif; y != end; y += dif)
                if (TileGroupAt(posA.X, y) != group && ItemTypeAt(posA.X, y) == ItemType.None)
                    PlotOnTemporaryMap(posA.X, y);
            for (int dif = posA.X < posB.X ? 1 : -1, x = posA.X, end = posB.X + dif; x != end; x += dif)
                if (TileGroupAt(x, posB.Y) != group && ItemTypeAt(x, posB.Y) == ItemType.None)
                    PlotOnTemporaryMap(x, posB.Y);
        }

        for (int ix = 0, siz = tempMapSize.X * tempMapSize.Y; ix < siz; ++ix)
        {
            Int2 tempPos = new(ix % tempMapSize.X, ix / tempMapSize.X);
            Int2 pos = tempPos + tempMapOrigin;
            if (tempMap[ix] == 0 || pos.X < 0 || pos.Y < 0 || pos.X >= MapSize.X || pos.Y >= MapSize.Y)
                continue;

            if (tempMap[ix] == 2 || TileGroupAt(pos) == group)
            {
                FloorType ftype = FloorTypeForSides(TileSidesForPosition(pos, group) | TempTileSidesForPosition(tempPos, group));
                if (TileAt(pos) != (group, ftype))
                {
                    if (temp)
                        CreateTemporaryTile(pos, group, ftype, tempMap[ix] == 2);
                    else
                        SetTile(pos, group, ftype);
                }
            }
        }

        DestroyTemporaryMap();
    }

    private void CreateTemporaryMap(Int2 posA, Int2 posB)
    {
        tempMapOrigin = new Int2(Mathf.Min(posA.X, posB.X) - 1, Mathf.Min(posA.Y, posB.Y) - 1);
        tempMapSize = new Int2(Mathf.Abs(posA.X - posB.X) + 3, Mathf.Abs(posA.Y - posB.Y) + 3);
        tempMap = new int[tempMapSize.X * tempMapSize.Y];
    }

    private void PlotOnTemporaryMap(int posX, int posY)
    {
        // No error checks. We assume the parameters always make sense.

        posX -= tempMapOrigin.X;
        posY -= tempMapOrigin.Y;
        tempMap[posX + posY * tempMapSize.X] = 2;

        tempMap[posX + posY * tempMapSize.X - 1] = Mathf.Max(1, tempMap[posX + posY * tempMapSize.X - 1]);
        tempMap[posX + posY * tempMapSize.X + 1] = Mathf.Max(1, tempMap[posX + posY * tempMapSize.X + 1]);
        tempMap[posX + (posY - 1) * tempMapSize.X] = Mathf.Max(1, tempMap[posX + (posY - 1) * tempMapSize.X]);
        tempMap[posX + (posY + 1) * tempMapSize.X] = Mathf.Max(1, tempMap[posX + (posY + 1) * tempMapSize.X]);

        tempMap[posX + (posY - 1) * tempMapSize.X + 1] = Mathf.Max(1, tempMap[posX + (posY - 1) * tempMapSize.X + 1]);
        tempMap[posX + (posY - 1) * tempMapSize.X - 1] = Mathf.Max(1, tempMap[posX + (posY - 1) * tempMapSize.X - 1]);
        tempMap[posX + (posY + 1) * tempMapSize.X + 1] = Mathf.Max(1, tempMap[posX + (posY + 1) * tempMapSize.X + 1]);
        tempMap[posX + (posY + 1) * tempMapSize.X - 1] = Mathf.Max(1, tempMap[posX + (posY + 1) * tempMapSize.X - 1]);

    }

    private void DestroyTemporaryMap()
    {
        tempMap = null;
    }

    public void HideTemporaryModels()
    {
        tempTiles.ForEach(sm => Destroy(sm));
        tempTiles = [];

        tempItemType = ItemType.None;
        if (tempItem != null)
            tempItem.IsActive = false;

        tempPosition = new(new(-1,-1), new(-1,-1));
    }

    // Creates the initial world with only grass tiles in MapSize grid dimensions.
    private void GenerateMap()
    {
        var tileDim = tileGenerator.TileDimension;
        var grassTile = tileGenerator.GetModel(FloorGroup.Grass, FloorType.FullTile);

        mapData = new FloorData[MapSize.X * MapSize.Y];
        //mapMeshIds = new Guid[MapSize.X * MapSize.Y];
        mapMeshes = new StaticModel[MapSize.X * MapSize.Y];

        itemMap = new ItemMapData[MapSize.X * MapSize.Y];

        for (int ix = 0, siz = MapSize.X * MapSize.Y; ix < siz; ++ix)
        {
            var tile = CreateTile(new Vector3(ix % MapSize.X * tileDim, 0.0, ix / MapSize.X * tileDim), grassTile);
            mapData[ix] = (FloorGroup.Grass, FloorType.FullTile);
            //mapMeshIds[ix] = tile.ID;
            mapMeshes[ix] = tile;
        }

        // Build the outer side to the park from generated tile data instead of models.
        const int extraHeight = 4;
        const int skipHeight = 5;
        const int endingHeight = 3;
        (FloorGroup, FloorType)[] groundData = new (FloorGroup, FloorType)[MapSize.X * (extraHeight + skipHeight + endingHeight)];

        for (int ix = 0, siz = MapSize.X * endingHeight; ix < siz; ++ix)
            groundData[ix] = (FloorGroup.Grass, FloorType.FullTile);

        for (int ix = MapSize.X * (endingHeight + skipHeight), siz = MapSize.X * (endingHeight + skipHeight + extraHeight); ix < siz; ++ix)
            groundData[ix] = (FloorGroup.Grass, FloorType.FullTile);
        foreach (int posX in EntryTiles)
        {
            for (int ix = endingHeight + skipHeight, siz = endingHeight + skipHeight + extraHeight; ix < siz; ++ix)
                groundData[MapSize.X * ix + posX] = (FloorGroup.WalkwayOnGrass, FloorTypeForSides(TileSide.Top | TileSide.Bottom));
        }
        var model = tileGenerator.CreateModel(groundData, MapSize.X, extraHeight + skipHeight + endingHeight);
        
        var ground = Actor.AddChild<StaticModel>();
        ground.Model = model;
        ground.Position = new Vector3(0.0, 0.0, -tileDim * (extraHeight + skipHeight + endingHeight));
        ground.SetMaterial(0, TileMaterial);

        if (EntryTiles.Length > 0)
        {
            var wall1 = tileGenerator.CreateRowOfModel(wallModel, EntryTiles[0] - 1, 1, tileDim, 0.0f);
            if (wall1 != null)
            {
                var wallModel = Actor.AddChild<StaticModel>();
                wallModel.Model = wall1;
                wallModel.Position = new Vector3(0.0, 0.0, -tileDim);
            }
            var trees1 = tileGenerator.CreateRowOfModel(treeModel, EntryTiles[0] - 1, 1, tileDim, 0.0f);
            if (trees1 != null)
            {
                var treesModel = Actor.AddChild<StaticModel>();
                treesModel.Model = trees1;
                treesModel.Position = new Vector3(0.0, 0.0, -tileDim * 2.0);
            }

            int from = EntryTiles.Last() + 2;
            var wall2 = tileGenerator.CreateRowOfModel(wallModel, MapSize.X - from, 1, tileDim, 0.0f);
            if (wall2 != null)
            {
                var wallModel = Actor.AddChild<StaticModel>();
                wallModel.Model = wall2;
                wallModel.Position = new Vector3(from * tileDim, 0.0, -tileDim);
            }

            var trees2 = tileGenerator.CreateRowOfModel(treeModel, MapSize.X - from, 1, tileDim, 0.0f);
            if (trees2 != null)
            {
                var treesModel = Actor.AddChild<StaticModel>();
                treesModel.Model = trees2;
                treesModel.Position = new Vector3(from * tileDim, 0.0, -tileDim * 2.0);
            }
        }
        var entrance = Actor.AddChild<StaticModel>();
        entrance.Model = entranceModel;
        entrance.Position = new Vector3(MapSize.X * 0.5 * tileDim, 0.0, -tileDim);

        // Two lanes of sidewalk.
        var sidewalk = tileGenerator.CreateRowOfModel(sidewalkModel, MapSize.X, 2, tileDim, tileDim);
        if (sidewalk != null)
        {
            var sidewalkActor = Actor.AddChild<StaticModel>();
            sidewalkActor.Model = sidewalk;
            sidewalkActor.Position = new Vector3(0.0, 0.0, -tileDim * 6.0);
        }

        var laneModelIndexes = new int[MapSize.X];
        var laneModelPositions = new Vector3[MapSize.X];
        for (int ix = 0, siz = MapSize.X; ix < siz; ++ix)
        {
            laneModelIndexes[ix] = ix % busLaneModels.Length;
            laneModelPositions[ix] = new Vector3(tileDim * ix, 0.0, 0.0);
        }
        var busLane = tileGenerator.CreateCompoundModel(busLaneModels, laneModelIndexes, laneModelPositions);
        if (busLane != null)
        {
            var busLaneActor = Actor.AddChild<StaticModel>();
            busLaneActor.Model = busLane;
            busLaneActor.Position = new Vector3(0.0, 0.0, -tileDim * 6.0);
        }

    }

    private int TileIndex(Int2 tilePos)
    {
        return TileIndex(tilePos.X, tilePos.Y);
    }

    private int TileIndex(int pos_x, int pos_y)
    {
        if (pos_x < 0 || pos_y < 0 || pos_x >= MapSize.X || pos_y >= MapSize.Y)
            return -1;

        return pos_y * MapSize.X + pos_x;
    }

    private Vector3 TileCoords(Int2 tilePos)
    {
        return TileCoords(tilePos.X, tilePos.Y);
    }

    private Vector3 TileCoords(int pos_x, int pos_y)
    {
        var tileDim = tileGenerator.TileDimension;
        return new Vector3(pos_x * tileDim, 0.0, pos_y * tileDim);
    }

    private TileSide TileSidesForPosition(Int2 tilePos, FloorGroup group)
    {
        return TileSidesForPosition(tilePos.X, tilePos.Y, group);
    }

    private TileSide TileSidesForPosition(int pos_x, int pos_y, FloorGroup group)
    {
        if (pos_x < 0 || pos_y < 0 || pos_x >= MapSize.X || pos_y >= MapSize.Y)
            throw new Exception();

        TileSide result = TileSide.None;
        if (pos_x > 0)
        {
            if (pos_y > 0 && mapData[TileIndex(pos_x - 1, pos_y - 1)].group == group)
                result |= TileSide.BottomLeft;
            if (pos_y < MapSize.Y - 1 && mapData[TileIndex(pos_x - 1, pos_y + 1)].group == group)
                result |= TileSide.TopLeft;
            if (mapData[TileIndex(pos_x - 1, pos_y)].group == group)
                result |= TileSide.Left;
        }

        if (pos_x < MapSize.X - 1)
        {
            if (pos_y > 0 && mapData[TileIndex(pos_x + 1, pos_y - 1)].group == group)
                result |= TileSide.BottomRight;
            if (pos_y < MapSize.Y - 1 && mapData[TileIndex(pos_x + 1, pos_y + 1)].group == group)
                result |= TileSide.TopRight;
            if (mapData[TileIndex(pos_x + 1, pos_y)].group == group)
                result |= TileSide.Right;
        }
        if (pos_y > 0 && mapData[TileIndex(pos_x, pos_y - 1)].group == group)
            result |= TileSide.Bottom;
        if (pos_y < MapSize.Y - 1 && mapData[TileIndex(pos_x, pos_y + 1)].group == group)
            result |= TileSide.Top;
        
        if (pos_y == 0)
        {
            foreach (int i in EntryTiles)
                if (pos_x == i)
                    result |= TileSide.Bottom;
        }

        return result;
    }

    private TileSide TempTileSidesForPosition(Int2 pos, FloorGroup group)
    {
        if (tempMap == null || pos.X < 0 || pos.Y < 0 || pos.X >= tempMapSize.X || pos.Y >= tempMapSize.Y)
            return TileSide.None;

        TileSide result = TileSide.None;
        int tempPos = pos.X + pos.Y * tempMapSize.X;
        if (pos.X > 0)
        {
            if (pos.Y > 0 && tempMap[tempPos - 1 - tempMapSize.X] == 2) // -1, -1
                result |= TileSide.BottomLeft;
            if (pos.Y < tempMapSize.Y - 1 && tempMap[tempPos - 1 + tempMapSize.X] == 2) // -1, +1
                result |= TileSide.TopLeft;
            if (tempMap[tempPos - 1] == 2) // -1, 0
                result |= TileSide.Left;
        }

        if (pos.X < tempMapSize.X - 1)
        {
            if (pos.Y > 0 && tempMap[tempPos + 1 - tempMapSize.X] == 2) // +1, -1
                result |= TileSide.BottomRight;
            if (pos.Y < tempMapSize.Y - 1 && tempMap[tempPos + 1 + tempMapSize.X] == 2) // +1, +1
                result |= TileSide.TopRight;
            if (tempMap[tempPos + 1] == 2) // +1, 0
                result |= TileSide.Right;
        }
        if (pos.Y > 0 && tempMap[tempPos - tempMapSize.X ] == 2) // 0, -1
            result |= TileSide.Bottom;
        if (pos.Y < tempMapSize.Y - 1 && tempMap[tempPos + tempMapSize.X] == 2) // 0, +1
            result |= TileSide.Top;

        return result;
    }

    private void CreateTemporaryTile(Int2 tilePos, FloorGroup group, FloorType ftype, bool placement = false)
    {
        CreateTemporaryTile(tilePos.X, tilePos.Y, group, ftype, placement);
    }

    private void CreateTemporaryTile(int pos_x, int pos_y, FloorGroup group, FloorType ftype, bool placement = false)
    {
        var tileDim = tileGenerator.TileDimension;
        tempTiles.Add(CreateTile(new Vector3(pos_x * tileDim, 0.1, pos_y * tileDim), group, ftype, placement));
    }

    private StaticModel CreateTile(Vector3 world_pos, FloorGroup group, FloorType ftype, bool placement = false)
    {
        return CreateTile(world_pos, tileGenerator.GetModel(group, ftype), placement);
    }

    private StaticModel CreateTile(Vector3 world_pos, Model model, bool placement = false)
    {
        var tile = Actor.AddChild<StaticModel>();
        tile.Model = model;
        tile.Position = world_pos;
        tile.SetMaterial(0, placement ? tilePlacementMaterial : TileMaterial);
        return tile;
    }

    private void SetTile(Int2 tilePos, FloorGroup group, FloorType ftype)
    {
        SetTile(tilePos.X, tilePos.Y, group, ftype);
    }

    private void SetTile(int pos_x, int pos_y, FloorGroup group, FloorType ftype)
    {
        var tileDim = tileGenerator.TileDimension;
        var tile = CreateTile(new Vector3(pos_x * tileDim, 0.0, pos_y * tileDim), group, ftype, false);
        var index = TileIndex(pos_x, pos_y);
        //Destroy(Find<StaticModel>(ref mapMeshIds[index]));
        Destroy(ref mapMeshes[index]);
        mapData[index] = (group, ftype);
        //mapMeshIds[index] = tile.ID;
        mapMeshes[index] = tile;
    }

    private static TileSide CombineSides(TileSide a, TileSide b)
    {
        var badA = a == TileSide.None || a == TileSide.TopLeft || a == TileSide.TopRight || a == TileSide.BottomLeft || a == TileSide.BottomRight;
        var badB = b == TileSide.None || b == TileSide.TopLeft || b == TileSide.TopRight || b == TileSide.BottomLeft || b == TileSide.BottomRight;

        // Bad or opposite sides.
        if (badA && badB || (a == TileSide.Left && b == TileSide.Right) || (b == TileSide.Left && a == TileSide.Right) ||
                (a == TileSide.Top && b == TileSide.Bottom) || (b == TileSide.Top && a == TileSide.Bottom))
            return TileSide.None;

        if (badB || a == b)
            return a;

        if (badA)
            return b;
        
        if (a == TileSide.Left || b == TileSide.Left)
        {
            if (b == TileSide.Top || a == TileSide.Top)
                return TileSide.TopLeft;
            if (b == TileSide.Bottom || a == TileSide.Bottom)
                return TileSide.BottomLeft;
        }

        // One of them is TileSide.Right
        if (b == TileSide.Top || a == TileSide.Top)
            return TileSide.TopRight;
        
        return TileSide.BottomRight;
    }

    private static FloorType FloorTypeForSides(TileSide side)
    {
        return TileGenerator.FloorTypeForSides(side);
    }

    private static TileSide FlipSide(TileSide side)
    {
        if (side == TileSide.Left)
            return TileSide.Right;
        if (side == TileSide.Right)
            return TileSide.Left;
        if (side == TileSide.Top)
            return TileSide.Bottom;
        if (side == TileSide.Bottom)
            return TileSide.Top;
        return TileSide.None;
    }

}
