using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using FlaxEngine;
using FlaxEngine.Assertions;
using FlaxEngine.Utilities;

namespace Scripts;

/// <summary>
/// TileGenerator Script.
/// </summary>
public class TileGenerator : Script
{

    public enum FloorGroup {
        None,
        Grass,
        WalkwayOnGrass
    }

    public enum TexType {
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
    }

    public enum FloorType {
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
    }

    public enum TileSide {
        None = 0,
        Left = 1,
        Top = 2,
        Right = 4,
        Bottom = 8,
        TopLeft = 16,
        TopRight = 32,
        BottomLeft = 64,
        BottomRight = 128
    }

    private enum TileOp {
        Add,
        Sub
    }

    private static readonly Dictionary<FloorGroup, Dictionary<TexType, Rectangle> > FloorUVData = new() {
        { FloorGroup.Grass, new Dictionary<TexType, Rectangle>() { { TexType.FullTile, new Rectangle(2, 2, 64, 64) }  } },
        { FloorGroup.WalkwayOnGrass, new Dictionary<TexType, Rectangle>() {
            { TexType.FullTile, new Rectangle(70, 2, 64, 64) },
            { TexType.InCornerTopLeft, new Rectangle(221, 75, 9, 9) },
            { TexType.InCornerTopRight, new Rectangle(212, 75, 9, 9) },
            { TexType.InCornerBottomLeft, new Rectangle(221, 2, 9, 9) },
            { TexType.InCornerBottomRight, new Rectangle(212, 2, 9, 9) },
            { TexType.OutCornerTopLeft, new Rectangle(138, 2, 35, 35) },
            { TexType.OutCornerTopRight, new Rectangle(173, 2, 35, 35) },
            { TexType.OutCornerBottomLeft, new Rectangle(138, 37, 35, 35) },
            { TexType.OutCornerBottomRight, new Rectangle(173, 37, 35, 35) },
            { TexType.OutSharpCornerTopLeft, new Rectangle(70, 70, 9, 9) },
            { TexType.OutSharpCornerTopRight, new Rectangle(83, 70, 9, 9) },
            { TexType.OutSharpCornerBottomLeft, new Rectangle(70, 83, 9, 9) },
            { TexType.OutSharpCornerBottomRight, new Rectangle(83, 83, 9, 9) },
            { TexType.OutEdgeLeft, new Rectangle(221, 11, 9, 64) },
            { TexType.OutEdgeRight, new Rectangle(212, 11, 9, 64) },
            { TexType.OutEdgeTop, new Rectangle(2, 79, 64, 9) },
            { TexType.OutEdgeBottom, new Rectangle(2, 70, 64, 9) }
        }}
    };

    private static readonly Dictionary<FloorGroup, FloorType[] > FloorGenData = new() {
        { FloorGroup.Grass, [ FloorType.FullTile ] },
        { FloorGroup.WalkwayOnGrass, [
            FloorType.FullTile,
            FloorType.HorzLane,
            FloorType.VertLane,
            FloorType.EdgeLeft,
            FloorType.EdgeTop,
            FloorType.EdgeRight,
            FloorType.EdgeBottom,
            FloorType.TurnTopRight,
            FloorType.TurnTopLeft,
            FloorType.TurnBottomRight,
            FloorType.TurnBottomLeft,
            FloorType.EdgeBottomLeft,
            FloorType.EdgeBottomRight,
            FloorType.EdgeTopLeft,
            FloorType.EdgeTopRight,
            FloorType.VertCrossRight,
            FloorType.VertCrossLeft,
            FloorType.HorzCrossTop,
            FloorType.HorzCrossBottom,
            FloorType.EdgeLeftCornerTopRight,
            FloorType.EdgeLeftCornerBottomRight,
            FloorType.EdgeRightCornerTopLeft,
            FloorType.EdgeRightCornerBottomLeft,
            FloorType.EdgeTopCornerBottomRight,
            FloorType.EdgeTopCornerBottomLeft,
            FloorType.EdgeBottomCornerTopRight,
            FloorType.EdgeBottomCornerTopLeft,
            FloorType.CornerExceptTopLeft,
            FloorType.CornerExceptTopRight,
            FloorType.CornerExceptBottomLeft,
            FloorType.CornerExceptBottomRight,
            FloorType.CornerAll,
            FloorType.CornerBothTop,
            FloorType.CornerBothRight,
            FloorType.CornerBothBottom,
            FloorType.CornerBothLeft,
            FloorType.CornerTopLeftBottomRight,
            FloorType.CornerTopRightBottomLeft,
            FloorType.OnlyCornerTopRight,
            FloorType.OnlyCornerTopLeft,
            FloorType.OnlyCornerBottomRight,
            FloorType.OnlyCornerBottomLeft,
            FloorType.IsolatedTile,
            FloorType.DeadendLeft,
            FloorType.DeadendRight,
            FloorType.DeadendTop,
            FloorType.DeadendBottom
        ] }
    };


    private readonly struct TileRectData
    {
        public readonly List<Float3> verts;
        public readonly List<Float2> uvs;
        public readonly List<int> indexes;

        public TileRectData()
        {
            verts = [];
            uvs = [];
            //indexes = [0, 1, 2, 1, 3, 2];
            indexes = [];
        }
    }

    private struct TileDataCache
    {
        public Float3[] verts;
        public Float2[] uvs;
        public int[] indexes;
        public Float3[] normals;
    }


    // Temporary struct to pass in a pair of rect data and alignment, optionally passing
    // just the data or both as a tuple.
    private readonly struct TileRectDataWithAlign
    {
        public readonly TileRectData rectdata;
        public readonly TileSide align;

        public TileRectDataWithAlign(TileRectData _data)
        {
            rectdata = _data;
            align = TileSide.None;
        }

        public TileRectDataWithAlign(TileRectData _data, TileSide _align)
        {
            rectdata = _data;
            align = _align;
        }

        public static implicit operator TileRectDataWithAlign(TileRectData d) => new(d);
        public static implicit operator TileRectDataWithAlign((TileRectData d, TileSide a) t) => new(t.d, t.a);
    }

    private readonly struct VertModifierWithAlign
    {
        public readonly TileSide initialValue;
        public readonly (TileOp op, int index)[] Xmods;
        public readonly (TileOp op, int index)[] Ymods;

        public VertModifierWithAlign(TileSide initial)
        {
            initialValue = initial;
            Xmods = null;
            Ymods = null;
        }

        public VertModifierWithAlign(TileSide initial, (TileOp, int)[] Xmod, (TileOp, int)[] Ymod)
        {
            initialValue = initial;
            Xmods = Xmod;
            Ymods = Ymod;
        }

        public static implicit operator VertModifierWithAlign(TileSide i) => new(i);
        public static implicit operator VertModifierWithAlign((TileSide i, ((TileOp op, int index)[] v1, (TileOp op, int index)[] v2) m) t) => new(t.i, t.m.v1, t.m.v2);
    }

    public Int2 TextureSize = new();
    public Int2 TileSize = new();
    public float TileDimension = 200.0f;

    private readonly Dictionary<FloorGroup, Dictionary<FloorType, Model>> FloorModels = [];

    /// <inheritdoc/>
    public override void OnAwake()
    {
        BuildInstances();
    }
    
    public Model GetModel(FloorGroup group, FloorType ftype)
    {
        return FloorModels[group][ftype];
    }

