using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.ComponentModel.Com2Interop;
using Timer = System.Windows.Forms.Timer;

namespace SPH2D_2
{
    public partial class MainForm : Form
    {
        private SPH2D sph;
        private Timer timer;
        public Vector2 ScreenSize = new Vector2(1500, 900);
        public Vector2 CurtainSize = new Vector2(1422, 800);
        public float BoxLeft { get { return (ScreenSize.X - CurtainSize.X) / 2; } }
        public float BoxTop { get { return (ScreenSize.Y - CurtainSize.Y) / 2; } }

        public MainForm()
        {
            //InitializeComponent();
            this.DoubleBuffered = true; // 启用双缓冲

            // 初始化SPH
            //sph = new SPH2D(50, 800, 600, 20, 1, 1, 0.1f);
            sph = new SPH2D();
            this.ClientSize = new Size((int)ScreenSize.X, (int)ScreenSize.Y);
            this.Text = "SPH 2D Liquid Simulation - EltanceX";

            Compute.thread = new Thread(Compute.Initialize);
            Compute.thread.Start();
            Compute.manualResetEvent.WaitOne(1000 * 16);

            // 设置定时器
            timer = new Timer();
            timer.Interval = 20; // 约60 FPS
            timer.Tick += Timer_Tick;
            timer.Start();

            // 订阅鼠标点击事件
            //this.MouseClick += new MouseEventHandler(OnMouseClick);
            this.MouseDown += new MouseEventHandler(OnMouseDown);
            this.MouseUp += new MouseEventHandler(OnMouseUp);
            this.MouseMove += new MouseEventHandler(OnMouseMove);
            this.MouseWheel += new MouseEventHandler(OnMouseWheel);
            this.Closing += OnClosing;



        }
        public static void OnClosing(object sender, CancelEventArgs e)
        {
            var gl = Compute.gl;
            //gl.DeleteShader(shader);
            //gl.DeleteProgram(program);
            //gl.DeleteBuffer(buffer);
            var window = Compute.window;
            window.Close();
            gl.Dispose();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            KeyControl.Update();
            if (ControlState.NextFrame)
            {
                ControlState.NextFrame = false;
            }
            else if (ControlState.Paused) return;

            //sph.Update(0.016f); // 更新时间
            //sph.Update(timer.Interval / 1000f); // 更新时间
            sph.Update(1f / 30f); // 更新时间
            this.Invalidate(); // 触发重绘
        }
        public Vector2 ToMapWinformAxis(float x, float y)
        {
            float pixelX = x / sph.BoundsSize.X * CurtainSize.X;
            float pixelY = y / sph.BoundsSize.Y * CurtainSize.Y;
            return new Vector2(pixelX, pixelY);
        }
        public Vector2 ToMapWinformAxis(Vector2 pos)
        {
            float pixelX = pos.X / sph.BoundsSize.X * CurtainSize.X;
            float pixelY = pos.Y / sph.BoundsSize.Y * CurtainSize.Y;
            return new Vector2(pixelX, pixelY);
        }
        public Vector2 ToDrawingAxis(float x, float y)
        {
            float left = BoxLeft;
            float top = BoxTop;
            Vector2 center = new Vector2(left, top) + CurtainSize / 2;

            float rectX = center.X + x;
            float rectY = ScreenSize.Y - (center.Y + y);

            return new Vector2(rectX, rectY);
        }
        public Vector2 ToDrawingAxis(Vector2 pos)
        {
            float left = BoxLeft;
            float top = BoxTop;
            Vector2 center = new Vector2(left, top) + CurtainSize / 2;

            float rectX = center.X + pos.X;
            float rectY = ScreenSize.Y - (center.Y + pos.Y);

            return new Vector2(rectX, rectY);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var particles = sph.GetParticles();
            float left = BoxLeft;
            float top = BoxTop;
            Vector2 center = new Vector2(left, top) + CurtainSize / 2;
            e.Graphics.DrawString("Mouse Function: " + ControlState.mouseFunction.ToString(), new Font("Arial", 12, FontStyle.Regular), new Pen(Color.Gray).Brush, 10, 10);
            foreach (var particle in particles)
            {
                Vector2 PixelVec = ToMapWinformAxis(particle.Position.X, particle.Position.Y);
                Vector2 ParticlePixelSize = ToMapWinformAxis(sph.particleSize, sph.particleSize);

                Vector2 ParticleDrawing = ToDrawingAxis(PixelVec.X, PixelVec.Y);
                float v = Math.Abs(particle.velocity.Length());
                Color vColor = Color.FromArgb(255, Math.Clamp((int)(v / 1f * 255f), 0, 255), 0, v > 10 ? 255 : 0);

                //偏移使粒子显示在中新
                e.Graphics.FillEllipse(new Pen(vColor).Brush, ParticleDrawing.X - ParticlePixelSize.X / 2, ParticleDrawing.Y - ParticlePixelSize.Y / 2, ParticlePixelSize.X, ParticlePixelSize.Y);

                if (ControlState.GradientDisplay)
                {
                    Color color = Color.Green;
                    Vector2 normalg = particle.gradient / 1000f * 100f;
                    if (normalg.LengthSquared() > 100f * 100f) normalg = Vector2.Normalize(normalg) * 100f;
                    try
                    {
                        e.Graphics.DrawLine(new Pen(color), ParticleDrawing.X, ParticleDrawing.Y, ParticleDrawing.X + normalg.X * 2, ParticleDrawing.Y + normalg.Y * 2);
                    }
                    catch (Exception) { }
                }
            }
            if (CollisionBoxControl.Creating && CollisionBoxControl.Position1 != null && CollisionBoxControl.Position2 != null)
            {
                Vector2 start = ToDrawingAxis(ToMapWinformAxis((Vector2)CollisionBoxControl.Position1));
                Vector2 end = ToDrawingAxis(ToMapWinformAxis((Vector2)CollisionBoxControl.Position2));
                // 计算矩形的起点和宽高
                float x = Math.Min(start.X, end.X);
                float y = Math.Min(start.Y, end.Y);
                float width = Math.Abs(end.X - start.X);
                float height = Math.Abs(end.Y - start.Y);
                e.Graphics.DrawRectangle(new Pen(Color.DarkCyan), x, y, width, height);
            }
            foreach (var rect in sph.particleDataList.CollisionBoxes)
            {
                Vector2 PixelVec = ToMapWinformAxis(rect.X, rect.Y);
                Vector2 PixelSize = ToMapWinformAxis(rect.Width, rect.Height);

                Vector2 RectDrawing = ToDrawingAxis(PixelVec.X, PixelVec.Y);

                e.Graphics.DrawRectangle(new Pen(Color.Blue), RectDrawing.X, RectDrawing.Y - PixelSize.Y, PixelSize.X, PixelSize.Y);
            }
            e.Graphics.DrawRectangle(new Pen(Brushes.Gray), left, top, CurtainSize.X, CurtainSize.Y);
        }









