using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Xml;
using FlaxEditor.Windows;
using FlaxEngine;
using FlaxEngine.GUI;
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
    public TileGenerator tileGenerator;
    // Grid size of the starting map
    public Int2 MapSize = new(16, 16);
    // Material to assign to created tiles
    public MaterialBase TileMaterial;
    // Material to assign to tiles for the planning phase.
    public MaterialBase PlacementMaterial;
    // Grid X coordinates at the bottom of the map where the park can connect
    // to the outside world.
    public int[] EntryTiles = [];
    
    // Group and floor type pairing for each map cell position.
    private FloorData[] mapData;
    // ID of meshes placed at each map cell position.
    private Guid[] mapMeshIds;

    private (Int2 A, Int2 B) tempPosition;
    private FloorGroup tempGroup;
    private List<StaticModel> tempTiles = [];

    private Int2 tempMapOrigin;
    private Int2 tempMapSize;
    private int[] tempMap;

    /// <inheritdoc/>
    public override void OnStart()
    {
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

    // Shows or hides a temporary tile to demonstrate the effect of placing something on the map.
    public void ShowTemporaryTile(Int2 tilePos, FloorGroup group)
    {
        if (tempPosition != (tilePos, tilePos) || tempGroup != group)
            HideTemporaryTiles();

        tempPosition = (tilePos, tilePos);
        tempGroup = group;

        var ix = TileIndex(tilePos);
        if (ix < 0 || mapData[ix].group == group)
            return;
        
        CreateTemporaryTile(tilePos, group, FloorTypeForSides(TileSidesForPosition(tilePos, group)), true);

        if (TileGroupAt(tilePos.X - 1, tilePos.Y) == group)
            CreateTemporaryTile(tilePos.X - 1, tilePos.Y, group, FloorTypeForSides(TileSidesForPosition(tilePos.X - 1, tilePos.Y, group) | TileSide.Right));
        if (TileGroupAt(tilePos.X, tilePos.Y - 1) == group)
            CreateTemporaryTile(tilePos.X, tilePos.Y - 1, group, FloorTypeForSides(TileSidesForPosition(tilePos.X, tilePos.Y - 1, group) | TileSide.Top));
        if (TileGroupAt(tilePos.X + 1, tilePos.Y) == group)
            CreateTemporaryTile(tilePos.X + 1, tilePos.Y, group, FloorTypeForSides(TileSidesForPosition(tilePos.X + 1, tilePos.Y, group) | TileSide.Left));
        if (TileGroupAt(tilePos.X, tilePos.Y + 1) == group)
            CreateTemporaryTile(tilePos.X, tilePos.Y + 1, group, FloorTypeForSides(TileSidesForPosition(tilePos.X, tilePos.Y + 1, group) | TileSide.Bottom));

        if (TileGroupAt(tilePos.X - 1, tilePos.Y - 1) == group)
            CreateTemporaryTile(tilePos.X - 1, tilePos.Y - 1, group, FloorTypeForSides(TileSidesForPosition(tilePos.X - 1, tilePos.Y - 1, group) | TileSide.TopRight));
        if (TileGroupAt(tilePos.X + 1, tilePos.Y - 1) == group)
            CreateTemporaryTile(tilePos.X + 1, tilePos.Y - 1, group, FloorTypeForSides(TileSidesForPosition(tilePos.X + 1, tilePos.Y - 1, group) | TileSide.TopLeft));
        if (TileGroupAt(tilePos.X + 1, tilePos.Y + 1) == group)
            CreateTemporaryTile(tilePos.X + 1, tilePos.Y + 1, group, FloorTypeForSides(TileSidesForPosition(tilePos.X + 1, tilePos.Y + 1, group) | TileSide.BottomLeft));
        if (TileGroupAt(tilePos.X - 1, tilePos.Y + 1) == group)
            CreateTemporaryTile(tilePos.X - 1, tilePos.Y + 1, group, FloorTypeForSides(TileSidesForPosition(tilePos.X - 1, tilePos.Y + 1, group) | TileSide.BottomRight));
    }

    public void PlaceTileSpan(Int2 posA, Int2 posB, FloorGroup group, bool flipped = false)
    {
        InnerPlaceTileSpan(posA, posB, group, flipped, false, false);
    }

    public void ShowTemporaryTileSpan(Int2 posA, Int2 posB, FloorGroup group, bool flipped, bool forceUpdate)
    {
        InnerPlaceTileSpan(posA, posB, group, flipped, true, forceUpdate);
    }

    private void InnerPlaceTileSpan(Int2 posA, Int2 posB, FloorGroup group, bool flipped, bool temp, bool forceUpdate)
    {
        if (posA == posB)
        {
            if (temp)
                ShowTemporaryTile(posA, group);
            else
                PlaceTile(posA, group);
            return;
        }

        bool invalidIndex = TileIndex(posA) < 0 || TileIndex(posB) < 0;

        if (invalidIndex || !temp || tempPosition != (posA, posB) || tempGroup != group || forceUpdate)
            HideTemporaryTiles();

        tempPosition = (posA, posB);
        tempGroup = group;

        if (invalidIndex || (!forceUpdate && mapData[TileIndex(posA)].group == group))
            return;

        CreateTemporaryMap(posA, posB);
        
        if ((Mathf.Abs(posA.X - posB.X) >= Mathf.Abs(posB.Y - posA.Y )) ^ flipped)
        {
            for (int dif = posA.X < posB.X ? 1 : -1, x = posA.X, end = posB.X + dif; x != end; x += dif)
                if (TileGroupAt(x, posA.Y) != group)
                    PlotOnTemporaryMap(x, posA.Y);
            for (int dif = posA.Y < posB.Y ? 1 : -1, y = posA.Y, end = posB.Y + dif; y != end; y += dif)
                if (TileGroupAt(posB.X, y) != group)
                    PlotOnTemporaryMap(posB.X, y);
        }
        else
        {
            for (int dif = posA.Y < posB.Y ? 1 : -1, y = posA.Y, end = posB.Y + dif; y != end; y += dif)
                if (TileGroupAt(posA.X, y) != group)
                    PlotOnTemporaryMap(posA.X, y);
            for (int dif = posA.X < posB.X ? 1 : -1, x = posA.X, end = posB.X + dif; x != end; x += dif)
                if (TileGroupAt(x, posB.Y) != group)
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

        posX = posX - tempMapOrigin.X;
        posY = posY - tempMapOrigin.Y;
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

    public void HideTemporaryTiles()
    {
        tempTiles.ForEach(sm => Destroy(sm));
        tempTiles = [];
    }


    public void PlaceTile(Int2 tilePos, FloorGroup group)
    {
        var ix = TileIndex(tilePos);
        if (ix < 0 || mapData[ix].group == group)
        {
            Debug.Log("PlaceTile refused");
            return;
        }
            Debug.Log("PlaceTile accepted");

        HideTemporaryTiles();

        SetTile(tilePos, group, FloorTypeForSides(TileSidesForPosition(tilePos, group)));
        if (TileGroupAt(tilePos.X - 1, tilePos.Y) == group)
            SetTile(tilePos.X - 1, tilePos.Y, group, FloorTypeForSides(TileSidesForPosition(tilePos.X - 1, tilePos.Y, group)));
        if (TileGroupAt(tilePos.X, tilePos.Y - 1) == group)
            SetTile(tilePos.X, tilePos.Y - 1, group, FloorTypeForSides(TileSidesForPosition(tilePos.X, tilePos.Y - 1, group)));
        if (TileGroupAt(tilePos.X + 1, tilePos.Y) == group)
            SetTile(tilePos.X + 1, tilePos.Y, group, FloorTypeForSides(TileSidesForPosition(tilePos.X + 1, tilePos.Y, group)));
        if (TileGroupAt(tilePos.X, tilePos.Y + 1) == group)
            SetTile(tilePos.X, tilePos.Y + 1, group, FloorTypeForSides(TileSidesForPosition(tilePos.X, tilePos.Y + 1, group)));

        if (TileGroupAt(tilePos.X - 1, tilePos.Y - 1) == group)
            SetTile(tilePos.X - 1, tilePos.Y - 1, group, FloorTypeForSides(TileSidesForPosition(tilePos.X - 1, tilePos.Y - 1, group)));
        if (TileGroupAt(tilePos.X + 1, tilePos.Y - 1) == group)
            SetTile(tilePos.X + 1, tilePos.Y - 1, group, FloorTypeForSides(TileSidesForPosition(tilePos.X + 1, tilePos.Y - 1, group)));
        if (TileGroupAt(tilePos.X + 1, tilePos.Y + 1) == group)
            SetTile(tilePos.X + 1, tilePos.Y + 1, group, FloorTypeForSides(TileSidesForPosition(tilePos.X + 1, tilePos.Y + 1, group)));
        if (TileGroupAt(tilePos.X - 1, tilePos.Y + 1) == group)
            SetTile(tilePos.X - 1, tilePos.Y + 1, group, FloorTypeForSides(TileSidesForPosition(tilePos.X - 1, tilePos.Y + 1, group)));

    }
    
    // Creates the initial world with only grass tiles in MapSize grid dimensions.
    private void GenerateMap()
    {
        var tileDim = tileGenerator.TileDimension;
        var grassTile = tileGenerator.GetModel(FloorGroup.Grass, FloorType.FullTile);

        mapData = new FloorData[MapSize.X * MapSize.Y];
        mapMeshIds = new Guid[MapSize.X * MapSize.Y];

        for (int ix = 0, siz = MapSize.X * MapSize.Y; ix < siz; ++ix)
        {
            var tile = CreateTile(new Vector3(ix % MapSize.X * tileDim, 0.0, ix / MapSize.X * tileDim), grassTile);
            mapData[ix] = (FloorGroup.Grass, FloorType.FullTile);
            mapMeshIds[ix] = tile.ID;
        }

        // Build the outer side to the park from generated tile data instead of models.
        (FloorGroup, FloorType)[] groundData = new (FloorGroup, FloorType)[MapSize.X * 2];
        for (int ix = 0, siz = MapSize.X * 2; ix < siz; ++ix)
            groundData[ix] = (FloorGroup.Grass, FloorType.FullTile);

        foreach (int posX in EntryTiles)
        {
            groundData[MapSize.X * 0 + posX] = (FloorGroup.WalkwayOnGrass, FloorTypeForSides(TileSide.Top | TileSide.Bottom));
            groundData[MapSize.X * 1 + posX] = (FloorGroup.WalkwayOnGrass, FloorTypeForSides(TileSide.Top | TileSide.Bottom));
        }
        var model = tileGenerator.CreateModel(groundData, MapSize.X, 2);
        
        var ground = Actor.AddChild<StaticModel>();
        ground.Model = model;
        ground.Position = new Vector3(0.0, 0.0, -tileDim * 2.0f);
        ground.SetMaterial(0, TileMaterial);
        

        // Float3[] grassV;
        // int[] grassIx;
        // Float2[] grassUV;
        // Float3[] grassN;
        // tileGenerator.GetInstanceData(FloorGroup.Grass, FloorType.FullTile, out grassV, out grassIx, out grassUV, out grassN);
        // Float3[] vroadV;
        // int[] vroadIx;
        // Float2[] vroadUV;
        // Float3[] vroadN;
        // tileGenerator.GetInstanceData(FloorGroup.Grass, FloorType.FullTile, out vroadV, out vroadIx, out vroadUV, out vroadN);
        

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
        tile.SetMaterial(0, placement ? PlacementMaterial : TileMaterial);
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
        Destroy(Find<StaticModel>(ref mapMeshIds[index]));
        mapData[index] = (group, ftype);
        mapMeshIds[index] = tile.ID;
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
