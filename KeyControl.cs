using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SPH2D_2
{
    public enum MouseFunction
    {
        Adsorbates,
        CollisionBoxes,
        NewParticle
    }
    public class ControlState
    {
        public static bool Paused = false;
        public static bool NextFrame = false;
        public static bool GradientDisplay = false;
        public static Vector2 MousePosition;
        //public static bool MouseDown
        public static bool MouseLeftDown = false;
        public static bool MouseRightDown = false;
        public static MouseFunction mouseFunction = MouseFunction.Adsorbates;
        public const int MouseFunctionLength = 2;
    }
    public class CollisionBoxControl
    {
        public static bool Creating = false;
        public static Vector2? Position1;
        public static Vector2? Position2;
        public static void Finish()
        {

            Creating = false;
            Position1 = null;
            Position2 = null;
        }
    }
    //public class MouseControl
    //{
    //    public static bool MouseLeftDown = false;
    //    public static bool MouseRightDown = false;
    //}
    public class KeyControl
    {
        public static Key? LastKey = null;
        public static bool IsKeyDownOnce(Key key)
        {
            if (Keyboard.IsKeyDown(key))
            {
                if (LastKey == null)
                {
                    LastKey = key;
                    return true;
                }
                return false;
            }
            if (LastKey == key) LastKey = null;
            return false;
        }
        public static void Update()
        {
            if (KeyControl.IsKeyDownOnce(Key.Space))
            {
                ControlState.Paused = !ControlState.Paused;
            }
            else if (ControlState.Paused && KeyControl.IsKeyDownOnce(Key.Right))
            {
                ControlState.NextFrame = true;
            }
            else if (KeyControl.IsKeyDownOnce(Key.X))
            {
                ControlState.GradientDisplay = !ControlState.GradientDisplay;
            }

        }
    }

    //public class MouseControl
    //{
    //    public static void Update()
    //    {
    //        //Mouse.AddMouseDownHandler();
    //    }
    //    public static void ProcessLeft(Particle particle)
    //    {

    //    }
    //}
}
