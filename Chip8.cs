using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CHIP8
{
    public static class OpcodeExtensions
    {
        public static byte N(this ushort opcode) => (byte)(opcode & 0x000F);
        public static byte NN(this ushort opcode) => (byte)(opcode & 0x00FF);
        public static ushort NNN(this ushort opcode) => (ushort)(opcode & 0x0FFF);
        public static byte X(this ushort opcode) => (byte)((opcode & 0x0F00) >> 8);
        public static byte Y(this ushort opcode) => (byte)((opcode & 0x00F0) >> 4);
    }
    public enum State
    {
        Running,
        Paused,
        AwaitingInput,
        Stopped
    }
    public class Chip8
    {
        // Current operation
        private ushort opcode;
        // Memory
        private byte[] memory = new byte[4096];
        // Data registers
        private byte[] V = new byte[16];
        // Address register - 12 bits wide
        private ushort I;
        // Program counter
        private ushort PC;
        // Stack
        private readonly Stack<ushort> Stack = new();
        // Graphics array
        public byte[] GFX = new byte[64 * 32];
        // Timers
        private byte delayTimer;
        private byte soundTimer;
        // Input
        public byte[] Key = new byte[16];
        // Whether to redraw
        public bool DrawFlag { get; set; } = true;
        public bool Awaiting { get; set; } = false;
        public State State { get; set; } = State.Running;

        static readonly byte[] chip8_fontset =
        {
          0xF0, 0x90, 0x90, 0x90, 0xF0, // 0
          0x20, 0x60, 0x20, 0x20, 0x70, // 1
          0xF0, 0x10, 0xF0, 0x80, 0xF0, // 2
          0xF0, 0x10, 0xF0, 0x10, 0xF0, // 3
          0x90, 0x90, 0xF0, 0x10, 0x10, // 4
          0xF0, 0x80, 0xF0, 0x10, 0xF0, // 5
          0xF0, 0x80, 0xF0, 0x90, 0xF0, // 6
          0xF0, 0x10, 0x20, 0x40, 0x40, // 7
          0xF0, 0x90, 0xF0, 0x90, 0xF0, // 8
          0xF0, 0x90, 0xF0, 0x10, 0xF0, // 9
          0xF0, 0x90, 0xF0, 0x90, 0x90, // A
          0xE0, 0x90, 0xE0, 0x90, 0xE0, // B
          0xF0, 0x80, 0x80, 0x80, 0xF0, // C
          0xE0, 0x90, 0x90, 0x90, 0xE0, // D
          0xF0, 0x80, 0xF0, 0x80, 0xF0, // E
          0xF0, 0x80, 0xF0, 0x80, 0x80  // F
        };

        public void Initialize()
        {
            // Application loaded at location 0x200
            PC = 0x200;
            // Clear address register
            I = 0;
            // Clear display
            ClearScreen();
            // Clear stack
            Stack.Clear();
            // Clear registers V0-VF
            V = new byte[16];
            // Clear memory
            memory = new byte[4096];

            // Load fontset
            for (int i = 0; i < 80; ++i)
                memory[i] = chip8_fontset[i];

            // Reset timers
            delayTimer = 0;
            soundTimer = 0;
        }

        public void ClearScreen() => GFX = new byte[64 * 32];

        public void LoadROM(byte[] bytes)
        {
            for (int i = 0; i < bytes.Length; ++i)
                memory[0x200 + i] = bytes[i];
        }

        private static void AddInputDelay()
        {
            Thread.Sleep(5);
        }

        public void EmulateCycle()
        {
            if (State == State.AwaitingInput)
            {
                for (int i = 0; i < 16; i++)
                {
                    if (Key[i] == 1)
                    {
                        V[opcode.X()] = (byte)i;
                        State = State.Running;
                    }
                }
                if (State == State.AwaitingInput)
                    return;
            }
            if (State == State.Paused)
                return;
            // Fetch Opcode
            opcode = (ushort)(memory[PC] << 8 | memory[PC + 1]);
            //Console.WriteLine($"OPCODE [{PC.ToString("x")}]: " + opcode.ToString("x"));
            // Decode Opcode
            switch (opcode & 0xF000)
            {
                // 0NNN:
                case 0x0000:
                    switch (opcode)
                    {
                        // 00E0 : Clears the screen. 
                        case 0x00E0:
                            ClearScreen();
                            PC += 2;
                            break;
                        // 00EE : Returns from a subroutine. 
                        case 0x00EE:
                            PC = Stack.Pop();
                            PC += 2;
                            //Console.WriteLine($"Popped from stack: {PC}");
                            break;
                        default:
                            Console.WriteLine($"Unknown opcode: 0x{opcode}");
                            break;
                    }
                    break;
                // 1NNN: GOTO NNN
                case 0x1000:
                    PC = opcode.NNN();
                    break;
                // 2NNN: Call subroutine NNN
                case 0x2000:
                    Stack.Push(PC);
                    //Console.WriteLine($"Pushed to stack: {PC}");
                    PC = opcode.NNN();
                    break;
                // 3XNN: Skips the next instruction if VX equals NN. (Usually the next instruction is a jump to skip a code block); 
                case 0x3000:
                    PC += (ushort)(V[opcode.X()] == opcode.NN() ? 4 : 2);
                    break;
                // 4XNN: Skips the next instruction if VX does not equal NN. (Usually the next instruction is a jump to skip a code block); 
                case 0x4000:
                    PC += (ushort)(V[opcode.X()] != opcode.NN() ? 4 : 2);
                    break;
                // 5XY0: Skips the next instruction if VX equals VY. (Usually the next instruction is a jump to skip a code block); 
                case 0x5000:
                    PC += (ushort)(V[opcode.X()] == V[opcode.Y()] ? 4 : 2);
                    break;
                // 6XNN: Sets VX to NN.  
                case 0x6000:
                    V[opcode.X()] = opcode.NN();
                    PC += 2;
                    break;
                // 7XNN: Sets VX to NN.  
                case 0x7000:
                    V[opcode.X()] += opcode.NN();
                    PC += 2;
                    break;
                // 8XYN: Sets VX to NN.  
                case 0x8000:
                    switch (opcode.N())
                    {
                        // 8XY0: Sets VX to the value of VY. 
                        case 0:
                            V[opcode.X()] = V[opcode.Y()];
                            PC += 2;
                            break;
                        // 8XY1: Sets VX to VX or VY. 
                        case 1:
                            V[opcode.X()] |= V[opcode.Y()];
                            PC += 2;
                            break;
                        // 8XY2: Sets VX to VX and VY. 
                        case 2:
                            V[opcode.X()] &= V[opcode.Y()];
                            PC += 2;
                            break;
                        // 8XY3: Sets VX to VX xor VY. 
                        case 3:
                            V[opcode.X()] ^= V[opcode.Y()];
                            PC += 2;
                            break;
                        // 8XY4: Adds VY to VX. VF is set to 1 when there's a carry, and to 0 when there is not. 
                        case 4:
                            if (V[opcode.Y()] > (0xFF - V[opcode.X()]))
                                V[0xF] = 1; // set carry
                            else
                                V[0xF] = 0;
                            V[opcode.X()] += V[opcode.Y()];
                            PC += 2;
                            break;
                        // 8XY5: VY is subtracted from VX. VF is set to 0 when there's a borrow, and 1 when there is not. 
                        case 5:
                            if (V[opcode.Y()] > V[opcode.X()])
                                V[0xF] = 0; // set borrow
                            else
                                V[0xF] = 1;
                            V[opcode.X()] -= V[opcode.Y()];
                            PC += 2;
                            break;
                        // 8XY6: Stores the least significant bit of VX in VF and then shifts VX to the right by 1
                        case 6:
                            V[0xF] = (byte)(V[opcode.X()] & 0x000F);
                            V[opcode.X()] >>= 1;
                            PC += 2;
                            break;
                        // 8XY7: Sets VX to VY minus VX. VF is set to 0 when there's a borrow, and 1 when there is not. 
                        case 7:
                            if (V[opcode.X()] > V[opcode.Y()])
                                V[0xF] = 0; // set borrow
                            else
                                V[0xF] = 1;
                            V[opcode.X()] = (byte)(V[opcode.Y()] - V[opcode.X()]);
                            PC += 2;
                            break;
                        // 8XYE: Stores the most significant bit of VX in VF and then shifts VX to the left by 1
                        case 0x000E:
                            V[0xF] = (byte)((V[opcode.X()] & 0xF000) >> 8);
                            V[opcode.X()] <<= 1;
                            PC += 2;
                            break;
                    }  
                    break;
                // 9XY0: Skips the next instruction if VX does not equal VY. (Usually the next instruction is a jump to skip a code block); 
                case 0x9000:
                    PC += (ushort)(V[opcode.X()] != V[opcode.Y()] ? 4 : 2);
                    break;
                // ANNN: Sets I to the address NNN
                case 0xA000: 
                    I = opcode.NNN();
                    PC += 2;
                    break;
                // BNNN: Jumps to the address NNN plus V0. 
                case 0xB000:
                    PC = (ushort)(opcode.NNN() + V[0]);
                    break;
                // CXNN: Sets VX to the result of a bitwise and operation on a random number (Typically: 0 to 255) and NN. 
                case 0xC000:
                    V[opcode.X()] = (byte)(opcode.NN() & Random.Shared.Next());
                    PC += 2;
                    break;
                // DXYN: Draws a sprite at coordinate (VX, VY) that has a width of 8 pixels and a height of N pixels. Each row of 8 pixels is read as bit-coded starting from memory location I; I value does not change after the execution of this instruction. As described above, VF is set to 1 if any screen pixels are flipped from set to unset when the sprite is drawn, and to 0 if that does not happen 
                case 0xD000: {
                        ushort height = (ushort)(opcode & 0x000F);
                        ushort pixel;
                        byte VX = V[opcode.X()];
                        byte VY = V[opcode.Y()];
                        V[0xF] = 0;

                        for (int y = 0; y < height; y++)
                        {
                            pixel = memory[I + y];
                            for (int x = 0; x < 8; x++)
                            {
                                if ((pixel & (0x80 >> x)) != 0)
                                {
                                    if (GFX[(VX + x + ((VY + y) * 64)) % (64 * 32)] == 1)
                                        V[0xF] = 1;
                                    GFX[(VX + x + ((VY + y) * 64)) % (64 * 32)] ^= 1;
                                }
                            }
                        }
                        DrawFlag = true;
                        PC += 2;
                    }
                    break;
                // EXNN: 
                case 0xE000:
                    switch (opcode & 0x00FF)
                    {
                        // EX9E: Skips the next instruction if the key stored in VX is pressed. (Usually the next instruction is a jump to skip a code block); 
                        case 0x009E:
                            AddInputDelay();
                            PC += (ushort) (Key[V[opcode.X()]] != 0 ? 4 : 2);
                            break;
                        // EXA1: Skips the next instruction if the key stored in VX is not pressed. (Usually the next instruction is a jump to skip a code block); 
                        case 0x00A1:
                            AddInputDelay();
                            PC += (ushort)(Key[V[opcode.X()]] == 0 ? 4 : 2);
                            break;
                    }
                    break;
                // FXNN:
                case 0xF000:
                    switch (opcode & 0x00FF)
                    {
                        // FX07: Sets VX to the value of the delay timer. 
                        case 0x0007:
                            V[opcode.X()] = delayTimer;
                            PC += 2;
                            break;
                        // FX0A: A key press is awaited, and then stored in VX. (Blocking Operation. All instruction halted until next key event); 
                        case 0x000A:
                            State = State.AwaitingInput;
                            break;
                        // FX15: Sets the delay timer to VX.  
                        case 0x0015:
                            delayTimer = V[opcode.X()];
                            PC += 2;
                            break;
                        // FX18: Sets the sound timer to VX.  
                        case 0x0018:
                            soundTimer = V[opcode.X()];
                            PC += 2;
                            break;
                        // FX1E: Adds VX to I. VF is not affected.
                        case 0x001E:
                            I += V[opcode.X()];
                            PC += 2;
                            break;
                        // FX29: Sets I to the location of the sprite for the character in VX. Characters 0-F (in hexadecimal) are represented by a 4x5 font. 
                        case 0x0029:
                            I = (ushort)(V[opcode.X()] * 0x5);
                            PC += 2;
                            break;
                        // FX33: Stores the binary-coded decimal representation of VX, with the most significant of three digits at the address in I, the middle digit at I plus 1, and the least significant digit at I plus 2. (In other words, take the decimal representation of VX, place the hundreds digit in memory at location in I, the tens digit at location I+1, and the ones digit at location I+2.); 
                        case 0x0033: 
                            memory[I]     = (byte)((V[opcode.X()] / 100) % 10);
                            memory[I + 1] = (byte)((V[opcode.X()] / 10) % 10);
                            memory[I + 2] = (byte)((V[opcode.X()] / 1) % 10);
                            PC += 2;
                            break;
                        // FX55: Stores from V0 to VX (including VX) in memory, starting at address I. The offset from I is increased by 1 for each value written, but I itself is left unmodified
                        case 0x0055:
                            for (int i = 0; i <= opcode.X(); i++)
                            {
                                memory[I + i] = V[i];
                            }
                            PC += 2;
                            break;
                        // FX65: Fills from V0 to VX (including VX) with values from memory, starting at address I. The offset from I is increased by 1 for each value written, but I itself is left unmodified.
                        case 0x0065:
                            for (int i = 0; i <= opcode.X(); i++)
                            {
                                V[i] = memory[I + i];
                            }
                            PC += 2;
                            break;
                    }
                    break;
                default:
                    Console.WriteLine($"Unknown opcode: 0x{opcode}");
                    break;
            }

            // Update timers
            if (delayTimer > 0)
            {
                delayTimer--;
            }
            if (soundTimer > 0)
            {
                if (soundTimer == 1)
                    Console.WriteLine("BEEP!\n");
                soundTimer--;
            }

        }
    }
}