    // Returns which floor type is fitting if the surrounding tiles have or not have the same tile group.
    // The tileSides value should be a mix of TileSide bits.
    public static FloorType FloorTypeForSides(TileSide tileSides)
    {
        var allSides = TileSide.Top | TileSide.Right | TileSide.Left | TileSide.Bottom;

        if ((allSides & tileSides) == 0)
            return FloorType.IsolatedTile;
        if ((tileSides & (allSides & ~TileSide.Top)) == 0)
            return FloorType.DeadendBottom; 
        if ((tileSides & (allSides & ~TileSide.Left)) == 0)
            return FloorType.DeadendRight; 
        if ((tileSides & (allSides & ~TileSide.Bottom)) == 0)
            return FloorType.DeadendTop; 
        if ((tileSides & (allSides & ~TileSide.Right)) == 0)
            return FloorType.DeadendLeft; 
        if (tileSides == allSides)
            return FloorType.CornerAll; 

        // Tiles above and below.
        if ((tileSides & (TileSide.Top | TileSide.Bottom)) == (TileSide.Top | TileSide.Bottom))
        {
            if ((tileSides & TileSide.Left) == 0)
            {
                if ((tileSides & TileSide.Right) == 0)
                    return FloorType.VertLane; 
                if ((tileSides & TileSide.TopRight) == 0)
                {
                    if ((tileSides & TileSide.BottomRight) == 0)
                        return FloorType.VertCrossRight; 
                    return FloorType.EdgeLeftCornerTopRight; 
                }
                if ((tileSides & TileSide.BottomRight) == 0)
                    return FloorType.EdgeLeftCornerBottomRight; 
                return FloorType.EdgeLeft; 
            }
            if ((tileSides & TileSide.Right) == 0)
            {
                if ((tileSides & TileSide.TopLeft) == 0)
                {
                    if ((tileSides & TileSide.BottomLeft) == 0)
                        return FloorType.VertCrossLeft; 
                    return FloorType.EdgeRightCornerTopLeft; 
                }
                if ((tileSides & TileSide.BottomLeft) == 0)
                    return FloorType.EdgeRightCornerBottomLeft; 
                return FloorType.EdgeRight; 
            }
            // Road on all sides. Check for diagonals
            if ((tileSides & TileSide.TopLeft) == 0)
            {
                if ((tileSides & TileSide.TopRight) == 0)
                {
                    if ((tileSides & TileSide.BottomLeft) == 0)
                    {
                        if ((tileSides & TileSide.BottomRight) == 0)
                            return FloorType.CornerAll; 
                        return FloorType.CornerExceptBottomRight; 
                    }
                    if ((tileSides & TileSide.BottomRight) == 0)
                        return FloorType.CornerExceptBottomLeft; 
                    return FloorType.CornerBothTop; 
                }
                if ((tileSides & TileSide.BottomLeft) == 0)
                {
                    if ((tileSides & TileSide.BottomRight) == 0)
                        return FloorType.CornerExceptTopRight;
                    return FloorType.CornerBothLeft;
                }
                if ((tileSides & TileSide.BottomRight) == 0)
                    return FloorType.CornerTopLeftBottomRight;
                return FloorType.OnlyCornerTopLeft;
            }
            if ((tileSides & TileSide.TopRight) == 0)
            {
                if ((tileSides & TileSide.BottomLeft) == 0)
                {
                    if ((tileSides & TileSide.BottomRight) == 0)
                        return FloorType.CornerExceptTopLeft;
                    return FloorType.CornerTopRightBottomLeft;
                }
                if ((tileSides & TileSide.BottomRight) == 0)
                    return FloorType.CornerBothRight;
                return FloorType.OnlyCornerTopRight;
            }
            if ((tileSides & TileSide.BottomLeft) == 0)
            {
                if ((tileSides & TileSide.BottomRight) == 0)
                    return FloorType.CornerBothBottom;
                return FloorType.OnlyCornerBottomLeft;
            }
            if ((tileSides & TileSide.BottomRight) == 0)
                return FloorType.OnlyCornerBottomRight;
            return FloorType.FullTile; 
        }
        // Tiles left and right.
        if ((tileSides & (TileSide.Left | TileSide.Right)) == (TileSide.Left | TileSide.Right))
        {
            if ((tileSides & TileSide.Top) == 0)
            {
                if ((tileSides & TileSide.Bottom) == 0)
                    return FloorType.HorzLane; 
                if ((tileSides & TileSide.BottomLeft) == 0)
                {
                    if ((tileSides & TileSide.BottomRight) == 0)
                        return FloorType.HorzCrossBottom; 
                    return FloorType.EdgeTopCornerBottomLeft; 
                }
                if ((tileSides & TileSide.BottomRight) == 0)
                    return FloorType.EdgeTopCornerBottomRight; 
                return FloorType.EdgeTop; 
            }
            if ((tileSides & TileSide.Bottom) == 0)
            {
                if ((tileSides & TileSide.TopLeft) == 0)
                {
                    if ((tileSides & TileSide.TopRight) == 0)
                        return FloorType.HorzCrossTop; 
                    return FloorType.EdgeBottomCornerTopLeft; 
                }
                if ((tileSides & TileSide.TopRight) == 0)
                    return FloorType.EdgeBottomCornerTopRight; 
                return FloorType.EdgeBottom; 
            }
        }
        
        if ((tileSides & TileSide.Left) == 0)
        {
            if ((tileSides & TileSide.Top) == 0)
            {
                if ((tileSides & TileSide.BottomRight) == 0)
                    return FloorType.TurnBottomRight; 
                return FloorType.EdgeTopLeft; 
            }
            if ((tileSides & TileSide.TopRight) == 0)
                return FloorType.TurnTopRight; 
            return FloorType.EdgeBottomLeft; 
        }
        if ((tileSides & TileSide.Top) == 0)
        {
            if ((tileSides & TileSide.BottomLeft) == 0)
                return FloorType.TurnBottomLeft; 
            return FloorType.EdgeTopRight; 
        }

        if ((tileSides & TileSide.TopLeft) == 0)
            return FloorType.TurnTopLeft; 
        return FloorType.EdgeBottomRight; 

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
    //public override void OnUpdate()
    //{
        // Here you can add code that needs to be called every frame
    //}

    public override void OnDestroy()
    {
        foreach (KeyValuePair<FloorGroup, Dictionary<FloorType, Model>> pairs in FloorModels)
        {
            foreach (KeyValuePair<FloorType, Model> modelpairs in pairs.Value)
                Destroy(modelpairs.Value);
        }
    }

    private void BuildInstances()
    {
        foreach (KeyValuePair<FloorGroup, FloorType[]> floorData in FloorGenData)
        {
            var group = floorData.Key;

            var Models = new Dictionary<FloorType, Model>();
            FloorModels.Add(group, Models);

            foreach (FloorType floorType in floorData.Value)            
            {
                GetInstanceData(group, floorType, out Float3[] verts, out int[] indexes, out Float2[] uvs, out Float3[] normals);

                Model new_model = Content.CreateVirtualAsset<Model>();
                new_model.SetupLODs([1]);
                new_model.LODs[0].Meshes[0].UpdateMesh(vertices: verts, triangles: indexes, normals: normals, uv: uvs);

                Models[floorType] = new_model;
            }
        }
    }

    // Creates a single model from multiple tiles as specified in the passed data array. The array should
    // be a continuous array of every tile. The passed width and height is used to determine the placement
    // of the tiles in the generated model.
    public Model CreateModel((FloorGroup group, FloorType ftype)[] data, int width, int height)
    {
        Dictionary<(FloorGroup group, FloorType ftype), TileDataCache> cache = [];

        int vertCount = 0;

        foreach (var pair in data)
        {
            TileDataCache tileData;
            if (!cache.TryGetValue(pair, out tileData))
            {
                tileData = new();
                GetInstanceData(pair.group, pair.ftype, out tileData.verts, out tileData.indexes, out tileData.uvs, out tileData.normals);
                cache[pair] = tileData;
            }
            vertCount += tileData.verts.Length;
        }

        Float3[] verts = new Float3[vertCount];
        Float2[] uvs = new Float2[vertCount];
        int[] indexes = new int[vertCount * 3];
        Float3[] normals = new Float3[vertCount];

        int posX = 0;
        int posY = 0;

        int vertpos = 0;
        int indexpos = 0;

        foreach (var pair in data)
        {
            var tileData = cache[pair];

            tileData.verts.CopyTo(verts, vertpos);
            tileData.uvs.CopyTo(uvs, vertpos);
            tileData.indexes.CopyTo(indexes, indexpos);
            tileData.normals.CopyTo(normals, vertpos);

            for (int ix = 0, siz = tileData.verts.Length; ix < siz; ++ix)
            {
                verts[vertpos + ix].X += TileDimension * posX;
                verts[vertpos + ix].Z += TileDimension * posY;
            }

            for (int ix = 0, siz = tileData.indexes.Length; ix < siz; ++ix)
                indexes[indexpos + ix] += vertpos;

            vertpos += tileData.verts.Length;
            indexpos += tileData.indexes.Length;

            ++posX;
            if (posX >= width)
            {
                posX = 0;
                ++posY;
            }
            if (posY >= height)
                break;
        }

        Model new_model = Content.CreateVirtualAsset<Model>();
        new_model.SetupLODs([1]);
        new_model.LODs[0].Meshes[0].UpdateMesh(vertices: verts, triangles: indexes, normals: normals, uv: uvs);
        return new_model;
    }

    private void GetInstanceData(FloorGroup group, FloorType floorType, out Float3[] verts, out int[] indexes, out Float2[] uvs, out Float3[] normals)
    {
        switch (floorType)
        {
            case FloorType.FullTile:
            {
                verts = [
                    new Float3(0.0f, 0.0f, 0.0f),
                    new Float3(TileDimension, 0.0f, 0.0f),
                    new Float3(0.0f, 0.0f, TileDimension),
                    new Float3(TileDimension, 0.0f, TileDimension)
                ];
                uvs = [.. BuildUVArrayFromData(group, TexType.FullTile)];
                indexes = [ 0, 1, 2, 1, 3, 2 ];
                break;
            }
            case FloorType.HorzLane:
            {
                var dataT = BuildRectData(group, TexType.OutEdgeTop);
                var dataB = BuildRectData(group, TexType.OutEdgeBottom);
                var dataBase = BuildRectData(group, TexType.FullTile);
                AdjustRectData(ref dataBase, top: dataT, bottom: dataB);
                BuildMeshData(out verts, out uvs, out indexes, dataT, dataBase, (dataB, TileSide.Bottom));
                break;
            }
            case FloorType.VertLane:
            {
                var dataL = BuildRectData(group, TexType.OutEdgeLeft);
                var dataR = BuildRectData(group, TexType.OutEdgeRight);
                var dataBase = BuildRectData(group, TexType.FullTile);
                AdjustRectData(ref dataBase, left: dataL, right: dataR);
                BuildMeshData(out verts, out uvs, out indexes, dataL, dataBase, (dataR, TileSide.Right));
                break;
            }
            case FloorType.EdgeLeft:
            {
                var dataL = BuildRectData(group, TexType.OutEdgeLeft);
                var dataBase = BuildRectData(group, TexType.FullTile);
                AdjustRectData(ref dataBase, left: dataL);
                BuildMeshData(out verts, out uvs, out indexes, dataL, dataBase);
                break;
            }
            case FloorType.EdgeTop:
            {
                var dataT = BuildRectData(group, TexType.OutEdgeTop);
                var dataBase = BuildRectData(group, TexType.FullTile);
                AdjustRectData(ref dataBase, top: dataT);
                BuildMeshData(out verts, out uvs, out indexes, dataT, dataBase);
                break;
            }
            case FloorType.EdgeRight:
            {
                var dataR = BuildRectData(group, TexType.OutEdgeRight);
                var dataBase = BuildRectData(group, TexType.FullTile);
                AdjustRectData(ref dataBase, right: dataR);
                BuildMeshData(out verts, out uvs, out indexes, dataBase, (dataR, TileSide.Right));
                break;
            }
            case FloorType.EdgeBottom:
            {
                var dataB = BuildRectData(group, TexType.OutEdgeBottom);
                var dataBase = BuildRectData(group, TexType.FullTile);
                AdjustRectData(ref dataBase, bottom: dataB);
                BuildMeshData(out verts, out uvs, out indexes, dataBase, (dataB, TileSide.Bottom));
                break;
            }
            case FloorType.TurnBottomLeft:
            {
                var curveData = BuildPolyData(group,
                        [TexType.OutCornerTopRight, TexType.OutEdgeRight, TexType.OutEdgeTop],
                        TileSide.TopLeft, TileSide.TopRight, TileSide.BottomLeft,
                        (TileSide.TopLeft, (null, new[] {(TileOp.Add, 2)} )),
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 1)}, null )),
                        TileSide.BottomRight);

                curveData.indexes.AddRange([0, 1, 3, 1, 4, 3, 1, 5, 4, 3, 4, 2]);

