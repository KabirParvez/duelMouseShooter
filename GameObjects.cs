namespace LoopGame;

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
	ushort ButtonFlags);

internal sealed class ProjectileTracer
{
	public ProjectileTracer(PointF start, PointF end, Color color)
	{
		Start = start;
		End = end;
		Color = color;
	}

	public PointF Start { get; }
	public PointF End { get; }
	public Color Color { get; }
	public float Progress { get; private set; }

	public bool Update(float elapsedSeconds)
	{
		Progress += elapsedSeconds / 0.18f;
		return Progress < 1f;
	}

	public PointF GetPosition()
	{
		return new PointF(
			Start.X + ((End.X - Start.X) * Progress),
			Start.Y + ((End.Y - Start.Y) * Progress));
	}

	public PointF GetTailPosition()
	{
		float tailProgress = MathF.Max(0f, Progress - 0.12f);
		return new PointF(
			Start.X + ((End.X - Start.X) * tailProgress),
			Start.Y + ((End.Y - Start.Y) * tailProgress));
	}
}
