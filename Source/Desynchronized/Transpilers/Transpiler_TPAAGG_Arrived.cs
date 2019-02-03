﻿using Harmony;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;

namespace Desynchronized.Transpilers
{
    /// <summary>
    /// Objective: rather simple actually;
    /// eradicate the signature double-layer loop.
    /// </summary>
    [HarmonyPatch(typeof(TransportPodsArrivalAction_GiveGift))]
    [HarmonyPatch("Arrived", MethodType.Normal)]
    public class Transpiler_TPAAGG_Arrived
    {
        /// <summary>
        /// This is very straight-forward; the double-layer loop is at the beginning of the method block.
        /// <para/>
        /// Ignore the first 41 assembly instructions.
        /// </summary>
        /// <param name="instructions"></param>
        /// <returns></returns>
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            int ignoreCount = 41;

            foreach (CodeInstruction instruction in instructions)
            {
                if (ignoreCount > 0)
                {
                    instruction.opcode = OpCodes.Nop;
                    ignoreCount--;
                }

                yield return instruction;
            }
        }
    }
}