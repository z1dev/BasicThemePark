using FlaxEngine;

namespace Game;

using Vector3 = FlaxEngine.Vector3;

/// <summary>
/// BusController Script.
/// </summary>
public class BusController : Script
{

    public Prefab busPrefab;
    // Number of seconds that need to pass until the bus appears at the start of the lane again.
    public float spawnInterval;
    public Transform spawnTransform;

    // World units to add to the front position of the bus for the spawn point in front of the door.
    public float doorOffset = 0.0f;
    
    public float moveSpeed = 500.0f;
    public float acceleration = 400.0f;
    public float wheelSpeedMultiplier = 2.0f;

    public Prefab visitorPrefab;

    private float stopXPosition = 0.0f;

    private Actor busActor = null;
    private float timeTillNextSpawn = 0;
    private bool busSpawned = false;
    private bool arriving = true;

    private float stopDistance = 0.0f;
    private float stopTime = 0.0f;
    private float timeToFullStop = 0.0f;

    private int visitorCount = 1000;


    /// <inheritdoc/>
    public override void OnStart()
    {
        if (busPrefab != null)
        {
            busActor = PrefabManager.SpawnPrefab(busPrefab, null);
            timeTillNextSpawn = Mathf.Max(0.0f, spawnInterval);
            busActor = busActor.FindActor<AnimatedModel>("bus");
            if (busActor != null)
                (busActor as AnimatedModel).SetParameterValue("WheelMultiplier", wheelSpeedMultiplier);
            stopTime = moveSpeed / acceleration;
            stopDistance = moveSpeed * stopTime * 0.5f;
        }
        
        stopXPosition = MapGlobals.EntryTiles[^1] * MapGlobals.TileDimension;
        // foreach (var pos in MapGlobals.EntryTiles)
        // {
        //     stopXPosition = Math.Max(stopXPosition, pos * MapGlobals.TileDimension );
        // }
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
        if (busActor == null)
            return;
        
        var delta = Time.DeltaTime;
        if (!busSpawned)
        {
            timeTillNextSpawn -= delta;
            if (timeTillNextSpawn <= 0)
            {
                timeTillNextSpawn = spawnInterval;
                busActor.SetParent(Actor, false);
                busActor.Transform = spawnTransform;
                (busActor as AnimatedModel).SetParameterValue("RunSpeed", 1.0f);
                busSpawned = true;
                arriving = true;
            }
            return;
        }

        if (arriving)
        {
            if (busActor.Position.X - stopDistance > stopXPosition)
            {
                busActor.Position -= new Vector3(moveSpeed * delta, 0.0, 0.0);
                if (busActor.Position.X < stopXPosition)
                {
                    // We overshot (because of frame rate?) Stop immediately.
                    arriving = false;
                    var p = busActor.Position;
                    p.X = stopXPosition;
                    busActor.Position = p;
                    timeTillNextSpawn = 0.5f;
                }
                else
                {
                    // Calculate the time that would have passed since starting of decceleration.

                    var distance = busActor.Position.X - stopXPosition;
                    timeToFullStop = Mathf.Sqrt(distance * 2.0f * acceleration) / acceleration;
                }
            }
            else
            {
                // Decceleration after nearing the stopping point.
                timeToFullStop -= delta;
                if (timeToFullStop <= 0)
                {
                    arriving = false;
                    var p = busActor.Position;
                    p.X = stopXPosition;
                    busActor.Position = p;
                    timeToFullStop = 0.0f;
                    (busActor as AnimatedModel).SetParameterValue("RunSpeed", 0.0f);
                    timeTillNextSpawn = 0.5f;
                }
                else
                {
                    var p = busActor.Position;
                    var distance = timeToFullStop * acceleration * timeToFullStop * 0.5f;
                    p.X = stopXPosition + distance;
                    (busActor as AnimatedModel).SetParameterValue("RunSpeed", distance / stopDistance);
                    busActor.Position = p;
                }
            }
            return;
        }

        if (visitorCount <= 0)
            return;
        timeTillNextSpawn -= Time.DeltaTime;
        if (timeTillNextSpawn > 0)
            return;
        visitorCount--;
        timeTillNextSpawn += 0.1f;

        var visitorActor = PrefabManager.SpawnPrefab(visitorPrefab, null);
        visitorActor = visitorActor.FindActor<AnimatedModel>("visitor") ?? visitorActor;
        //var pos = busActor.Position;
        //pos.Z += MapGlobals.TileDimension * 0.5f;
        //pos.Y = 0.0f;
        //pos.X += doorOffset;
        //visitorActor.Position = pos;
        visitorActor.SetParent(Actor, false);
    }


}
