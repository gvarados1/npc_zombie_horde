﻿
using System.Runtime.CompilerServices;
using static Sandbox.Event;
using static Sandbox.Package;

namespace ZombieHorde;

[Library]
public partial class BaseZomWalkController : BasePlayerController
{
	[Net] public float SprintSpeed { get; set; } = 320.0f;
	[Net] public float WalkSpeed { get; set; } = 150.0f;
	[Net] public float DefaultSpeed { get; set; } = 190.0f;
	[Net] public float Acceleration { get; set; } = 8.0f;
	[Net] public float AirAcceleration { get; set; } = 20.0f;
	[Net] public float GroundFriction { get; set; } = 4.0f;
	[Net] public float StopSpeed { get; set; } = 100.0f;
	[Net] public float GroundAngle { get; set; } = 46.0f;
	[Net] public float StepSize { get; set; } = 18.0f;
	[Net] public float MaxNonJumpVelocity { get; set; } = 140.0f;
	[Net] public float BodyGirth { get; set; } = 32.0f;
	[Net] public float BodyHeight { get; set; } = 72.0f;
	[Net] public float EyeHeight { get; set; } = 64.0f;
	[Net] public float Gravity { get; set; } = 800.0f; // 800 default
	[Net] public float AirControl { get; set; } = 80.0f;
	public bool Swimming { get; set; } = false;
	[Net] public bool AutoJump { get; set; } = false;

	[Net, Predicted] public bool IsSprinting { get; set; } = false;
	[Net, Predicted] public TimeSince TimeSinceClimb { get; set; } = 0;
	[Net, Predicted] public TimeSince TimeSinceLanded { get; set; } = 0;
	[Net, Predicted] public TimeSince TimeSinceJumped { get; set; } = 0;
	[Net, Predicted] public float LastVelocityZ { get; set; } = 0;

	public Sandbox.Entity Owner;

	public BaseZomDuck Duck;
	public Unstuck Unstuck;


	public BaseZomWalkController()
	{
		Duck = new BaseZomDuck( this );
		Unstuck = new Unstuck( this );
	}

	/// <summary>
	/// This is temporary, get the hull size for the player's collision
	/// </summary>
	public override BBox GetHull()
	{
		var girth = BodyGirth * 0.5f;
		var mins = new Vector3( -girth, -girth, 0 );
		var maxs = new Vector3( +girth, +girth, BodyHeight );

		return new BBox( mins, maxs );
	}


	// Duck body height 32
	// Eye Height 64
	// Duck Eye Height 28

	protected Vector3 mins;
	protected Vector3 maxs;

	public virtual void SetBBox( Vector3 mins, Vector3 maxs )
	{
		if ( this.mins == mins && this.maxs == maxs )
			return;

		this.mins = mins;
		this.maxs = maxs;
	}

	/// <summary>
	/// Update the size of the bbox. We should really trigger some shit if this changes.
	/// </summary>
	public virtual void UpdateBBox()
	{
		var girth = BodyGirth * 0.5f;

		var mins = new Vector3( -girth, -girth, 0 ) * Pawn.Scale;
		var maxs = new Vector3( +girth, +girth, BodyHeight ) * Pawn.Scale;

		Duck.UpdateBBox( ref mins, ref maxs, Pawn.Scale );

		SetBBox( mins, maxs );
	}

	public override TraceResult TraceBBox( Vector3 start, Vector3 end, Vector3 mins, Vector3 maxs, float liftFeet = 0.0f )
	{
		if ( liftFeet > 0 )
		{
			start += Vector3.Up * liftFeet;
			maxs = maxs.WithZ( maxs.z - liftFeet );
		}

		var tr = Trace.Ray( start + TraceOffset, end + TraceOffset )
					.Size( mins, maxs )
					.WithAnyTags( "solid", "playerclip", "passbullets", "zombie" )
					.WithoutTags( "gib" )
					.Ignore( Pawn )
					.Run();

		tr.EndPosition -= TraceOffset;
		return tr;
	}

