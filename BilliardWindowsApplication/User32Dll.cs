using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
namespace BilliardWindowsApplication
{
	public class USER32Dll
	{
		public const int SM_CXSCREEN = 0;
		public const int SM_CYSCREEN = 1;

		[DllImport("user32.dll", EntryPoint = "GetDesktopWindow")]
		public static extern IntPtr GetDesktopWindow();

		[DllImport("user32.dll", EntryPoint = "GetDC")]
		public static extern IntPtr GetDC(IntPtr ptr);

		[DllImport("user32.dll", EntryPoint = "GetSystemMetrics")]
		public static extern int GetSystemMetrics(int abc);

		[DllImport("user32.dll", EntryPoint = "GetWindowDC")]
		public static extern IntPtr GetWindowDC(Int32 ptr);

		[DllImport("user32.dll", EntryPoint = "ReleaseDC")]
		public static extern IntPtr ReleaseDC(IntPtr hWnd, IntPtr hDc);
		[DllImport("user32.dll")]
		public static extern IntPtr MoveWindow(IntPtr hWnd, int x, int y, int w, int h);
		[DllImport("user32.dll")]
		public static extern IntPtr ShowWindow(IntPtr hWnd, int nCommand);
	}
	public enum ShowWindowCommands
	{
		SW_HIDE, SW_SHOWNORMAL = 1, SW_NORMAL = 1, SW_SHOWMINIMIZED, SW_SHOWMAXIMIZED, SW_MAXIMIZE, 
		SW_SHOWNOACTIVATE, SW_SHOW, SW_MINIMIZE, SW_SHOWMINNOACTIVE, SW_SHOWNA, SW_RESTORE, SW_SHOWDEFAULT, 
		SW_FORCEMINIMIZE, SW_MAX
	}	
}
