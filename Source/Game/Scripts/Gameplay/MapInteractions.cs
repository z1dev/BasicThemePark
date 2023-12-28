using System;
using System.Collections.Generic;
using System.Numerics;
using FlaxEngine;
using Scripts;

namespace Game;

using FloorGroup = TileGenerator.FloorGroup;

using Vector3 = FlaxEngine.Vector3;
using Quaternion = FlaxEngine.Quaternion;
using ItemType = TileMap.ItemType;

/// <summary>
/// MapInteractions Script.
/// </summary>
public class MapInteractions : Script
{

    public Camera camera = null;
    public TileMap tileMap = null;

    private FlyCam flyCam = null;

    private enum Interaction {
        None,
        CameraMove,
        CameraRotate,
        TilePlacement,
        TreePlacement,
        Selecting
    }

    private enum ToolSelection {
        Road,
        Tree,
        Demolish,

        MaxValue
    }

    // The tile coordinates under the cursor on the last frame or starting an operation, like placing a road.
    private Int2 activeCoords = new(-1, -1);
    private Int2 endCoords = new(-1, -1);

    // Whether to flip the vertical and horizontal strip during tile placement.
    private bool placeTilesFlipped = false;
    // Changes the `placeTilesFlipped` value. When it's true, the temporary tile placement will update even
    // if the mouse didn't move.
    private bool flipReceived = false;

    // Used during some operations that need direct coordinates in world units. It's not updated
    // any other time.
    private Vector3 worldPlaneCoords = new(0.0, 0.0, 0.0);
    // Used during some operations that need to save screen coordinates. It's not updated any other time.
    private Float2 screenCoords = new(0.0f, 0.0f);

    // Set to true when the tile under the mouse might have changed due to the world (or camera) moving.
    private bool needsTileUpdate = false;

    private Interaction interaction;
    private ToolSelection tool;

    /// <inheritdoc/>
    public override void OnStart()
    {
        flyCam = camera.GetScript<FlyCam>();
        if (flyCam != null)
            flyCam.MovedOrRotated += new EventHandler(CameraMovedOrRotated);
    }

    private void HandleMouseWheel(Float2 f2, float f)
    {
        if (interaction == Interaction.TilePlacement)
        {
            placeTilesFlipped = !placeTilesFlipped;
            flipReceived = true;
        }
    }
    
    /// <inheritdoc/>
    public override void OnEnable()
    {
        Input.MouseWheel += HandleMouseWheel;
    }

    /// <inheritdoc/>
    public override void OnDisable()
    {
        Input.MouseWheel -= HandleMouseWheel;
    }