	// bit of a hack. probably a better way to do this...
	public virtual TraceResult TraceBBoxIgnoreZom( Vector3 start, Vector3 end, Vector3 mins, Vector3 maxs, float liftFeet = 0.0f )
	{
		if ( liftFeet > 0 )
		{
			start += Vector3.Up * liftFeet;
			maxs = maxs.WithZ( maxs.z - liftFeet );
		}

		var tr = Trace.Ray( start + TraceOffset, end + TraceOffset )
					.Size( mins, maxs )
					.WithAnyTags( "solid", "playerclip", "passbullets" )
					.WithoutTags( "gib" )
					.Ignore( Pawn )
					.Run();

		tr.EndPosition -= TraceOffset;
		return tr;
	}

	protected float SurfaceFriction;


	public override void FrameSimulate()
	{
		base.FrameSimulate();

		EyeRotation = Input.Rotation;
		if ( Local.Pawn is HumanPlayer ply )
		{
			EyeRotation *= ply.ViewPunchOffset.ToRotation();
		}
	}

	public bool WasClimbing = false;
	public float ClimbHeight = 0;
	public override void Simulate()
	{
		//EyeLocalPosition = Vector3.Up * (EyeHeight * Pawn.Scale);
		var duckHeight = Duck.IsActive ? 0.75f : 1f;
		EyeLocalPosition = EyeLocalPosition.LerpTo( Vector3.Up * (EyeHeight * Pawn.Scale) * duckHeight, .5f, true );
		UpdateBBox();

		// note: I need to multiply pitch by fov/90 (90/fov?). How do I bring the fov over here?
		EyeLocalPosition += TraceOffset;
		EyeRotation = Input.Rotation * Rotation.FromPitch( 5.6f );

		if ( Host.IsServer && Owner is HumanPlayer ply )
		{
			EyeRotation *= ply.ViewPunchOffset.ToRotation();
		}
		
		if(Host.IsClient && Local.Pawn is HumanPlayer ply1 )
		{
			EyeRotation *= ply1.ViewPunchOffset.ToRotation();
		}

		RestoreGroundPos();

		//Velocity += BaseVelocity * ( 1 + Time.Delta * 0.5f );
		//BaseVelocity = Vector3.Zero;

		//Rot = Rotation.LookAt( Input.Rotation.Forward.WithZ( 0 ), Vector3.Up );

		if ( Unstuck.TestAndFix() )
			return;

		// Check Stuck
		// Unstuck - or return if stuck

		// Set Ground Entity to null if  falling faster then 250

		// store water level to compare later

		// if not on ground, store fall velocity

		// player->UpdateStepSound( player->m_pSurfaceData, mv->GetAbsOrigin(), mv->m_vecVelocity )


		// RunLadderMode

		CheckLadder();
		Swimming = Pawn.WaterLevel > 0.6f;

		//
		// Start Gravity
		//
		if ( !Swimming && !IsTouchingLadder )
		{
			Velocity -= new Vector3( 0, 0, Gravity * 0.5f ) * Time.Delta;
			Velocity += new Vector3( 0, 0, BaseVelocity.z ) * Time.Delta;

			BaseVelocity = BaseVelocity.WithZ( 0 );
		}


		/*
             if (player->m_flWaterJumpTime)
            {
	            WaterJump();
	            TryPlayerMove();
	            // See if we are still in water?
	            CheckWater();
	            return;
            }
            */

		// if ( underwater ) do underwater movement

		if ( AutoJump ? Input.Down( InputButton.Jump ) : Input.Pressed( InputButton.Jump ) )
		{
			CheckJumpButton();
		}
		else if ( Input.Down( InputButton.Jump ) )
		{
			TestClimb();
		}

		// Fricion is handled before we add in any base velocity. That way, if we are on a conveyor,
		//  we don't slow when standing still, relative to the conveyor.
		bool bStartOnGround = GroundEntity != null;
		//bool bDropSound = false;
		if ( bStartOnGround )
		{
			//if ( Velocity.z < FallSoundZ ) bDropSound = true;

			Velocity = Velocity.WithZ( 0 );
			//player->m_Local.m_flFallVelocity = 0.0f;

			if ( GroundEntity != null )
			{
				ApplyFriction( GroundFriction * SurfaceFriction );
			}
		}

		//
		// Work out wish velocity.. just take input, rotate it to view, clamp to -1, 1
		//
		WishVelocity = new Vector3( Input.Forward, Input.Left, 0 );
		var inSpeed = WishVelocity.Length.Clamp( 0, 1 );
		WishVelocity *= Input.Rotation.Angles().WithPitch( 0 ).ToRotation();

		if ( !Swimming && !IsTouchingLadder )
		{
			WishVelocity = WishVelocity.WithZ( 0 );
		}

		WishVelocity = WishVelocity.Normal * inSpeed;
		WishVelocity *= GetWishSpeed();

		Duck.PreTick();

		bool bStayOnGround = false;
		if ( Swimming )
		{
			ApplyFriction( 1 );
			WaterMove();
		}
		else if ( IsTouchingLadder )
		{
			SetTag( "climbing" );
			LadderMove();
		}
		else if ( GroundEntity != null )
		{
			bStayOnGround = true;
			WalkMove();
		}
		else if(TimeSinceClimb < .5f )
		{
			ClimbMove();
		}
		else
		{
			TestClimb();
			AirMove();
		}

		if ( WasClimbing )
		{
			if(ClimbHeight > 70 )
			{
				// re-deploy weapon if climbing a tall object
				((Pawn as HumanPlayer).ActiveChild as BaseZomWeapon).Deploy();
			}
			WasClimbing = false;
		}

		CategorizePosition( bStayOnGround );

		// FinishGravity
		if ( !Swimming && !IsTouchingLadder )
		{
			Velocity -= new Vector3( 0, 0, Gravity * 0.5f ) * Time.Delta;
		}

		if ( GroundEntity != null )
		{
			Velocity = Velocity.WithZ( 0 );
		}

		// CheckFalling(); // fall damage etc

		// Land Sound
		// Swim Sounds

		LastVelocityZ = Velocity.z;
		SaveGroundPos();

		if ( Debug )
		{
			DebugOverlay.Box( Position + TraceOffset, mins, maxs, Color.Red );
			DebugOverlay.Box( Position, mins, maxs, Color.Blue );

			var lineOffset = 0;
			if ( Host.IsServer ) lineOffset = 10;

			DebugOverlay.ScreenText( $"        Position: {Position}", lineOffset + 0 );
			DebugOverlay.ScreenText( $"        Velocity: {Velocity}", lineOffset + 1 );
			DebugOverlay.ScreenText( $"    BaseVelocity: {BaseVelocity}", lineOffset + 2 );
			DebugOverlay.ScreenText( $"    GroundEntity: {GroundEntity} [{GroundEntity?.Velocity}]", lineOffset + 3 );
			DebugOverlay.ScreenText( $" SurfaceFriction: {SurfaceFriction}", lineOffset + 4 );
			DebugOverlay.ScreenText( $"    WishVelocity: {WishVelocity}", lineOffset + 5 );
		}

	}

