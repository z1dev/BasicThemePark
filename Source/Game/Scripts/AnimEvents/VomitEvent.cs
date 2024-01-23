using FlaxEngine;
using System.Collections.Generic;


namespace Game;


public class BoolParamChangeEvent : AnimEvent
{
    public string paramName;
    public bool paramChecked;

    public override void OnEvent(AnimatedModel actor, Animation anim, float time, float deltaTime)
    {
        actor.SetParameterValue(paramName, paramChecked);
    }
}



/// <summary>
/// VomitEvent Script.
/// </summary>
public class VomitEvent : AnimContinuousEvent
{
    public override void OnBegin(AnimatedModel actor, Animation anim, float time, float deltaTime)
    {
        var visitorScript = actor.GetScript<VisitorBehavior>();

        StaticModel model = visitorScript.GetBarfModel();

        model.Position = actor.Position;
        model.Orientation = actor.Orientation;
        model.IsActive = true;

        visitorScript.BarfModel = model;

        var map = MapGlobals.TileMap;

        model = map.Actor.AddChild<StaticModel>();
        model.Model = AssetGlobals.Vomit;
        model.Position = actor.Position - Vector3.Forward * actor.Orientation * 45f;
        model.Scale = new Float3(0.1f);
        visitorScript.VomitModel = model;
    }

    public override void OnEvent(AnimatedModel actor, Animation anim, float time, float deltaTime)
    {
        var visitorScript = actor.GetScript<VisitorBehavior>();
        StaticModel model = visitorScript.BarfModel;

        var mat = model.GetMaterial(0);
        mat.SetParameterValue("UVOffset", (float)mat.GetParameterValue("UVOffset") - 6f * deltaTime);

        model = visitorScript.VomitModel;
        model.Scale = new Float3(Mathf.Min(1.0f, model.Scale.X + 0.8f * deltaTime));
    }

    public override void OnEnd(AnimatedModel actor, Animation anim, float time, float deltaTime)
    {
        var visitorScript = actor.GetScript<VisitorBehavior>();

        visitorScript.HideBarfModel();

        var model = visitorScript.VomitModel;
        visitorScript.VomitModel = null;
        model.Scale = new Float3(1.0f);

    }
}

