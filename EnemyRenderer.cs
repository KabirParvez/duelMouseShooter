namespace LoopGame;

using System.Numerics;

/// <summary>
/// Draws the enemy exosuit as a procedural solid built from world-space boxes.
/// Every vertex is placed in XYZ world space and pushed through the shared camera
/// projection, so perspective, yaw, pitch and distance scaling are all inherited
/// from the existing pipeline rather than faked.
/// </summary>
internal static class EnemyRenderer
{
	private static readonly Vector3 LightDirection = Vector3.Normalize(new Vector3(-0.42f, 0.80f, -0.44f));
	private static readonly Color FogColor = Color.FromArgb(18, 35, 47);
	private static readonly Color ArmorColor = Color.FromArgb(58, 69, 82);
	private static readonly Color ArmorDarkColor = Color.FromArgb(38, 46, 57);
	private static readonly Color PlateColor = Color.FromArgb(82, 95, 110);
	private static readonly Color AccentColor = Color.FromArgb(228, 76, 84);
	private static readonly Color VisorColor = Color.FromArgb(255, 134, 104);

	private const float FogStart = 16f;
	private const float FogRange = 34f;
	private const float MaxFog = 0.55f;

	/// <summary>Unit corner signs of a box, ordered so the face table below lines up.</summary>
	private static readonly Vector3[] CornerSigns =
	{
		new(-1f, -1f, -1f), new(1f, -1f, -1f), new(1f, 1f, -1f), new(-1f, 1f, -1f),
		new(-1f, -1f, 1f), new(1f, -1f, 1f), new(1f, 1f, 1f), new(-1f, 1f, 1f)
	};

	private static readonly int[] FaceCorners =
	{
		4, 5, 6, 7, // +Z
		1, 0, 3, 2, // -Z
		5, 1, 2, 6, // +X
		0, 4, 7, 3, // -X
		7, 6, 2, 3, // +Y
		0, 1, 5, 4  // -Y
	};

	private static readonly Vector3[] FaceNormals =
	{
		new(0f, 0f, 1f), new(0f, 0f, -1f), new(1f, 0f, 0f), new(-1f, 0f, 0f), new(0f, 1f, 0f), new(0f, -1f, 0f)
	};

	/// <summary>
	/// The exosuit itself, in local metres. Origin is between the feet, +Y is up
	/// and +Z is the direction the suit faces.
	/// </summary>
	private static readonly ArmorBox[] Parts =
	{
		// Legs and boots
		new(new Vector3(-0.19f, 0.52f, 0f), new Vector3(0.12f, 0.42f, 0.15f), ArmorDarkColor, false),
		new(new Vector3(0.19f, 0.52f, 0f), new Vector3(0.12f, 0.42f, 0.15f), ArmorDarkColor, false),
		new(new Vector3(-0.19f, 0.58f, 0.13f), new Vector3(0.13f, 0.13f, 0.06f), PlateColor, false),
		new(new Vector3(0.19f, 0.58f, 0.13f), new Vector3(0.13f, 0.13f, 0.06f), PlateColor, false),
		new(new Vector3(-0.19f, 0.07f, 0.05f), new Vector3(0.15f, 0.07f, 0.22f), PlateColor, false),
		new(new Vector3(0.19f, 0.07f, 0.05f), new Vector3(0.15f, 0.07f, 0.22f), PlateColor, false),

		// Pelvis and torso
		new(new Vector3(0f, 1.02f, 0f), new Vector3(0.27f, 0.14f, 0.17f), ArmorDarkColor, false),
		new(new Vector3(0f, 1.42f, 0f), new Vector3(0.32f, 0.29f, 0.19f), ArmorColor, false),
		new(new Vector3(0f, 1.48f, 0.17f), new Vector3(0.28f, 0.21f, 0.05f), PlateColor, false),
		new(new Vector3(0f, 1.46f, 0.22f), new Vector3(0.07f, 0.09f, 0.03f), AccentColor, true),

		// Back unit and thruster vents
		new(new Vector3(0f, 1.52f, -0.27f), new Vector3(0.22f, 0.24f, 0.09f), ArmorDarkColor, false),
		new(new Vector3(-0.13f, 1.31f, -0.34f), new Vector3(0.05f, 0.05f, 0.03f), AccentColor, true),
		new(new Vector3(0.13f, 1.31f, -0.34f), new Vector3(0.05f, 0.05f, 0.03f), AccentColor, true),

		// Shoulders, arms and wrist emitters
		new(new Vector3(-0.46f, 1.63f, 0f), new Vector3(0.16f, 0.13f, 0.20f), PlateColor, false),
		new(new Vector3(0.46f, 1.63f, 0f), new Vector3(0.16f, 0.13f, 0.20f), PlateColor, false),
		new(new Vector3(-0.46f, 1.34f, 0.02f), new Vector3(0.10f, 0.20f, 0.12f), ArmorDarkColor, false),
		new(new Vector3(0.46f, 1.34f, 0.02f), new Vector3(0.10f, 0.20f, 0.12f), ArmorDarkColor, false),
		new(new Vector3(-0.48f, 0.98f, 0.10f), new Vector3(0.12f, 0.20f, 0.14f), ArmorColor, false),
		new(new Vector3(0.48f, 0.98f, 0.10f), new Vector3(0.12f, 0.20f, 0.14f), ArmorColor, false),
		new(new Vector3(-0.48f, 0.90f, 0.28f), new Vector3(0.06f, 0.06f, 0.10f), AccentColor, true),
		new(new Vector3(0.48f, 0.90f, 0.28f), new Vector3(0.06f, 0.06f, 0.10f), AccentColor, true),

		// Neck, helmet, crest and visor
		new(new Vector3(0f, 1.76f, 0f), new Vector3(0.09f, 0.06f, 0.09f), ArmorDarkColor, false),
		new(new Vector3(0f, 1.93f, -0.01f), new Vector3(0.18f, 0.15f, 0.19f), ArmorColor, false),
		new(new Vector3(0f, 2.06f, -0.02f), new Vector3(0.04f, 0.06f, 0.17f), PlateColor, false),
		new(new Vector3(0f, 1.94f, 0.17f), new Vector3(0.14f, 0.06f, 0.04f), VisorColor, true)
	};