	public virtual float GetWishSpeed()
	{
		var ws = Duck.GetWishSpeed();
		if ( ws >= 0 ) return ws;

		if ( Input.Down( InputButton.Run ) ) return SprintSpeed;
		if ( Input.Down( InputButton.Walk ) ) return WalkSpeed;

		return DefaultSpeed;
	}

	public virtual void WalkMove()
	{
		var wishdir = WishVelocity.Normal;
		var wishspeed = WishVelocity.Length;

		WishVelocity = WishVelocity.WithZ( 0 );
		WishVelocity = WishVelocity.Normal * wishspeed;

		Velocity = Velocity.WithZ( 0 );
		Accelerate( wishdir, wishspeed, 0, Acceleration );
		Velocity = Velocity.WithZ( 0 );

		//   Player.SetAnimParam( "forward", Input.Forward );
		//   Player.SetAnimParam( "sideward", Input.Right );
		//   Player.SetAnimParam( "wishspeed", wishspeed );
		//    Player.SetAnimParam( "walkspeed_scale", 2.0f / 190.0f );
		//   Player.SetAnimParam( "runspeed_scale", 2.0f / 320.0f );

		//  DebugOverlay.Text( 0, Pos + Vector3.Up * 100, $"forward: {Input.Forward}\nsideward: {Input.Right}" );

		// Add in any base velocity to the current velocity.
		Velocity += BaseVelocity;

		try
		{
			if ( Velocity.Length < 1.0f )
			{
				Velocity = Vector3.Zero;
				return;
			}

			// first try just moving to the destination
			var dest = (Position + Velocity * Time.Delta).WithZ( Position.z );

			var pm = TraceBBox( Position, dest );

			if ( pm.Fraction == 1 )
			{
				Position = pm.EndPosition;
				StayOnGround();
				return;
			}

			StepMove();
		}
		finally
		{

			// Now pull the base velocity back out.   Base velocity is set if you are on a moving object, like a conveyor (or maybe another monster?)
			Velocity -= BaseVelocity;
		}

		StayOnGround();
	}