        public Vector2 ConvertToSimulatePos(float x, float y)
        {
            y = CurtainSize.Y - y;
            Vector2 center = CurtainSize / 2;
            float particleX = x - center.X;
            float particleY = y - center.Y;

            return new Vector2(particleX / CurtainSize.X * sph.BoundsSize.X, particleY / CurtainSize.Y * sph.BoundsSize.Y);
        }
        public void OnMouseWheel(object sender, MouseEventArgs e)
        {
            int mouseFunction = (int)ControlState.mouseFunction;
            if (e.Delta > 0)// UP
            {
                if (--mouseFunction < 0)
                {
                    mouseFunction = ControlState.MouseFunctionLength;
                }
            }
            else // Down
            {
                if (++mouseFunction > ControlState.MouseFunctionLength)
                {
                    mouseFunction = 0;
                }
            }
            ControlState.mouseFunction = (MouseFunction)mouseFunction;
        }
        public void OnMouseMove(object sender, MouseEventArgs e)
        {
            //相对位置
            Point relativePosition = e.Location;
            float boxRX = e.Location.X - BoxLeft;
            float boxRY = e.Location.Y - BoxTop;
            if (boxRX < 0 || boxRY < 0 || boxRX > CurtainSize.X || boxRY > CurtainSize.Y)
            {
                return;
            }
            Vector2 SimulatePos = ConvertToSimulatePos(boxRX, boxRY);
            ControlState.MousePosition = SimulatePos;

            if (ControlState.mouseFunction == MouseFunction.CollisionBoxes)
            {
                CollisionBoxControl.Position2 = SimulatePos;
            }
            else if (ControlState.mouseFunction == MouseFunction.NewParticle && ControlState.MouseLeftDown)
            {
                sph.particleDataList.AddParticle(SimulatePos);
            }
        }
        public void OnMouseDown(object sender, MouseEventArgs e)
        {
            //Point absolutePosition = Control.MousePosition;

            //相对位置
            Point relativePosition = e.Location;
            float boxRX = e.Location.X - BoxLeft;
            float boxRY = e.Location.Y - BoxTop;
            if (boxRX < 0 || boxRY < 0 || boxRX > CurtainSize.X || boxRY > CurtainSize.Y)
            {
                return;
            }
            Vector2 SimulatePos = ConvertToSimulatePos(boxRX, boxRY);
            ControlState.MousePosition = SimulatePos;
            if (e.Button == MouseButtons.Left)
            {
                ControlState.MouseLeftDown = true;
                Console.WriteLine($"Left Down: {SimulatePos.X}, {SimulatePos.Y}");
                if (ControlState.mouseFunction == MouseFunction.CollisionBoxes)
                {
                    CollisionBoxControl.Creating = true;
                    CollisionBoxControl.Position1 = SimulatePos;
                }
                else if (ControlState.mouseFunction == MouseFunction.NewParticle)
                {
                    sph.particleDataList.AddParticle(SimulatePos);
                }
            }
            else if (e.Button == MouseButtons.Right)
            {
                ControlState.MouseRightDown = true;
                Console.WriteLine("Right Down");
            }
        }
        public void OnMouseUp(object sender, MouseEventArgs e)
        {
            Console.WriteLine("Mouse Up");
            if (e.Button == MouseButtons.Left)
            {
                ControlState.MouseLeftDown = false;
                if (CollisionBoxControl.Creating)
                {
                    var p1 = CollisionBoxControl.Position1;
                    var p2 = CollisionBoxControl.Position2;
                    if (p1 != null && p2 != null)
                    {
                        Vector2 start = (Vector2)p1;
                        Vector2 end = (Vector2)p2;
                        float x = Math.Min(start.X, end.X);
                        float y = Math.Min(start.Y, end.Y);
                        float width = Math.Abs(end.X - start.X);
                        float height = Math.Abs(end.Y - start.Y);
                        sph.particleDataList.AddCollision(new RectangleF(x, y, width, height));
                    }
                    CollisionBoxControl.Finish();
                }
            }
            else if (e.Button == MouseButtons.Right) ControlState.MouseRightDown = false;

        }
    }
}
