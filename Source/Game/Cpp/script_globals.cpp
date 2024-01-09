#include "script_globals.h"
//#include "Engine/Scripting/ManagedCLR/MClass.h"
//#include "Engine/Scripting/ManagedCLR/MMethod.h"


float ScriptGlobals::tile_dimension = 200.0f;
Script *ScriptGlobals::map_navigation = nullptr;

ScriptGlobals::ScriptGlobals(const SpawnParams& params)
    : Script(params), TileDimension(200.0f)
{
    // Enable ticking OnUpdate function
    //_tickUpdate = true;
}

void ScriptGlobals::OnAwake()
{
    tile_dimension = TileDimension;
    map_navigation = MapNavigation.Get();
}
