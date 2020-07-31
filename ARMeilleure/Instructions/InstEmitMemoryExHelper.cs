﻿using ARMeilleure.IntermediateRepresentation;
using ARMeilleure.State;
using ARMeilleure.Translation;

using static ARMeilleure.Instructions.InstEmitHelper;
using static ARMeilleure.IntermediateRepresentation.OperandHelper;

namespace ARMeilleure.Instructions
{
    static class InstEmitMemoryExHelper
    {
        private const int ErgSizeLog2 = 4;

        public static Operand EmitLoadExclusive(ArmEmitterContext context, Operand address, bool exclusive, int size)
        {
            if (exclusive)
            {
                Operand value;

                if (size == 4)
                {
                    Operand isUnalignedAddr = InstEmitMemoryHelper.EmitAddressCheck(context, address, size);

                    Operand lblFastPath = Label();

                    context.BranchIfFalse(lblFastPath, isUnalignedAddr);

                    // The call is not expected to return (it should throw).
                    context.Call(typeof(NativeInterface).GetMethod(nameof(NativeInterface.ThrowInvalidMemoryAccess)), address);

                    context.MarkLabel(lblFastPath);

                    // Only 128-bit CAS is guaranteed to have a atomic load.
                    Operand physAddr = InstEmitMemoryHelper.EmitPtPointerLoad(context, address, null, write: false);

                    Operand zero = context.VectorZero();

                    value = context.CompareAndSwap(physAddr, zero, zero);
                }
                else
                {
                    value = InstEmitMemoryHelper.EmitReadIntAligned(context, address, size);
                }

                Operand arg0 = context.LoadArgument(OperandType.I64, 0);

                Operand exAddrPtr  = context.Add(arg0, Const((long)NativeContext.GetExclusiveAddressOffset()));
                Operand exValuePtr = context.Add(arg0, Const((long)NativeContext.GetExclusiveValueOffset()));

                context.Store(exAddrPtr, context.BitwiseAnd(address, Const(address.Type, GetExclusiveAddressMask())));
                context.Store(exValuePtr, value);

                return value;
            }
            else
            {
                return InstEmitMemoryHelper.EmitReadIntAligned(context, address, size);
            }
        }

        public static void EmitStoreExclusive(
            ArmEmitterContext context,
            Operand address,
            Operand value,
            bool exclusive,
            int size,
            int rs,
            bool a32)
        {
            if (size < 3)
            {
                value = context.ConvertI64ToI32(value);
            }

            if (exclusive)
            {
                void SetRs(Operand value)
                {
                    if (a32)
                    {
                        SetIntA32(context, rs, value);
                    }
                    else
                    {
                        SetIntOrZR(context, rs, value);
                    }
                }

                Operand arg0 = context.LoadArgument(OperandType.I64, 0);

                Operand exAddrPtr = context.Add(arg0, Const((long)NativeContext.GetExclusiveAddressOffset()));
                Operand exAddr = context.Load(address.Type, exAddrPtr);

                // STEP 1: Check if we have exclusive access to this memory region. If not, fail and skip store.
                Operand maskedAddress = context.BitwiseAnd(address, Const(GetExclusiveAddressMask()));

                Operand exFailed = context.ICompareNotEqual(exAddr, maskedAddress);

                Operand lblExit = Label();

                SetRs(exFailed);

                context.BranchIfTrue(lblExit, exFailed);

                // STEP 2: We have exclusive access, make sure that the address is valid.
                Operand isUnalignedAddr = InstEmitMemoryHelper.EmitAddressCheck(context, address, size);

                Operand lblFastPath = Label();

                context.BranchIfFalse(lblFastPath, isUnalignedAddr);

                // The call is not expected to return (it should throw).
                context.Call(typeof(NativeInterface).GetMethod(nameof(NativeInterface.ThrowInvalidMemoryAccess)), address);

                // STEP 3: We have exclusive access and the address is valid, attempt the store using CAS.
                context.MarkLabel(lblFastPath);

                Operand physAddr = InstEmitMemoryHelper.EmitPtPointerLoad(context, address, null, write: true);

                Operand exValuePtr = context.Add(arg0, Const((long)NativeContext.GetExclusiveValueOffset()));
                Operand exValue = size switch
                {
                    0 => context.Load8(exValuePtr),
                    1 => context.Load16(exValuePtr),
                    2 => context.Load(OperandType.I32, exValuePtr),
                    3 => context.Load(OperandType.I64, exValuePtr),
                    _ => context.Load(OperandType.V128, exValuePtr)
                };

                Operand currValue = size switch
                {
                    0 => context.CompareAndSwap8(physAddr, exValue, value),
                    1 => context.CompareAndSwap16(physAddr, exValue, value),
                    _ => context.CompareAndSwap(physAddr, exValue, value)
                };

                // STEP 4: Check if we succeeded by comparing expected and in-memory values.
                Operand storeFailed;

                if (size == 4)
                {
                    Operand currValueLow  = context.VectorExtract(OperandType.I64, currValue, 0);
                    Operand currValueHigh = context.VectorExtract(OperandType.I64, currValue, 1);

                    Operand exValueLow  = context.VectorExtract(OperandType.I64, exValue, 0);
                    Operand exValueHigh = context.VectorExtract(OperandType.I64, exValue, 1);

                    storeFailed = context.BitwiseOr(
                        context.ICompareNotEqual(currValueLow,  exValueLow),
                        context.ICompareNotEqual(currValueHigh, exValueHigh));
                }
                else
                {
                    storeFailed = context.ICompareNotEqual(currValue, exValue);
                }

                SetRs(storeFailed);

                context.MarkLabel(lblExit);
            }
            else
            {
                InstEmitMemoryHelper.EmitWriteIntAligned(context, address, value, size);
            }
        }

        public static void EmitClearExclusive(ArmEmitterContext context)
        {
            Operand arg0 = context.LoadArgument(OperandType.I64, 0);

            Operand exAddrPtr = context.Add(arg0, Const((long)NativeContext.GetExclusiveAddressOffset()));

            // We store ULONG max to force any exclusive address checks to fail,
            // since this value is not aligned to the ERG mask.
            context.Store(exAddrPtr, Const(ulong.MaxValue));
        }

        private static long GetExclusiveAddressMask() => ~((4L << ErgSizeLog2) - 1);
    }
}
