#region License
//
// The Open Toolkit Library License
//
// Copyright (c) 2006 - 2013 Stefanos Apostolopoulos
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights to 
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
// the Software, and to permit persons to whom the Software is furnished to do
// so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.
//
#endregion

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using OpenTK.Graphics;
using OpenTK.Platform;

namespace OpenTK
{
    class Sdl2GLControl : IGLControl
    {
        class Sdl2Wrapper : System.Windows.Forms.NativeWindow
        {
            const WindowFlags DefaultFlags =
                WindowFlags.AllowHighDpi |
                WindowFlags.Borderless |
                WindowFlags.OpenGL |
                WindowFlags.Shown;

            Control winforms_parent;
            IWindowInfo sdl_window;

            public Sdl2Wrapper(Control parent, GraphicsMode mode)
            {
                IntPtr window = IntPtr.Zero;
                SysWMInfo info;
                try
                {
                     window = NativeMethods.CreateWindow(
                        "SDL GLControl", 0, 0, 10, 10, DefaultFlags);
                    if (window != IntPtr.Zero && NativeMethods.GetWindowWMInfo(window, out info))
                    {
                        winforms_parent = parent;
                        sdl_window = Utilities.CreateSdl2WindowInfo(window);

                        if (NativeMethods.GetWindowWMInfo(window, out info))
                        {
                            switch (info.Subsystem)
                            {
                                case SysWMType.Windows:
                                    AssignHandle(info.Info.Windows.Window);
                                    break;

                                case SysWMType.X11:
                                    AssignHandle(info.Info.X11.Window);
                                    break;

                                default:
                                    throw new PlatformNotSupportedException(
                                        "SDL2 Windows.Forms integration is not supported on this platform.");
                            }
                        }
                    }
                    else
                    {
                        throw new PlatformNotSupportedException(
                            String.Format("SDL GetWindowWMInfo failed with error {0}", NativeMethods.GetError()));
                    }
                }
                catch
                {
                    if (window != IntPtr.Zero)
                    {
                        //NativeMethods.DestroyWinodw
                    }
                    throw;
                }
            }

            public override void CreateHandle(CreateParams cp)
            {
                cp.Parent = winforms_parent.Handle;
                cp.X = cp.Y = 0;
                cp.Width = winforms_parent.Width;
                cp.Height = winforms_parent.Height;
                base.CreateHandle(cp);
            }

            public IWindowInfo WindowInfo
            {
                get { return sdl_window; }
            }
        }


        Sdl2Wrapper wrapper;
        GraphicsMode mode;

        public Sdl2GLControl(GraphicsMode mode, Control control)
        {
            this.mode = mode;
            wrapper = new Sdl2Wrapper(control, mode);
        }

        public Graphics.IGraphicsContext CreateContext(int major, int minor, Graphics.GraphicsContextFlags flags)
        {
            return new GraphicsContext(mode, wrapper.WindowInfo, major, minor, flags);
        }

        public bool IsIdle
        {
            get { return NativeMethods.HasEvents(0, 0xffff); }
        }

        public Platform.IWindowInfo WindowInfo
        {
            get { return wrapper.WindowInfo; }
        }

        static class NativeMethods
        {
            const string lib = "SDL2.dll";

            static string IntPtrToString(IntPtr ptr)
            {
                return Marshal.PtrToStringAnsi(ptr);
            }

            [DllImport(lib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_CreateWindow", ExactSpelling = true)]
            public static extern IntPtr CreateWindow(string title, int x, int y, int w, int h, WindowFlags flags);

            [DllImport(lib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_DestroyWindow", ExactSpelling = true)]
            public static extern void DestroyWindow(IntPtr window);

            [DllImport(lib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetError", ExactSpelling = true)]
            static extern IntPtr GetErrorInternal();
            public static string GetError()
            {
                return IntPtrToString(GetErrorInternal());
            }

            [DllImport(lib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetVersion", ExactSpelling = true)]
            public static extern void GetVersion(out Version version);

            [DllImport(lib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_HasEvents", ExactSpelling = true)]
            public static extern bool HasEvents(int minType, int maxType);

            public static bool GetWindowWMInfo(IntPtr window, out SysWMInfo info)
            {
                info = new SysWMInfo();
                GetVersion(out info.Version);
                return GetWindowWMInfoInternal(window, ref info);
            }

            [DllImport(lib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetWindowWMInfo", ExactSpelling = true)]
            static extern bool GetWindowWMInfoInternal(IntPtr window, ref SysWMInfo info);
        }

        enum WindowFlags
        {
            Default = 0,
            OpenGL = 0x00000002,
            Shown = 0x00000004,
            Borderless = 0x00000010,
            AllowHighDpi = 0x00002000,
        }

        enum SysWMType
        {
            Unknown = 0,
            Windows,
            X11,
            Wayland,
            DirectFB,
            Cocoa,
            UIKit,
        }

        struct SysWMInfo
        {
            public Version Version;
            public SysWMType Subsystem;
            public SysInfo Info;

            [StructLayout(LayoutKind.Explicit)]
            public struct SysInfo
            {
                [FieldOffset(0)]
                public WindowsInfo Windows;
                [FieldOffset(0)]
                public X11Info X11;
                [FieldOffset(0)]
                public WaylandInfo Wayland;
                [FieldOffset(0)]
                public DirectFBInfo DirectFB;
                [FieldOffset(0)]
                public CocoaInfo Cocoa;
                [FieldOffset(0)]
                public UIKitInfo UIKit;

                public struct WindowsInfo
                {
                    public IntPtr Window;
                }

                public struct X11Info
                {
                    public IntPtr Display;
                    public IntPtr Window;
                }

                public struct WaylandInfo
                {
                    public IntPtr Display;
                    public IntPtr Surface;
                    public IntPtr ShellSurface;
                }

                public struct DirectFBInfo
                {
                    public IntPtr Dfb;
                    public IntPtr Window;
                    public IntPtr Surface;
                }

                public struct CocoaInfo
                {
                    public IntPtr Window;
                }

                public struct UIKitInfo
                {
                    public IntPtr Window;
                }
            }
        }

        struct Version
        {
            public byte Major;
            public byte Minor;
            public byte Patch;

            public int Number
            {
                get { return 1000 * Major + 100 * Minor + Patch; }
            }
        }
    }
}
