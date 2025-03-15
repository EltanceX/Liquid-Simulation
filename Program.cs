#define GPUEnabled

using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Unity;
using Unity.Mathematics;
using Random = System.Random;


namespace SPH2D_2
{
    public class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
    public class ParticleDataList
    {
        public SPH2D SPHInstance;
        public float[] Densities;
        public Vector2[] Positions;
        public Vector2[] Velocities;
        public Vector2[] PredictedPositions;
        public Vector2[] PressureForces;
        public RectangleF[] CollisionBoxes; //Left Bottom Width Height
        public ParticleDataList(int Count, SPH2D sph)
        {
            SPHInstance = sph;
            Densities = new float[Count];
            Positions = new Vector2[Count];
            Velocities = new Vector2[Count];
            PredictedPositions = new Vector2[Count];
            PressureForces = new Vector2[Count];
            CollisionBoxes = new RectangleF[0];
            //CollisionBoxes[0] = new RectangleF(0, 0, 5, 1);
        }
        public void AddCollision(RectangleF rect)
        {
            Array.Resize(ref CollisionBoxes, CollisionBoxes.Length + 1);
            CollisionBoxes[CollisionBoxes.Length - 1] = rect;
        }
        public void AddParticle(Vector2 position)
        {
            Array.Resize(ref Densities, Densities.Length + 1);
            Densities[Densities.Length - 1] = 0;
            Array.Resize(ref Positions, Positions.Length + 1);
            Positions[Positions.Length - 1] = position;
            Array.Resize(ref Velocities, Velocities.Length + 1);
            Velocities[Velocities.Length - 1] = new Vector2(0);
            Array.Resize(ref PredictedPositions, PredictedPositions.Length + 1);
            PredictedPositions[PredictedPositions.Length - 1] = position;
            Array.Resize(ref PressureForces, PressureForces.Length + 1);
            PressureForces[PressureForces.Length - 1] = new Vector2(0);
            SPHInstance.Particles.Add(new Particle(position, SPHInstance.particleDataList));
        }
    }
    public class Particle
    {
        public static int IndexDefined = -1;
        public int I;
        public Vector2 Position
        {
            get => DataList.Positions[I];
            set => DataList.Positions[I] = value;
        }
        //public Vector2[] Positions;
        public Vector2 PredictedPosition
        {
            get => DataList.PredictedPositions[I];
            set => DataList.PredictedPositions[I] = value;
        }
        //public Vector2[] PredictedPositions;
        public Vector2 velocity
        {
            get => DataList.Velocities[I];
            set => DataList.Velocities[I] = value;
        }
        //public Vector2[] Velocities;
        public float property = 0;
        public float propertyCalc = 0;
        public float density
        {
            get => DataList.Densities[I];
            set => DataList.Densities[I] = value;
        }
        public Vector2 PressureForce
        {
            get => DataList.PressureForces[I];
            set => DataList.PressureForces[I] = value;
        }
        //public float[] Densities;
        public Vector2 gradient = new Vector2(0, 0);
        public ParticleDataList DataList;
        public Particle(Vector2 position, ParticleDataList dataList, int index = -1)
        {
            if (index == -1) I = ++IndexDefined;
            DataList = dataList;
            dataList.Positions[I] = position;
            dataList.Densities[I] = 0;
            dataList.Velocities[I] = new Vector2(0, 0);
            dataList.PredictedPositions[I] = new Vector2(0, 0);
        }
    }
    public class SPH2D
    {
        //public const bool GPUEnabled = false;

        public float gravity = 1f;
        public float particleSize = 0.06f;
        public float particleSpacing = 0f;
        public Vector2 BoundsSize = new Vector2(16, 9);
        public float collisionDamping = 0.85f;
        public float smoothingRadius = 0.25f;
        public float mass = 2;
        public float targetDensity = 10.0f; //2.75
        public float pressureMultiplier = 5f; //0.5

        public float MouseAffectRange = 1f;
        public ParticleDataList particleDataList;
        public bool Updating = false;

        public List<Particle> Particles = new List<Particle>();
        public SPH2D()
        {
            //Particles.Add(new Particle(new Vector2(0f, 0f)));
            //CreateParticles(66666, 400);
            Start(900);
        }
        public void ResolveBoindCollision(Particle particle, ref Vector2 NextPos)
        {
            Vector2 halfBoundsSize = BoundsSize / 2 - Vector2.One * particleSize;
            if (Math.Abs(NextPos.X) > halfBoundsSize.X)
            {
                NextPos.X = halfBoundsSize.X * Math.Sign(NextPos.X);
                particle.DataList.Velocities[particle.I].X *= -1 * collisionDamping;
            }
            if (Math.Abs(NextPos.Y) > halfBoundsSize.Y)
            {
                NextPos.Y = halfBoundsSize.Y * Math.Sign(NextPos.Y);
                particle.DataList.Velocities[particle.I].Y *= -1 * collisionDamping;
            }
        }

