#pragma once

#include "Engine/Scripting/Script.h"
#include "Engine/Core/RandomStream.h"

class Randomizer
{
public:
	static float Rand();
private:
	Randomizer();

	static void InitIfNeeded();
	static bool inited;
	static RandomStream stream;
};