namespace LoopGame;

using System.Numerics;

internal delegate Vector3 WorldToViewTransform(Vector3 worldPosition);

internal delegate PointF ViewToScreenTransform(Vector3 viewPosition);

/// <summary>
/// A per-frame snapshot of the player camera. This does NOT replace the camera
/// maths in GameForm - it wraps the existing WorldToCameraLocal / projection
/// functions so world objects (enemy, particles, battlefield geometry) can all be
/// drawn through the exact same world-to-screen pipeline, with proper near-plane
/// clipping so nothing behind the camera is drawn as though it were in front.
/// </summary>
internal sealed class CameraView
{
	/// <summary>Anything closer than this in camera space is clipped away.</summary>
	public const float NearPlane = 0.26f;

	private readonly WorldToViewTransform toView;
	private readonly ViewToScreenTransform toScreen;
	private readonly List<Vector3> viewBuffer = new(12);
	private readonly List<Vector3> clipBuffer = new(16);

	public CameraView(
		Vector3 position,
		Vector3 right,
		Vector3 up,
		Vector3 forward,
		Size viewport,
		WorldToViewTransform toView,
		ViewToScreenTransform toScreen)
	{
		Position = position;
		Right = right;
		Up = up;
		Forward = forward;
		Viewport = viewport;
		this.toView = toView;
		this.toScreen = toScreen;
	}

	public Vector3 Position { get; }
	public Vector3 Right { get; }
	public Vector3 Up { get; }
	public Vector3 Forward { get; }
	public Size Viewport { get; }

	public Vector3 ToView(Vector3 worldPosition)
	{
		return toView(worldPosition);
	}

	/// <summary>Projects a world point, returning false when it sits behind the near plane.</summary>
	public bool TryProject(Vector3 worldPosition, out PointF screenPosition)
	{
		Vector3 viewPosition = toView(worldPosition);
		if (viewPosition.Z < NearPlane)
		{
			screenPosition = PointF.Empty;
			return false;
		}

		screenPosition = toScreen(viewPosition);
		return true;
	}

	/// <summary>
	/// Projects a world-space polygon, clipping it against the near plane first
	/// (Sutherland-Hodgman) so faces that straddle the camera do not invert.
	/// </summary>
	public bool TryProjectPolygon(IReadOnlyList<Vector3> worldPoints, out PointF[] screenPoints)
	{
		screenPoints = Array.Empty<PointF>();
		if (worldPoints.Count < 3)
		{
			return false;
		}

		viewBuffer.Clear();
		for (int index = 0; index < worldPoints.Count; index++)
		{
			viewBuffer.Add(toView(worldPoints[index]));
		}

		clipBuffer.Clear();
		for (int index = 0; index < viewBuffer.Count; index++)
		{
			Vector3 current = viewBuffer[index];
			Vector3 next = viewBuffer[(index + 1) % viewBuffer.Count];
			bool currentInside = current.Z >= NearPlane;
			bool nextInside = next.Z >= NearPlane;

			if (currentInside)
			{
				clipBuffer.Add(current);
			}

			if (currentInside != nextInside)
			{
				float fraction = (NearPlane - current.Z) / (next.Z - current.Z);
				clipBuffer.Add(Vector3.Lerp(current, next, fraction));
			}
		}

		if (clipBuffer.Count < 3)
		{
			return false;
		}

		screenPoints = new PointF[clipBuffer.Count];
		for (int index = 0; index < clipBuffer.Count; index++)
		{
			screenPoints[index] = toScreen(clipBuffer[index]);
		}

		return true;
	}

	/// <summary>
	/// Converts a world-space radius at a given world position into a screen radius,
	/// by projecting the point and an offset point along the camera right vector.
	/// Size therefore always follows real world distance.
	/// </summary>
	public bool TryProjectRadius(Vector3 worldPosition, float worldRadius, out PointF center, out float screenRadius)
	{
		screenRadius = 0f;
		if (!TryProject(worldPosition, out center))
		{
			return false;
		}

		if (!TryProject(worldPosition + (Right * worldRadius), out PointF edge))
		{
			return false;
		}

		float deltaX = edge.X - center.X;
		float deltaY = edge.Y - center.Y;
		screenRadius = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
		return true;
	}
}