	public virtual void StepMove()
	{
		ZomMoveHelper mover = new ZomMoveHelper( Position, Velocity );
		mover.Trace = mover.Trace.Size( mins, maxs ).Ignore( Pawn );
		mover.MaxStandableAngle = GroundAngle;

		mover.TryMoveWithStep( Time.Delta, StepSize );

		Position = mover.Position;
		Velocity = mover.Velocity;
	}

	public virtual void Move()
	{
		ZomMoveHelper mover = new ZomMoveHelper( Position, Velocity );
		mover.Trace = mover.Trace.Size( mins, maxs ).Ignore( Pawn );
		mover.MaxStandableAngle = GroundAngle;

		mover.TryMove( Time.Delta );

		Position = mover.Position;
		Velocity = mover.Velocity;
	}

	/// <summary>
	/// Add our wish direction and speed onto our velocity
	/// </summary>
	public virtual void Accelerate( Vector3 wishdir, float wishspeed, float speedLimit, float acceleration )
	{
		// This gets overridden because some games (CSPort) want to allow dead (observer) players
		// to be able to move around.
		// if ( !CanAccelerate() )
		//     return;

		if ( speedLimit > 0 && wishspeed > speedLimit )
			wishspeed = speedLimit;

		// See if we are changing direction a bit
		var currentspeed = Velocity.Dot( wishdir );

		// Reduce wishspeed by the amount of veer.
		var addspeed = wishspeed - currentspeed;

		// If not going to add any speed, done.
		if ( addspeed <= 0 )
			return;

		// Determine amount of acceleration.
		var accelspeed = acceleration * Time.Delta * wishspeed * SurfaceFriction;

		// Cap at addspeed
		if ( accelspeed > addspeed )
			accelspeed = addspeed;

		Velocity += wishdir * accelspeed;
	}

	/// <summary>
	/// Remove ground friction from velocity
	/// </summary>
	public virtual void ApplyFriction( float frictionAmount = 1.0f )
	{
		// If we are in water jump cycle, don't apply friction
		//if ( player->m_flWaterJumpTime )
		//   return;

		// Not on ground - no friction


		// Calculate speed
		var speed = Velocity.Length;
		if ( speed < 0.1f ) return;

		// Bleed off some speed, but if we have less than the bleed
		//  threshold, bleed the threshold amount.
		float control = (speed < StopSpeed) ? StopSpeed : speed;

		// Add the amount to the drop amount.
		var drop = control * Time.Delta * frictionAmount;

		// scale the velocity
		float newspeed = speed - drop;
		if ( newspeed < 0 ) newspeed = 0;

		if ( newspeed != speed )
		{
			newspeed /= speed;
			Velocity *= newspeed;
		}

		// mv->m_outWishVel -= (1.f-newspeed) * mv->m_vecVelocity;
	}

