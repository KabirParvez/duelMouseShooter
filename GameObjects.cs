namespace LoopGame;

using System.Numerics;

internal enum AssignmentState
{
	WaitingForLeft,
	WaitingForRight,
	BothHandsReady
}

internal sealed record MouseAssignment(IntPtr DeviceHandle, string DeviceName);

internal sealed record RawMouseMovement(
	IntPtr DeviceHandle,
	string DeviceName,
	int DeltaX,
	int DeltaY,
	ushort ButtonFlags,
	ushort ButtonData);

internal sealed class ProjectileTracer
{
	public ProjectileTracer(Vector3 position, Vector3 velocity, Color color, string owner)
	{
		Position = position;
		PreviousPosition = position;
		Velocity = velocity;
		Color = color;
		Owner = owner;
	}

	public Vector3 Position { get; private set; }
	public Vector3 PreviousPosition { get; private set; }
	public Vector3 Velocity { get; }
	public Color Color { get; }
	public string Owner { get; }
	public float Lifetime { get; private set; }
	public float MaxLifetime { get; } = 1.8f;

	public bool Update(float elapsedSeconds)
	{
		PreviousPosition = Position;
		Position += Velocity * elapsedSeconds;
		Lifetime += elapsedSeconds;

		// Lifetime alone bounds the range now. The old "Position.Z < 90" test was a
		// leftover from screen space and killed shots early once the player rotated
		// or walked far along world Z.
		return Lifetime < MaxLifetime;
	}
}
