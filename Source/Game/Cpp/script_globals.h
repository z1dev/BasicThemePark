#pragma once

#include "Engine/Scripting/Script.h"
#include "Engine/Scripting/ScriptingObjectReference.h"


API_CLASS() class GAME_API ScriptGlobals : public Script
{
API_AUTO_SERIALIZATION();
DECLARE_SCRIPTING_TYPE(ScriptGlobals);
    
    static float tile_dimension;
    static Script *map_navigation;

    // [Script]
    void OnAwake() override;

    API_FIELD() float TileDimension;
    API_FIELD() ScriptingObjectReference<Script> MapNavigation;
};
