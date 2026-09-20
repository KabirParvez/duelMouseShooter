using System.Drawing.Drawing2D;

namespace LoopGame;

internal sealed class GameForm : Form
{
	private readonly Font titleFont = new("Segoe UI", 30, FontStyle.Bold);
	private readonly Font instructionFont = new("Segoe UI", 15, FontStyle.Regular);
	private readonly Font detailFont = new("Consolas", 11, FontStyle.Regular);
	private RawMouseInput? rawMouseInput;
	private AssignmentState assignmentState = AssignmentState.WaitingForLeft;
	private MouseAssignment? leftAssignment;
	private MouseAssignment? rightAssignment;

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
	}

	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
		rawMouseInput = new RawMouseInput(Handle);
		rawMouseInput.MouseMoved += OnRawMouseMoved;
	}

	private void OnRawMouseMoved(object? sender, RawMouseMovement movement)
	{
		if (assignmentState == AssignmentState.WaitingForLeft)
		{
			leftAssignment = new MouseAssignment(movement.DeviceHandle, movement.DeviceName);
			assignmentState = AssignmentState.WaitingForRight;
			Invalidate();
			return;
		}

		if (assignmentState == AssignmentState.WaitingForRight && leftAssignment?.DeviceHandle != movement.DeviceHandle)
		{
			rightAssignment = new MouseAssignment(movement.DeviceHandle, movement.DeviceName);
			assignmentState = AssignmentState.BothHandsReady;
			Invalidate();
		}
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
			DrawCenteredText(graphics, "Move the mouse you want to use for your LEFT HAND", instructionFont, 220, Color.White);
		}
		else if (assignmentState == AssignmentState.WaitingForRight)
		{
			DrawCenteredText(graphics, "LEFT HAND ASSIGNED", instructionFont, 180, Color.FromArgb(96, 239, 228));
			DrawCenteredText(graphics, "Move the other mouse for your RIGHT HAND", instructionFont, 245, Color.White);
		}
		else
		{
			DrawCenteredText(graphics, "BOTH HANDS READY", instructionFont, 145, Color.FromArgb(96, 239, 228));
			DrawCenteredText(graphics, $"LEFT ARM:  {leftAssignment?.DeviceName}", detailFont, 230, Color.White);
			DrawCenteredText(graphics, $"RIGHT ARM: {rightAssignment?.DeviceName}", detailFont, 275, Color.White);
		}
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
			titleFont.Dispose();
			instructionFont.Dispose();
			detailFont.Dispose();
		}

		base.Dispose(disposing);
	}
}
