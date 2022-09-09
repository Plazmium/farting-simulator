using Sandbox;

partial class SandboxPlayer : Player
{
	private TimeSince timeSinceJumpReleased;

	private DamageInfo lastDamage;

	private Sound sound;
	private Sound deathSound;


	/// <summary>
	/// The clothing container is what dresses the citizen
	/// </summary>
	public ClothingContainer Clothing = new();

	/// <summary>
	/// Default init
	/// </summary>
	public SandboxPlayer()
	{
	}

	/// <summary>
	/// Initialize using this client
	/// </summary>
	public SandboxPlayer( Client cl )
	{
		// Load clothing from client data
		Clothing.LoadFromClient( cl );
	}

	public override void Respawn()
	{
		SetModel( "models/citizen/citizen.vmdl" );

		Controller = new WalkController();

		if ( DevController is NoclipController )
		{
			DevController = null;
		}

		EnableAllCollisions = true;
		EnableDrawing = true;
		EnableHideInFirstPerson = true;
		EnableShadowInFirstPerson = true;

		Clothing.DressEntity( this );

		CameraMode = new FirstPersonCamera();

		base.Respawn();
	}

	public override void OnKilled()
	{
		base.OnKilled();

		BecomeRagdollOnClient( Velocity, lastDamage.Flags, lastDamage.Position, lastDamage.Force, GetHitboxBone( lastDamage.HitboxIndex ) );

		Controller = null;

		EnableAllCollisions = false;
		EnableDrawing = false;

		CameraMode = new SpectateRagdollCamera();

		foreach ( var child in Children )
		{
			child.EnableDrawing = false;
		}
	}

	public override void TakeDamage( DamageInfo info )
	{
		if ( GetHitboxGroup( info.HitboxIndex ) == 1 )
		{
			info.Damage *= 10.0f;
		}

		lastDamage = info;

		TookDamage( lastDamage.Flags, lastDamage.Position, lastDamage.Force );

		base.TakeDamage( info );
	}

	[ClientRpc]
	public void TookDamage( DamageFlags damageFlags, Vector3 forcePos, Vector3 force )
	{
	}

	public override PawnController GetActiveController()
	{
		if ( DevController != null ) return DevController;

		return base.GetActiveController();
	}

	public override void Simulate( Client cl )
	{
		base.Simulate( cl );

		if ( Input.ActiveChild != null )
		{
			ActiveChild = Input.ActiveChild;
		}

		if ( LifeState != LifeState.Alive )
			return;

		var controller = GetActiveController();
		if ( controller != null )
		{
			EnableSolidCollisions = !controller.HasTag( "noclip" );

			SimulateAnimation( controller );
		}

		TickPlayerUse();
		SimulateActiveChild( cl, ActiveChild );

		if ( Input.Pressed( InputButton.View ) )
		{
			if ( CameraMode is ThirdPersonCamera )
			{
				CameraMode = new FirstPersonCamera();
			}
			else
			{
				CameraMode = new ThirdPersonCamera();
			}
		}

		if ( Input.Released( InputButton.Jump ) )
		{
			if ( timeSinceJumpReleased < 0.3f )
			{
				Game.Current?.DoPlayerNoclip( cl );
			}

			timeSinceJumpReleased = 0;
		}

		if ( Input.Left != 0 || Input.Forward != 0 )
		{
			timeSinceJumpReleased = 1;
		}

		if ( Input.Pressed( InputButton.PrimaryAttack ) & sound.Finished & IsServer)
		{
			var pos = GetBoneTransform( "spine_0" );
			SpawnParticles( pos.Position);
			sound = PlaySound( "fart" );
			UpdateLeaderboard( cl, 1, "Fart" );
		}
		if ( Input.Pressed( InputButton.SecondaryAttack ) & sound.Finished & IsServer )
		{
			var pos = GetBoneTransform( "head" );
			SpawnParticles( pos.Position );
			sound = PlaySound( "burp" );
			UpdateLeaderboard( cl, 1, "Burp" );
		}
		if ( Input.Pressed( InputButton.Menu ) & deathSound.Finished & IsServer )
		{
			deathSound = PlaySound( "death" );
			UpdateLeaderboard( cl, 1, "Death" );
		}

		if ( IsServer )
		{
			if ( deathSound.ElapsedTime > 1.15 )
			{
				var pos = GetBoneTransform( "spine_0" );
				var velocity = Vector3.Random * 1000;
				SpawnDeathParticles( pos.Position, velocity );
				var damageInfo = new DamageInfo { Damage = 999 };
				Velocity += velocity;
				TakeDamage( damageInfo );
			}
		}
	}

