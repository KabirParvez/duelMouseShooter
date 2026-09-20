namespace LoopGame;

using System.Numerics;

internal enum EnemyState
{
	Approaching,
	Attacking,
	Dying,
	Dead
}

/// <summary>
/// One hostile exosuit. Pure gameplay state - it owns no GDI objects and does no
/// drawing. Rendering lives in EnemyRenderer so logic and presentation stay apart.
/// The enemy exists entirely in XYZ world space; screen size is a consequence of
/// the shared perspective projection, never of a hand-tuned scale factor.
/// </summary>
internal sealed class Enemy
{
	// --- Tuning constants -------------------------------------------------
	/// <summary>How far ahead of the player the exosuit drops in, in world metres.</summary>
	public const float SpawnDistance = 26f;

	/// <summary>Ground speed in world metres per second.</summary>
	public const float MoveSpeed = 2.15f;

	public const float MaxHealth = 100f;

	/// <summary>Ground distance at which the exosuit stops and starts hitting the player.</summary>
	public const float AttackDistance = 2.4f;

	public const float AttackDamage = 16f;

	/// <summary>Seconds between melee strikes once in range.</summary>
	public const float AttackInterval = 1.0f;

	/// <summary>Radius of the capsule used for projectile collision, in world metres.</summary>
	public const float BodyRadius = 0.58f;

	/// <summary>Full standing height of the exosuit, in world metres.</summary>
	public const float BodyHeight = 2.12f;

	/// <summary>Height of the helmet centre above the feet.</summary>
	public const float HeadHeight = 1.93f;

	public const float HeadRadius = 0.30f;

	/// <summary>Seconds the collapse animation plays before the wreck is removed.</summary>
	public const float DeathDuration = 1.9f;

	public const int ScoreValue = 250;

	private const float HitFlashDuration = 0.16f;
	private const float TurnRate = 3.6f;
	private const float ChestHeight = 1.42f;

	private float hitFlash;
	private float attackCooldown;

	public Enemy(Vector3 spawnPosition, float facing)
	{
		// Always pinned to the ground plane - the exosuit never floats.
		Position = new Vector3(spawnPosition.X, 0f, spawnPosition.Z);
		Facing = facing;
		Health = MaxHealth;
		State = EnemyState.Approaching;
	}

	/// <summary>Feet position on the ground plane. Y is always zero.</summary>
	public Vector3 Position { get; private set; }

	/// <summary>Yaw in radians, matching the camera convention: atan2(dx, dz).</summary>
	public float Facing { get; private set; }

	public float Health { get; private set; }

	public EnemyState State { get; private set; }

	/// <summary>Collapse animation progress, 0 to 1.</summary>
	public float DeathProgress { get; private set; }

	/// <summary>Normalised 0..1 hit flash used by the renderer.</summary>
	public float HitFlashAmount => Math.Clamp(hitFlash / HitFlashDuration, 0f, 1f);

	/// <summary>True while the exosuit is an active combatant.</summary>
	public bool IsAlive => State is EnemyState.Approaching or EnemyState.Attacking;

	/// <summary>True while anything should still be drawn for it.</summary>
	public bool IsVisible => State != EnemyState.Dead;

	public Vector3 HeadPosition => Position + new Vector3(0f, HeadHeight, 0f);

	public Vector3 ChestPosition => Position + new Vector3(0f, ChestHeight, 0f);

	public Vector3 HealthBarAnchor => Position + new Vector3(0f, BodyHeight + 0.42f, 0f);

	public float HealthFraction => Math.Clamp(Health / MaxHealth, 0f, 1f);

	/// <summary>
	/// Advances the exosuit. Returns the damage it dealt to the player this frame
	/// (zero on almost every frame). All movement is XZ only and deltaTime scaled.
	/// </summary>
	public float Update(float deltaTime, Vector3 playerPosition)
	{
		hitFlash = MathF.Max(0f, hitFlash - deltaTime);

		if (State == EnemyState.Dying)
		{
			DeathProgress = MathF.Min(1f, DeathProgress + (deltaTime / DeathDuration));
			if (DeathProgress >= 1f)
			{
				State = EnemyState.Dead;
			}

			return 0f;
		}

		if (!IsAlive)
		{
			return 0f;
		}

		// Recalculated every frame, so the exosuit re-aims if the player strafes.
		Vector3 toPlayer = new(playerPosition.X - Position.X, 0f, playerPosition.Z - Position.Z);
		float distance = toPlayer.Length();

		if (distance > 0.0005f)
		{
			Facing = RotateToward(Facing, MathF.Atan2(toPlayer.X, toPlayer.Z), TurnRate * deltaTime);
		}

		if (distance > AttackDistance)
		{
			State = EnemyState.Approaching;
			attackCooldown = MathF.Max(0f, attackCooldown - deltaTime);

			float travel = MathF.Min(MoveSpeed * deltaTime, distance - AttackDistance);
			Vector3 step = (toPlayer / distance) * travel;
			Position = new Vector3(Position.X + step.X, 0f, Position.Z + step.Z);
			return 0f;
		}

		State = EnemyState.Attacking;
		attackCooldown -= deltaTime;
		if (attackCooldown > 0f)
		{
			return 0f;
		}

		attackCooldown = AttackInterval;
		return AttackDamage;
	}

	/// <summary>
	/// World-space projectile test. The exosuit is treated as an upright capsule
	/// from its feet to its crown; the projectile is treated as the segment it
	/// swept this frame. No screen rectangles are involved.
	/// </summary>
	public bool TryHit(Vector3 segmentStart, Vector3 segmentEnd, out Vector3 hitPoint, out bool headshot)
	{
		hitPoint = segmentEnd;
		headshot = false;

		if (!IsAlive)
		{
			return false;
		}

		Vector3 feet = Position;
		Vector3 crown = Position + new Vector3(0f, BodyHeight, 0f);
		float distanceSquared = Geometry3D.ClosestPointsBetweenSegments(
			segmentStart,
			segmentEnd,
			feet,
			crown,
			out Vector3 closestOnProjectile,
			out Vector3 closestOnBody);

		if (distanceSquared > BodyRadius * BodyRadius)
		{
			return false;
		}

		hitPoint = closestOnProjectile;
		headshot = distanceSquared <= HeadRadius * HeadRadius
			&& MathF.Abs(closestOnBody.Y - (Position.Y + HeadHeight)) <= 0.22f;
		return true;
	}

	/// <summary>Applies damage. Returns true only on the frame the exosuit is destroyed.</summary>
	public bool TakeDamage(float amount)
	{
		if (!IsAlive)
		{
			return false;
		}

		Health = MathF.Max(0f, Health - amount);
		hitFlash = HitFlashDuration;

		if (Health > 0f)
		{
			return false;
		}

		State = EnemyState.Dying;
		DeathProgress = 0f;
		return true;
	}

	private static float RotateToward(float current, float target, float maxDelta)
	{
		float difference = WrapAngle(target - current);
		return MathF.Abs(difference) <= maxDelta
			? WrapAngle(target)
			: WrapAngle(current + (MathF.Sign(difference) * maxDelta));
	}

	private static float WrapAngle(float angle)
	{
		while (angle > MathF.PI)
		{
			angle -= MathF.Tau;
		}

		while (angle < -MathF.PI)
		{
			angle += MathF.Tau;
		}

		return angle;
	}
}
