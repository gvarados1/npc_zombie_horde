﻿namespace ZombieHorde;

public partial class HumanWalkController : WalkController
	{
	public HumanWalkController()
	{
		WalkSpeed = 240;
		SprintSpeed = 140;
		DefaultSpeed = 240;
		AirAcceleration = 10;
	}
}

