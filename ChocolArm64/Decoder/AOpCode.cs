using ChocolArm64.Instruction;
using ChocolArm64.State;
using System;

namespace ChocolArm64.Decoder
{
    class AOpCode : IOpCode
    {
        public long Position  { get; private set; }
        public int  RawOpCode { get; private set; }

        public InstEmitter     Emitter      { get; protected set; }
        public InstInterpreter Interpreter  { get; protected set; }
        public RegisterSize    RegisterSize { get; protected set; }

        public AOpCode(Inst inst, long position, int opCode)
        {
            Position  = position;
            RawOpCode = opCode;

            RegisterSize = RegisterSize.Int64;

            Emitter     = inst.Emitter;
            Interpreter = inst.Interpreter;
        }

        public int GetBitsCount()
        {
            switch (RegisterSize)
            {
                case RegisterSize.Int32:   return 32;
                case RegisterSize.Int64:   return 64;
                case RegisterSize.Simd64:  return 64;
                case RegisterSize.Simd128: return 128;
            }

            throw new InvalidOperationException();
        }
    }
}