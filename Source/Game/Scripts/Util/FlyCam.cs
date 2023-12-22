using System;
using System.Collections.Generic;
using FlaxEngine;

namespace Game;

/// <summary>
/// FlyCam Script.
/// </summary>
public class FlyCam : Script
{
    public event EventHandler MovedOrRotated;

    public float horizontalSpeed = 1000.0f;
    public float verticalSpeed = 1000.0f;
    public float rotationSpeed = 50.0f;
    public float cameraDragRotationSpeed = 0.1f;
    public float tiltSpeed = 50.0f;

    public float minTiltAngle = 25.0f;
    public float maxTiltAngle = 85.0f;

    // Whether the camera can be moved around with keys. Lock it with Lock() and unlock with Unlock()
    // when the camera needs to be manipulated from the outside. Call Unlock the number of times Lock
    // was called.
    private int cameraLocked = 0;

    /// <inheritdoc/>
    public override void OnStart()
    {
        // Here you can add code that needs to be called when script is created, just before the first game update
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
        if (cameraLocked != 0)
            return;

        bool changed = false;

        float delta = Time.DeltaTime;
        Float3 inputvec = new(Input.GetAxis("CamHorz") * horizontalSpeed, 0.0f, Input.GetAxis("CamVert") * verticalSpeed);
        Float3 camangles = Actor.Orientation.EulerAngles;
        if (!inputvec.IsZero)
        {
            Float3 input_rotation = new Float3(0.0f, camangles.Y, 0.0f);
            
            var pos = Actor.Position;
            pos += Vector3.Transform(inputvec * delta, Quaternion.Euler(input_rotation));
            Actor.Position = pos;

            changed = true;
        }

        float rotate = Input.GetAxis("CamRotation");
        if (rotate != 0.0f)
        {
            camangles.Y += rotationSpeed * delta * rotate;
            Actor.Orientation = Quaternion.Euler(camangles);

            changed = true;
        }
        
        float tilt = Input.GetAxis("CamTilt");
        if (tilt != 0.0f)
        {
            camangles.X += delta * tiltSpeed * tilt;
            camangles.X = Mathf.Clamp(camangles.X, minTiltAngle, maxTiltAngle);
            Actor.Orientation = Quaternion.Euler(camangles);

            changed = true;
        }

        if (changed)
        {
            MovedOrRotated?.Invoke(this, null);
        }
    }

    public void Lock()
    {
        ++cameraLocked;
    }

    public void Unlock()
    {
        if (cameraLocked == 0)
            throw new Exception();
        --cameraLocked;
    }
}