        public static bool PosContains(RectangleF rect, Vector2 pos)
        {
            if (pos.X >= rect.X && pos.Y >= rect.Y &&
                pos.X <= rect.Right && pos.Y <= rect.Y + rect.Height)
            {
                return true;
            }
            return false;
        }
        public static bool PosContainsX(RectangleF rect, Vector2 pos)
        {
            if (pos.X >= rect.X && pos.X <= rect.Right) return true;
            return false;
        }
        public static bool PosContainsY(RectangleF rect, Vector2 pos)
        {
            if (pos.Y >= rect.Y && pos.Y <= rect.Y + rect.Height) return true;
            return false;
        }
        public void ResolveBoxCollision(Particle particle, ref Vector2 NextPos)
        {
            var boxes = particleDataList.CollisionBoxes;
            var pos = particle.Position;
            var velocities = particleDataList.Velocities;
            for (int i = 0; i < boxes.Length; i++)
            {
                var box = boxes[i];
                if (PosContains(boxes[i], NextPos))
                {
                    //if (PosContains(boxes[i], pos))
                    //{
                    //    float BottomDst = pos.Y - box.Y;
                    //    float TopDst = box.Height - BottomDst;
                    //    NextPos.Y = BottomDst > TopDst ? box.Y + box.Height : box.Y;
                    //    velocities[particle.I].Y *= -1 * collisionDamping;

                    //    continue;
                    //}

                    //NextPos.X = pos.X - box.X > 0 ? box.Right : box.X;
                    //velocities[particle.I].X *= -1 * collisionDamping;

                    //NextPos.Y = pos.Y - box.Y > 0 ? box.Y + box.Height : box.Y;
                    //velocities[particle.I].Y *= -1 * collisionDamping;

                    if (pos.X >= box.Right)
                    {
                        NextPos.X = box.Right;
                        velocities[particle.I].X *= -1 * collisionDamping;
                    }
                    else if (pos.X <= box.Left)
                    {
                        NextPos.X = box.X;
                        velocities[particle.I].X *= -1 * collisionDamping;
                    }

                    if (pos.Y >= box.Y + box.Height)
                    {
                        NextPos.Y = box.Y + box.Height;
                        velocities[particle.I].Y *= -1 * collisionDamping;
                    }
                    else if (pos.Y <= box.Y)
                    {
                        NextPos.Y = box.Y;
                        velocities[particle.I].Y *= -1 * collisionDamping;
                    }
                    //break;
                }
            }
        }
        public void ResolveCollisions(Particle particle, float dt)
        {
            Vector2 NextPos = particle.Position + particle.velocity * dt;

            ResolveBoindCollision(particle, ref NextPos);
            ResolveBoxCollision(particle, ref NextPos);

            particle.Position = NextPos;
        }
        public List<Particle> GetParticles()
        {
            return Particles;
        }
        public void CreateParticles(int seed, int num)
        {
            //Random rng = new Random(seed);
            //Particles = new List<Particle>();
            //for (int i = 0; i < num; i++)
            //{
            //    float x = (float)((rng.NextDouble() - 0.5) * BoundsSize.X);
            //    float y = (float)((rng.NextDouble() - 0.5) * BoundsSize.Y);
            //    Vector2 pos = new Vector2(x, y);
            //    var particle = new Particle(pos);
            //    particle.property = ExampleFunction(pos);
            //    Particles.Add(particle);
            //}

        }
        public void Start(int num)
        {
            ParticleDataList particleDataList = new ParticleDataList(num, this);
            this.particleDataList = particleDataList;
            int particlesPerRow = (int)Math.Sqrt(num);
            int particlesPerCow = (num - 1) / particlesPerRow + 1;
            float spacing = particleSize * 2 + particleSpacing;
            for (int i = 0; i < num; i++)
            {
                float x = (i % particlesPerRow - particlesPerRow / 2f + 0.5f) * spacing;
                float y = (i / particlesPerRow - particlesPerRow / 2f + 0.5f) * spacing;
                Particles.Add(new Particle(new Vector2(x, y), particleDataList));
            }
        }
        public void Update(float dt)
        {
            if (Updating) return;
            Updating = true;



            foreach (var particle in Particles)
            {
                particle.velocity += (-1 * Vector2.UnitY) * gravity * dt;
                ProcessMouseEvent(particle, dt);
                //Update density
                //particle.PredictedPosition = particle.Position + particle.velocity * 1 / 120;
                particle.PredictedPosition = particle.Position + particle.velocity * dt;

            }

#if GPUEnabled
            Stopwatch sw = Stopwatch.StartNew();

            var computeData = Compute.ComputeParticleData(this);
            sw.Stop();
            Console.WriteLine($"Timing: {sw.Elapsed.TotalMilliseconds}");

            particleDataList.Densities = computeData.Densities;
            particleDataList.PressureForces = computeData.PressureForces;

            //Pressure
            foreach (var particle in Particles)
            {
                //Vector2 pressureForce = CalculatePressureForce(particle);
                Vector2 pressureForce = particle.PressureForce;
                particle.gradient = pressureForce;
                Vector2 pressureAcceleration = pressureForce / particle.density;
                particle.velocity += pressureAcceleration * dt; //+=赋予速度，=表现压力
            }
#else
            foreach (var particle in Particles)
            {
                //particle.density  = CalculateDensity(particle.Position);
                particle.density = CalculateDensity(particle.PredictedPosition);
            }
            foreach (var particle in Particles)
            {
                Vector2 pressureForce = CalculatePressureForce(particle);
                //Vector2 pressureForce = particle.PressureForce;
                particle.gradient = pressureForce;
                Vector2 pressureAcceleration = pressureForce / particle.density;
                particle.velocity += pressureAcceleration * dt; //+=赋予速度，=表现压力
            }

#endif

            foreach (var particle in Particles)
            {
                //particle.Position += particle.velocity * dt;
                ResolveCollisions(particle, dt);
            }


            //foreach (var particle in Particles)
            //{
            //    particle.propertyCalc = CalculateProperty(particle.Position);
            //}
            Updating = false;
        }
        public void ProcessMouseEvent(Particle particle, float dt)
        {
            if (!(ControlState.MouseLeftDown || ControlState.MouseRightDown) || ControlState.mouseFunction != MouseFunction.Adsorbates) return;
            Vector2 pos = ControlState.MousePosition;
            if (pos == null) return;

            Vector2 relative = pos - particle.Position;
            Vector2 relative2 = particle.Position - pos;
            float dst = relative.Length();
            if (dst > MouseAffectRange) return;

            if (ControlState.MouseLeftDown)
            {
                particle.velocity += Vector2.Normalize(relative) * 10 * dt;// * dt;
            }
            else if (ControlState.MouseRightDown)
            {
                particle.velocity += Vector2.Normalize(relative2) * 10 * dt;// * dt;

            }
        }



