#include "randomizer.h"

bool Randomizer::inited = false;
RandomStream Randomizer::stream;



void Randomizer::InitIfNeeded()
{
	if (inited)
		return;
	inited = true;
	stream.GenerateNewSeed();
}

float Randomizer::Rand()
{
	InitIfNeeded();
	return stream.Rand();
}