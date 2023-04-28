using System;
using SFML.Audio;
using SFML.Graphics;
using SFML.System;
using SFML.Window;


namespace CHIP8
{
    class Program
    {
        public static Chip8 CPU = new();
        private static readonly uint scale = 20;
        private static readonly uint inputDelay = 75;
        private static RenderWindow window;

        static void Main(string[] args)
        {
            Console.WriteLine("Start");
            window = new RenderWindow(new VideoMode(64 * scale, 32 * scale), "CHIP-8");
            window.Closed += (_, __) => window.Close();

            window.KeyPressed += new(KeyboardDown);
            window.KeyReleased += new(KeyboardUp);

            CPU.Initialize();
            CPU.LoadROM(File.ReadAllBytes("../../../roms/pong2.c8"));

            while (window.IsOpen)
            {
                CPU.EmulateCycle();

                window.DispatchEvents();

                window.Clear(Color.Black);
                if (CPU.DrawFlag)
                    DrawGraphics(window);

                /*foreach ((var k, var v) in KeyMappping)
                {
                    CPU.Key[v] = Convert.ToByte(Keyboard.IsKeyPressed(k));
                }*/

                window.Display();
            }
        }

        public static void DrawGraphics (RenderWindow window)
        {
            var width = 64;
            var height = 32;
            for (var i = 0; i < width * height; i++)
            {
                var x = (i % width) * scale;
                var y = (i / width) * scale;

                if (Convert.ToBoolean(CPU.GFX[i]))
                {
                    RectangleShape pixel = new()
                    {
                        Size = new Vector2f(scale, scale),
                        FillColor = Color.White,
                        Position = new Vector2f(x, y)
                    };

                    window.Draw(pixel);
                }
            }
        }

        private static SoundBuffer GenerateSineWave(double frequency, double volume, int seconds)
        {
            uint sampleRate = 44100;
            var samples = new short[seconds * sampleRate];

            for (int i = 0; i < samples.Length; i++)
                samples[i] = (short)(Math.Sin(frequency * (2 * Math.PI) * i / sampleRate) * volume * short.MaxValue);

            return new SoundBuffer(samples, 1, sampleRate);
        }

        static readonly Dictionary<Keyboard.Key, byte> KeyMappping = new()
        {
            { Keyboard.Key.Num1, 0x1 },
            { Keyboard.Key.Num2, 0x2 },
            { Keyboard.Key.Num3, 0x3 },
            { Keyboard.Key.Num4, 0xC },

            { Keyboard.Key.Q, 0x4 },
            { Keyboard.Key.W, 0x5 },
            { Keyboard.Key.E, 0x6 },
            { Keyboard.Key.R, 0xD },

            { Keyboard.Key.A, 0x7 },
            { Keyboard.Key.S, 0x8 },
            { Keyboard.Key.D, 0x9 },
            { Keyboard.Key.F, 0xE },

            { Keyboard.Key.Z, 0xA },
            { Keyboard.Key.X, 0x0 },
            { Keyboard.Key.C, 0xB },
            { Keyboard.Key.V, 0xF },
        };

        static void KeyboardDown(object? sender, KeyEventArgs args)
        {
            if (KeyMappping.ContainsKey(args.Code))
                CPU.Key[KeyMappping[args.Code]] = 1;
            else if (args.Code == Keyboard.Key.P)
            {
                if (CPU.State == State.Running)
                {
                    CPU.State = State.Paused;
                    window.SetTitle("CHIP-8 (Paused)");
                }
                else if (CPU.State == State.Paused)
                {
                    CPU.State = State.Running;
                    window.SetTitle("CHIP-8");
                }
            }
        }

        static void KeyboardUp(object? sender, KeyEventArgs args)
        {
            if (KeyMappping.ContainsKey(args.Code))
                CPU.Key[KeyMappping[args.Code]] = 0;
        }
    }
}