	public virtual void CheckJumpButton()
	{
		//if ( !player->CanJump() )
		//    return false;


		/*
            if ( player->m_flWaterJumpTime )
            {
                player->m_flWaterJumpTime -= gpGlobals->frametime();
                if ( player->m_flWaterJumpTime < 0 )
                    player->m_flWaterJumpTime = 0;

                return false;
            }*/



		// If we are in the water most of the way...
		if ( Swimming )
		{
			// swimming, not jumping
			ClearGroundEntity();

			Velocity = Velocity.WithZ( 100 );

			// play swimming sound
			//  if ( player->m_flSwimSoundTime <= 0 )
			{
				// Don't play sound again for 1 second
				//   player->m_flSwimSoundTime = 1000;
				//   PlaySwimSound();
			}

			return;
		}

		if ( GroundEntity == null ) return;
		if ( TimeSinceLanded < .05f )
		{
			// slight delay before you can jump again
			// but allow climbing
			TestClimb();
			return;
		}

		/*
            if ( player->m_Local.m_bDucking && (player->GetFlags() & FL_DUCKING) )
                return false;
            */

		/*
            // Still updating the eye position.
            if ( player->m_Local.m_nDuckJumpTimeMsecs > 0u )
                return false;
            */

		ClearGroundEntity();

		// player->PlayStepSound( (Vector &)mv->GetAbsOrigin(), player->m_pSurfaceData, 1.0, true );

		// MoveHelper()->PlayerSetAnimation( PLAYER_JUMP );

		float flGroundFactor = 1.0f;
		//if ( player->m_pSurfaceData )
		{
			//   flGroundFactor = g_pPhysicsQuery->GetGameSurfaceproperties( player->m_pSurfaceData )->m_flJumpFactor;
		}

		//float flMul = 268.3281572999747f * 1.2f; // about 322
		float flMul = 290;

		float startz = Velocity.z;

		if ( Duck.IsActive )
			flMul *= 0.8f;

		Velocity = Velocity.WithZ( startz + flMul * flGroundFactor );

		//Velocity -= new Vector3( 0, 0, Gravity * 0.5f ) * Time.Delta;

		// mv->m_outJumpVel.z += mv->m_vecVelocity[2] - startz;
		// mv->m_outStepHeight += 0.15f;

		// don't jump again until released
		//mv->m_nOldButtons |= IN_JUMP;

		Rand.SetSeed( Time.Tick );
		// viewpunch when jumpping
		// this feels dumb. is there a better way to do this?
		if ( Host.IsServer && Owner is HumanPlayer ply )
			ply.ViewPunch( Rand.Float( .1f ) + -.2f, Rand.Float( 1f ) - .5f );
		if ( Host.IsClient && Local.Pawn is HumanPlayer ply1 )
			ply1.ViewPunch( Rand.Float( .1f ) + -.2f, Rand.Float( 1f ) - .5f );
		AddEvent( "jump" );

		// sound
		var tr = Trace.Ray( Position, Position + Vector3.Down * 20 )
		.Radius( 1 )
		.Ignore( Pawn )
		.Run();

		if ( tr.Hit )
			tr.Surface.DoFootstep( Pawn, tr, 1, 10 );
		TimeSinceJumped = 0;
	}

