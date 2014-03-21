 #region License
 //
 // The Open Toolkit Library License
 //
 // Copyright (c) 2006 - 2010 the Open Toolkit library.
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
using System.Diagnostics;
using System.Runtime.InteropServices;
using OpenTK.Input;

namespace OpenTK.Platform.X11
{
    // Todo: multi-mouse support. Right now we aggregate all data into a single mouse device.
    // This should be easy: just read the device id and route the data to the correct device.
    sealed class XI2Mouse : IMouseDriver2
    {
        class MouseDescriptor
        {
            public MouseState State;
            public XIDeviceInfo Device;
        }
        
        List<MouseDescriptor> mice = new List<MouseDescriptor>();
        Dictionary<int, int> rawids = new Dictionary<int, int>(); // maps raw ids to mouse ids
        internal readonly X11WindowInfo window;
        static int XIOpCode;

        static readonly Type XIRawEventType = typeof(XIRawEvent);
        static readonly Type XIValuatorClassInfoType = typeof(XIValuatorClassInfo);

        static readonly Functions.EventPredicate PredicateImpl = IsEventValid;
        readonly IntPtr Predicate = Marshal.GetFunctionPointerForDelegate(PredicateImpl);

        public XI2Mouse()
        {
            Debug.WriteLine("Using XI2Mouse.");

            using (new XLock(API.DefaultDisplay))
            {
                window = new X11WindowInfo();
                window.Display = API.DefaultDisplay;
                window.Screen = Functions.XDefaultScreen(window.Display);
                window.RootWindow = Functions.XRootWindow(window.Display, window.Screen);
                window.Handle = window.RootWindow;
            }

            if (!IsSupported(window.Display))
                throw new NotSupportedException("XInput2 not supported.");

            using (XIEventMask mask = new XIEventMask(
                1, // AllMasterDevices
                XIEventMasks.RawButtonPressMask |
                XIEventMasks.RawButtonReleaseMask |
                XIEventMasks.RawMotionMask))
            {
                Functions.XISelectEvents(window.Display, window.Handle, mask);
                Functions.XISelectEvents(window.Display, window.RootWindow, mask);
            }
        }

        // Checks whether XInput2 is supported on the specified display.
        // If a display is not specified, the default display is used.
        internal static bool IsSupported(IntPtr display)
        {
            if (display == IntPtr.Zero)
                display = API.DefaultDisplay;

            using (new XLock(display))
            {
                int major, ev, error;
                if (Functions.XQueryExtension(display, "XInputExtension", out major, out ev, out error) == 0)
                {
                    return false;
                }
                XIOpCode = major;
            }

            return true;
        }

        #region IMouseDriver2 Members

        public MouseState GetState()
        {
            ProcessEvents();
            MouseState master = new MouseState();
            foreach (MouseDescriptor ms in mice)
            {
                master.MergeBits(ms.State);
            }
            return master;
        }

        public MouseState GetState(int index)
        {
            ProcessEvents();
            if (mice.Count > index)
                return mice[index].State;
            else
                return new MouseState();
        }

        public void SetPosition(double x, double y)
        {
            using (new XLock(window.Display))
            {
                Functions.XIWarpPointer(
                    window.Display,
                    1, // AllMasterDevices
                    IntPtr.Zero,
                    window.RootWindow,
                    0, 0, 0, 0,
                    (int)Math.Round(x), (int)Math.Round(y));
                Functions.XSync(window.Display, false);
            }

            ProcessEvents();
        }

        #endregion