	[ClientRpc]
	void SpawnParticles(Vector3 position)
	{
		Particles particles = Particles.Create( "particles/particle.vpcf" );
		particles.SetPosition( 0, position );
	}
	[ClientRpc]
	async void SpawnDeathParticles( Vector3 position, Vector3 velocity )
	{
		var model = Model.Load( "models/poopemoji/poopemoji.vmdl" );
		var ent = new Prop
		{
			Position = position,
			Model = model,
			Velocity = velocity
		};
		SpawnParticles( position );
		Particles particles = Particles.Create( "particles/shitexplode.vpcf" );
		particles.SetPosition( 0, position );
		await ent.ExplodeAsync( 60 );
	}

	async void UpdateLeaderboard(Client client, int score, string boardName)
	{
		var combinedLeaderboard = await Leaderboard.FindOrCreate( "Combined_Score",false );
		var seperateLeaderboard = await Leaderboard.FindOrCreate( boardName,false );
		var combinedScore = await combinedLeaderboard.Value.GetScore( client.PlayerId );
		var seperateScore = await seperateLeaderboard.Value.GetScore( client.PlayerId );
		await combinedLeaderboard.Value.Submit( client, score+combinedScore.Value.Score, true );
		await seperateLeaderboard.Value.Submit( client, score+seperateScore.Value.Score, true );
		Log.Info( score + combinedScore.Value.Score );
		Log.Info( score + seperateScore.Value.Score );
	}

	void SimulateAnimation( PawnController controller )
	{
		if ( controller == null )
			return;

		// where should we be rotated to
		var turnSpeed = 0.02f;
		var idealRotation = Rotation.LookAt( Input.Rotation.Forward.WithZ( 0 ), Vector3.Up );
		Rotation = Rotation.Slerp( Rotation, idealRotation, controller.WishVelocity.Length * Time.Delta * turnSpeed );
		Rotation = Rotation.Clamp( idealRotation, 45.0f, out var shuffle ); // lock facing to within 45 degrees of look direction

		CitizenAnimationHelper animHelper = new CitizenAnimationHelper( this );

		animHelper.WithWishVelocity( controller.WishVelocity );
		animHelper.WithVelocity( controller.Velocity );
		animHelper.WithLookAt( EyePosition + EyeRotation.Forward * 100.0f, 1.0f, 1.0f, 0.5f );
		animHelper.AimAngle = Input.Rotation;
		animHelper.FootShuffle = shuffle;
		animHelper.DuckLevel = MathX.Lerp( animHelper.DuckLevel, controller.HasTag( "ducked" ) ? 1 : 0, Time.Delta * 10.0f );
		animHelper.VoiceLevel = (Host.IsClient && Client.IsValid()) ? Client.TimeSinceLastVoice < 0.5f ? Client.VoiceLevel : 0.0f : 0.0f;
		animHelper.IsGrounded = GroundEntity != null;
		animHelper.IsSitting = controller.HasTag( "sitting" );
		animHelper.IsNoclipping = controller.HasTag( "noclip" );
		animHelper.IsClimbing = controller.HasTag( "climbing" );
		animHelper.IsSwimming = WaterLevel >= 0.5f;
		animHelper.IsWeaponLowered = false;

		if ( controller.HasEvent( "jump" ) ) animHelper.TriggerJump();

		if ( ActiveChild is BaseCarriable carry )
		{
			carry.SimulateAnimator( animHelper );
		}
		else
		{
			animHelper.HoldType = CitizenAnimationHelper.HoldTypes.None;
			animHelper.AimBodyWeight = 0.5f;
		}
	}

	public override float FootstepVolume()
	{
		return Velocity.WithZ( 0 ).Length.LerpInverse( 0.0f, 200.0f ) * 5.0f;
	}
}