	[Net, Predicted] public Vector3 ClimbForward { get; set; }
	[Net, Predicted] public Vector3 LastGroundPos { get; set; }
	public Vector3 ClimbTargetPos { get; set; }
	public virtual void TestClimb()
	{
		// check if we're trying to go forward while in the air
		if ( !Input.Down( InputButton.Forward ) && TimeSinceJumped > .5f ) return;
		if ( TimeSinceClimb < 0 ) return;

		// simple bbox trace
		var tr = TraceBBoxIgnoreZom( Position + Vector3.Up * StepSize, Position + Vector3.Up * StepSize + Rotation.Forward.WithZ( 0 ).Normal * 10, 2 );
		if ( tr.Hit )
		{
			ClimbForward = Rotation.Forward.WithZ( 0 ).Normal;

			// another trace up to see how far we should go
			var maxHeight = 110;
			var minHeight = StepSize;
			var adjustedMaxHeight = maxHeight + 72;
			var trCheck = TraceBBoxIgnoreZom( Position, Position + Vector3.Up * adjustedMaxHeight );

			if ( trCheck.Distance < minHeight ) return;

			// note: we should probably do incremental traces at different heights to check for windows that we can climb through.
			// this shouldn't really matter with such a low climb height though?

			// trace forward from hit level
			var trCheck2 = TraceBBoxIgnoreZom( trCheck.EndPosition, trCheck.EndPosition + ClimbForward * 10 );
			// if we hit less than 10 units forwards that means it's a dumb ledge that we shouldn't climb to
			if ( trCheck2.Hit ) return;

			// reset ground pose if we're on the ground
			if ( GroundEntity != null )
			{
				LastGroundPos = Position;
				GroundEntity = null;
			}

			// final check, trace down to find height
			var trCheck3 = TraceBBoxIgnoreZom( trCheck2.EndPosition, trCheck2.EndPosition + Vector3.Down * adjustedMaxHeight );
			ClimbHeight = trCheck3.EndPosition.z - LastGroundPos.z;
			// make sure we're not climbing down somehow lol
			if ( trCheck3.EndPosition.z - Position.z < 0 ) return;
			if( ClimbHeight > maxHeight ) return;

			ClimbTargetPos = trCheck3.HitPosition;
			TimeSinceClimb = 0;
			Sound.FromWorld( "player.crouch", Position );
			//ClimbMove();
		}
	}
	public virtual void ClimbMove()
	{
		// move forward once we reached the top
		if ( TimeSinceClimb > Time.Delta * 2 )
		{
			Velocity = ClimbForward * 60;
			if ( TimeSinceClimb > Time.Delta * 4 )
				Velocity += Vector3.Down * 60;
			Move();
			return;
		}

		WasClimbing = true;
		Duck.IsActive = true;

		var climbVelocity = 200;
		Velocity = Vector3.Up * climbVelocity;
		Velocity += ClimbForward * 60;

		Move();

		// something went wrong if we're above our target position...
		if ( ClimbTargetPos.z + 8 < Position.z )
			return;

		// constantly check if we should still be climbing. Will prevent getting stuck floating forever lol
		var trCheck = TraceBBoxIgnoreZom( Position + Vector3.Down * 4, Position + Vector3.Down * 4 + ClimbForward * 10, 2 );
		if ( trCheck.Hit )
		{
			var trCheck2 = TraceBBoxIgnoreZom( Position, Position + Vector3.Up * StepSize, 2 );
			if ( !trCheck2.Hit )
				TimeSinceClimb = 0;
		}
	}
	public virtual void AirMove()
	{
		var wishdir = WishVelocity.Normal;
		var wishspeed = WishVelocity.Length;

		Accelerate( wishdir, wishspeed, AirControl, AirAcceleration );

		Velocity += BaseVelocity;

		Move();

		Velocity -= BaseVelocity;
	}

	public virtual void WaterMove()
	{
		var wishdir = WishVelocity.Normal;
		var wishspeed = WishVelocity.Length;

		wishspeed *= 0.8f;

		Accelerate( wishdir, wishspeed, 100, Acceleration );

		Velocity += BaseVelocity;

		Move();

		Velocity -= BaseVelocity;
	}

	bool IsTouchingLadder = false;
	Vector3 LadderNormal;

	public virtual void CheckLadder()
	{
		var wishvel = new Vector3( Input.Forward, Input.Left, 0 );
		wishvel *= Input.Rotation.Angles().WithPitch( 0 ).ToRotation();
		wishvel = wishvel.Normal;

		if ( IsTouchingLadder )
		{
			if ( Input.Pressed( InputButton.Jump ) )
			{
				Velocity = LadderNormal * 100.0f;
				IsTouchingLadder = false;

				return;

			}
			else if ( GroundEntity != null && LadderNormal.Dot( wishvel ) > 0 )
			{
				IsTouchingLadder = false;

				return;
			}
		}

		const float ladderDistance = 1.0f;
		var start = Position;
		Vector3 end = start + (IsTouchingLadder ? (LadderNormal * -1.0f) : wishvel) * ladderDistance;

		var pm = Trace.Ray( start, end )
					.Size( mins, maxs )
					.WithTag( "ladder" )
					.Ignore( Pawn )
					.Run();

		IsTouchingLadder = false;

		if ( pm.Hit )
		{
			IsTouchingLadder = true;
			LadderNormal = pm.Normal;
		}
	}