        void ProcessEvents()
        {
            while (true)
            {
                XEvent e = new XEvent();
                XGenericEventCookie cookie;

                using (new XLock(window.Display))
                {
                    if (!Functions.XCheckIfEvent(window.Display, ref e, Predicate, new IntPtr(XIOpCode)))
                        return;

                    cookie = e.GenericEventCookie;
                    if (Functions.XGetEventData(window.Display, ref cookie) != 0)
                    {
                        XIRawEvent raw = (XIRawEvent)
                            Marshal.PtrToStructure(cookie.data, XIRawEventType);

                        MouseDescriptor mouse = GetMouseDescriptor(raw.deviceid);
                        MouseState state = mouse.State;

                        switch (raw.evtype)
                        {
                            case XIEventType.RawMotion:
                                state = ProcessRawMotion(mouse, ref raw);
                                break;

                            case XIEventType.RawButtonPress:
                                switch (raw.detail)
                                {
                                    case 1: state.EnableBit((int)MouseButton.Left); break;
                                    case 2: state.EnableBit((int)MouseButton.Middle); break;
                                    case 3: state.EnableBit((int)MouseButton.Right); break;
                                    case 4: state.WheelPrecise++; break;
                                    case 5: state.WheelPrecise--; break;
                                    case 6: state.EnableBit((int)MouseButton.Button1); break;
                                    case 7: state.EnableBit((int)MouseButton.Button2); break;
                                    case 8: state.EnableBit((int)MouseButton.Button3); break;
                                    case 9: state.EnableBit((int)MouseButton.Button4); break;
                                    case 10: state.EnableBit((int)MouseButton.Button5); break;
                                    case 11: state.EnableBit((int)MouseButton.Button6); break;
                                    case 12: state.EnableBit((int)MouseButton.Button7); break;
                                    case 13: state.EnableBit((int)MouseButton.Button8); break;
                                    case 14: state.EnableBit((int)MouseButton.Button9); break;
                                }
                                break;

                            case XIEventType.RawButtonRelease:
                                switch (raw.detail)
                                {
                                    case 1: state.DisableBit((int)MouseButton.Left); break;
                                    case 2: state.DisableBit((int)MouseButton.Middle); break;
                                    case 3: state.DisableBit((int)MouseButton.Right); break;
                                    case 6: state.DisableBit((int)MouseButton.Button1); break;
                                    case 7: state.DisableBit((int)MouseButton.Button2); break;
                                    case 8: state.DisableBit((int)MouseButton.Button3); break;
                                    case 9: state.DisableBit((int)MouseButton.Button4); break;
                                    case 10: state.DisableBit((int)MouseButton.Button5); break;
                                    case 11: state.DisableBit((int)MouseButton.Button6); break;
                                    case 12: state.DisableBit((int)MouseButton.Button7); break;
                                    case 13: state.DisableBit((int)MouseButton.Button8); break;
                                    case 14: state.DisableBit((int)MouseButton.Button9); break;
                                }
                                break;
                        }
                        mice[rawids[raw.deviceid]].State = state;
                    }
                    Functions.XFreeEventData(window.Display, ref cookie);
                }
             }
        }

        MouseDescriptor GetMouseDescriptor(int deviceid)
        {
            if (!rawids.ContainsKey(deviceid))
            {
                XIDeviceInfo info;
                if (Functions.XIQueryDevice(
                    window.Display,
                    deviceid,
                    out info))
                {
                    MouseDescriptor mouse = new MouseDescriptor();
                    mouse.State.SetIsConnected(info.enabled);
                    mouse.Device = info;

                    mice.Add(mouse);
                    rawids.Add(deviceid, mice.Count - 1);
                }
            }
            return mice[rawids[deviceid]];
        }

        MouseState ProcessRawMotion(MouseDescriptor mouse, ref XIRawEvent raw)
        {
            MouseState state = mouse.State;
            double x = 0;
            double y = 0;

            for (int i = 0; i < mouse.Device.num_classes; i++)
            {
                IntPtr pinfo =
                    Marshal.ReadIntPtr(mouse.Device.classes, i * IntPtr.Size);

                // Make sure the class info pointer is valid
                if (pinfo == IntPtr.Zero)
                    continue;

                // We only want to read valuator information
                XIClassType type = (XIClassType)Marshal.ReadInt32(pinfo);
                if (type != XIClassType.Valuator)
                    continue;

                // This is a valuator class, proceed
                unsafe
                {
                    XIValuatorClassInfo vinfo =
                        *(XIValuatorClassInfo*)pinfo;

                    double range = vinfo.max - vinfo.min;
                    if (range <= 0.0)
                        range = 1.0;
    
                    if (IsBitSet(raw.valuators.mask, raw.valuators.mask_len, vinfo.number))
                    {
                        // Todo: check for "Rel X", "Rel Y" labels
                        if (vinfo.number == 0 && vinfo.mode == XIValuatorMode.Relative)
                        {
                            x = BitConverter.Int64BitsToDouble(Marshal.ReadInt64(raw.valuators.values, 0));
                            //x /= range;
                        }
                        else if (vinfo.number == 1 && vinfo.mode == XIValuatorMode.Relative)
                        {
                            y = BitConverter.Int64BitsToDouble(Marshal.ReadInt64(raw.valuators.values, 8));
                            //y /= range;
                        }
                    }
                }
            }
            state.X += (int)Math.Round(x);
            state.Y += (int)Math.Round(y);
            return state;
        }

        static bool IsEventValid(IntPtr display, ref XEvent e, IntPtr arg)
        {
            return e.GenericEventCookie.extension == arg.ToInt32() &&
                (e.GenericEventCookie.evtype == (int)XIEventType.RawMotion ||
                e.GenericEventCookie.evtype == (int)XIEventType.RawButtonPress ||
                e.GenericEventCookie.evtype == (int)XIEventType.RawButtonRelease);
        }

        static bool IsBitSet(IntPtr mask, int mask_len, int bit)
        {
            int byte_offset = bit / 8;
            int bit_offset = bit - (byte_offset * 8);
            if (bit < 0 || byte_offset >= mask_len)
            {
                Debug.Print("[X11] Error: invalid bit offset {0} in XIRawEvent. Possible memory alignment bug.", bit);
                return false;
            }

            byte b = Marshal.ReadByte(mask, byte_offset);
            return (b & (1 << bit_offset)) != 0;
        }
    }
}