	public static void Draw(Graphics graphics, Enemy enemy, CameraView view)
	{
		if (!enemy.IsVisible)
		{
			return;
		}

		// Collapse forward when destroyed, pivoting about the feet.
		float fall = enemy.State == EnemyState.Dying ? EaseOut(enemy.DeathProgress) * 1.45f : 0f;
		float fade = enemy.State == EnemyState.Dying ? enemy.DeathProgress * 0.55f : 0f;
		float flash = enemy.HitFlashAmount;
		float distance = Vector3.Distance(enemy.ChestPosition, view.Position);
		float fog = Math.Clamp((distance - FogStart) / FogRange, 0f, 1f) * MaxFog;

		DrawShadow(graphics, enemy, view, fall);

		// Painter's algorithm: sort the parts back to front, since there is no depth buffer.
		int[] order = new int[Parts.Length];
		float[] depth = new float[Parts.Length];
		for (int index = 0; index < Parts.Length; index++)
		{
			order[index] = index;
			Vector3 center = LocalToWorld(Parts[index].Center, enemy.Facing, fall, enemy.Position);
			depth[index] = Vector3.DistanceSquared(center, view.Position);
		}

		Array.Sort(depth, order);
		Array.Reverse(order);

		Vector3[] corners = new Vector3[8];
		Vector3[] face = new Vector3[4];

		for (int slot = 0; slot < order.Length; slot++)
		{
			ArmorBox box = Parts[order[slot]];
			DrawBox(graphics, box, enemy, view, fall, fog, fade, flash, corners, face);
		}
	}

	public static void DrawHealthBar(Graphics graphics, Enemy enemy, CameraView view, Font font)
	{
		if (!enemy.IsAlive)
		{
			return;
		}

		Vector3 anchor = enemy.HealthBarAnchor;
		if (!view.TryProjectRadius(anchor, 0.62f, out PointF center, out float halfWidth))
		{
			return;
		}

		if (center.X < -260f || center.Y < -160f || center.X > view.Viewport.Width + 260f || center.Y > view.Viewport.Height + 160f)
		{
			return;
		}

		halfWidth = Math.Clamp(halfWidth, 18f, 96f);
		float width = halfWidth * 2f;
		float height = MathF.Max(5f, width * 0.11f);
		float left = center.X - halfWidth;
		float top = center.Y - (height * 0.5f);

		using var backdropBrush = new SolidBrush(Color.FromArgb(170, 8, 14, 20));
		graphics.FillRectangle(backdropBrush, left - 1f, top - 1f, width + 2f, height + 2f);

		float fraction = enemy.HealthFraction;
		Color fill = fraction > 0.55f
			? Color.FromArgb(232, 96, 226, 142)
			: fraction > 0.25f
				? Color.FromArgb(232, 246, 198, 92)
				: Color.FromArgb(232, 236, 92, 88);

		using var fillBrush = new SolidBrush(fill);
		graphics.FillRectangle(fillBrush, left, top, width * fraction, height);

		using var framePen = new Pen(Color.FromArgb(150, 168, 196, 208), 1f);
		graphics.DrawRectangle(framePen, left, top, width, height);

		// Only label the target when it is close enough for the text to be readable.
		if (width < 52f)
		{
			return;
		}

		using var labelBrush = new SolidBrush(Color.FromArgb(205, 236, 138, 132));
		using var labelFormat = new StringFormat { Alignment = StringAlignment.Center };
		graphics.DrawString("ENEMY", font, labelBrush, center.X, top - 20f, labelFormat);
	}

