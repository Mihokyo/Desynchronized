﻿using Desynchronized.TNDBS.Utilities;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace Desynchronized.TNDBS
{
    public class TaleNewsPawnDied : TaleNewsNegativeIndividual
    {
        public enum DeathMethod
        {
            INDETERMINATE = -1,
            NON_EXECUTION = 0,
            EXECUTION = 1
        }

        private DeathBrutality brutalityDegree;

        private DamageDef killingBlowDamageType;

        public Pawn Killer => Instigator;

        public Pawn Victim => PrimaryVictim;

        /// <summary>
        /// Defaults to HUMANE if something went wrong.
        /// </summary>
        public DeathBrutality BrutalityDegree
        {
            get
            {
                return brutalityDegree;
            }
            private set
            {
                brutalityDegree = value;
            }
        }

        public DamageDef KillingBlowDamageDef
        {
            get
            {
                return killingBlowDamageType;
            }
        }

        /// <summary>
        /// This member is of type DamageInfo?. Be careful.
        /// </summary>
        [Obsolete]
        public DamageInfo? DeathCause { get; }

        private DeathMethod methodOfDeath = DeathMethod.INDETERMINATE;

        public DeathMethod MethodOfDeath
        {
            get
            {
                return methodOfDeath;
            }
        }

        public TaleNewsPawnDied()
        {

        }

        public TaleNewsPawnDied(Pawn victim, DamageInfo? dinfo): base(victim, InstigatorInfo.NoInstigator)
        {
            // DesynchronizedMain.LogError("Dumping stacktrace: " + Environment.StackTrace);

            methodOfDeath = DeathMethod.NON_EXECUTION;

            if (dinfo.HasValue)
            {
                DamageInfo dinfoUnpacked = dinfo.Value;
                killingBlowDamageType = dinfoUnpacked.Def;
                if (dinfoUnpacked.Instigator is Pawn instigator)
                {
                    InstigatorInfo = (InstigatorInfo) instigator;
                }
            }
        }

        public TaleNewsPawnDied(Pawn victim, DeathBrutality brutality): base(victim, InstigatorInfo.NoInstigator)
        {
            methodOfDeath = DeathMethod.EXECUTION;
        }

        [Obsolete]
        private TaleNewsPawnDied(Pawn victim, DeathMethod method, object argument): base(victim, InstigatorInfo.NoInstigator)
        {
            methodOfDeath = method;
            if (methodOfDeath == DeathMethod.NON_EXECUTION)
            {
                DeathCause = argument as DamageInfo?;
                InstigatorInfo = (InstigatorInfo) Killer;
            }
            else
            {
                DeathBrutality? temp = argument as DeathBrutality?;
                if (temp.HasValue)
                {
                    BrutalityDegree = temp.Value;
                }
                else
                {
                    BrutalityDegree = DeathBrutality.HUMANE;
                    Log.Error("[V1024-DESYNC] Cannot recognize \"DeathBrutality\" argument for Pawn execution; did something go wrong?\n" + Environment.StackTrace, true);
                }
            }
        }

        public static TaleNewsPawnDied GenerateAsExecution(Pawn victim, DeathBrutality brutality)
        {
            return new TaleNewsPawnDied(victim, brutality);
        }

        public static TaleNewsPawnDied GenerateGenerally(Pawn victim, DamageInfo? dinfo)
        {
            return new TaleNewsPawnDied(victim, dinfo);
        }

        public override string GetNewsIdentifier()
        {
            return "Pawn Died";
        }

        protected override void ConductSaveFileIO()
        {
            base.ConductSaveFileIO();
            Scribe_Values.Look(ref methodOfDeath, "methodOfDeath", DeathMethod.INDETERMINATE);
            Scribe_Values.Look(ref brutalityDegree, "brutalityDegree", DeathBrutality.HUMANE);
            Scribe_Defs.Look(ref killingBlowDamageType, "killingBlowDamageType");
        }

        /// <summary>
        /// Attempts to process this TaleNews as an execution event. Completes processing and returns true if successful; aborts and returns false otherwise.
        /// </summary>
        /// <param name="recipient"></param>
        /// <returns></returns>
        private bool TryProcessAsExecutionEvent(Pawn recipient)
        {
            bool result = false;

            if (methodOfDeath == DeathMethod.EXECUTION)
            {
                result = true;

                // Rather simple. Copied from vanilla code.
                int forcedStage = (int)BrutalityDegree;
                ThoughtDef thoughtToGive = Victim.IsColonist ? ThoughtDefOf.KnowColonistExecuted : ThoughtDefOf.KnowGuestExecuted;
                recipient.needs.mood.thoughts.memories.TryGainMemory(ThoughtMaker.MakeThought(thoughtToGive, forcedStage), null);
            }

            return result;
        }

        /// <summary>
        /// Attempts to give the killer their appropriate excitement (or disappointment). Aborts if there is no killer.
        /// </summary>
        /// <param name="recipient"></param>
        private void TryProcessKillerHigh(Pawn recipient)
        {
            // Killer != null => DamageInfo != null
            if (Killer != null)
            {
                // Currently you can't really kill yourself.
                // That would be something interesting, but we don't do that here.
                if (recipient == Killer)
                {
                    // IDK, is this something we can consider checking?
                    if (KillingBlowDamageDef.ExternalViolenceFor(Victim))
                    {
                        // Why this check tho
                        if (recipient.story != null)
                        {
                            // Bloodlust thoughts for Bloodlust guys, currently only for human victims
                            // We can expand upon this, and add in witnessed death (animals) with bloodlust
                            if (Victim.RaceProps.Humanlike)
                            {
                                // Try to add Bloodlust thoughts; will be auto-rejected if recipient does not have Bloodlust
                                new IndividualThoughtToAdd(ThoughtDefOf.KilledHumanlikeBloodlust, recipient).Add();

                                // Try to add Defeated Hostile Leader thoughts
                                if (Victim.HostileTo(Killer) && Victim.Faction != null && PawnUtility.IsFactionLeader(Victim) && Victim.Faction.HostileTo(Killer.Faction))
                                {
                                    new IndividualThoughtToAdd(ThoughtDefOf.DefeatedHostileFactionLeader, Killer, Victim).Add();
                                }
                            }
                        }
                    }
                }
            }

            /*
            // Note that "animal bonds" is a type of relationships.
            if (Killer != Victim && Killer == recipient)
            {
                HandleExcitementOfKiller(recipient);
            }
            */
        }

        /// <summary>
        /// Attempts to give the recipient eye-witness thoughts; general thoughts otherwise.
        /// </summary>
        /// <param name="recipient"></param>
        private void TryProcessEyeWitness(Pawn recipient)
        {
            // There is potential to expand upon this condition.
            if (Victim.RaceProps.Humanlike)
            {
                if (recipient.CanWitnessOtherPawn(Victim))
                {
                    GiveOutEyewitnessThoughts(recipient);
                }
                else
                {
                    GiveOutGenericThoughts(recipient);
                }
            }
        }

        /// <summary>
        /// Attempts to give the recipient thoughts that are derived from their relationship statuses, if there are any.
        /// </summary>
        /// <param name="recipient"></param>
        private void TryProcessRelationshipThoughts(Pawn recipient)
        {
            // Check if there is any relationship at all
            if (Victim.relations.PotentiallyRelatedPawns.Contains(recipient))
            {
                GiveOutRelationshipBasedThoughts(recipient);
            }
        }

        /// <summary>
        /// Handles the excitement (or disappointment) of the killer right when the kill happens.
        /// <para/>
        /// You should check legitimacy before calling this method, e.g., recipient.IsCapableOfThought()
        /// </summary>
        /// 
        [Obsolete("", true)]
        private void HandleExcitementOfKiller(Pawn recipient)
        {
            // Killer != null => DamageInfo != null
            // The obligatory .Value is extremely triggering.
            if (Killer != null)
            {
                if (Killer == PrimaryVictim)
                {
                    // We don't handle suicide cases.
                    return;
                }

                if (recipient == Killer && KillingBlowDamageDef.ExternalViolenceFor(Victim))
                {
                    // Confirmed NewsReceipient is Killer

                    // IsCapableOfThought has already been called.
                    // NewsReceipient is definitely capable of thought.

                    // Why this check tho
                    if (recipient.story != null)
                    {
                        // Bloodlust thoughts for Bloodlust guys, currently only for human victims
                        // We can expand upon this, and add in witnessed death (animals) with bloodlust
                        if (Victim.RaceProps.Humanlike)
                        {
                            // Try to add Bloodlust thoughts; will be auto-rejected if recipient does not have Bloodlust
                            new IndividualThoughtToAdd(ThoughtDefOf.KilledHumanlikeBloodlust, recipient).Add();

                            // Try to add Defeated Hostile Leader thoughts
                            if (Victim.HostileTo(Killer) && Victim.Faction != null && PawnUtility.IsFactionLeader(Victim) && Victim.Faction.HostileTo(Killer.Faction))
                            {
                                new IndividualThoughtToAdd(ThoughtDefOf.DefeatedHostileFactionLeader, Killer, Victim).Add();
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gives out eyewitness Thoughts.
        /// <para/>
        /// Only for eyewitnesses, that is, those Pawns whose .CanWitnessOtherPawn(Victim) returns true
        /// </summary>
        private void GiveOutEyewitnessThoughts(Pawn recipient)
        {
            if (recipient.Faction == Victim.Faction)
            {
                new IndividualThoughtToAdd(ThoughtDefOf.WitnessedDeathAlly, recipient).Add();
            }
            else if (Victim.Faction == null || !Victim.Faction.HostileTo(recipient.Faction))
            {
                new IndividualThoughtToAdd(ThoughtDefOf.WitnessedDeathNonAlly, recipient).Add();
            }
            if (recipient.relations.FamilyByBlood.Contains(Victim))
            {
                new IndividualThoughtToAdd(ThoughtDefOf.WitnessedDeathFamily, recipient).Add();
            }
            new IndividualThoughtToAdd(ThoughtDefOf.WitnessedDeathBloodlust, recipient).Add();
        }

        /// <summary>
        /// Gives out generic thoughts such as Colonist Died or Prisoner Died, etc.
        /// <para/>
        /// For everyone, though eyewitnesses should preferrably use GiveOutEyewitnessThoughts()
        /// </summary>
        private void GiveOutGenericThoughts(Pawn recipient)
        {
            if (Victim.Faction == Faction.OfPlayer && Victim.Faction == recipient.Faction && Victim.HostFaction != recipient.Faction)
            {
                recipient.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.KnowColonistDied, Victim);
            }
            bool prisonerIsInnocent = Victim.IsPrisonerOfColony && !Victim.guilt.IsGuilty && !Victim.InAggroMentalState;
            if (prisonerIsInnocent && recipient.Faction == Faction.OfPlayer && !recipient.IsPrisoner)
            {
                recipient.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.KnowPrisonerDiedInnocent, Victim);
            }
        }

        /// <summary>
        /// Gives out relationship-based Thoughts, such as My Relative Died, and Killer Killed My Relative/Friend/Rival.
        /// <para/>
        /// Most thoughts given out here are all social thoughts. The very few excptions to this are the Bonded Animal Died thought
        /// </summary>
        private void GiveOutRelationshipBasedThoughts(Pawn recipient)
        {
            PawnRelationDef mostImportantRelation = recipient.GetMostImportantRelation(Victim);
            if (mostImportantRelation != null)
            {
                // Step 2.1: My relative died
                ThoughtDef genderSpecificDiedThought = mostImportantRelation.GetGenderSpecificDiedThought(Victim);
                if (genderSpecificDiedThought != null)
                {
                    new IndividualThoughtToAdd(genderSpecificDiedThought, recipient, Victim, 1f, 1f).Add();
                }

                // Step 2.2: Hatred towards killer; does not allow self-hating
                if (Killer != null && Killer != Victim && Killer != recipient)
                {
                    // This killer killed my relatives
                    GiveOutHatredTowardsKiller_ForRelatives(mostImportantRelation, recipient);

                    // This killer killed my friend/rival
                    if (recipient.RaceProps.IsFlesh)
                    {
                        GiveOutHatredTowardsKiller_ForFriendsOrRivals(recipient);
                    }
                }
            }
        }

        private void GiveOutHatredTowardsKiller_ForRelatives(PawnRelationDef mostImportantRelation, Pawn recipient)
        {
            ThoughtDef genderSpecificKilledThought = mostImportantRelation.GetGenderSpecificKilledThought(Victim);
            if (genderSpecificKilledThought != null)
            {
                new IndividualThoughtToAdd(genderSpecificKilledThought, recipient, Killer, 1f, 1f).Add();
            }
        }

        private void GiveOutHatredTowardsKiller_ForFriendsOrRivals(Pawn recipient)
        {
            int opinion = recipient.relations.OpinionOf(Victim);
            IndividualThoughtToAdd? tempThought = null;

            // Woah we got our asses handed with a bad coding style...
            // The IndividualThoughtToAdd struct does not allow null ThoughtDef
            // So here we are using another way to write concise code
            if (opinion >= 20)
            {
                tempThought = new IndividualThoughtToAdd(ThoughtDefOf.KilledMyFriend, recipient, Killer, 1f, Victim.relations.GetFriendDiedThoughtPowerFactor(opinion));
            }
            else if (opinion <= -20)
            {
                tempThought = new IndividualThoughtToAdd(ThoughtDefOf.KilledMyRival, recipient, Killer, 1f, Victim.relations.GetRivalDiedThoughtPowerFactor(opinion));
            }
            
            tempThought?.Add();
        }

        /// <summary>
        /// Gives My Friend/Rival Died mood thoughts
        /// </summary>
        private void GiveOutFriendOrRivalDiedThoughts(Pawn recipient)
        {
            int opinion = recipient.relations.OpinionOf(Victim);
            if (opinion >= 20)
            {
                new IndividualThoughtToAdd(ThoughtDefOf.PawnWithGoodOpinionDied, recipient, Victim, Victim.relations.GetFriendDiedThoughtPowerFactor(opinion), 1f).Add();
            }
            else if (opinion <= -20)
            {
                new IndividualThoughtToAdd(ThoughtDefOf.PawnWithBadOpinionDied, recipient, Victim, Victim.relations.GetRivalDiedThoughtPowerFactor(opinion), 1f).Add();
            }
        }

        protected override void GiveThoughtsToReceipient(Pawn recipient)
        {
            if (!recipient.IsCapableOfThought())
            {
                return;
            }

            if (!TryProcessAsExecutionEvent(recipient))
            {
                // Someone killed another one.
                TryProcessKillerHigh(recipient);
                TryProcessEyeWitness(recipient);
            }

            // These are general thoughts.
            TryProcessRelationshipThoughts(recipient);
            GiveOutFriendOrRivalDiedThoughts(recipient);
        }

        /*
        private List<Thought_Memory> DetermineMemoriesToBeAdded(Pawn recipient)
        {
            List<Thought_Memory> resultList = new List<Thought_Memory>();

            if (!recipient.IsCapableOfThought())
            {
                return;
            }

            if (!TryAppendAsExecutionEvent(recipient, resultList))
            {
                // Someone killed another one.
                TryProcessKillerHigh(ref recipient);
                TryProcessEyeWitness(ref recipient);
            }

            // These are general thoughts.
            TryProcessRelationshipThoughts(ref recipient);
            GiveOutFriendOrRivalDiedThoughts(recipient);
        }

        private bool TryAppendAsExecutionEvent(Pawn recipient, List<Thought_Memory> io)
        {
            bool result = false;

            if (methodOfDeath == DeathMethod.EXECUTION)
            {
                // Rather simple. Adapted from vanilla code.
                int forcedStage = (int)BrutalityDegree;
                ThoughtDef thoughtToGive = Victim.IsColonist ? ThoughtDefOf.KnowColonistExecuted : ThoughtDefOf.KnowGuestExecuted;
                io.Add(ThoughtMaker.MakeThought(thoughtToGive, forcedStage));
            }

            return result;
        }

        private void TryAppendKillerHigh(Pawn recipient, List<Thought_Memory> io)
        {
            // Killer != null => DamageInfo != null
            if (Killer != null)
            {
                // Currently you can't really kill yourself.
                // That would be something interesting, but we don't do that here.
                if (recipient == Killer)
                {
                    // IDK, is this something we can consider checking?
                    if (KillingBlowDamageDef.ExternalViolenceFor(Victim))
                    {
                        // Why this check tho
                        if (recipient.story != null)
                        {
                            // Bloodlust thoughts for Bloodlust guys, currently only for human victims
                            // We can expand upon this, and add in witnessed death (animals) with bloodlust
                            if (Victim.RaceProps.Humanlike)
                            {
                                // Try to add Bloodlust thoughts; will be auto-rejected if recipient does not have Bloodlust
                                io.Add(new IndividualThoughtToAdd(ThoughtDefOf.KilledHumanlikeBloodlust, recipient).thought);

                                // Try to add Defeated Hostile Leader thoughts
                                if (Victim.HostileTo(Killer) && Victim.Faction != null && PawnUtility.IsFactionLeader(Victim) && Victim.Faction.HostileTo(Killer.Faction))
                                {
                                    io.Add(new IndividualThoughtToAdd(ThoughtDefOf.DefeatedHostileFactionLeader, Killer, Victim).thought);
                                }
                            }
                        }
                    }
                }
            }

            /*
            // Note that "animal bonds" is a type of relationships.
            if (Killer != Victim && Killer == recipient)
            {
                HandleExcitementOfKiller(recipient);
            }
            
        }
        */

        public override float CalculateNewsImportanceForPawn(Pawn pawn, TaleNewsReference reference)
        {
            // Again, this code is also a placeholder.

            float result = pawn.GetSocialProximityScoreForOther(Victim);
            // We have 2500 tick for 1 RW hour; hence, 60000 tick for 1 RW day
            result += (int)reference.ShockGrade * Mathf.Pow(0.5f, (1.0f / (60000 * 15)) * (Find.TickManager.TicksGame - reference.TickReceived));
            float victimKindScore = Victim.RaceProps.Animal ? 1 : 10;
            // First determine thought by relations
            ThoughtDef potentialGivenThought = pawn.GetMostImportantRelation(Victim)?.GetGenderSpecificDiedThought(Victim) ?? null;
            // If there exists none, then determine by factions
            if (potentialGivenThought == null)
            {
                if ((bool) pawn.Faction?.Equals(Victim.Faction))
                {
                    potentialGivenThought = ThoughtDefOf.KnowColonistDied;
                }
            }

            float relationalDeathImpact;
            if (potentialGivenThought != null)
            {
                relationalDeathImpact = Mathf.Abs(ThoughtMaker.MakeThought(potentialGivenThought).MoodOffset());
            }
            else
            {
                relationalDeathImpact = 0;
            }

            float mainImpact = (victimKindScore + relationalDeathImpact);
            mainImpact *= 1 + Mathf.Abs(((float)pawn.relations.OpinionOf(Victim)) / 50);
            int interFactionGoodwill;
            if (pawn.Faction == null)
            {
                interFactionGoodwill = 0;
            }
            else if (pawn.Faction == PrimaryVictim.Faction)
            {
                interFactionGoodwill = 100;
            }
            else
            {
                interFactionGoodwill = pawn.Faction.RelationWith(Victim.Faction).goodwill;
            }
            mainImpact *= 1 + Mathf.Abs((float)interFactionGoodwill / 50);

            // Decays at a rate of half strength per year passed
            result += mainImpact * Mathf.Pow(0.5f, (1.0f / (60000 * 15 * 4)) * (Find.TickManager.TicksGame - reference.TickReceived));

            return result;
        }
    }
}