                var dataBase = BuildPolyData(group,
                        [TexType.FullTile, TexType.OutCornerTopRight, TexType.OutEdgeRight, TexType.OutEdgeTop, TexType.InCornerBottomLeft],
                        (TileSide.TopLeft, (null, new[] {(TileOp.Add, 3)})),
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 1)}, new[] {(TileOp.Add, 3)})),
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 1)}, new[] {(TileOp.Add, 1)})),
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 2)}, new[] {(TileOp.Add, 1)})),
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 2)}, null )),
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 4)}, new[] {(TileOp.Sub, 4)})),
                        (TileSide.BottomLeft, (null, new[] {(TileOp.Sub, 4)})),
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 4)}, null)) );
                dataBase.indexes.AddRange([0, 1, 2, 2, 3, 4, 0, 2, 5, 2, 4, 5, 0, 5, 6, 5, 4, 7]);

                var dataR = BuildRectData(group, TexType.OutEdgeRight);
                AdjustRectData(ref dataR, top: curveData);
                var dataT = BuildRectData(group, TexType.OutEdgeTop);
                AdjustRectData(ref dataT, right: curveData);
                var dataC = BuildRectData(group, TexType.InCornerBottomLeft);

                BuildMeshData(out verts, out uvs, out indexes, dataT, (curveData, TileSide.TopRight), dataBase,
                        (dataR, TileSide.BottomRight), (dataC, TileSide.BottomLeft));
                break;
            }
            case FloorType.TurnBottomRight:
            {
                var curveData = BuildPolyData(group,
                        [TexType.OutCornerTopLeft, TexType.OutEdgeLeft, TexType.OutEdgeTop],
                        TileSide.TopLeft, TileSide.TopRight, TileSide.BottomLeft,
                        (TileSide.TopRight, (null, new[] {(TileOp.Add, 2)} )),
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 1)}, null )),
                        TileSide.BottomRight);

                curveData.indexes.AddRange([0, 1, 3, 0, 3, 4, 0, 4, 2, 3, 5, 4]);

                var dataBase = BuildPolyData(group,
                        [TexType.FullTile, TexType.OutCornerTopLeft, TexType.OutEdgeLeft, TexType.OutEdgeTop, TexType.InCornerBottomRight],

                        (TileSide.TopLeft, (new[] {(TileOp.Add, 1)}, new[] {(TileOp.Add, 3)})),
                        (TileSide.TopRight, (null, new[] {(TileOp.Add, 3)})),
                        (TileSide.TopLeft, (new[] {(TileOp.Add, 1)}, new[] {(TileOp.Add, 1)})),
                        (TileSide.TopLeft, (new[] {(TileOp.Add, 2)}, new[] {(TileOp.Add, 1)})),
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 2)}, null )),
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 4)}, new[] {(TileOp.Sub, 4)})),
                        (TileSide.BottomRight, (null, new[] {(TileOp.Sub, 4)})),
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 4)}, null)) );
                dataBase.indexes.AddRange([0, 1, 2, 3, 2, 4, 1, 5, 2, 2, 5, 4, 1, 6, 5, 5, 7, 4]);

                var dataL = BuildRectData(group, TexType.OutEdgeLeft);
                AdjustRectData(ref dataL, top: curveData);
                var dataT = BuildRectData(group, TexType.OutEdgeTop);
                AdjustRectData(ref dataT, left: curveData);
                var dataC = BuildRectData(group, TexType.InCornerBottomRight);

                BuildMeshData(out verts, out uvs, out indexes, curveData, (dataT, TileSide.TopRight), dataBase,
                        (dataL, TileSide.BottomLeft), dataBase, (dataC, TileSide.BottomRight));
                break;
            }
            case FloorType.TurnTopLeft:
            {
                var curveData = BuildPolyData(group,
                        [TexType.OutCornerBottomRight, TexType.OutEdgeRight, TexType.OutEdgeBottom],

                        TileSide.TopLeft, TileSide.TopRight,
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 1)}, null )),
                        (TileSide.BottomLeft, (null, new[] {(TileOp.Sub, 2)} )),
                        TileSide.BottomLeft,
                        TileSide.BottomRight);

                curveData.indexes.AddRange([0, 2, 3, 2, 1, 5, 2, 5, 3, 3, 5, 4]);

                var dataBase = BuildPolyData(group,
                        [TexType.FullTile, TexType.OutCornerBottomRight, TexType.OutEdgeRight, TexType.OutEdgeBottom, TexType.InCornerTopLeft],

                        (TileSide.TopLeft, (new[] {(TileOp.Add, 4)}, null)),
                        (TileSide.TopLeft, (new[] {(TileOp.Add, 4)}, new[] {(TileOp.Add, 4)})),
                        (TileSide.TopLeft, (null, new[] {(TileOp.Add, 4)})),
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 2)}, null )),
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 1)}, new[] {(TileOp.Sub, 1)} )),
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 2)}, new[] {(TileOp.Sub, 1)})),
                        (TileSide.BottomLeft, (null, new[] {(TileOp.Sub, 3)})),
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 1)}, new[] {(TileOp.Sub, 3)})) );
                dataBase.indexes.AddRange([0, 3, 1, 3, 4, 1, 3, 5, 4, 2, 1, 6, 1, 4, 6, 4, 7, 6]);

                var dataR = BuildRectData(group, TexType.OutEdgeRight);
                AdjustRectData(ref dataR, bottom: curveData);
                var dataB = BuildRectData(group, TexType.OutEdgeBottom);
                AdjustRectData(ref dataB, right: curveData);
                var dataC = BuildRectData(group, TexType.InCornerTopLeft);

                BuildMeshData(out verts, out uvs, out indexes, dataC, dataBase,
                        (dataR, TileSide.TopRight), (dataB, TileSide.BottomLeft),
                        (curveData, TileSide.BottomRight));
                break;
            }
            case FloorType.TurnTopRight:
            {
                var curveData = BuildPolyData(group,
                        [TexType.OutCornerBottomLeft, TexType.OutEdgeLeft, TexType.OutEdgeBottom],
                        TileSide.TopLeft, TileSide.TopRight,
                        (TileSide.TopLeft, (new[] {(TileOp.Add, 1)}, null )),
                        (TileSide.BottomRight, (null, new[] {(TileOp.Sub, 2)} )),
                        TileSide.BottomLeft,
                        TileSide.BottomRight);

                curveData.indexes.AddRange([0, 2, 4, 2, 1, 3, 2, 3, 4, 3, 5, 4]);

                var dataBase = BuildPolyData(group,
                        [TexType.FullTile, TexType.OutCornerBottomLeft, TexType.OutEdgeLeft, TexType.OutEdgeBottom, TexType.InCornerTopRight],

                        (TileSide.TopLeft, (new[] {(TileOp.Add, 2)}, null)),
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 4)}, null)),
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 4)}, new[] {(TileOp.Add, 4)})),
                        (TileSide.TopRight, (null, new[] {(TileOp.Add, 4)} )),
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 2)}, new[] {(TileOp.Sub, 1)} )),
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 1)}, new[] {(TileOp.Sub, 1)})),
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 1)}, new[] {(TileOp.Sub, 3)})),
                        (TileSide.BottomRight, (null, new[] {(TileOp.Sub, 3)})) );
                dataBase.indexes.AddRange([0, 1, 2, 0, 2, 5, 0, 5, 4, 2, 3, 7, 2, 7, 5, 5, 7, 6]);

                var dataL = BuildRectData(group, TexType.OutEdgeLeft);
                AdjustRectData(ref dataL, bottom: curveData);
                var dataB = BuildRectData(group, TexType.OutEdgeBottom);
                AdjustRectData(ref dataB, left: curveData);
                var dataC = BuildRectData(group, TexType.InCornerTopRight);

                BuildMeshData(out verts, out uvs, out indexes, dataL, dataBase,
                        (dataC, TileSide.TopRight), (curveData, TileSide.BottomLeft),
                        (dataB, TileSide.BottomRight));
                break;
            }
            case FloorType.EdgeTopRight:
            {
                var curveData = BuildPolyData(group,
                        [TexType.OutCornerTopRight, TexType.OutEdgeRight, TexType.OutEdgeTop],

                        TileSide.TopLeft, TileSide.TopRight, TileSide.BottomLeft,
                        (TileSide.TopLeft, (null, new[] {(TileOp.Add, 2)} )),
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 1)}, null )),
                        TileSide.BottomRight);

                curveData.indexes.AddRange([0, 1, 3, 1, 4, 3, 1, 5, 4, 3, 4, 2]);

                var dataBase = BuildPolyData(group,
                        [TexType.FullTile, TexType.OutCornerTopRight, TexType.OutEdgeRight, TexType.OutEdgeTop],

                        (TileSide.TopLeft, (null, new[] {(TileOp.Add, 3)})),
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 1)}, new[] {(TileOp.Add, 3)})),
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 1)}, new[] {(TileOp.Add, 1)})),
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 2)}, new[] {(TileOp.Add, 1)} )),
                        TileSide.BottomLeft,
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 2)}, null)) );
                dataBase.indexes.AddRange([0, 1, 2, 2, 3, 5, 0, 2, 4, 2, 5, 4]);

                var dataR = BuildRectData(group, TexType.OutEdgeRight);
                AdjustRectData(ref dataR, top: curveData);
                var dataT = BuildRectData(group, TexType.OutEdgeTop);
                AdjustRectData(ref dataT, right: curveData);

                BuildMeshData(out verts, out uvs, out indexes, dataT, (curveData, TileSide.TopRight),
                        dataBase, (dataR, TileSide.BottomRight));
                break;
            }
            case FloorType.EdgeTopLeft:
            {
                var curveData = BuildPolyData(group,
                        [TexType.OutCornerTopLeft, TexType.OutEdgeLeft, TexType.OutEdgeTop],

                        TileSide.TopLeft, TileSide.TopRight, TileSide.BottomLeft,
                        (TileSide.TopRight, (null, new[] {(TileOp.Add, 2)} )),
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 1)}, null )),
                        TileSide.BottomRight);

                curveData.indexes.AddRange([0, 1, 3, 0, 3, 4, 0, 4, 2, 3, 5, 4]);

                var dataBase = BuildPolyData(group,
                        [TexType.FullTile, TexType.OutCornerTopLeft, TexType.OutEdgeLeft, TexType.OutEdgeTop],

                        (TileSide.TopLeft, (new[] {(TileOp.Add, 1)}, new[] {(TileOp.Add, 3)})),
                        (TileSide.TopRight, (null, new[] {(TileOp.Add, 3)})),
                        (TileSide.TopLeft, (new[] {(TileOp.Add, 1)}, new[] {(TileOp.Add, 1)})),
                        (TileSide.TopLeft, (new[] {(TileOp.Add, 2)}, new[] {(TileOp.Add, 1)} )),
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 2)}, null)),
                        TileSide.BottomRight);
                dataBase.indexes.AddRange([0, 1, 2, 1, 5, 2, 3, 2, 4, 2, 5, 4]);

                var dataL = BuildRectData(group, TexType.OutEdgeLeft);
                AdjustRectData(ref dataL, top: curveData);
                var dataT = BuildRectData(group, TexType.OutEdgeTop);
                AdjustRectData(ref dataT, left: curveData);

                BuildMeshData(out verts, out uvs, out indexes, curveData, (dataT, TileSide.TopRight),
                        dataL, dataBase);
                break;
            }

            case FloorType.EdgeBottomRight:
            {
                var curveData = BuildPolyData(group,
                        [TexType.OutCornerBottomRight, TexType.OutEdgeRight, TexType.OutEdgeBottom],

                        TileSide.TopLeft, TileSide.TopRight,
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 1)}, null )),
                        (TileSide.BottomLeft, (null, new[] {(TileOp.Sub, 2)} )),
                        TileSide.BottomLeft,
                        TileSide.BottomRight);

                curveData.indexes.AddRange([0, 2, 3, 2, 1, 5, 2, 5, 3, 3, 5, 4]);

                var dataBase = BuildPolyData(group,
                        [TexType.FullTile, TexType.OutCornerBottomRight, TexType.OutEdgeRight, TexType.OutEdgeBottom],

                        TileSide.TopLeft,
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 2)}, null)),
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 1)}, new[] {(TileOp.Sub, 1)})),
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 2)}, new[] {(TileOp.Sub, 1)})),
                        (TileSide.BottomLeft, (null, new[] {(TileOp.Sub, 3)})),
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 1)}, new[] {(TileOp.Sub, 3)})) );
                dataBase.indexes.AddRange([0, 1, 2, 1, 3, 2, 0, 2, 4, 2, 5, 4]);

                var dataR = BuildRectData(group, TexType.OutEdgeRight);
                AdjustRectData(ref dataR, bottom: curveData);
                var dataB = BuildRectData(group, TexType.OutEdgeBottom);
                AdjustRectData(ref dataB, right: curveData);

                BuildMeshData(out verts, out uvs, out indexes, dataBase, (dataR, TileSide.TopRight),
                        (dataB, TileSide.BottomLeft), (curveData, TileSide.BottomRight));
                break;
            }
            case FloorType.EdgeBottomLeft:
            {
                var curveData = BuildPolyData(group,
                        [TexType.OutCornerBottomLeft, TexType.OutEdgeLeft, TexType.OutEdgeBottom],

                        TileSide.TopLeft, TileSide.TopRight,
                        (TileSide.TopLeft, (new[] {(TileOp.Add, 1)}, null )),
                        (TileSide.BottomRight, (null, new[] {(TileOp.Sub, 2)} )),
                        TileSide.BottomLeft,
                        TileSide.BottomRight);

                curveData.indexes.AddRange([0, 2, 4, 2, 1, 3, 2, 3, 4, 3, 5, 4]);

                var dataBase = BuildPolyData(group,
                        [TexType.FullTile, TexType.OutCornerBottomLeft, TexType.OutEdgeLeft, TexType.OutEdgeBottom],

                        (TileSide.TopLeft, (new[] {(TileOp.Add, 2)}, null)),
                        TileSide.TopRight,
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 2)}, new[] {(TileOp.Sub, 1)})),
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 1)}, new[] {(TileOp.Sub, 1)})),
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 1)}, new[] {(TileOp.Sub, 3)})),
                        (TileSide.BottomRight, (null, new[] {(TileOp.Sub, 3)})) );
                dataBase.indexes.AddRange([0, 1, 3, 0, 3, 2, 1, 5, 3, 3, 5, 4]);

                var dataL = BuildRectData(group, TexType.OutEdgeLeft);
                AdjustRectData(ref dataL, bottom: curveData);
                var dataB = BuildRectData(group, TexType.OutEdgeBottom);
                AdjustRectData(ref dataB, left: curveData);

                BuildMeshData(out verts, out uvs, out indexes, dataL, dataBase, 
                        (curveData, TileSide.BottomLeft), (dataB, TileSide.BottomRight));
                break;
            }
            case FloorType.VertCrossRight:
            {
                var dataBase = BuildPolyData(group,
                        [TexType.FullTile, TexType.OutEdgeLeft, TexType.InCornerTopRight, TexType.InCornerBottomRight],

                        (TileSide.TopLeft, (new[] {(TileOp.Add, 1)}, null)),
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 2)}, null)),
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 2)}, new[] {(TileOp.Add, 2)})),
                        (TileSide.TopRight, (null, new[] {(TileOp.Add, 2)})),
                        (TileSide.BottomRight, (null, new[] {(TileOp.Sub, 3)})),
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 3)}, new[] {(TileOp.Sub, 3)})),
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 1)}, null)),
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 3)}, null)) );
                dataBase.indexes.AddRange([0, 1, 2, 0, 2, 5, 0, 5, 6, 5, 7, 6, 2, 3, 5, 3, 4, 5]);

                var dataL = BuildRectData(group, TexType.OutEdgeLeft);
                var dataCTR = BuildRectData(group, TexType.InCornerTopRight);
                var dataCBR = BuildRectData(group, TexType.InCornerBottomRight);

                BuildMeshData(out verts, out uvs, out indexes, dataL, dataBase, 
                        (dataCTR, TileSide.TopRight), (dataCBR, TileSide.BottomRight));
                break;
            }
            case FloorType.VertCrossLeft:
            {
                var dataBase = BuildPolyData(group,
                        [TexType.FullTile, TexType.OutEdgeRight, TexType.InCornerTopLeft, TexType.InCornerBottomLeft],

                        (TileSide.TopLeft, (new[] {(TileOp.Add, 2)}, null)),
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 1)}, null)),
                        (TileSide.TopLeft, (null, new[] {(TileOp.Add, 2)})),
                        (TileSide.TopLeft, (new[] {(TileOp.Add, 2)}, new[] {(TileOp.Add, 2)})),
                        (TileSide.BottomLeft, (null, new[] {(TileOp.Sub, 3)})),
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 3)}, new[] {(TileOp.Sub, 3)})),
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 3)}, null)),
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 1)}, null)) );
                dataBase.indexes.AddRange([2, 3, 5, 2, 5, 4, 0, 1, 3, 1, 5, 3, 1, 7, 5, 5, 7, 6]);

                var dataR = BuildRectData(group, TexType.OutEdgeRight);
                var dataCTL = BuildRectData(group, TexType.InCornerTopLeft);
                var dataCBL = BuildRectData(group, TexType.InCornerBottomLeft);

                BuildMeshData(out verts, out uvs, out indexes, dataCTL, dataBase, 
                        (dataR, TileSide.TopRight), (dataCBL, TileSide.BottomLeft));
                break;
            }
            case FloorType.HorzCrossTop:
            {
                var dataBase = BuildPolyData(group,
                        [TexType.FullTile, TexType.OutEdgeBottom, TexType.InCornerTopLeft, TexType.InCornerTopRight],

                        (TileSide.TopLeft, (new[] {(TileOp.Add, 2)}, null)),
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 3)}, null)),
                        (TileSide.TopLeft, (null, new[] {(TileOp.Add, 2)})),
                        (TileSide.TopLeft, (new[] {(TileOp.Add, 2)}, new[] {(TileOp.Add, 2)})),
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 3)}, new[] {(TileOp.Add, 3)})),
                        (TileSide.TopRight, (null, new[] {(TileOp.Add, 3)})),
                        (TileSide.BottomLeft, (null, new[] {(TileOp.Sub, 1)})),
                        (TileSide.BottomRight, (null, new[] {(TileOp.Sub, 1)})) );
                dataBase.indexes.AddRange([0, 1, 3, 1, 4, 3, 3, 6, 2, 4, 5, 7, 3, 4, 6, 4, 7, 6]);

                var dataB = BuildRectData(group, TexType.OutEdgeBottom);
                var dataCTL = BuildRectData(group, TexType.InCornerTopLeft);
                var dataCTR = BuildRectData(group, TexType.InCornerTopRight);

                BuildMeshData(out verts, out uvs, out indexes, dataCTL, dataBase, 
                        (dataCTR, TileSide.TopRight), (dataB, TileSide.BottomLeft));
                break;
            }
            case FloorType.HorzCrossBottom:
            {
                var dataBase = BuildPolyData(group,
                        [TexType.FullTile, TexType.OutEdgeTop, TexType.InCornerBottomLeft, TexType.InCornerBottomRight],

                        (TileSide.TopLeft, (null, new[] {(TileOp.Add, 1)})),
                        (TileSide.TopRight, (null, new[] {(TileOp.Add, 1)})),
                        (TileSide.BottomLeft, (null, new[] {(TileOp.Sub, 2)})),
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 2)}, new[] {(TileOp.Sub, 2)})),
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 3)}, new[] {(TileOp.Sub, 3)})),
                        (TileSide.BottomRight, (null, new[] {(TileOp.Sub, 3)})),
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 2)}, null)),
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 3)}, null)) );
                dataBase.indexes.AddRange([0, 3, 2, 0, 1, 3, 1, 4, 3, 1, 5, 4, 3, 4, 7, 3, 7, 6]);

                var dataT = BuildRectData(group, TexType.OutEdgeTop);
                var dataCBL = BuildRectData(group, TexType.InCornerBottomLeft);
                var dataCBR = BuildRectData(group, TexType.InCornerBottomRight);

                BuildMeshData(out verts, out uvs, out indexes, dataT, dataBase, 
                        (dataCBL, TileSide.BottomLeft), (dataCBR, TileSide.BottomRight));
                break;
            }
            case FloorType.EdgeLeftCornerTopRight:
            {
                var dataBase = BuildPolyData(group,
                        [TexType.FullTile, TexType.OutEdgeLeft, TexType.InCornerTopRight],

                        (TileSide.TopLeft, (new[] {(TileOp.Add, 1)}, null)),
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 2)}, null)),
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 2)}, new[] {(TileOp.Add, 2)})),
                        (TileSide.TopRight, (null, new[] {(TileOp.Add, 2)})),
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 1)}, null)),
                        TileSide.BottomRight );
                dataBase.indexes.AddRange([0, 1, 2, 0, 2, 4, 2, 5, 4, 2, 3, 5]);

                var dataL = BuildRectData(group, TexType.OutEdgeLeft);
                var dataCTR = BuildRectData(group, TexType.InCornerTopRight);

                BuildMeshData(out verts, out uvs, out indexes, dataL, dataBase, 
                        (dataCTR, TileSide.TopRight));
                break;
            }
            case FloorType.EdgeLeftCornerBottomRight:
            {
                var dataBase = BuildPolyData(group,
                        [TexType.FullTile, TexType.OutEdgeLeft, TexType.InCornerBottomRight],

                        (TileSide.TopLeft, (new[] {(TileOp.Add, 1)}, null)),
                        TileSide.TopRight,
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 1)}, null)),
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 2)}, new[] {(TileOp.Sub, 2)})),
                        (TileSide.BottomRight, (null, new[] {(TileOp.Sub, 2)})),
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 2)}, null)) );
                dataBase.indexes.AddRange([0, 1, 3, 1, 4, 3, 0, 3, 2, 3, 5, 2]);

                var dataL = BuildRectData(group, TexType.OutEdgeLeft);
                var dataCBR = BuildRectData(group, TexType.InCornerBottomRight);

                BuildMeshData(out verts, out uvs, out indexes, dataL, dataBase, 
                        (dataCBR, TileSide.BottomRight));
                break;
            }
            case FloorType.EdgeRightCornerTopLeft:
            {
                var dataBase = BuildPolyData(group,
                        [TexType.FullTile, TexType.OutEdgeRight, TexType.InCornerTopLeft],

                        (TileSide.TopLeft, (new[] {(TileOp.Add, 2)}, null)),
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 1)}, null)),
                        (TileSide.TopLeft, (null, new[] {(TileOp.Add, 2)})),
                        (TileSide.TopLeft, (new[] {(TileOp.Add, 2)}, new[] {(TileOp.Add, 2)})),
                        TileSide.BottomLeft,
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 1)}, null)) );
                dataBase.indexes.AddRange([0, 1, 3, 1, 5, 3, 3, 5, 4, 2, 3, 4]);

                var dataR = BuildRectData(group, TexType.OutEdgeRight);
                var dataCTL = BuildRectData(group, TexType.InCornerTopLeft);

                BuildMeshData(out verts, out uvs, out indexes, dataCTL, dataBase, 
                        (dataR, TileSide.TopRight));
                break;
            }
            case FloorType.EdgeRightCornerBottomLeft:
            {
                var dataBase = BuildPolyData(group,
                        [TexType.FullTile, TexType.OutEdgeRight, TexType.InCornerBottomLeft],

                        TileSide.TopLeft,
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 1)}, null)),
                        (TileSide.BottomLeft, (null, new[] {(TileOp.Sub, 2)})),
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 2)}, new[] {(TileOp.Sub, 2)})),
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 2)}, null)),
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 1)}, null)) );
                dataBase.indexes.AddRange([0, 3, 2, 0, 1, 3, 1, 5, 3, 3, 5, 4]);

                var dataR = BuildRectData(group, TexType.OutEdgeRight);
                var dataCBL = BuildRectData(group, TexType.InCornerBottomLeft);

                BuildMeshData(out verts, out uvs, out indexes, dataBase, (dataR, TileSide.TopRight), 
                        (dataCBL, TileSide.BottomLeft));
                break;
            }
            case FloorType.EdgeTopCornerBottomRight:
            {
                var dataBase = BuildPolyData(group,
                        [TexType.FullTile, TexType.OutEdgeTop, TexType.InCornerBottomRight],

                        (TileSide.TopLeft, (null, new[] {(TileOp.Add, 1)})),
                        (TileSide.TopRight, (null, new[] {(TileOp.Add, 1)})),
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 2)}, new[] {(TileOp.Sub, 2)})),
                        (TileSide.BottomRight, (null, new[] {(TileOp.Sub, 2)})),
                        TileSide.BottomLeft,
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 2)}, null)) );
                dataBase.indexes.AddRange([0, 1, 2, 1, 3, 2, 0, 2, 4, 2, 5, 4]);

                var dataT = BuildRectData(group, TexType.OutEdgeTop);
                var dataCBR = BuildRectData(group, TexType.InCornerBottomRight);

                BuildMeshData(out verts, out uvs, out indexes, dataT, dataBase,
                        (dataCBR, TileSide.BottomRight));
                break;
            }  
            case FloorType.EdgeTopCornerBottomLeft:
            {
                var dataBase = BuildPolyData(group,
                        [TexType.FullTile, TexType.OutEdgeTop, TexType.InCornerBottomLeft],

                        (TileSide.TopLeft, (null, new[] {(TileOp.Add, 1)})),
                        (TileSide.TopRight, (null, new[] {(TileOp.Add, 1)})),
                        (TileSide.BottomLeft, (null, new[] {(TileOp.Sub, 2)})),
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 2)}, new[] {(TileOp.Sub, 2)})),
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 2)}, null)),
                        TileSide.BottomRight );
                dataBase.indexes.AddRange([0, 3, 2, 0, 1, 3, 1, 5, 3, 3, 5, 4]);

                var dataT = BuildRectData(group, TexType.OutEdgeTop);
                var dataCBL = BuildRectData(group, TexType.InCornerBottomLeft);

                BuildMeshData(out verts, out uvs, out indexes, dataT, dataBase,
                        (dataCBL, TileSide.BottomLeft));
                break;
            }
            case FloorType.EdgeBottomCornerTopRight:
            {
                var dataBase = BuildPolyData(group,
                        [TexType.FullTile, TexType.OutEdgeBottom, TexType.InCornerTopRight],

                        TileSide.TopLeft,
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 2)}, null)),
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 2)}, new[] {(TileOp.Add, 2)})),
                        (TileSide.TopRight, (null, new[] {(TileOp.Add, 2)})),
                        (TileSide.BottomLeft, (null, new[] {(TileOp.Sub, 1)})),
                        (TileSide.BottomRight, (null, new[] {(TileOp.Sub, 1)})));
                dataBase.indexes.AddRange([0, 1, 2, 0, 2, 4, 2, 3, 5, 2, 5, 4]);

                var dataB = BuildRectData(group, TexType.OutEdgeBottom);
                var dataCTR = BuildRectData(group, TexType.InCornerTopRight);

                BuildMeshData(out verts, out uvs, out indexes, dataBase, (dataCTR, TileSide.TopRight),
                        (dataB, TileSide.BottomLeft));
                break;
            }                      
            case FloorType.EdgeBottomCornerTopLeft:
            {
                var dataBase = BuildPolyData(group,
                        [TexType.FullTile, TexType.OutEdgeBottom, TexType.InCornerTopLeft],

                        (TileSide.TopLeft, (new[] {(TileOp.Add, 2)}, null)),
                        TileSide.TopRight,
                        (TileSide.TopLeft, (null, new[] {(TileOp.Add, 2)})),
                        (TileSide.TopLeft, (new[] {(TileOp.Add, 2)}, new[] {(TileOp.Add, 2)})),
                        (TileSide.BottomLeft, (null, new[] {(TileOp.Sub, 1)})),
                        (TileSide.BottomRight, (null, new[] {(TileOp.Sub, 1)})));
                dataBase.indexes.AddRange([0, 1, 3, 2, 3, 4, 1, 5, 3, 3, 5, 4]);

                var dataB = BuildRectData(group, TexType.OutEdgeBottom);
                var dataCTL = BuildRectData(group, TexType.InCornerTopLeft);

                BuildMeshData(out verts, out uvs, out indexes, dataCTL, dataBase, 
                        (dataB, TileSide.BottomLeft));
                break;
            }
            case FloorType.CornerExceptTopLeft:
            {
                var dataBase = BuildPolyData(group,
                        [TexType.FullTile, TexType.InCornerTopRight, TexType.InCornerBottomLeft, TexType.InCornerBottomRight],

                        TileSide.TopLeft,
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 1)}, null)),
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 1)}, new[] {(TileOp.Add, 1)})),
                        (TileSide.TopRight, (null, new[] {(TileOp.Add, 1)})),
                        (TileSide.BottomLeft, (null, new[] {(TileOp.Sub, 2)})),
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 2)}, new[] {(TileOp.Sub, 2)})),
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 2)}, null)),
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 3)}, null)),
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 3)}, new[] {(TileOp.Sub, 3)})),
                        (TileSide.BottomRight, (null, new[] {(TileOp.Sub, 3)})));
                dataBase.indexes.AddRange([0, 1, 2, 0, 2, 8, 0, 8, 5, 0, 5, 4, 2, 3, 9, 2, 9, 8, 5, 8, 7, 5, 7, 6]);

                var dataCTR = BuildRectData(group, TexType.InCornerTopRight);
                var dataCBL = BuildRectData(group, TexType.InCornerBottomLeft);
                var dataCBR = BuildRectData(group, TexType.InCornerBottomRight);

                BuildMeshData(out verts, out uvs, out indexes, dataBase, (dataCTR, TileSide.TopRight), 
                        (dataCBL, TileSide.BottomLeft), (dataCBR, TileSide.BottomRight));

                break;
            }
            case FloorType.CornerExceptTopRight:
            {
                var dataBase = BuildPolyData(group,
                        [TexType.FullTile, TexType.InCornerTopLeft, TexType.InCornerBottomLeft, TexType.InCornerBottomRight],

                        (TileSide.TopLeft, (new[] {(TileOp.Add, 1)}, null)),
                        TileSide.TopRight,
                        (TileSide.TopLeft, (null, new[] {(TileOp.Add, 1)})),
                        (TileSide.TopLeft, (new[] {(TileOp.Add, 1)}, new[] {(TileOp.Add, 1)})),
                        (TileSide.BottomLeft, (null, new[] {(TileOp.Sub, 2)})),
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 2)}, new[] {(TileOp.Sub, 2)})),
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 2)}, null)),
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 3)}, null)),
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 3)}, new[] {(TileOp.Sub, 3)})),
                        (TileSide.BottomRight, (null, new[] {(TileOp.Sub, 3)})));
                dataBase.indexes.AddRange([0, 1, 3, 2, 3, 4, 3, 5, 4, 1, 5, 3, 1, 8, 5, 1, 9, 8, 5, 8, 6, 8, 7, 6]);

                var dataCTL = BuildRectData(group, TexType.InCornerTopLeft);
                var dataCBL = BuildRectData(group, TexType.InCornerBottomLeft);
                var dataCBR = BuildRectData(group, TexType.InCornerBottomRight);

                BuildMeshData(out verts, out uvs, out indexes, dataCTL, dataBase, 
                        (dataCBL, TileSide.BottomLeft), (dataCBR, TileSide.BottomRight));

                break;
            }
            case FloorType.CornerExceptBottomLeft:
            {
                var dataBase = BuildPolyData(group,
                        [TexType.FullTile, TexType.InCornerTopLeft, TexType.InCornerTopRight, TexType.InCornerBottomRight],

                        (TileSide.TopLeft, (new[] {(TileOp.Add, 1)}, null)),
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 2)}, null)),
                        (TileSide.TopLeft, (null, new[] {(TileOp.Add, 1)})),
                        (TileSide.TopLeft, (new[] {(TileOp.Add, 1)}, new[] {(TileOp.Add, 1)})),
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 2)}, new[] {(TileOp.Add, 1)})),
                        (TileSide.TopRight, (null, new[] {(TileOp.Add, 2)})),
                        TileSide.BottomLeft,
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 3)}, null)),
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 3)}, new[] {(TileOp.Sub, 3)})),
                        (TileSide.BottomRight, (null, new[] {(TileOp.Sub, 3)})));
                dataBase.indexes.AddRange([0, 1, 3, 1, 4, 3, 2, 3, 6, 3, 4, 6, 4, 8, 6, 8, 7, 6, 4, 5, 8, 5, 9, 8]);

                var dataCTL = BuildRectData(group, TexType.InCornerTopLeft);
                var dataCTR = BuildRectData(group, TexType.InCornerTopRight);
                var dataCBR = BuildRectData(group, TexType.InCornerBottomRight);

                BuildMeshData(out verts, out uvs, out indexes, dataCTL, dataBase, 
                        (dataCTR, TileSide.TopRight), (dataCBR, TileSide.BottomRight));

                break;
            }
            case FloorType.CornerExceptBottomRight:
            {
                var dataBase = BuildPolyData(group,
                        [TexType.FullTile, TexType.InCornerTopLeft, TexType.InCornerTopRight, TexType.InCornerBottomLeft],

                        (TileSide.TopLeft, (new[] {(TileOp.Add, 1)}, null)),
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 2)}, null)),
                        (TileSide.TopLeft, (null, new[] {(TileOp.Add, 1)})),
                        (TileSide.TopLeft, (new[] {(TileOp.Add, 1)}, new[] {(TileOp.Add, 1)})),
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 2)}, new[] {(TileOp.Add, 2)})),
                        (TileSide.TopRight, (null, new[] {(TileOp.Add, 2)})),
                        (TileSide.BottomLeft, (null, new[] {(TileOp.Sub, 3)})),
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 3)}, new[] {(TileOp.Sub, 3)})),
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 3)}, null)),
                        TileSide.BottomRight);
                dataBase.indexes.AddRange([0, 1, 4, 0, 4, 3, 2, 3, 7, 2, 7, 6, 3, 4, 9, 4, 5, 9, 3, 9, 7, 7, 9, 8]);

                var dataCTL = BuildRectData(group, TexType.InCornerTopLeft);
                var dataCTR = BuildRectData(group, TexType.InCornerTopRight);
                var dataCBL = BuildRectData(group, TexType.InCornerBottomLeft);

                BuildMeshData(out verts, out uvs, out indexes, dataCTL, dataBase, 
                        (dataCTR, TileSide.TopRight), (dataCBL, TileSide.BottomLeft));

                break;
            }
            case FloorType.CornerAll:
            {
                var dataBase = BuildPolyData(group,
                        [TexType.FullTile, TexType.InCornerTopLeft, TexType.InCornerTopRight,
                        TexType.InCornerBottomLeft, TexType.InCornerBottomRight],

                        (TileSide.TopLeft, (new[] {(TileOp.Add, 1)}, null)),
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 2)}, null)),
                        (TileSide.TopLeft, (null, new[] {(TileOp.Add, 1)})),
                        (TileSide.TopLeft, (new[] {(TileOp.Add, 1)}, new[] {(TileOp.Add, 1)})),
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 2)}, new[] {(TileOp.Add, 2)})),
                        (TileSide.TopRight, (null, new[] {(TileOp.Add, 2)})),
                        (TileSide.BottomLeft, (null, new[] {(TileOp.Sub, 3)})),
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 3)}, new[] {(TileOp.Sub, 3)})),
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 3)}, null)),
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 4)}, null)),
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 4)}, new[] {(TileOp.Sub, 4)})),
                        (TileSide.BottomRight, (null, new[] {(TileOp.Sub, 4)}))
                        );
                dataBase.indexes.AddRange([0, 1, 3, 1, 4, 3, 2, 3, 6, 3, 7, 6, 4, 5, 10, 5, 11, 10, 3, 4, 7, 4, 10, 7, 7, 10, 8, 10, 9, 8]);

                var dataCTL = BuildRectData(group, TexType.InCornerTopLeft);
                var dataCTR = BuildRectData(group, TexType.InCornerTopRight);
                var dataCBL = BuildRectData(group, TexType.InCornerBottomLeft);
                var dataCBR = BuildRectData(group, TexType.InCornerBottomRight);

                BuildMeshData(out verts, out uvs, out indexes, dataCTL, dataBase, 
                        (dataCTR, TileSide.TopRight), (dataCBL, TileSide.BottomLeft), (dataCBR, TileSide.BottomRight));

                break;
            }
            case FloorType.CornerBothTop:
            {
                var dataBase = BuildPolyData(group,
                        [TexType.FullTile, TexType.InCornerTopLeft, TexType.InCornerTopRight],

                        (TileSide.TopLeft, (new[] {(TileOp.Add, 1)}, null)),
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 2)}, null)),
                        (TileSide.TopLeft, (null, new[] {(TileOp.Add, 1)})),
                        (TileSide.TopLeft, (new[] {(TileOp.Add, 1)}, new[] {(TileOp.Add, 1)})),
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 2)}, new[] {(TileOp.Add, 2)})),
                        (TileSide.TopRight, (null, new[] {(TileOp.Add, 2)})),
                        TileSide.BottomLeft,
                        TileSide.BottomRight);
                dataBase.indexes.AddRange([0, 1, 4, 0, 4, 3, 2, 3, 6, 3, 7, 6, 3, 4, 7, 4, 5, 7]);

                var dataCTL = BuildRectData(group, TexType.InCornerTopLeft);
                var dataCTR = BuildRectData(group, TexType.InCornerTopRight);

                BuildMeshData(out verts, out uvs, out indexes, dataCTL, dataBase, 
                        (dataCTR, TileSide.TopRight));

                break;
            }
            case FloorType.CornerBothRight:
            {
                var dataBase = BuildPolyData(group,
                        [TexType.FullTile, TexType.InCornerTopRight, TexType.InCornerBottomRight],

                        TileSide.TopLeft,
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 1)}, null)),
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 1)}, new[] {(TileOp.Add, 1)})),
                        (TileSide.TopRight, (null, new[] {(TileOp.Add, 1)})),
                        TileSide.BottomLeft,
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 2)}, null)),
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 2)}, new[] {(TileOp.Sub, 2)})),
                        (TileSide.BottomRight, (null, new[] {(TileOp.Sub, 2)})));
                dataBase.indexes.AddRange([0, 1, 2, 0, 2, 4, 2, 6, 4, 6, 5, 4, 2, 3, 6, 3, 7, 6]);

                var dataCTR = BuildRectData(group, TexType.InCornerTopRight);
                var dataCBR = BuildRectData(group, TexType.InCornerBottomRight);

                BuildMeshData(out verts, out uvs, out indexes, dataBase,
                        (dataCTR, TileSide.TopRight), 
                        (dataCBR, TileSide.BottomRight));
                break;
            }
            case FloorType.CornerBothBottom:
            {
                var dataBase = BuildPolyData(group,
                        [TexType.FullTile, TexType.InCornerBottomLeft, TexType.InCornerBottomRight],

                        TileSide.TopLeft,
                        TileSide.TopRight,
                        (TileSide.BottomLeft, (null, new[] {(TileOp.Sub, 1)})),
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 1)}, new[] {(TileOp.Sub, 1)})),
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 1)}, null)),
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 2)}, null)),
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 2)}, new[] {(TileOp.Sub, 2)})),
                        (TileSide.BottomRight, (null, new[] {(TileOp.Sub, 2)})));
                dataBase.indexes.AddRange([0, 3, 2, 0, 6, 3, 0, 1, 6, 1, 7, 6, 3, 6, 5, 3, 5, 4]);

                var dataCBL = BuildRectData(group, TexType.InCornerBottomLeft);
                var dataCBR = BuildRectData(group, TexType.InCornerBottomRight);

                BuildMeshData(out verts, out uvs, out indexes, dataBase,
                        (dataCBL, TileSide.BottomLeft), 
                        (dataCBR, TileSide.BottomRight));
                break;
            }
            case FloorType.CornerBothLeft:
            {
                var dataBase = BuildPolyData(group,
                        [TexType.FullTile, TexType.InCornerTopLeft, TexType.InCornerBottomLeft],

                        (TileSide.TopLeft, (new[] {(TileOp.Add, 1)}, null)),
                        TileSide.TopRight,
                        (TileSide.TopLeft, (null, new[] {(TileOp.Add, 1)})),
                        (TileSide.TopLeft, (new[] {(TileOp.Add, 1)}, new[] {(TileOp.Add, 1)})),
                        (TileSide.BottomLeft, (null, new[] {(TileOp.Sub, 2)})),
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 2)}, new[] {(TileOp.Sub, 2)})),
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 2)}, null)),
                        TileSide.BottomRight );
                dataBase.indexes.AddRange([0, 1, 3, 2, 3, 4, 3, 5, 4, 1, 5, 3, 1, 7, 5, 5, 7, 6]);

                var dataCTL = BuildRectData(group, TexType.InCornerTopLeft);
                var dataCBL = BuildRectData(group, TexType.InCornerBottomLeft);

                BuildMeshData(out verts, out uvs, out indexes, dataCTL, dataBase,
                        (dataCBL, TileSide.BottomLeft));
                break;
            }
            case FloorType.CornerTopLeftBottomRight:
            {
                var dataBase = BuildPolyData(group,
                        [TexType.FullTile, TexType.InCornerTopLeft, TexType.InCornerBottomRight],

                        (TileSide.TopLeft, (new[] {(TileOp.Add, 1)}, null)),
                        TileSide.TopRight,
                        (TileSide.TopLeft, (null, new[] {(TileOp.Add, 1)})),
                        (TileSide.TopLeft, (new[] {(TileOp.Add, 1)}, new[] {(TileOp.Add, 1)})),
                        TileSide.BottomLeft,
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 2)}, null)),
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 2)}, new[] {(TileOp.Sub, 2)})),
                        (TileSide.BottomRight, (null, new[] {(TileOp.Sub, 2)})) );
                dataBase.indexes.AddRange([0, 1, 3, 2, 3, 4, 1, 6, 3, 1, 7, 6, 3, 6, 4, 6, 5, 4]);

                var dataCTL = BuildRectData(group, TexType.InCornerTopLeft);
                var dataCBR = BuildRectData(group, TexType.InCornerBottomRight);

                BuildMeshData(out verts, out uvs, out indexes, dataCTL, dataBase,
                        (dataCBR, TileSide.BottomRight));
                break;
            }
            case FloorType.CornerTopRightBottomLeft:
            {
                var dataBase = BuildPolyData(group,
                        [TexType.FullTile, TexType.InCornerTopRight, TexType.InCornerBottomLeft],

                        TileSide.TopLeft,
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 1)}, null)),
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 1)}, new[] {(TileOp.Add, 1)})),
                        (TileSide.TopRight, (null, new[] {(TileOp.Add, 1)})),
                        (TileSide.BottomLeft, (null, new[] {(TileOp.Sub, 2)})),
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 2)}, new[] {(TileOp.Sub, 2)})),
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 2)}, null)),
                        TileSide.BottomRight );
                dataBase.indexes.AddRange([0, 1, 2, 0, 2, 5, 0, 5, 4, 2, 3, 7, 2, 7, 5, 5, 7, 6]);

                var dataCTR = BuildRectData(group, TexType.InCornerTopRight);
                var dataCBL = BuildRectData(group, TexType.InCornerBottomLeft);

                BuildMeshData(out verts, out uvs, out indexes, dataBase,
                        (dataCTR, TileSide.TopRight), 
                        (dataCBL, TileSide.BottomLeft));
                break;
            }
            case FloorType.OnlyCornerTopRight:
            {
                var dataBase = BuildPolyData(group,
                        [TexType.FullTile, TexType.InCornerTopRight],

                        TileSide.TopLeft,
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 1)}, null)),
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 1)}, new[] {(TileOp.Add, 1)})),
                        (TileSide.TopRight, (null, new[] {(TileOp.Add, 1)})),
                        TileSide.BottomLeft,
                        TileSide.BottomRight );
                dataBase.indexes.AddRange([0, 1, 2, 0, 2, 4, 2, 3, 5, 2, 5, 4]);

                var dataCTR = BuildRectData(group, TexType.InCornerTopRight);

                BuildMeshData(out verts, out uvs, out indexes, dataBase,
                        (dataCTR, TileSide.TopRight));
                break;
            }
            case FloorType.OnlyCornerTopLeft:
            {
                var dataBase = BuildPolyData(group,
                        [TexType.FullTile, TexType.InCornerTopLeft],

                        (TileSide.TopLeft, (new[] {(TileOp.Add, 1)}, null)),
                        TileSide.TopRight,
                        (TileSide.TopLeft, (null, new[] {(TileOp.Add, 1)})),
                        (TileSide.TopLeft, (new[] {(TileOp.Add, 1)}, new[] {(TileOp.Add, 1)})),
                        TileSide.BottomLeft,
                        TileSide.BottomRight );
                dataBase.indexes.AddRange([0, 1, 3, 2, 3, 4, 1, 5, 3, 3, 5, 4]);

                var dataCTL = BuildRectData(group, TexType.InCornerTopLeft);

                BuildMeshData(out verts, out uvs, out indexes, dataCTL, dataBase);
                break;
            }
            case FloorType.OnlyCornerBottomRight:
            {
                var dataBase = BuildPolyData(group,
                        [TexType.FullTile, TexType.InCornerBottomRight],

                        TileSide.TopLeft,
                        TileSide.TopRight,
                        TileSide.BottomLeft,
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 1)}, null)),
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 1)}, new[] {(TileOp.Sub, 1)})),
                        (TileSide.BottomRight, (null, new[] {(TileOp.Sub, 1)})) );
                dataBase.indexes.AddRange([1, 5, 4, 0, 1, 4, 0, 4, 2, 4, 3, 2]);

                var dataCBR = BuildRectData(group, TexType.InCornerBottomRight);

                BuildMeshData(out verts, out uvs, out indexes, dataBase, (dataCBR, TileSide.BottomRight));
                break;
            }
            case FloorType.OnlyCornerBottomLeft:
            {
                var dataBase = BuildPolyData(group,
                        [TexType.FullTile, TexType.InCornerBottomLeft],

                        TileSide.TopLeft,
                        TileSide.TopRight,
                        (TileSide.BottomLeft, (null, new[] {(TileOp.Sub, 1)})),
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 1)}, new[] {(TileOp.Sub, 1)})),
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 1)}, null)),
                        TileSide.BottomRight );
                dataBase.indexes.AddRange([0, 3, 2, 0, 1, 3, 1, 5, 3, 3, 5, 4]);

                var dataCBL = BuildRectData(group, TexType.InCornerBottomLeft);

                BuildMeshData(out verts, out uvs, out indexes, dataBase, (dataCBL, TileSide.BottomLeft));
                break;
            }
            case FloorType.IsolatedTile:
            {
                var dataBase = BuildPolyData(group,
                        [TexType.FullTile, TexType.OutSharpCornerTopLeft,
                        TexType.OutSharpCornerTopRight, TexType.OutSharpCornerBottomLeft,
                        TexType.OutSharpCornerBottomRight],

                        (TileSide.TopLeft, (new[] {(TileOp.Add, 1)}, new[] {(TileOp.Add, 1)})),
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 2)}, new[] {(TileOp.Add, 2)})),
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 3)}, new[] {(TileOp.Sub, 3)})),
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 4)}, new[] {(TileOp.Sub, 4)})));
                dataBase.indexes.AddRange([0, 1, 3, 0, 3, 2]);

                var dataSCTL = BuildRectData(group, TexType.OutSharpCornerTopLeft);
                var dataSCTR = BuildRectData(group, TexType.OutSharpCornerTopRight);
                var dataSCBL = BuildRectData(group, TexType.OutSharpCornerBottomLeft);
                var dataSCBR = BuildRectData(group, TexType.OutSharpCornerBottomRight);
                var dataT = BuildRectData(group, TexType.OutEdgeTop);
                AdjustRectData(ref dataT, left: dataSCTL, right: dataSCTR);
                var dataL = BuildRectData(group, TexType.OutEdgeLeft);
                AdjustRectData(ref dataL, top: dataSCTL, bottom: dataSCBL);
                var dataB = BuildRectData(group, TexType.OutEdgeBottom);
                AdjustRectData(ref dataB, left: dataSCBL, right: dataSCBR);
                var dataR = BuildRectData(group, TexType.OutEdgeRight);
                AdjustRectData(ref dataR, top: dataSCTR, bottom: dataSCBR);

                BuildMeshData(out verts, out uvs, out indexes, dataSCTL,
                        (dataT, TileSide.Top),
                        (dataSCTR, TileSide.TopRight),
                        (dataL, TileSide.Left),
                        dataBase,
                        (dataR, TileSide.Right),
                        (dataSCBL, TileSide.BottomLeft),
                        (dataB, TileSide.Bottom),
                        (dataSCBR, TileSide.BottomRight));
                break;
            }
            case FloorType.DeadendLeft:
            {
                var dataBase = BuildPolyData(group,
                        [TexType.FullTile, TexType.OutEdgeTop, TexType.OutEdgeBottom,
                        TexType.OutSharpCornerTopLeft, TexType.OutSharpCornerBottomLeft],

                        (TileSide.TopLeft, (new[] {(TileOp.Add, 3)}, new[] {(TileOp.Add, 3)})),
                        (TileSide.TopRight, (null, new[] {(TileOp.Add, 1)})),
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 4)}, new[] {(TileOp.Sub, 4)})),
                        (TileSide.BottomRight, (null, new[] {(TileOp.Sub, 2)})));
                dataBase.indexes.AddRange([0, 1, 3, 0, 3, 2]);

                var dataSCTL = BuildRectData(group, TexType.OutSharpCornerTopLeft);
                var dataSCBL = BuildRectData(group, TexType.OutSharpCornerBottomLeft);
                var dataT = BuildRectData(group, TexType.OutEdgeTop);
                AdjustRectData(ref dataT, left: dataSCTL);
                var dataL = BuildRectData(group, TexType.OutEdgeLeft);
                AdjustRectData(ref dataL, top: dataSCTL, bottom: dataSCBL);
                var dataB = BuildRectData(group, TexType.OutEdgeBottom);
                AdjustRectData(ref dataB, left: dataSCBL);

                BuildMeshData(out verts, out uvs, out indexes, dataSCTL,
                        (dataT, TileSide.Top),
                        (dataL, TileSide.Left),
                        dataBase,
                        (dataSCBL, TileSide.BottomLeft),
                        (dataB, TileSide.Bottom));
                break;
            }
            case FloorType.DeadendTop:
            {
                var dataBase = BuildPolyData(group,
                        [TexType.FullTile, TexType.OutEdgeLeft, TexType.OutEdgeRight,
                        TexType.OutSharpCornerTopLeft, TexType.OutSharpCornerTopRight],

                        (TileSide.TopLeft, (new[] {(TileOp.Add, 3)}, new[] {(TileOp.Add, 3)})),
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 4)}, new[] {(TileOp.Add, 4)})),
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 1)}, null)),
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 2)}, null)));
                dataBase.indexes.AddRange([0, 1, 3, 0, 3, 2]);

                var dataSCTL = BuildRectData(group, TexType.OutSharpCornerTopLeft);
                var dataSCTR = BuildRectData(group, TexType.OutSharpCornerTopRight);
                var dataT = BuildRectData(group, TexType.OutEdgeTop);
                AdjustRectData(ref dataT, left: dataSCTL, right: dataSCTR);
                var dataL = BuildRectData(group, TexType.OutEdgeLeft);
                AdjustRectData(ref dataL, top: dataSCTL);
                var dataR = BuildRectData(group, TexType.OutEdgeRight);
                AdjustRectData(ref dataR, top: dataSCTR);

                BuildMeshData(out verts, out uvs, out indexes, dataSCTL,
                        (dataT, TileSide.Top),
                        (dataSCTR, TileSide.TopRight),
                        (dataL, TileSide.Left),
                        dataBase,
                        (dataR, TileSide.Right));
                break;
            }
            case FloorType.DeadendRight:
            {
                var dataBase = BuildPolyData(group,
                        [TexType.FullTile, TexType.OutEdgeTop, TexType.OutEdgeBottom,
                        TexType.OutSharpCornerTopRight, TexType.OutSharpCornerBottomRight],

                        (TileSide.TopLeft, (null, new[] {(TileOp.Add, 1)})),
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 3)}, new[] {(TileOp.Add, 3)})),
                        (TileSide.BottomLeft, (null, new[] {(TileOp.Sub, 2)})),
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 4)}, new[] {(TileOp.Sub, 4)})));
                dataBase.indexes.AddRange([0, 1, 3, 0, 3, 2]);

                var dataSCTR = BuildRectData(group, TexType.OutSharpCornerTopRight);
                var dataSCBR = BuildRectData(group, TexType.OutSharpCornerBottomRight);
                var dataT = BuildRectData(group, TexType.OutEdgeTop);
                AdjustRectData(ref dataT, right: dataSCTR);
                var dataR = BuildRectData(group, TexType.OutEdgeRight);
                AdjustRectData(ref dataR, top: dataSCTR, bottom: dataSCBR);
                var dataB = BuildRectData(group, TexType.OutEdgeBottom);
                AdjustRectData(ref dataB, right: dataSCBR);

                BuildMeshData(out verts, out uvs, out indexes,
                        (dataT, TileSide.TopLeft),
                        (dataSCTR, TileSide.TopRight),
                        dataBase,
                        (dataR, TileSide.Right),
                        (dataB, TileSide.BottomLeft),
                        (dataSCBR, TileSide.BottomRight));
                break;
            }
            case FloorType.DeadendBottom:
            {
                var dataBase = BuildPolyData(group,
                        [TexType.FullTile, TexType.OutEdgeLeft, TexType.OutEdgeRight,
                        TexType.OutSharpCornerBottomLeft, TexType.OutSharpCornerBottomRight],

                        (TileSide.TopLeft, (new[] {(TileOp.Add, 1)}, null)),
                        (TileSide.TopRight, (new[] {(TileOp.Sub, 2)}, null)),
                        (TileSide.BottomLeft, (new[] {(TileOp.Add, 3)}, new[] {(TileOp.Sub, 3)})),
                        (TileSide.BottomRight, (new[] {(TileOp.Sub, 4)}, new[] {(TileOp.Sub, 4)})));
                dataBase.indexes.AddRange([0, 1, 3, 0, 3, 2]);

                var dataSCBL = BuildRectData(group, TexType.OutSharpCornerBottomLeft);
                var dataSCBR = BuildRectData(group, TexType.OutSharpCornerBottomRight);
                var dataB = BuildRectData(group, TexType.OutEdgeBottom);
                AdjustRectData(ref dataB, left: dataSCBL, right: dataSCBR);
                var dataL = BuildRectData(group, TexType.OutEdgeLeft);
                AdjustRectData(ref dataL, bottom: dataSCBL);
                var dataR = BuildRectData(group, TexType.OutEdgeRight);
                AdjustRectData(ref dataR, bottom: dataSCBR);

                BuildMeshData(out verts, out uvs, out indexes,
                        (dataL, TileSide.TopLeft),
                        dataBase,
                        (dataR, TileSide.TopRight),
                        (dataSCBL, TileSide.BottomLeft),
                        (dataB, TileSide.Bottom),
                        (dataSCBR, TileSide.BottomRight));
                break;
            }
            default:
            {
                verts = null;
                uvs = null;
                indexes = null;
                normals = null;
                return;
            }
        }

        normals = new Float3[verts.Length];
        for (int ix = 0, siz = verts.Length; ix < siz; ++ix)
        {
            normals[ix] = Float3.Up;

            // The data was generated for a different engine originally, which has a reverse Z direction to Flax.
            var v = verts[ix];
            v.Z *= -1;
            v.Z += TileDimension;
            verts[ix] = v;
        }
    }

    private Float2[] BuildUVArrayFromData(FloorGroup group, TexType ttype)
    {
        var rect = FloorUVData[group][ttype];
        var texSize = new Float2((float)TextureSize.X, (float)TextureSize.Y);
        return [ rect.Location / texSize,
                rect.UpperRight / texSize,
                rect.BottomLeft / texSize,
                rect.BottomRight / texSize ];
    }

    private TileRectData BuildRectData(FloorGroup group, TexType ttype)
    {
        var data = new TileRectData();
        data.indexes.AddRange([0, 1, 2, 1, 3, 2]);
        Rectangle r = FloorUVData[group][ttype];
        data.verts.AddRange([
            new Float3(0.0f, 0.0f, 0.0f),
            new Float3(r.Size.X / TileSize.X * TileDimension, 0.0f, 0.0f),
            new Float3(0.0f, 0.0f, r.Size.Y / TileSize.Y * TileDimension),
            new Float3(r.Size.X / TileSize.X * TileDimension, 0.0f, r.Size.Y / TileSize.Y * TileDimension)
        ]);
        data.uvs.AddRange(BuildUVArrayFromData(group, ttype));
        return data;
    }

    // Creates a mesh build data object from the given array of [TileRectData, TileSide (or null)]
    // pairs. Makes sure the verts, uvs and indexes are properly ordered. The TileSide member
    // of the pair determines if the given vertices should be moved to be aligned some way or not.
    private void BuildMeshData(out Float3[] verts, out Float2[] uvs, out int[] indexes, params TileRectDataWithAlign[] dataparams)
    {
        int vertCount = 0;
        foreach (TileRectDataWithAlign data in dataparams)
            vertCount += data.rectdata.verts.Count;

        verts = new Float3[vertCount];
        uvs = new Float2[vertCount];

        TileRectData[] vertArray = new TileRectData[dataparams.Length];
        int ix = 0;
        int vertPos = 0;

        foreach (TileRectDataWithAlign data in dataparams)
        {
            if (data.align == TileSide.None)
                data.rectdata.verts.CopyTo(verts, vertPos);
            else
                AlignRectPoints(data.rectdata.verts, data.align, verts, vertPos);
            data.rectdata.uvs.CopyTo(uvs, vertPos);
            vertPos += data.rectdata.verts.Count;
            vertArray[ix++] = data.rectdata;
        }

        indexes = BuildIndexArray(vertArray);
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
    private TileRectData BuildPolyData(FloorGroup group, TexType[] texes, params VertModifierWithAlign[] modifiers)
    {
        List<Rectangle> rects = [];
        foreach (TexType t in texes)
            rects.Add(FloorUVData[group][t]);

        var data = new TileRectData();
        var basePos = rects[0].Location;
        var baseSize = rects[0].Size;

        foreach (VertModifierWithAlign m in modifiers)
        {
            Float3 v;
            Float2 uv;
            switch (m.initialValue)
            {
                case TileSide.TopLeft:
                    v = Float3.Zero;
                    uv = basePos / new Float2(TextureSize.X, TextureSize.Y);
                    break;
                case TileSide.TopRight:
                    v = new Float3(baseSize.X / (float)TileSize.X, 0.0f, 0.0f) * TileDimension;
                    uv = new Float2(basePos.X + baseSize.X, basePos.Y) / new Float2(TextureSize.X, TextureSize.Y);
                    break;
                case TileSide.BottomLeft:
                    v = new Float3(0.0f, 0.0f, baseSize.Y / (float)TileSize.Y) * TileDimension;
                    uv = new Float2(basePos.X, basePos.Y + baseSize.Y) / new Float2(TextureSize.X, TextureSize.Y);
                    break;
                default:
                    v = new Float3(baseSize.X / (float)TileSize.X, 0.0f, baseSize.Y / (float)TileSize.Y) * TileDimension;
                    uv = new Float2(basePos.X + baseSize.X, basePos.Y + baseSize.Y) / new Float2(TextureSize.X, TextureSize.Y);
                    break;
            }

            if (m.Xmods != null && m.Xmods.Length != 0)
            {
                foreach ((TileOp op, int index) t in m.Xmods)
                {
                    if (t.op == TileOp.Add)
                    {
                        var r = rects[t.index];
                        v.X += r.Size.X * TileDimension / (float)TileSize.X;
                        uv.X += r.Size.X / (float)TextureSize.X;
                    }
                    else if (t.op == TileOp.Sub)
                    {
                        var r = rects[t.index];
                        v.X -= r.Size.X * TileDimension / (float)TileSize.X;
                        uv.X -= r.Size.X / (float)TextureSize.X;
                    }
                }
            }
            if (m.Ymods != null && m.Ymods.Length != 0)
            {

                foreach ((TileOp op, int index) t in m.Ymods)
                {
                    if (t.op == TileOp.Add)
                    {
                        var r = rects[t.index];
                        v.Z += r.Size.Y * TileDimension / (float)TileSize.Y;
                        uv.Y += r.Size.Y / (float)TextureSize.Y;
                    }
                    else if (t.op == TileOp.Sub)
                    {
                        var r = rects[t.index];
                        v.Z -= r.Size.Y * TileDimension / (float)TileSize.Y;
                        uv.Y -= r.Size.Y / (float)TextureSize.Y;
                    }
                }
            }
            data.verts.AddRange([v]);
            data.uvs.AddRange([uv]);
        }

        return data;
    }

    
    private void AlignRectPoints(List<Float3> verts, TileSide side, Float3[] outVerts, int arrayIndex)
    {
        verts.CopyTo(outVerts, arrayIndex);

        var bounds = CalculateRectBounds(verts);

        if (bounds.Location.X != 0.0f && (side == TileSide.Left || side == TileSide.TopLeft || side == TileSide.BottomLeft))
        {
            for (int ix = arrayIndex, siz = outVerts.Length; ix < siz; ++ix)
                outVerts[ix].X -= bounds.Location.X;
        }

        if (Mathf.Abs(bounds.Location.X + bounds.Width - TileDimension) > 0.1e-6 && (side == TileSide.Right || side == TileSide.TopRight || side == TileSide.BottomRight))
        {
            var dif = TileDimension - (bounds.Location.X + bounds.Width);
            for (int ix = arrayIndex, siz = outVerts.Length; ix < siz; ++ix)
                outVerts[ix].X += dif;
        }

        if (bounds.Location.Y != 0.0f && (side == TileSide.Top || side == TileSide.TopLeft || side == TileSide.TopRight))
        {
            for (int ix = arrayIndex, siz = outVerts.Length; ix < siz; ++ix)
                outVerts[ix].Z -= bounds.Location.Y;
        }

        if (Mathf.Abs(bounds.Location.Y + bounds.Height - TileDimension) > 0.1e-6 && (side == TileSide.Bottom || side == TileSide.BottomLeft || side == TileSide.BottomRight))
        {
            var dif = TileDimension - (bounds.Location.Y + bounds.Height);
            for (int ix = arrayIndex, siz = outVerts.Length; ix < siz; ++ix)
                outVerts[ix].Z += dif;
        }
    }

    // Given a collection of vertex data, returns an array that concatenated the contents, but
    // increasing the values to not overlap. For example if first vertex indexes are [0, 1, 2],
    // the second indexes would be starting at 0 too. Increasing them by 3 avoids overlap.
    private int[] BuildIndexArray(TileRectData[] from)
    {
        int vertCount = 0;
        foreach (TileRectData data in from)
            vertCount += data.indexes.Count;
        
        var result = new int[vertCount];
        int offset = 0;
        int vertPos = 0;

        foreach (TileRectData data in from)
        {
            foreach (int i in data.indexes)
                result[vertPos++] = i + offset;
            offset += data.verts.Count;
        }

        return result;
    }

    // Calculates the bounding rectangle of a mesh of two triangles.
    private Rectangle CalculateRectBounds(Float3[] verts)
    {
        float minX = TileDimension;
        float maxX = 0.0f;
        float minY = TileDimension;
        float maxY = 0.0f;
        foreach (Float3 v in verts)
        {
            minX = Mathf.Min(v.X, minX);
            maxX = Mathf.Max(v.X, maxX);
            minY = Mathf.Min(v.Z, minY);
            maxY = Mathf.Max(v.Z, maxY);
        }
        return new Rectangle(minX, minY, maxX - minX, maxY - minY);
    }
    
    // Calculates the bounding rectangle of a mesh of two triangles.
    private Rectangle CalculateRectBounds(List<Float3> verts)
    {
        float minX = TileDimension;
        float maxX = 0.0f;
        float minY = TileDimension;
        float maxY = 0.0f;
        foreach (Float3 v in verts)
        {
            minX = Mathf.Min(v.X, minX);
            maxX = Mathf.Max(v.X, maxX);
            minY = Mathf.Min(v.Z, minY);
            maxY = Mathf.Max(v.Z, maxY);
        }
        return new Rectangle(minX, minY, maxX - minX, maxY - minY);
    }

    
    private void UpdateV3InList(List<Float3> list, int index, float xdif, float zdif)
    {
        var v = list[index];
        v.X += xdif;
        v.Z += zdif;
        list[index] = v;
    }

    private void UpdateV2InList(List<Float2> list, int index, float xdif, float ydif)
    {
        var v = list[index];
        v.X += xdif;
        v.Y += ydif;
        list[index] = v;
    }

    // Modifies the verts and uvs arrays in data, subtracting the rectangle area of the passed
    // data on left, top, right and bottom sides. There is no error checking for subtracting too much.
    private void AdjustRectData(ref TileRectData data, TileRectData? left = null, TileRectData?  top = null, TileRectData?  right = null, TileRectData?  bottom = null)
    {
        if (left is TileRectData l)
        {
            var siz = CalculateRectBounds(l.verts).Size.X;
            var usiz = siz / TileDimension * TileSize.X / TextureSize.X;
            UpdateV3InList(data.verts, 0, siz, 0.0f);
            UpdateV3InList(data.verts, 2, siz, 0.0f);
            UpdateV2InList(data.uvs, 0, usiz, 0.0f);
            UpdateV2InList(data.uvs, 2, usiz, 0.0f);
            //data.verts[0].X += siz;
            //data.verts[2].X += siz;
            //data.uvs[0].X += usiz;
            //data.uvs[2].X += usiz;
        }
        if (right is TileRectData r)
        {
            var siz = CalculateRectBounds(r.verts).Size.X;
            var usiz = siz / TileDimension * TileSize.X / TextureSize.X;
            UpdateV3InList(data.verts, 1, -siz, 0.0f);
            UpdateV3InList(data.verts, 3, -siz, 0.0f);
            UpdateV2InList(data.uvs, 1, -usiz, 0.0f);
            UpdateV2InList(data.uvs, 3, -usiz, 0.0f);
            //data.verts[1].X -= siz;
            //data.verts[3].X -= siz;
            //data.uvs[1].X -= usiz;
            //data.uvs[3].X -= usiz;
        }
        if (top is TileRectData t)
        {
            var siz = CalculateRectBounds(t.verts).Size.Y;
            var usiz = siz / TileDimension * TileSize.Y / TextureSize.Y;
            UpdateV3InList(data.verts, 0, 0.0f, siz);
            UpdateV3InList(data.verts, 1, 0.0f, siz);
            UpdateV2InList(data.uvs, 0, 0.0f, usiz);
            UpdateV2InList(data.uvs, 1, 0.0f, usiz);
            //data.verts[0].Z += siz;
            //data.verts[1].Z += siz;
            //data.uvs[0].Y += usiz;
            //data.uvs[1].Y += usiz;
        }
        if (bottom is TileRectData b)
        {
            var siz = CalculateRectBounds(b.verts).Size.Y;
            var usiz = siz / TileDimension * TileSize.Y / TextureSize.Y;
            UpdateV3InList(data.verts, 2, 0.0f, -siz);
            UpdateV3InList(data.verts, 3, 0.0f, -siz);
            UpdateV2InList(data.uvs, 2, 0.0f, -usiz);
            UpdateV2InList(data.uvs, 3, 0.0f, -usiz);
            //data.verts[2].Z -= siz;
            //data.verts[3].Z -= siz;
            //data.uvs[2].Y -= usiz;
            //data.uvs[3].Y -= usiz;
        }
    }
}

