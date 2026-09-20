using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;

namespace LoopGame;

internal sealed class RawMouseInput : IDisposable
{
	internal const int WmInput = 0x00FF;
	internal const ushort LeftButtonDown = 0x0001;
	internal const ushort RightButtonDown = 0x0004;

	private const uint RidInput = 0x10000003;
	private const uint RidevInputSink = 0x00000100;
	private const uint RidiDevicename = 0x20000007;
	private const uint RimTypeMouse = 0;

	private bool disposed;

	public event EventHandler<RawMouseMovement>? MouseMoved;
	public int MousePacketCount { get; private set; }
	public bool RegistrationSucceeded { get; }
	public int RegistrationErrorCode { get; }

	public RawMouseInput(IntPtr windowHandle)
	{
		Log("Raw Input registration starts.");
		Log($"Exact hwnd passed to RegisterRawInputDevices: {windowHandle}");
		var devices = new RawInputDevice[]
		{
			new()
			{
				UsagePage = 0x01,
				Usage = 0x02,
				Flags = RidevInputSink,
				Target = windowHandle
			}
		};

		uint deviceSize = (uint)Marshal.SizeOf<RawInputDevice>();
		Log($"Exact RAWINPUTDEVICE size: {deviceSize}");
		if (!RegisterRawInputDevices(devices, (uint)devices.Length, deviceSize))
		{
			RegistrationErrorCode = Marshal.GetLastWin32Error();
			Log($"RegisterRawInputDevices return value: FALSE");
			Log($"Marshal.GetLastWin32Error(): {RegistrationErrorCode}");
			Log("Raw Input registration completes: FAILED.");
			return;
		}

		RegistrationSucceeded = true;
		Log("RegisterRawInputDevices return value: TRUE");
		Log("Raw Input registration completes: SUCCESS.");
	}

	public bool HandlesMessage(int message)
	{
		return message == WmInput;
	}

	public void ProcessWindowMessage(IntPtr inputHandle)
	{
		Log($"Before calling GetRawInputData. LPARAM={inputHandle}");
		ProcessInput(inputHandle);
	}

	private void ProcessInput(IntPtr inputHandle)
	{
		Log("Before initial GetRawInputData size query.");
		uint dataSize = 0;
		uint queryResult = GetRawInputData(
			inputHandle,
			RidInput,
			IntPtr.Zero,
			ref dataSize,
			(uint)Marshal.SizeOf<RawInputHeader>());
		Log($"Initial GetRawInputData return value: {queryResult}");
		Log($"Required buffer size: {dataSize}");

		if (queryResult == uint.MaxValue || dataSize < (uint)Marshal.SizeOf<RawInputHeader>())
		{
			return;
		}

		IntPtr buffer = Marshal.AllocHGlobal((int)dataSize);
		try
		{
			uint bytesCopied = GetRawInputData(
				inputHandle,
				RidInput,
				buffer,
				ref dataSize,
				(uint)Marshal.SizeOf<RawInputHeader>());
			Log($"Actual GetRawInputData return value: {bytesCopied}");

			if (bytesCopied == uint.MaxValue)
			{
				return;
			}

			int headerSize = Marshal.SizeOf<RawInputHeader>();
			if (dataSize < (uint)(headerSize + Marshal.SizeOf<RawMouse>()))
			{
				return;
			}

			RawInputHeader header = Marshal.PtrToStructure<RawInputHeader>(buffer);
			Log($"Parsed RAWINPUT header: Type={header.Type}, Size={header.Size}, Param={header.Param}");
			if (header.Type != RimTypeMouse)
			{
				return;
			}

			RawMouse mouse = Marshal.PtrToStructure<RawMouse>(IntPtr.Add(buffer, headerSize));
			Log($"Parsed device handle: {header.Device}");
			Log($"Parsed mouse flags: 0x{mouse.ButtonFlags:X4}");
			Log($"Parsed mouse movement: X={mouse.LastX}, Y={mouse.LastY}");
			MousePacketCount++;
			MouseMoved?.Invoke(this, new RawMouseMovement(
				header.Device,
				GetDeviceName(header.Device),
				mouse.LastX,
				mouse.LastY,
				mouse.ButtonFlags));
		}
		finally
		{
			Marshal.FreeHGlobal(buffer);
		}
	}

	private static string GetDeviceName(IntPtr deviceHandle)
	{
		uint characterCount = 0;
		GetRawInputDeviceInfo(deviceHandle, RidiDevicename, IntPtr.Zero, ref characterCount);

		if (characterCount == 0)
		{
			return $"Raw Input device {deviceHandle}";
		}

		var name = new StringBuilder((int)characterCount);
		uint result = GetRawInputDeviceInfo(deviceHandle, RidiDevicename, name, ref characterCount);
		return result == uint.MaxValue ? $"Raw Input device {deviceHandle}" : name.ToString();
	}

	public void Dispose()
	{
		if (disposed)
		{
			return;
		}

		disposed = true;
		GC.SuppressFinalize(this);
	}

	internal static void Log(string message)
	{
		string line = $"[RAWINPUT] {message}";
		Debug.WriteLine(line);
		Console.WriteLine(line);
	}

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool RegisterRawInputDevices(
		[In] RawInputDevice[] pRawInputDevices,
		uint uiNumDevices,
		uint cbSize);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern uint GetRawInputData(
		IntPtr hRawInput,
		uint uiCommand,
		IntPtr pData,
		ref uint pcbSize,
		uint cbSizeHeader);

	[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern uint GetRawInputDeviceInfo(
		IntPtr hDevice,
		uint uiCommand,
		IntPtr pData,
		ref uint pcbSize);

	[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern uint GetRawInputDeviceInfo(
		IntPtr hDevice,
		uint uiCommand,
		StringBuilder pData,
		ref uint pcbSize);

	[StructLayout(LayoutKind.Sequential, Pack = 8)]
	private struct RawInputDevice
	{
		public ushort UsagePage;
		public ushort Usage;
		public uint Flags;
		public IntPtr Target;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 8)]
	private struct RawInputHeader
	{
		public uint Type;
		public uint Size;
		public IntPtr Device;
		public IntPtr Param;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 8)]
	private struct RawMouse
	{
		public ushort Flags;
		public ushort ButtonFlags;
		public ushort ButtonData;
		public uint RawButtons;
		public int LastX;
		public int LastY;
		public uint ExtraInformation;
	}
}
