﻿using Sandbox;
using System.IO;

namespace ZombieHorde;

public partial class SpecialZombie : BaseZombie
{
	// nothing in here yet

	public override void Spawn()
	{
		base.Spawn();

		UpdateClothes();
		Dress();
	}
}