        public static float SmoothingKernel(float dst, float radius)
        {
            if (dst >= radius) return 0;

            float volume = (float)((Math.PI * Math.Pow(radius, 4)) / 6);
            return (radius - dst) * (radius - dst) / volume;
        }
        public static float SmoothingKernalDerivate(float dst, float radius)
        {
            if (dst >= radius) return 0;
            float scale = (float)(12 / (Math.Pow(radius, 4) * Math.PI));
            return (dst - radius) * scale;
        }


        public float CalculateDensity(Vector2 samplePoint)
        {
            float density = 1;
            foreach (var particle in Particles)
            {
                //float dst = (particle.Position - samplePoint).Length();
                float dst = (particle.PredictedPosition - samplePoint).Length();
                //Vector2.Normalize()
                float influence = SmoothingKernel(dst, smoothingRadius);
                density += mass * influence;
            }
            return density;
        }
        public float ExampleFunction(Vector2 pos)
        {
            return (float)Math.Cos(pos.Y - 3 + Math.Sin(pos.X));
        }
        public float ConvertDensityToPressure(float density)
        {
            float densityError = density - targetDensity;
            float pressure = densityError * pressureMultiplier;
            return pressure;
        }
        public float CalculateSharedPressure(float densityA, float densityB)
        {
            float pressureA = ConvertDensityToPressure(densityA);
            float pressureB = ConvertDensityToPressure(densityB);
            return (pressureA + pressureB) / 2;
        }
        public Vector2 CalculatePressureForce(Particle particle)
        {
            //Vector2 samplePoint = particle.Position;
            Vector2 samplePoint = particle.PredictedPosition;
            Vector2 pressureForce = Vector2.Zero;

            for (int i = 0; i < Particles.Count; i++)
            {
                //float dst = (Particles[i].Position - samplePoint).Length();
                float dst = (Particles[i].PredictedPosition - samplePoint).Length();
                if (dst == 0) continue;
                //Vector2 dir = (Particles[i].Position - samplePoint) / dst;
                //dst = Math.Max(dst, 0.5f);
                Vector2 dir = (Particles[i].PredictedPosition - samplePoint) / dst;
                float slope = SmoothingKernalDerivate(dst, smoothingRadius);
                //float density = CalculateDensity(Particles[i].Position);
                //if (dst <= 0.01) Debugger.Break();

                float density = Particles[i].density;
                //pressureForce += -Particles[i].propertyCalc * dir * slope * mass / density;
                //pressureForce += ConvertDensityToPressure(density) * dir * slope * mass / density;

                float otherDensity = Particles[i].density;
                float sharedPressure = CalculateSharedPressure(otherDensity, particle.density);
                pressureForce += sharedPressure * dir * slope * mass / otherDensity;

            }
            return pressureForce;
        }
    }
}