	private static void DrawBox(
		Graphics graphics,
		ArmorBox box,
		Enemy enemy,
		CameraView view,
		float fall,
		float fog,
		float fade,
		float flash,
		Vector3[] corners,
		Vector3[] face)
	{
		for (int index = 0; index < 8; index++)
		{
			Vector3 local = box.Center + (CornerSigns[index] * box.HalfExtents);
			corners[index] = LocalToWorld(local, enemy.Facing, fall, enemy.Position);
		}

		using var edgePen = new Pen(Color.FromArgb(120, 16, 22, 30), 1f);

		for (int faceIndex = 0; faceIndex < 6; faceIndex++)
		{
			Vector3 normal = RotateLocal(FaceNormals[faceIndex], enemy.Facing, fall);
			Vector3 faceCenter = Vector3.Zero;

			for (int corner = 0; corner < 4; corner++)
			{
				face[corner] = corners[FaceCorners[(faceIndex * 4) + corner]];
				faceCenter += face[corner];
			}

			faceCenter *= 0.25f;

			// Back-face cull against the real camera position.
			if (Vector3.Dot(normal, faceCenter - view.Position) >= 0f)
			{
				continue;
			}

			if (!view.TryProjectPolygon(face, out PointF[] screenPoints))
			{
				continue;
			}

			using var brush = new SolidBrush(Shade(box.Color, normal, box.Glow, fog, fade, flash));
			graphics.FillPolygon(brush, screenPoints);

			if (!box.Glow && fog < 0.4f)
			{
				graphics.DrawPolygon(edgePen, screenPoints);
			}
		}
	}

	private static void DrawShadow(Graphics graphics, Enemy enemy, CameraView view, float fall)
	{
		const int segments = 12;
		float radius = Enemy.BodyRadius * (1f + (fall * 0.35f));
		Vector3[] ring = new Vector3[segments];

		for (int index = 0; index < segments; index++)
		{
			float angle = index * MathF.Tau / segments;
			ring[index] = new Vector3(
				enemy.Position.X + (MathF.Cos(angle) * radius),
				0.02f,
				enemy.Position.Z + (MathF.Sin(angle) * radius));
		}

		if (!view.TryProjectPolygon(ring, out PointF[] screenPoints))
		{
			return;
		}

		using var brush = new SolidBrush(Color.FromArgb(90, 3, 9, 13));
		graphics.FillPolygon(brush, screenPoints);
	}

	private static Color Shade(Color baseColor, Vector3 normal, bool glow, float fog, float fade, float flash)
	{
		float brightness = glow
			? 1f
			: 0.46f + (0.54f * MathF.Max(0f, Vector3.Dot(normal, LightDirection)));
		brightness *= 1f - fade;

		float red = baseColor.R * brightness;
		float green = baseColor.G * brightness;
		float blue = baseColor.B * brightness;

		if (flash > 0f)
		{
			float mix = flash * 0.78f;
			red += (255f - red) * mix;
			green += (238f - green) * mix;
			blue += (206f - blue) * mix;
		}

		if (fog > 0f)
		{
			red += (FogColor.R - red) * fog;
			green += (FogColor.G - green) * fog;
			blue += (FogColor.B - blue) * fog;
		}

		return Color.FromArgb(
			255,
			(int)Math.Clamp(red, 0f, 255f),
			(int)Math.Clamp(green, 0f, 255f),
			(int)Math.Clamp(blue, 0f, 255f));
	}

	/// <summary>Local exosuit space to world space: fall tilt about X, then yaw, then translate.</summary>
	private static Vector3 LocalToWorld(Vector3 local, float yaw, float fall, Vector3 origin)
	{
		return origin + RotateLocal(local, yaw, fall);
	}

	private static Vector3 RotateLocal(Vector3 local, float yaw, float fall)
	{
		float fallCos = MathF.Cos(fall);
		float fallSin = MathF.Sin(fall);
		Vector3 tilted = new(
			local.X,
			(local.Y * fallCos) - (local.Z * fallSin),
			(local.Y * fallSin) + (local.Z * fallCos));

		float yawCos = MathF.Cos(yaw);
		float yawSin = MathF.Sin(yaw);
		return new Vector3(
			(tilted.X * yawCos) + (tilted.Z * yawSin),
			tilted.Y,
			(-tilted.X * yawSin) + (tilted.Z * yawCos));
	}

	private static float EaseOut(float value)
	{
		float clamped = Math.Clamp(value, 0f, 1f);
		return 1f - ((1f - clamped) * (1f - clamped));
	}

	private readonly record struct ArmorBox(Vector3 Center, Vector3 HalfExtents, Color Color, bool Glow);
}
