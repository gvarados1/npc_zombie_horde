﻿using Sandbox;
using Sandbox.Component;
using Sandbox.Internal;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.Design;

namespace ZombieHorde;

public partial class Flames : ModelEntity
{
	public static readonly Model WorldModel = Model.Load( "models/gameplay/volumes/molotov_physics.vmdl" );

	public TimeUntil TimeUntilExpire = 16;
	public Particles Particle;
	public Sound Sound;
	public float BurnRadius = 150;
	public PointLightEntity Light;
	public TimeSince TimeSinceTickedPlayers = 0;

	public override void Spawn()
	{
		base.Spawn();

		// invisible block for world physics collisions
		Model = WorldModel;

		PhysicsEnabled = true;
		UsePhysicsCollision = true;

		Tags.Add( "trigger" );

		Particle = Particles.Create( "particles/fire_molotov_01.vpcf", this );
		Sound = Sound.FromEntity( "molotov.burn_loop", this );

		Light = new PointLightEntity()
		{
			Color = Color.Orange,
			Brightness = 5,
			Range = 300,
			Position = this.Position + Vector3.Up * 32,
			//Falloff = .1f,
			LinearAttenuation = 1,
			QuadraticAttenuation = 0f,
			Parent = this,
		};
	}

	[Event.Tick]
	public void Tick()
	{
		if ( !IsServer ) return;
		if ( TimeUntilExpire < 0 )
			Expire();

		// light flicker
		var brightnessOffset = MathF.Sin( Time.Tick ) * .3f;
		if (TimeUntilExpire < 2 )
		{
			Light.Brightness = TimeUntilExpire * 2.5f + brightnessOffset * .2f;
		}
		else
		{
			Light.Brightness = 4.5f + brightnessOffset;
		}

		//DebugOverlay.Sphere( Position, BurnRadius, Color.Red );
		var zombies = Entity.FindInSphere( Position, BurnRadius ).OfType<CommonZombie>();

		foreach ( var zom in zombies )
		{
			zom.Ignite();
		}

		if(TimeSinceTickedPlayers > .5f )
		{
			TimeSinceTickedPlayers = 0;

			var raduis = 100;
			var players = Entity.FindInSphere( Position, raduis ).OfType<HumanPlayer>();

			foreach ( var ply in players )
			{
				var damageInfo = DamageInfo.Generic( 1 );
				ply.TakeDamage( damageInfo );
				//todo: replace with a burn sound
				ply.PlaySound( "sounds/impacts/impact-bullet-flesh.sound" );
			}

		}
	}

	public void Expire()
	{
		Sound.Stop();
		Particle.Destroy();
		Delete();
	}

}
