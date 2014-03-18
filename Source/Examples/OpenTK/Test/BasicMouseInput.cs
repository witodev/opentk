// This code was written for the OpenTK library and has been released
// to the Public Domain by Andy Korth
// It is provided "as is" without express or implied warranty of any kind.

#region --- Using Directives ---

using System;
using System.Collections.Generic;
using System.Drawing;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;

#endregion --- Using Directives ---

namespace Examples.Tests
{
    /// <summary>
    /// Demonstrates basic recommended mouse input, and to see if it actually works
    /// </summary>
    [Example("Basic Mouse Input", ExampleCategory.OpenTK,"Basic Mouse Input")]
    public class BasicMouseInput : GameWindow
    {
        Font text_font = new Font(FontFamily.GenericSansSerif, 16);
        Bitmap text_surface;
        Graphics text_renderer;
        bool size_changed = true;

        int texture;

        MouseState mouse_previous;
        KeyboardState keyboard_previous;

        public BasicMouseInput()
            : base(800, 600)
        { }

        void RecreateTextures()
        {
            if (text_surface != null)
            {
                text_surface.Dispose();
            }
            if (text_renderer != null)
            {
                text_renderer.Dispose();
            }

            text_surface = new Bitmap(
                Width,
                Height,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            text_renderer = Graphics.FromImage(text_surface);

            if (GL.IsTexture(texture))
            {
                GL.BindTexture(TextureTarget.Texture2D, 0);
                GL.DeleteTexture(texture);
            }

            GL.Enable(EnableCap.Texture2D);
            texture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, texture);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)All.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)All.Nearest);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, Width, Height,
                0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
        }

        protected override void OnLoad(EventArgs e)
        {
            MouseEnter += (sender, ev) =>
                Console.WriteLine("MouseEnter: at " + new Point(Mouse.X, Mouse.Y));
            MouseLeave += (sender, ev) =>
                Console.WriteLine("MouseLeave: at " + new Point(Mouse.X, Mouse.Y));
            Mouse.ButtonUp += (sender, ev) =>
                Console.WriteLine("Mouse.ButtonUp: " + ev.Button + " at " + ev.Position);
            Mouse.ButtonDown += (sender, ev) =>
                Console.WriteLine("Mouse.ButtonDown: " + ev.Button + " at " + ev.Position);
            Mouse.Move += (sender, ev) => 
                Console.WriteLine("Mouse.Move: at " + ev.Position);

            RecreateTextures();
        }

        protected override void OnResize(EventArgs e)
        {
            size_changed = true;
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            var mouse = OpenTK.Input.Mouse.GetState();
            var keyboard = OpenTK.Input.Keyboard.GetState();

            if (mouse[MouseButton.Left])
            {
                if (CursorVisible)
                {
                    CursorVisible = false;
                }
            }

            if (keyboard[Key.Escape] && !keyboard_previous[Key.Escape])
            {
                if (!CursorVisible)
                {
                    CursorVisible = true;
                }
                else
                {
                    Exit();
                }
            }

            if (keyboard[Key.Enter])
            {
                if (WindowState != WindowState.Fullscreen)
                {
                    WindowState = WindowState.Fullscreen;
                }
                else
                {
                    WindowState = WindowState.Normal;
                }
            }

            if (keyboard[Key.Space])
            {
                Point point = new Point(Width / 2, Height / 2);
                point = PointToScreen(point);
                OpenTK.Input.Mouse.SetPosition(
                    point.X,
                    point.Y);
            }

            if (size_changed)
            {
                size_changed = false;
                RecreateTextures();
            }

            text_renderer.Clear(Color.MidnightBlue);

            text_renderer.DrawString(
                "Click to hide mouse cursor",
                text_font, Brushes.White, 0, 0);

            text_renderer.DrawString(
                "Press space to reset mouse position",
                text_font, Brushes.White, 0, 24);

            text_renderer.DrawString(
                "Press escape to exit",
                text_font, Brushes.White, 0, 48);

            text_renderer.DrawString(
                String.Format("OpenTK.Input.Mouse: ({0}; {1})", mouse.X, mouse.Y),
                text_font, Brushes.White, 0, 72);

            text_renderer.DrawString(
                String.Format("GameWindow.Mouse: ({0}; {1})", Mouse.X, Mouse.Y),
                text_font, Brushes.White, 0, 96);

            mouse_previous = mouse;
            keyboard_previous = keyboard;
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            GL.Viewport(ClientRectangle);
            GL.Clear(ClearBufferMask.ColorBufferBit);

            GL.Enable(EnableCap.Texture2D);
            GL.BindTexture(TextureTarget.Texture2D, texture);

            var data =
                text_surface.LockBits(
                    new Rectangle(Point.Empty, text_surface.Size),
                    System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0,
                data.Width, data.Height,
                PixelFormat.Bgra, PixelType.UnsignedByte,
                data.Scan0);
            text_surface.UnlockBits(data);

            GL.Begin(PrimitiveType.Quads);

            GL.TexCoord2(0.0f, 1.0f); GL.Vertex2(-1f, -1f);
            GL.TexCoord2(1.0f, 1.0f); GL.Vertex2(1f, -1f);
            GL.TexCoord2(1.0f, 0.0f); GL.Vertex2(1f, 1f);
            GL.TexCoord2(0.0f, 0.0f); GL.Vertex2(-1f, 1f);

            GL.End();

            SwapBuffers();
        }

        [STAThread]
        public static void Main()
        {
            using (BasicMouseInput example = new BasicMouseInput())
            {
                // Get the title and category  of this example using reflection.
                ExampleAttribute info = ((ExampleAttribute)example.GetType().GetCustomAttributes(false)[0]);
                example.Title = String.Format("OpenTK | {0} {1}: {2}", info.Category, info.Difficulty, info.Title);

                example.Run(30.0);
            }
        }

    }
}
