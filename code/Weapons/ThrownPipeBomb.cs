﻿namespace ZombieHorde;

partial class ThrownPipeBomb : BasePhysics
{
	public static readonly Model WorldModel = Model.Load( "weapons/licensed/hqfpsweapons/fp_equipment/throwables/pipebomb/w_pipebomb.vmdl" );

	Particles GrenadeParticles;
	private TimeSince TimeSinceBeeped = 0;

	public override void Spawn()
	{
		base.Spawn();
		Tags.Add( "Grenade" );
		Tags.Add( "Gib" );

		Model = WorldModel;
		SetupPhysicsFromModel( PhysicsMotionType.Dynamic );

		GrenadeParticles = Particles.Create( "particles/grenade.vpcf", this, "trail_particle", true );
		GrenadeParticles.SetPosition( 0, Position );
	}

	[Event.Tick.Server]
	public void Tick()
	{
		if(TimeSinceBeeped > .4f )
		{
			TimeSinceBeeped = 0;
			PlaySound( "pipebomb.beep" );

			// lure zombies
			foreach ( CommonZombie zom in Entity.FindInSphere( Position, 1400 ).OfType<CommonZombie>() )
			{
				zom.StartLure( Position);
			}
		}
	}

	public async Task BlowIn( float seconds )
	{
		await Task.DelaySeconds( seconds );

		if ( !IsValid ) return;

		Sound.FromWorld( "grenade.explode", Position );
		PipeExplosion( this, Owner, Position, 700, 300, 1.5f );
		ZombieGame.Explosion( this, Owner, Position, 300, 60, 5f );
		Delete();
	}

	public void PipeExplosion( Entity weapon, Entity owner, Vector3 position, float radius, float damage, float forceScale )
	{
		// same thing as regular explosion but only zombies, don't check los, and don't gib
		var overlaps = Entity.FindInSphere( position, radius ).OfType<CommonZombie>();

		foreach ( var overlap in overlaps )
		{
			if ( overlap is not ModelEntity ent || !ent.IsValid() )
				continue;

			if ( ent.LifeState != LifeState.Alive )
				continue;

			if ( !ent.PhysicsBody.IsValid() )
				continue;

			if ( ent.IsWorld )
				continue;

			var targetPos = ent.PhysicsBody.MassCenter;

			var dist = Vector3.DistanceBetween( position, targetPos );
			if ( dist > radius )
				continue;

			var distanceMul = 1.0f - Math.Clamp( dist / radius, 0.0f, 1.0f );
			var dmg = damage * distanceMul;
			var force = (forceScale * distanceMul) * ent.PhysicsBody.Mass;
			var forceDir = (targetPos - position - Vector3.Down*80).Normal;

			var damageInfo = DamageInfoExt.FromCustom( position, forceDir * force, dmg, DamageFlags.DoNotGib )
				.WithWeapon( weapon )
				.WithAttacker( owner );

			ent.TakeDamage( damageInfo );
		}
	}
}