	public virtual void LadderMove()
	{
		var velocity = WishVelocity;
		float normalDot = velocity.Dot( LadderNormal );
		var cross = LadderNormal * normalDot;
		Velocity = (velocity - cross) + (-normalDot * LadderNormal.Cross( Vector3.Up.Cross( LadderNormal ).Normal ));

		Move();
	}


	public virtual void CategorizePosition( bool bStayOnGround )
	{
		SurfaceFriction = 1.0f;

		// Doing this before we move may introduce a potential latency in water detection, but
		// doing it after can get us stuck on the bottom in water if the amount we move up
		// is less than the 1 pixel 'threshold' we're about to snap to.	Also, we'll call
		// this several times per frame, so we really need to avoid sticking to the bottom of
		// water on each call, and the converse case will correct itself if called twice.
		//CheckWater();

		var point = Position - Vector3.Up * 2;
		var vBumpOrigin = Position;

		//
		//  Shooting up really fast.  Definitely not on ground trimed until ladder shit
		//
		bool bMovingUpRapidly = Velocity.z > MaxNonJumpVelocity;
		bool bMovingUp = Velocity.z > 0;

		bool bMoveToEndPos = false;

		if ( GroundEntity != null ) // and not underwater
		{
			bMoveToEndPos = true;
			point.z -= StepSize;
		}
		else if ( bStayOnGround )
		{
			bMoveToEndPos = true;
			point.z -= StepSize;
		}

		if ( bMovingUpRapidly || Swimming ) // or ladder and moving up
		{
			ClearGroundEntity();
			return;
		}

		var pm = TraceBBox( vBumpOrigin, point, 4.0f );

		if ( pm.Entity == null || Vector3.GetAngle( Vector3.Up, pm.Normal ) > GroundAngle )
		{
			ClearGroundEntity();
			bMoveToEndPos = false;

			if ( Velocity.z > 0 )
				SurfaceFriction = 0.25f;
		}
		else
		{
			UpdateGroundEntity( pm );
		}

		if ( bMoveToEndPos && !pm.StartedSolid && pm.Fraction > 0.0f && pm.Fraction < 1.0f )
		{
			Position = pm.EndPosition;
		}

	}

	/// <summary>
	/// We have a new ground entity
	/// </summary>
	public virtual void UpdateGroundEntity( TraceResult tr )
	{
		GroundNormal = tr.Normal;

		// VALVE HACKHACK: Scale this to fudge the relationship between vphysics friction values and player friction values.
		// A value of 0.8f feels pretty normal for vphysics, whereas 1.0f is normal for players.
		// This scaling trivially makes them equivalent.  REVISIT if this affects low friction surfaces too much.
		SurfaceFriction = tr.Surface.Friction * 1.25f;
		if ( SurfaceFriction > 1 ) SurfaceFriction = 1;

		//if ( tr.Entity == GroundEntity ) return;

		Vector3 oldGroundVelocity = default;
		if ( GroundEntity != null ) oldGroundVelocity = GroundEntity.Velocity;

		bool wasOffGround = GroundEntity == null;

		var prevGround = GroundEntity;
		GroundEntity = tr.Entity;
		if(GroundEntity != null && prevGround == null )
		{
			TimeSinceLanded = 0;
			// sound
			var tr1 = Trace.Ray( Position, Position + Vector3.Down * 20 )
			.Radius( 1 )
			.Ignore( Pawn )
			.Run();

			if ( tr1.Hit )
				tr.Surface.DoFootstep( Pawn, tr1, 1, 10 );

			// fall damage
			var fallDamage = 0f;
			if(LastVelocityZ < -600 )
			{
				fallDamage = -LastVelocityZ/600;
				fallDamage *= 10;
				fallDamage = MathF.Round( fallDamage );
				var damageInfo = DamageInfoExt.FromCustom( Position, 0, fallDamage, DamageFlags.Fall );

				Pawn.TakeDamage( damageInfo );
				Velocity = 0;
				Sound.FromWorld( "human.falldamage", Position );
			}

			// viewpunch when landing
			Rand.SetSeed( Time.Tick );

			if(fallDamage > 0 )
			{
				if ( Host.IsServer && Owner is HumanPlayer ply )
					ply.ViewPunch( Rand.Float( .1f ) + 4.2f, Rand.Float( .3f ) - 2.15f );
				if ( Host.IsClient && Local.Pawn is HumanPlayer ply1 )
					ply1.ViewPunch( Rand.Float( .1f ) + 4.2f, Rand.Float( .3f ) - 2.15f );
			}
			else
			{
				if ( Host.IsServer && Owner is HumanPlayer ply )
					ply.ViewPunch( Rand.Float( .1f ) + .2f, Rand.Float( .3f ) - .15f );
				if ( Host.IsClient && Local.Pawn is HumanPlayer ply1 )
					ply1.ViewPunch( Rand.Float( .1f ) + .2f, Rand.Float( .3f ) - .15f );
			}
		}

		if ( GroundEntity != null )
		{
			BaseVelocity = GroundEntity.Velocity;
		}

		/*
              	m_vecGroundUp = pm.m_vHitNormal;
            player->m_surfaceProps = pm.m_pSurfaceProperties->GetNameHash();
            player->m_pSurfaceData = pm.m_pSurfaceProperties;
            const CPhysSurfaceProperties *pProp = pm.m_pSurfaceProperties;

            const CGameSurfaceProperties *pGameProps = g_pPhysicsQuery->GetGameSurfaceproperties( pProp );
            player->m_chTextureType = (int8)pGameProps->m_nLegacyGameMaterial;
            */
	}