    /// <inheritdoc/>
    public override void OnUpdate()
    {
        if (interaction == Interaction.None && (needsTileUpdate || Input.MousePositionDelta != Float2.Zero))
        {
            needsTileUpdate = false;

            Int2 tilepos = MouseTile();

            if (tilepos != activeCoords)
            {
                activeCoords = tilepos;

                switch (tool)
                {
                    case ToolSelection.Road:
                        tileMap.ShowTemporaryTile(activeCoords, FloorGroup.WalkwayOnGrass);
                        break;
                    case ToolSelection.Tree:
                        tileMap.ShowTemporaryModel(activeCoords, ItemType.Tree);
                        break;
                    case ToolSelection.Demolish:
                        tileMap.ShowDemolishObjects(activeCoords, activeCoords);
                        break;
                }
            }
        }

        if (interaction == Interaction.None && Input.GetMouseButtonDown(MouseButton.Left))
        {
            Int2 tilePos = MouseTile();
            FloorGroup group = tileMap.TileGroupAt(tilePos);
            if (group == FloorGroup.Grass)
            {
                if (tool == ToolSelection.Road)
                {
                    interaction = Interaction.TilePlacement;
                    activeCoords = tilePos;
                    placeTilesFlipped = false;
                    endCoords = new(-1, -1);
                }
                if (tool == ToolSelection.Tree)
                {
                    interaction = Interaction.TreePlacement;
                    activeCoords = tilePos;
                    tileMap.PlaceModel(activeCoords, ItemType.Tree);
                }
                if (tool == ToolSelection.Demolish)
                {
                    interaction = Interaction.Selecting;
                    activeCoords = tilePos;
                    tileMap.ShowDemolishObjects(activeCoords, activeCoords);
                }
            }
        }

        if (interaction == Interaction.None && Input.GetMouseButtonDown(MouseButton.Right))
        {
            interaction = Interaction.CameraMove;
            worldPlaneCoords = MouseFloorPos();
            flyCam.Lock();
        }

        if (interaction == Interaction.None && Input.GetMouseButtonDown(MouseButton.Middle))
        {
            interaction = Interaction.CameraRotate;
            screenCoords = Input.MousePosition;
            // Saved to move camera to appear like it rotates around the screen center.
            worldPlaneCoords = FloorPos(Screen.Size / 2.0f);
            flyCam.Lock();

            Screen.CursorLock = CursorLockMode.Locked;
            Screen.CursorVisible = false;
        }

        if (interaction == Interaction.None && Input.GetAction("SwitchBuild"))
        {
            tool++;
            if (tool == ToolSelection.MaxValue)
                tool = ToolSelection.Road;
            tileMap.HideTemporaryModels();
            tileMap.DeselectAll();
            needsTileUpdate = true;
            activeCoords.Y = -1;
        }

        if (interaction != Interaction.None)
        {
            if (interaction == Interaction.CameraMove)
            {
                if (Input.GetMouseButtonUp(MouseButton.Right) || !Input.GetMouseButton(MouseButton.Right))
                {
                    interaction = Interaction.None;
                    needsTileUpdate = true;
                    flyCam.Unlock();
                }
                else 
                {
                    var moveDelta = worldPlaneCoords - MouseFloorPos();
                    if (Math.Abs(moveDelta.X) > 0.1e-4 || Math.Abs(moveDelta.Y) > 0.1e-4)
                        camera.Position += moveDelta;
                }
            }

            if (interaction == Interaction.CameraRotate)
            {
                if (Input.GetMouseButtonUp(MouseButton.Middle) || !Input.GetMouseButton(MouseButton.Middle))
                {
                    interaction = Interaction.None;
                    needsTileUpdate = true;
                    flyCam.Unlock();
                    Screen.CursorLock = CursorLockMode.None;
                    Input.MousePosition = screenCoords;
                    Screen.CursorVisible = true;
                }
                else 
                {
                    Float3 camangles = camera.Orientation.EulerAngles;
                    camangles.Y += Input.GetAxis("CamMouseRot") * flyCam.cameraDragRotationSpeed;
                    camera.Orientation = Quaternion.Euler(camangles);

                    // Move camera to appear like it rotates around the screen center.
                    var moveDelta = worldPlaneCoords - FloorPos(Screen.Size / 2.0f);
                    if (Math.Abs(moveDelta.X) > 0.1e-4 || Math.Abs(moveDelta.Y) > 0.1e-4)
                        camera.Position += moveDelta;
                }
            }

            if (interaction == Interaction.TilePlacement)
            {
                if (Input.GetMouseButtonUp(MouseButton.Left))
                {
                    interaction = Interaction.None;
                    Int2 tilePos = MouseTile();
                    tileMap.PlaceTileSpan(activeCoords, tilePos, FloorGroup.WalkwayOnGrass, placeTilesFlipped);
                    activeCoords = tilePos;
                    needsTileUpdate = true;
                }
                else if (!Input.GetMouseButton(MouseButton.Left) || Input.GetMouseButtonDown(MouseButton.Right))
                {
                    interaction = Interaction.None;
                    tileMap.HideTemporaryModels();
                }
                else
                {
                    if (Input.GetMouseButtonDown(MouseButton.Middle))
                    {
                        placeTilesFlipped = !placeTilesFlipped;
                        flipReceived = true;
                    }
                    Int2 tilePos = MouseTile();
                    if (tilePos != endCoords || flipReceived)
                        tileMap.ShowTemporaryTileSpan(activeCoords, tilePos, FloorGroup.WalkwayOnGrass, placeTilesFlipped, flipReceived);
                    flipReceived = false;
                    endCoords = tilePos;
                }
            }

            if (interaction == Interaction.TreePlacement)
            {
                if (Input.GetMouseButtonUp(MouseButton.Left) || !Input.GetMouseButton(MouseButton.Left))
                {
                    interaction = Interaction.None;
                    needsTileUpdate = true;
                }
            }

            if (interaction == Interaction.Selecting)
            {
                if (Input.GetMouseButtonUp(MouseButton.Left) || !Input.GetMouseButton(MouseButton.Left) || Input.GetMouseButtonDown(MouseButton.Right))
                {
                    tileMap.DeselectAll();
                    interaction = Interaction.None;
                    needsTileUpdate = true;
                    activeCoords.Y = -1;
                }
                else
                {
                    Int2 tilePos = MouseTile();
                    tileMap.ShowDemolishObjects(activeCoords, tilePos);
                }
            }
        }
    }

    private void CameraMovedOrRotated(object sender, EventArgs args)
    {
        needsTileUpdate = true;
    }

    private Int2 MouseTile()
    {
        Float2 mousepos = Input.MousePosition;
        var ray = camera.ConvertMouseToRay(mousepos);
        return tileMap.FindTilePosition(ray);
    }

    private Vector3 MouseFloorPos()
    {
        Float2 mousepos = Input.MousePosition;
        var ray = camera.ConvertMouseToRay(mousepos);
        return ray.GetPoint(ray.Position.Y / -ray.Direction.Y);
    }

    private Vector3 FloorPos(Float2 screenCoordinates)
    {
        var ray = camera.ConvertMouseToRay(screenCoordinates);
        return ray.GetPoint(ray.Position.Y / -ray.Direction.Y);
    }

}
