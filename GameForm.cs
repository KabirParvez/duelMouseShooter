using System.Drawing.Drawing2D;

namespace LoopGame;

internal sealed class GameForm : Form
{
	private readonly Font titleFont = new("Segoe UI", 30, FontStyle.Bold);
	private readonly Font instructionFont = new("Segoe UI", 15, FontStyle.Regular);
	private readonly Font detailFont = new("Consolas", 11, FontStyle.Regular);
	private readonly System.Windows.Forms.Timer fireIndicatorTimer;
	private RawMouseInput? rawMouseInput;
	private AssignmentState assignmentState = AssignmentState.WaitingForLeft;
	private MouseAssignment? leftAssignment;
	private MouseAssignment? rightAssignment;
	private PointF leftCrosshair = new(280, 330);
	private PointF rightCrosshair = new(680, 330);
	private string? fireIndicator;
	private RawMouseMovement? lastRawInput;
	private string lastRawInputType = "none";

	public GameForm()
	{
		Text = "Loop Game";
		ClientSize = new Size(960, 540);
		MinimumSize = new Size(640, 400);
		StartPosition = FormStartPosition.CenterScreen;
		BackColor = Color.FromArgb(9, 15, 24);
		ForeColor = Color.FromArgb(225, 239, 247);
		DoubleBuffered = true;
		SetStyle(ControlStyles.ResizeRedraw, true);
		fireIndicatorTimer = new System.Windows.Forms.Timer { Interval = 450 };
		fireIndicatorTimer.Tick += ClearFireIndicator;
	}

	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
		rawMouseInput = new RawMouseInput(Handle);
		rawMouseInput.MouseMoved += OnRawMouseMoved;
	}

	protected override void WndProc(ref Message message)
	{
		if (rawMouseInput?.HandlesMessage(message.Msg) == true)
		{
			rawMouseInput.ProcessWindowMessage(message.LParam);
		}

		base.WndProc(ref message);
	}

	private void OnRawMouseMoved(object? sender, RawMouseMovement movement)
	{
		lastRawInput = movement;
		lastRawInputType = GetRawInputType(movement);

		if (assignmentState == AssignmentState.WaitingForLeft)
		{
			if ((movement.ButtonFlags & RawMouseInput.RightButtonDown) == 0)
			{
				return;
			}

			leftAssignment = new MouseAssignment(movement.DeviceHandle, movement.DeviceName);
			assignmentState = AssignmentState.WaitingForRight;
			Invalidate();
			return;
		}

		if (assignmentState == AssignmentState.WaitingForRight && leftAssignment?.DeviceHandle != movement.DeviceHandle)
		{
			if ((movement.ButtonFlags & RawMouseInput.LeftButtonDown) == 0)
			{
				return;
			}

			rightAssignment = new MouseAssignment(movement.DeviceHandle, movement.DeviceName);
			assignmentState = AssignmentState.BothHandsReady;
			Invalidate();
			return;
		}

		if (assignmentState != AssignmentState.BothHandsReady)
		{
			return;
		}

		if (movement.DeviceHandle == leftAssignment?.DeviceHandle)
		{
			leftCrosshair = MoveCrosshair(leftCrosshair, movement.DeltaX, movement.DeltaY);
			if ((movement.ButtonFlags & RawMouseInput.RightButtonDown) != 0)
			{
				ShowFireIndicator("LEFT FIRE");
			}
		}
		else if (movement.DeviceHandle == rightAssignment?.DeviceHandle)
		{
			rightCrosshair = MoveCrosshair(rightCrosshair, movement.DeltaX, movement.DeltaY);
			if ((movement.ButtonFlags & RawMouseInput.LeftButtonDown) != 0)
			{
				ShowFireIndicator("RIGHT FIRE");
			}
		}

		Invalidate();
	}

	private static string GetRawInputType(RawMouseMovement movement)
	{
		if ((movement.ButtonFlags & RawMouseInput.LeftButtonDown) != 0)
		{
			return "left button";
		}

		if ((movement.ButtonFlags & RawMouseInput.RightButtonDown) != 0)
		{
			return "right button";
		}

		return movement.DeltaX != 0 || movement.DeltaY != 0 ? "movement" : "other";
	}

	private PointF MoveCrosshair(PointF current, int deltaX, int deltaY)
	{
		const float crosshairRadius = 22;
		float x = Math.Clamp(current.X + deltaX, crosshairRadius, ClientSize.Width - crosshairRadius);
		float y = Math.Clamp(current.Y + deltaY, 125 + crosshairRadius, ClientSize.Height - crosshairRadius);
		return new PointF(x, y);
	}

	private void ShowFireIndicator(string indicator)
	{
		fireIndicator = indicator;
		fireIndicatorTimer.Stop();
		fireIndicatorTimer.Start();
	}

	private void ClearFireIndicator(object? sender, EventArgs e)
	{
		fireIndicator = null;
		fireIndicatorTimer.Stop();
		Invalidate();
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		using var backgroundBrush = new LinearGradientBrush(ClientRectangle, Color.FromArgb(7, 13, 22), Color.FromArgb(18, 35, 47), 35f);
		graphics.FillRectangle(backgroundBrush, ClientRectangle);

		using var accentPen = new Pen(Color.FromArgb(35, 205, 203), 2);
		graphics.DrawLine(accentPen, 64, 92, ClientSize.Width - 64, 92);

		StringFormat centered = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
		using var titleBrush = new SolidBrush(Color.FromArgb(96, 239, 228));
		graphics.DrawString("LOOP GAME", titleFont, titleBrush, new RectangleF(40, 38, ClientSize.Width - 80, 50), centered);

		if (assignmentState == AssignmentState.WaitingForLeft)
		{
			DrawCenteredText(graphics, "CHOOSE YOUR LEFT ARM", instructionFont, 190, Color.FromArgb(96, 239, 228));
			DrawCenteredText(graphics, "Right-click the mouse you want to use as your LEFT ARM", instructionFont, 255, Color.White);
		}
		else if (assignmentState == AssignmentState.WaitingForRight)
		{
			DrawCenteredText(graphics, "CHOOSE YOUR RIGHT ARM", instructionFont, 190, Color.FromArgb(96, 239, 228));
			DrawCenteredText(graphics, "Left-click the other mouse", instructionFont, 255, Color.White);
		}
		else
		{
			DrawCenteredText(graphics, "BOTH HANDS READY", instructionFont, 145, Color.FromArgb(96, 239, 228));
			DrawCenteredText(graphics, $"LEFT ARM:  {leftAssignment?.DeviceName}", detailFont, 230, Color.White);
			DrawCenteredText(graphics, $"RIGHT ARM: {rightAssignment?.DeviceName}", detailFont, 275, Color.White);
			DrawCrosshair(graphics, leftCrosshair, Color.FromArgb(96, 239, 228), "LEFT");
			DrawCrosshair(graphics, rightCrosshair, Color.FromArgb(255, 184, 92), "RIGHT");
			if (fireIndicator is not null)
			{
				DrawCenteredText(graphics, fireIndicator, instructionFont, ClientSize.Height - 72, Color.FromArgb(255, 220, 120));
			}
		}

		if (assignmentState != AssignmentState.BothHandsReady)
		{
			DrawRawInputDiagnostic(graphics);
		}
	}

	private void DrawRawInputDiagnostic(Graphics graphics)
	{
		string device = lastRawInput is null ? "NONE" : lastRawInput.DeviceHandle.ToString();
		string flags = lastRawInput is null ? "NONE" : $"0x{lastRawInput.ButtonFlags:X4}";
		DrawDiagnosticText(graphics, "RAW INPUT DETECTED", 335, Color.FromArgb(255, 220, 120));
		DrawDiagnosticText(graphics, $"Device: {device}", 370, Color.White);
		DrawDiagnosticText(graphics, $"Type: {lastRawInputType}", 395, Color.White);
		DrawDiagnosticText(graphics, $"Flags: {flags}", 420, Color.White);
		DrawDiagnosticText(graphics, $"LEFT ARM DEVICE: {leftAssignment?.DeviceHandle.ToString() ?? "NONE"}", 455, Color.FromArgb(96, 239, 228));
		DrawDiagnosticText(graphics, $"RIGHT ARM DEVICE: {rightAssignment?.DeviceHandle.ToString() ?? "NONE"}", 480, Color.FromArgb(255, 184, 92));
	}

	private void DrawDiagnosticText(Graphics graphics, string text, float y, Color color)
	{
		using var brush = new SolidBrush(color);
		graphics.DrawString(text, detailFont, brush, 70, y);
	}

	private void DrawCrosshair(Graphics graphics, PointF center, Color color, string label)
	{
		using var pen = new Pen(color, 2);
		const float radius = 22;
		graphics.DrawEllipse(pen, center.X - radius, center.Y - radius, radius * 2, radius * 2);
		graphics.DrawLine(pen, center.X - radius - 10, center.Y, center.X + radius + 10, center.Y);
		graphics.DrawLine(pen, center.X, center.Y - radius - 10, center.X, center.Y + radius + 10);
		using var brush = new SolidBrush(color);
		using var format = new StringFormat { Alignment = StringAlignment.Center };
		graphics.DrawString(label, detailFont, brush, center.X, center.Y + radius + 14, format);
	}

	private void DrawCenteredText(Graphics graphics, string text, Font font, float y, Color color)
	{
		using var brush = new SolidBrush(color);
		using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
		graphics.DrawString(text, font, brush, new RectangleF(32, y, ClientSize.Width - 64, 56), format);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			rawMouseInput?.Dispose();
			fireIndicatorTimer.Dispose();
			titleFont.Dispose();
			instructionFont.Dispose();
			detailFont.Dispose();
		}

		base.Dispose(disposing);
	}
}