	/// <summary>
	/// We're no longer on the ground, remove it
	/// </summary>
	public virtual void ClearGroundEntity()
	{
		if ( GroundEntity == null ) return;

		GroundEntity = null;
		GroundNormal = Vector3.Up;
		SurfaceFriction = 1.0f;
		LastGroundPos = Position;
	}

	/// <summary>
	/// Traces the current bbox and returns the result.
	/// liftFeet will move the start position up by this amount, while keeping the top of the bbox at the same
	/// position. This is good when tracing down because you won't be tracing through the ceiling above.
	/// </summary>
	public override TraceResult TraceBBox( Vector3 start, Vector3 end, float liftFeet = 0.0f )
	{
		return TraceBBox( start, end, mins, maxs, liftFeet );
	}
	public virtual TraceResult TraceBBoxIgnoreZom( Vector3 start, Vector3 end, float liftFeet = 0.0f )
	{
		return TraceBBoxIgnoreZom( start, end, mins, maxs, liftFeet );
	}

	/// <summary>
	/// Try to keep a walking player on the ground when running down slopes etc
	/// </summary>
	public virtual void StayOnGround()
	{
		var start = Position + Vector3.Up * 2;
		var end = Position + Vector3.Down * StepSize;

		// See how far up we can go without getting stuck
		var trace = TraceBBox( Position, start );
		start = trace.EndPosition;

		// Now trace down from a known safe position
		trace = TraceBBox( start, end );

		if ( trace.Fraction <= 0 ) return;
		if ( trace.Fraction >= 1 ) return;
		if ( trace.StartedSolid ) return;
		if ( Vector3.GetAngle( Vector3.Up, trace.Normal ) > GroundAngle ) return;

		// This is incredibly hacky. The real problem is that trace returning that strange value we can't network over.
		// float flDelta = fabs( mv->GetAbsOrigin().z - trace.m_vEndPos.z );
		// if ( flDelta > 0.5f * DIST_EPSILON )

		Position = trace.EndPosition;
	}

	void RestoreGroundPos()
	{
		if ( GroundEntity == null || GroundEntity.IsWorld )
			return;

		//var Position = GroundEntity.Transform.ToWorld( GroundTransform );
		//Pos = Position.Position;
	}

	void SaveGroundPos()
	{
		if ( GroundEntity == null || GroundEntity.IsWorld )
			return;

		//GroundTransform = GroundEntity.Transform.ToLocal( new Transform( Pos, Rot ) );
	}

}
